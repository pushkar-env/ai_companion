using System.Globalization;
using System.Text.Json;
using Npgsql;

// Bounded local synthetic transport, never a provider dispatch or token-delta stream.
static class EventStream {
    static readonly SemaphoreSlim Slots=new(4,4);
    public static async Task Run(Guid id,long? after,HttpContext context,NpgsqlDataSource source,DateTimeOffset expiry) {
        var header=context.Request.Headers["Last-Event-ID"];
        long cursor=after??0;
        if(header.Count>0 && (header.Count!=1 || !long.TryParse(header[0],NumberStyles.None,CultureInfo.InvariantCulture,out cursor))) {
            await Reject(context,400,"invalid_cursor");return;
        }
        if(cursor<0 || (header.Count>0 && after.HasValue && after.Value!=cursor)) {
            await Reject(context,400,"invalid_cursor");return;
        }
        if(!await Slots.WaitAsync(0,context.RequestAborted)) {await Reject(context,429,"stream_limit");return;}
        using var lifetime=CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        var remaining=expiry-DateTimeOffset.UtcNow;
        if(remaining<=TimeSpan.Zero){Slots.Release();await Reject(context,401,"unauthenticated");return;}
        lifetime.CancelAfter(remaining<TimeSpan.FromSeconds(30)?remaining:TimeSpan.FromSeconds(30));
        var cancellation=lifetime.Token;
        try {
            while(true) {
                // Release the connection/transaction before waiting on a client or polling.
                var page=await Read(id,cursor,(Guid)context.Items["actor"]!,source,cancellation);
                if(page.Status!=200) {
                    if(!context.Response.HasStarted)await Reject(context,page.Status,"resource_unavailable");
                    return;
                }
                if(!context.Response.HasStarted) {
                    context.Response.ContentType="text/event-stream";
                    context.Response.Headers["X-Accel-Buffering"]="no";
                    await context.Response.WriteAsync("retry: 1000\n: local-synthetic-accounts\n\n",cancellation);
                    await context.Response.Body.FlushAsync(cancellation);
                }
                foreach(var item in page.Items) {
                    cancellation.ThrowIfCancellationRequested();
                    if(DateTimeOffset.UtcNow>=expiry)return;
                    await context.Response.WriteAsync($"id: {item.sequence.ToString(CultureInfo.InvariantCulture)}\nevent: {item.type}\ndata: {JsonSerializer.Serialize(item)}\n\n",cancellation);
                    await context.Response.Body.FlushAsync(cancellation);
                    cursor=item.sequence;
                }
                if(page.Items.Count==50)continue;
                // Comment frames keep idle clients connected without advancing their cursor.
                await context.Response.WriteAsync(": heartbeat\n\n",cancellation);
                await context.Response.Body.FlushAsync(cancellation);
                await Task.Delay(500,cancellation);
            }
        }
        catch(OperationCanceledException) when(cancellation.IsCancellationRequested) { }
        catch(NpgsqlException) when(context.Response.HasStarted) { context.Abort(); }
        catch(IOException) { context.Abort(); }
        finally {Slots.Release();}
    }
    static Task Reject(HttpContext context,int status,string code)=>Results.Problem(statusCode:status,title:code).ExecuteAsync(context);
    static async Task<(int Status,List<Event> Items)> Read(Guid id,long cursor,Guid actor,NpgsqlDataSource source,CancellationToken cancellation) {
        await using var db=await source.OpenConnectionAsync(cancellation);
        await using var tx=await db.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead,cancellation);
        await using(var identity=new NpgsqlCommand("SELECT set_config('companion.user_id',$1,true)",db,tx)) {
            identity.Parameters.AddWithValue(actor.ToString());await identity.ExecuteScalarAsync(cancellation);
        }
        await using var bounds=new NpgsqlCommand("SELECT c.next_event_sequence FROM companion.conversations c JOIN companion.users u ON u.id=c.user_id WHERE c.id=$1 AND u.status='active'",db,tx);
        bounds.Parameters.AddWithValue(id);
        var next=await bounds.ExecuteScalarAsync(cancellation);
        if(next==null)return (404,[]);
        if(cursor>=(long)next)return (400,[]);
        await using var command=new NpgsqlCommand("SELECT e.event_id,e.event_sequence,e.turn_id,e.event_type,e.aggregate_version,m.text FROM companion.outbox e LEFT JOIN companion.messages m ON m.turn_id=e.turn_id AND m.role=CASE e.event_type WHEN 'turn.accepted' THEN 'user' WHEN 'turn.completed' THEN 'assistant' ELSE NULL END WHERE e.conversation_id=$1 AND e.event_sequence>$2 ORDER BY e.event_sequence LIMIT 50",db,tx);
        command.Parameters.AddWithValue(id);command.Parameters.AddWithValue(cursor);
        var items=new List<Event>();
        await using(var reader=await command.ExecuteReaderAsync(cancellation)) {
            while(await reader.ReadAsync(cancellation))items.Add(new(reader.GetGuid(0),reader.GetInt64(1),reader.GetGuid(2),reader.GetString(3),reader.GetInt64(4),reader.IsDBNull(5)?null:reader.GetString(5)));
        }
        await tx.CommitAsync(cancellation);
        return (200,items);
    }
    record Event(Guid event_id,long sequence,Guid turn_id,string type,long version,string? text);
}
