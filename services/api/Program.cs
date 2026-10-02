using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Companion.Core;
using Companion.Contracts;

// This executable has no non-local mode, production identity, persistence or provider path.
if ((Environment.GetEnvironmentVariable("APP_ENV") ?? "local") != "local" ||
    (Environment.GetEnvironmentVariable("MOCK_EXTERNAL_SERVICES") ?? "true") != "true")
    throw new InvalidOperationException("M0 only supports APP_ENV=local and MOCK_EXTERNAL_SERVICES=true.");
var builder=WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:8080");
builder.Logging.ClearProviders(); // Never log chat/request bodies in the fixture service.
var json=new JsonSerializerOptions { IncludeFields=true, DefaultIgnoreCondition=JsonIgnoreCondition.WhenWritingNull };
builder.Services.ConfigureHttpJsonOptions(o=> { o.SerializerOptions.IncludeFields=true; o.SerializerOptions.UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow; });
var app=builder.Build();
var turns=new ConcurrentDictionary<string,LocalTurn>();
var keys=new Dictionary<string,(string Text,string Message,string Turn)>();
var messages=new Dictionary<string,(string Text,string Turn)>();
var gate=new object();
app.MapGet("/health",()=>new {mode="mock",storage="volatile",external_services=false});
app.MapPost("/v1/conversations/{id:guid}/messages",(Guid id,SendMessage request,HttpContext context)=> {
    if(!Guid.TryParse(context.Request.Headers["Idempotency-Key"],out var key) || !Guid.TryParse(request.client_message_id,out _)
       || string.IsNullOrWhiteSpace(request.text) || UnicodeText.Length(request.text)>8000)
        return Results.Problem(statusCode:400,title:"Invalid message or Idempotency-Key");
    lock(gate) {
        string scope=id+":"+key; string messageScope=id+":"+request.client_message_id;
        if(keys.TryGetValue(scope,out var old)) {
            if(old.Text!=request.text || old.Message!=request.client_message_id) return Results.Conflict(new {code="idempotency_conflict"});
            return Accepted(old.Turn);
        }
        if(messages.TryGetValue(messageScope,out var prior)) {
            if(prior.Text!=request.text) return Results.Conflict(new {code="message_conflict"});
            keys[scope]=(request.text,request.client_message_id,prior.Turn); return Accepted(prior.Turn);
        }
        if(turns.Values.Any(t=>t.Conversation==id.ToString()&&!t.Terminal)) return Results.Conflict(new {code="active_turn"});
        if(turns.Count>=100) return Results.Problem(statusCode:429,title:"Mock capacity reached; restart the local server to reset synthetic data.");
        string turnId=MockTextProvider.StableId(scope);
        var events=new MockTextProvider().Create(request.text,turnId,MockScenario.Normal,0);
        events[0].payload.user_message_id=request.client_message_id;
        turns[turnId]=new LocalTurn(id.ToString(),events); keys[scope]=(request.text,request.client_message_id,turnId); messages[messageScope]=(request.text,turnId);
        return Accepted(turnId);
    }
    IResult Accepted(string turnId)=>Results.Json(new TurnAccepted{turn_id=turnId,user_message_id=request.client_message_id,status="accepted",events_url="/v1/turns/"+turnId+"/events"},json,statusCode:202);
});
app.MapGet("/v1/turns/{id:guid}",(Guid id)=>turns.TryGetValue(id.ToString(),out var t)?Results.Json(t.Snapshot(id.ToString()),json):Results.NotFound());
app.MapPost("/v1/turns/{id:guid}/cancel",(Guid id)=> {
    if(!turns.TryGetValue(id.ToString(),out var t)) return Results.NotFound();
    lock(t) {
        if(!t.Terminal) { t.Cancelled=true; t.Terminal=true; t.Status="cancelled"; t.Emitted.Add(new TextEvent{event_id=MockTextProvider.StableId(id+":cancel"),aggregate_id=id.ToString(),schema_version=1,seq=t.Emitted.Count,type="turn.cancelled",occurred_at="2026-09-25T10:00:00Z",trace_id="mock-v1",payload=new EventPayload{reason="user",partial_text=t.Text}}); }
        return Results.Json(t.Snapshot(id.ToString()),json);
    }
});
app.MapGet("/v1/turns/{id:guid}/events",async (Guid id,HttpContext context)=> {
    if(!turns.TryGetValue(id.ToString(),out var t)) {context.Response.StatusCode=404; return;}
    int cursor=0; string last=context.Request.Headers["Last-Event-ID"];
    lock(t) {
        if(!string.IsNullOrEmpty(last)) {cursor=t.Emitted.FindIndex(e=>e.event_id==last)+1; if(cursor==0){context.Response.StatusCode=409; return;}}
    }
    context.Response.ContentType="text/event-stream";
    try {
        while(!context.RequestAborted.IsCancellationRequested) {
            TextEvent item=null;
            lock(t) {
                if(cursor<t.Emitted.Count) item=t.Emitted[cursor++];
                else if(!t.Terminal && t.Emitted.Count<t.Planned.Count) {
                    item=t.Planned[t.Emitted.Count]; t.Emitted.Add(item); cursor++;
                    if(item.type=="turn.text.delta") {t.Text+=item.payload.text;t.Status="streaming";}
                    if(item.type=="turn.completed") {t.Terminal=true;t.Status="completed";}
                }
                else break;
            }
            await context.Response.WriteAsync("id: "+item.event_id+"\nevent: "+item.type+"\ndata: "+JsonSerializer.Serialize(item,json)+"\n\n",context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
            await Task.Delay(70,context.RequestAborted);
        }
    } catch(OperationCanceledException) { /* Disconnect preserves emitted events for replay. */ }
});
Console.WriteLine("MOCK ONLY: http://127.0.0.1:8080 • synthetic local data • restart clears all turns");
app.Run();
sealed class LocalTurn(string conversation,IReadOnlyList<TextEvent> events)
{
    public string Conversation=conversation,Text="",Status="accepted";
    public volatile bool Terminal;
    public bool Cancelled;
    public IReadOnlyList<TextEvent> Planned=events;
    public List<TextEvent> Emitted=new();
    public TurnState Snapshot(string id) {lock(this) return new TurnState{turn_id=id,conversation_id=Conversation,status=Status,text=Text,last_seq=Math.Max(0,Emitted.Count-1),assistant_message_id=Status=="completed"?MockTextProvider.StableId(id+":assistant"):null};}
}
