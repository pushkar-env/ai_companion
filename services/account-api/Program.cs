using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;

// Explicit synthetic-account harness. No production auth/provider path is enabled.
if (Environment.GetEnvironmentVariable("APP_ENV") != "local" ||
    Environment.GetEnvironmentVariable("SYNTHETIC_ACCOUNTS_ONLY") != "true")
    throw new InvalidOperationException("Local synthetic-account configuration required.");
var fixture = JsonSerializer.Deserialize<Fixture>(File.ReadAllText(
    Environment.GetEnvironmentVariable("COMPANION_ACCOUNT_FIXTURE") ?? throw new InvalidOperationException("Missing local fixture.")))!;
if (fixture.Port is < 1024 or > 65535 || fixture.Accounts.Length is < 1 or > 10 ||
    fixture.Accounts.Any(a => a.UserId == Guid.Empty || a.Token.Length != 64 || !a.Token.All(Uri.IsHexDigit)) ||
    fixture.Accounts.Select(a => a.Token).Distinct().Count() != fixture.Accounts.Length ||
    fixture.ExpiresAt <= DateTimeOffset.UtcNow || fixture.ExpiresAt > DateTimeOffset.UtcNow.AddHours(1) ||
    fixture.Budget == Guid.Empty || fixture.Units is < 1 or > 1000000)
    throw new InvalidOperationException("Invalid local fixture.");
var connection = new NpgsqlConnectionStringBuilder(fixture.Database);
if (connection.Host != "127.0.0.1" || connection.Username is "postgres" or "companion_test_admin" || connection.NoResetOnClose)
    throw new InvalidOperationException("Loopback non-owner database role required.");
connection.MaxPoolSize = 4; connection.Timeout = 5; connection.CommandTimeout = 10;
await using var dataSource = NpgsqlDataSource.Create(connection.ConnectionString);
await using (var probe = dataSource.CreateCommand("SELECT rolsuper OR rolbypassrls OR EXISTS(SELECT 1 FROM pg_tables WHERE schemaname='companion' AND tableowner=current_user) FROM pg_roles WHERE rolname=current_user"))
    if ((bool)(await probe.ExecuteScalarAsync())!) throw new InvalidOperationException("Unsafe database role.");
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://127.0.0.1:{fixture.Port}");
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 65536);
builder.Logging.ClearProviders(); // Never log tokens, connection strings or request bodies.
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);
var app = builder.Build();
app.Use(async (context, next) => {
    context.Response.Headers.CacheControl = "no-store";
    if (context.Request.Host.Value != $"127.0.0.1:{fixture.Port}" || context.Request.Headers.ContainsKey("Origin")) {
        await Problem(403,"local_only").ExecuteAsync(context); return;
    }
    if (context.Request.Path == "/health") { await next(); return; }
    var supplied = Encoding.UTF8.GetBytes(context.Request.Headers.Authorization.ToString());
    var account = fixture.Accounts.FirstOrDefault(a => CryptographicOperations.FixedTimeEquals(supplied,Encoding.UTF8.GetBytes("Bearer " + a.Token)));
    if (account == null || DateTimeOffset.UtcNow >= fixture.ExpiresAt) {
        await Problem(401,"unauthenticated").ExecuteAsync(context); return;
    }
    context.Items["actor"] = account.UserId;
    try { await next(); }
    catch (PostgresException e) {
        var (status,code) = e.SqlState switch {
            "42501" => (404,"resource_unavailable"),
            "23505" or "40001" => (409,"conflict"),
            "22023" => (400,"invalid_request"),
            "P0001" when e.MessageText == "quota_exceeded" => (429,"quota_exceeded"),
            _ => (503,"storage_unavailable")
        };
        await Problem(status,code).ExecuteAsync(context);
    }
    catch (OperationCanceledException) when(context.RequestAborted.IsCancellationRequested) { }
    catch (NpgsqlException) { await Problem(503,"storage_unavailable").ExecuteAsync(context); }
});
app.MapGet("/health", () => new { mode="local-synthetic-accounts", storage="postgresql", providers=false });
// Development route deliberately separate from the versioned production OpenAPI contract.
app.MapPost("/local/v1/conversations/{id:guid}/admissions", async (Guid id, Admission body, HttpContext context) => {
    if (!Guid.TryParse(context.Request.Headers["Idempotency-Key"],out var key) || key==Guid.Empty ||
        body.client_message_id==Guid.Empty || string.IsNullOrWhiteSpace(body.text) || body.text.EnumerateRunes().Count()>8000)
        return Problem(400,"invalid_request");
    await using var db = await dataSource.OpenConnectionAsync(context.RequestAborted);
    await using var tx = await db.BeginTransactionAsync(context.RequestAborted);
    await SetActor(db,tx,(Guid)context.Items["actor"]!,context.RequestAborted);
    await using var command = new NpgsqlCommand("SELECT companion.admit_metered_text($1,$2,$3,$4,$5,$6)",db,tx);
    command.Parameters.AddWithValue(id);command.Parameters.AddWithValue(key);command.Parameters.AddWithValue(body.client_message_id);
    command.Parameters.AddWithValue(body.text);command.Parameters.AddWithValue(fixture.Budget);command.Parameters.AddWithValue(fixture.Units);
    var turn=(Guid)(await command.ExecuteScalarAsync(context.RequestAborted))!;
    await tx.CommitAsync(context.RequestAborted);
    return Results.Json(new {turn_id=turn,status="accepted",mode="local-synthetic-accounts"},statusCode:202);
});
app.MapGet("/local/v1/turns/{id:guid}",async (Guid id,HttpContext context) => {
    await using var db = await dataSource.OpenConnectionAsync(context.RequestAborted);
    await using var tx = await db.BeginTransactionAsync(context.RequestAborted);
    await SetActor(db,tx,(Guid)context.Items["actor"]!,context.RequestAborted);
    await using var command=new NpgsqlCommand("SELECT id,conversation_id,state,version,reservation_id FROM companion.turns WHERE id=$1",db,tx);
    command.Parameters.AddWithValue(id);
    object? result=null;
    await using(var reader=await command.ExecuteReaderAsync(context.RequestAborted)) {
        if(await reader.ReadAsync(context.RequestAborted))result=new {turn_id=reader.GetGuid(0),conversation_id=reader.GetGuid(1),status=reader.GetString(2),version=reader.GetInt64(3),reservation_id=reader.IsDBNull(4)?(Guid?)null:reader.GetGuid(4)};
    }
    await tx.CommitAsync(context.RequestAborted);
    return result==null?Problem(404,"resource_unavailable"):Results.Json(result);
});
// Terminal cancellation does not infer a provider refund: trusted settlement remains separate.
app.MapPost("/local/v1/turns/{id:guid}/cancel",async (Guid id,Cancel body,HttpContext context) => {
    if(body.expected_version<1)return Problem(400,"invalid_request");
    await using var db=await dataSource.OpenConnectionAsync(context.RequestAborted);
    await using var tx=await db.BeginTransactionAsync(context.RequestAborted);
    await SetActor(db,tx,(Guid)context.Items["actor"]!,context.RequestAborted);
    await using var command=new NpgsqlCommand("SELECT companion.finish_text($1,$2,'cancelled',NULL)",db,tx);
    command.Parameters.AddWithValue(id);command.Parameters.AddWithValue(body.expected_version);
    await command.ExecuteScalarAsync(context.RequestAborted);
    await tx.CommitAsync(context.RequestAborted);
    return Results.Json(new {turn_id=id,status="cancelled",mode="local-synthetic-accounts"});
});
app.MapGet("/local/v1/conversations/{id:guid}/events",(Guid id,long? after,int? limit,HttpContext context)=>ReadPage(id,after,limit,true,context,dataSource));
app.MapGet("/local/v1/conversations/{id:guid}/messages",(Guid id,long? after,int? limit,HttpContext context)=>ReadPage(id,after,limit,false,context,dataSource));
app.MapGet("/local/v1/conversations/{id:guid}/stream",(Guid id,long? after,HttpContext context)=>EventStream.Run(id,after,context,dataSource,fixture.ExpiresAt));
await app.RunAsync();
static IResult Problem(int status,string code)=>Results.Problem(statusCode:status,title:code,extensions:new Dictionary<string,object?>{{"code",code},{"retryable",status==503}});
static async Task SetActor(NpgsqlConnection db,NpgsqlTransaction tx,Guid actor,CancellationToken cancellation) {
    await using var command=new NpgsqlCommand("SELECT set_config('companion.user_id',$1,true)",db,tx);
    command.Parameters.AddWithValue(actor.ToString());await command.ExecuteScalarAsync(cancellation);
}
static async Task<IResult> ReadPage(Guid id,long? after,int? limit,bool events,HttpContext context,NpgsqlDataSource source) {
    long cursor=after??0;int size=limit??50;
    if(cursor<0 || size<1 || size>100)return Problem(400,"invalid_request");
    var cancellation=context.RequestAborted;
    await using var db=await source.OpenConnectionAsync(cancellation);
    // One stable snapshot for ownership, cursor bounds, and the page.
    await using var tx=await db.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead,cancellation);
    await SetActor(db,tx,(Guid)context.Items["actor"]!,cancellation);
    await using var bounds=new NpgsqlCommand(events
        ? "SELECT next_event_sequence FROM companion.conversations WHERE id=$1"
        : "SELECT next_sequence FROM companion.conversations WHERE id=$1",db,tx);
    bounds.Parameters.AddWithValue(id);
    var next=await bounds.ExecuteScalarAsync(cancellation);
    if(next==null)return Problem(404,"resource_unavailable");
    if(cursor>=(long)next)return Problem(400,"invalid_request");
    await using var command=new NpgsqlCommand(events
        ? "SELECT e.event_id,e.event_sequence,e.turn_id,e.event_type,e.aggregate_version,m.text FROM companion.outbox e LEFT JOIN companion.messages m ON m.turn_id=e.turn_id AND m.role=CASE e.event_type WHEN 'turn.accepted' THEN 'user' WHEN 'turn.completed' THEN 'assistant' ELSE NULL END WHERE e.conversation_id=$1 AND e.event_sequence>$2 ORDER BY e.event_sequence LIMIT $3"
        : "SELECT id,sequence,turn_id,role,status,text FROM companion.messages WHERE conversation_id=$1 AND sequence>$2 ORDER BY sequence LIMIT $3",db,tx);
    command.Parameters.AddWithValue(id);command.Parameters.AddWithValue(cursor);command.Parameters.AddWithValue(size+1);
    var items=new List<object>();bool more=false;long last=cursor;
    await using(var reader=await command.ExecuteReaderAsync(cancellation)) {
        while(await reader.ReadAsync(cancellation)) {
            if(items.Count==size){more=true;break;}
            last=reader.GetInt64(1);
            if(events)items.Add(new {event_id=reader.GetGuid(0),sequence=last,turn_id=reader.GetGuid(2),type=reader.GetString(3),version=reader.GetInt64(4),text=reader.IsDBNull(5)?null:reader.GetString(5)});
            else items.Add(new {message_id=reader.GetGuid(0),sequence=last,turn_id=reader.GetGuid(2),role=reader.GetString(3),status=reader.GetString(4),text=reader.GetString(5)});
        }
    }
    await tx.CommitAsync(cancellation);
    return Results.Json(new {conversation_id=id,items,next_cursor=last,has_more=more,mode="local-synthetic-accounts"});
}
record Cancel(long expected_version);
record Admission(Guid client_message_id,string text);
record Account(Guid UserId,string Token);
record Fixture(int Port,string Database,Guid Budget,long Units,DateTimeOffset ExpiresAt,Account[] Accounts);
