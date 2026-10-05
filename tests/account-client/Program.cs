using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Companion.AccountClient;

Environment.SetEnvironmentVariable("APP_ENV","local");
Environment.SetEnvironmentVariable("SYNTHETIC_ACCOUNTS_ONLY","true");
void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS client "+label);}
string Frame(StoredEvent e)=>$"id: {e.sequence}\nevent: {e.type}\ndata: {JsonSerializer.Serialize(e)}\n\n";
var turn=Guid.NewGuid();
var accepted=new StoredEvent(Guid.NewGuid(),1,turn,"turn.accepted",1,"Hello नमस्ते 🌼");
var completed=new StoredEvent(Guid.NewGuid(),2,turn,"turn.completed",2,"[SYNTHETIC] reply");
var history=new History();
Check(history.Apply(accepted) && !history.Apply(accepted) && history.Apply(completed) && history.Turns.Count==1 && history.Cursor==2,"duplicates do not duplicate turns or regress terminal state");
try{history.Apply(completed with {sequence=4});throw new Exception("Gap accepted");}catch(InvalidDataException){}
Check(history.Cursor==2,"gap rejection preserves last applied checkpoint");
var items=new List<StoredEvent>();
await foreach(var e in Sse.Read(new Fragmented(Encoding.UTF8.GetBytes(": comment\r\n\r\n"+Frame(accepted)+Frame(completed)))))items.Add(e);
Check(items.SequenceEqual(new[]{accepted,completed}),"fragmented UTF8 SSE preserves Hindi emoji and event boundaries");
items.Clear();
await foreach(var e in Sse.Read(new MemoryStream(Encoding.UTF8.GetBytes(Frame(accepted)+Frame(completed).TrimEnd('\n')))))items.Add(e);
Check(items.Count==1,"EOF discards incomplete frame without advancing checkpoint");
items.Clear();
var maximum=accepted with {text=string.Concat(Enumerable.Repeat("🌼",8000))};
await foreach(var e in Sse.Read(new MemoryStream(Encoding.UTF8.GetBytes(Frame(maximum)))))items.Add(e);
Check(items.Single().text==maximum.text,"maximum admitted Unicode text fits escaped JSON frame bound");
try{await foreach(var e in Sse.Read(new MemoryStream(Encoding.UTF8.GetBytes(new string('x',131073))))){}throw new Exception("Oversized frame accepted");}catch(InvalidDataException){}
Check(true,"oversized frame rejected with bounded parser buffer");
try{new SyntheticClient(new Uri("http://example.invalid/"),new string('a',64),Guid.NewGuid());throw new Exception("Remote accepted");}catch(ArgumentException){}
Check(true,"client rejects non-loopback endpoint");
// Actual sockets simulate interrupted transport followed by duplicate replay.
var socket=new TcpListener(IPAddress.Loopback,0);socket.Start();int port=((IPEndPoint)socket.LocalEndpoint).Port;socket.Stop();
using var listener=new HttpListener();listener.Prefixes.Add($"http://127.0.0.1:{port}/");listener.Start();
var server=Task.Run(async()=>{
    for(int index=0;index<2;index++) {
        var context=await listener.GetContextAsync();
        if(context.Request.Headers["Last-Event-ID"]!=(index==0?"0":"1"))throw new Exception("Wrong resume checkpoint");
        context.Response.ContentType="text/event-stream";
        byte[] bytes=Encoding.UTF8.GetBytes(index==0?Frame(accepted)+Frame(completed).TrimEnd('\n'):Frame(accepted)+Frame(completed));
        await context.Response.OutputStream.WriteAsync(bytes);context.Response.Close();
    }
});
using(var client=new SyntheticClient(new Uri($"http://127.0.0.1:{port}/"),new string('a',64),Guid.NewGuid())) {
    using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(10));int applied=0;
    try{await client.Follow(e=>{applied++;if(e.sequence==2)stop.Cancel();},stop.Token);}catch(OperationCanceledException) when(stop.IsCancellationRequested){}
    await server.WaitAsync(TimeSpan.FromSeconds(5));
    Check(applied==2 && client.History.Cursor==2 && client.History.Turns.Single().AssistantText==completed.text,"reconnect uses applied cursor and deduplicates replay after partial frame");
    Check(client.State=="stopped","caller cancellation stops reconnect loop");
}
listener.Stop();
if(args.Length>0) {
    // Only the ignored fixture path is passed, never bearer values in command arguments.
    var fixture=JsonDocument.Parse(await File.ReadAllTextAsync(args[0])).RootElement;
    var endpoint=new Uri($"http://127.0.0.1:{fixture.GetProperty("Port").GetInt32()}/");
    var token=fixture.GetProperty("Accounts")[0].GetProperty("Token").GetString()!;
    var other=fixture.GetProperty("Accounts")[1].GetProperty("Token").GetString()!;
    var conversation=Guid.Parse(args[1]);
    using var client=new SyntheticClient(endpoint,token,conversation);
    await client.Load();
    Check(client.History.Cursor==2 && client.History.Turns.Single().State=="cancelled","real API history loads canonical cancelled conversation");
    await client.Load();
    Check(client.History.Turns.Count==1 && client.History.Cursor==2,"real API repeat history load preserves checkpoint and no duplicates");
    using var denied=new SyntheticClient(endpoint,other,conversation);
    try{await denied.Load();throw new Exception("Wrong owner accepted");}catch(HttpRequestException e) when(e.StatusCode==HttpStatusCode.NotFound){}
    Check(denied.History.Cursor==0 && denied.State=="error","real API wrong owner cannot hydrate client state");
    using var replay=new SyntheticClient(endpoint,token,conversation);
    using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(8));
    try{await replay.Follow(e=>{if(e.sequence==2)stop.Cancel();},stop.Token);}catch(OperationCanceledException) when(stop.IsCancellationRequested){}
    Check(replay.History.Cursor==2 && replay.History.Turns.Single().State=="cancelled","real API SSE projects the same terminal history");
}
sealed class Fragmented(byte[] bytes):MemoryStream(bytes) {
    public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellationToken=default)=>base.ReadAsync(buffer[..Math.Min(buffer.Length,1)],cancellationToken);
}
