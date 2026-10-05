using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
namespace Companion.AccountClient;

// Deliberately separate from Unity's session-only chat and production identity.
public sealed class SyntheticClient:IDisposable {
    readonly HttpClient http;
    readonly Guid conversation;
    readonly string token;
    readonly SemaphoreSlim readGate=new(1,1);
    public History History {get;}=new();
    public string State {get;private set;}="idle";
    public SyntheticClient(Uri endpoint,string bearer,Guid conversationId) {
        if(Environment.GetEnvironmentVariable("APP_ENV")!="local" || Environment.GetEnvironmentVariable("SYNTHETIC_ACCOUNTS_ONLY")!="true"
            || endpoint.Scheme!="http" || endpoint.Host!="127.0.0.1" || endpoint.AbsolutePath!="/"
            || endpoint.UserInfo.Length>0 || endpoint.Query.Length>0 || endpoint.Fragment.Length>0
            || conversationId==Guid.Empty || bearer.Length!=64 || !bearer.All(Uri.IsHexDigit))
            throw new ArgumentException("Explicit loopback synthetic configuration required.");
        http=new(new HttpClientHandler {AllowAutoRedirect=false,UseProxy=false}){BaseAddress=endpoint,Timeout=TimeSpan.FromSeconds(45)};
        token=bearer;conversation=conversationId;
    }
    HttpRequestMessage Request(string suffix) {
        var request=new HttpRequestMessage(HttpMethod.Get,$"local/v1/conversations/{conversation}/{suffix}");
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);return request;
    }
    public async Task Load(CancellationToken cancellation=default) {
        await readGate.WaitAsync(cancellation);
        try {
            State="loading";
            while(true) {
                using var request=Request($"events?after={History.Cursor}&limit=50");
                using var response=await http.SendAsync(request,cancellation);response.EnsureSuccessStatusCode();
                var page=await response.Content.ReadFromJsonAsync<Page>(cancellation)??throw new InvalidDataException("Missing history.");
                if(page.conversation_id!=conversation || page.mode!="local-synthetic-accounts")throw new InvalidDataException("Unexpected history scope.");
                foreach(var item in page.items)History.Apply(item);
                if(page.next_cursor!=History.Cursor || (page.has_more && page.items.Length==0))throw new InvalidDataException("Invalid history cursor.");
                if(!page.has_more)break;
            }
            State="ready";
        } catch(OperationCanceledException){State="stopped";throw;}
          catch{State="error";throw;}
        finally{readGate.Release();}
    }
    // Three reconnects maximum per call, including EOF; caller explicitly starts another session.
    public async Task Follow(Action<StoredEvent>? applied=null,CancellationToken cancellation=default) {
        await readGate.WaitAsync(cancellation);
        try {
            for(int attempt=0;attempt<4;attempt++) {
                using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                deadline.CancelAfter(TimeSpan.FromSeconds(45));
                try {
                    State=attempt==0?"connecting":"reconnecting";
                    using var request=Request("stream");request.Headers.Add("Last-Event-ID",History.Cursor.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,deadline.Token);
                    if(response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
                        throw new IOException("Stream temporarily unavailable.");
                    response.EnsureSuccessStatusCode();
                    if(response.Content.Headers.ContentType?.MediaType!="text/event-stream")throw new InvalidDataException("Unexpected stream type.");
                    State="connected";
                    await using var stream=await response.Content.ReadAsStreamAsync(deadline.Token);
                    await foreach(var item in Sse.Read(stream,deadline.Token))if(History.Apply(item))applied?.Invoke(item);
                }
                catch(OperationCanceledException) when(!cancellation.IsCancellationRequested) { }
                catch(HttpRequestException error) when(error.StatusCode is null) { }
                catch(IOException) { }
                if(attempt==3)throw new IOException("Reconnect limit reached.");
                State="reconnecting";
                await Task.Delay(TimeSpan.FromMilliseconds(1000*(1<<attempt)+Random.Shared.Next(250)),cancellation);
            }
        } catch(OperationCanceledException){State="stopped";throw;}
          catch{State="error";throw;}
        finally{readGate.Release();}
    }
    public void Dispose(){http.Dispose();readGate.Dispose();}
    sealed record Page(Guid conversation_id,StoredEvent[] items,long next_cursor,bool has_more,string mode);
}
