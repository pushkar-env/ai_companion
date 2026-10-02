using Companion.Core;
using Companion.Contracts;
using System.Text.Json;

int passed=0;
void Check(string name,Action test) {test();Console.WriteLine("PASS "+name);passed++;}
void Require(bool condition,string message="assertion failed") {if(!condition)throw new Exception(message);}
ChatSession Session()=>new(new MockTextProvider());
void Drain(ChatSession s) {for(int i=0;i<200&&s.Busy;i++)s.Tick();Require(!s.Busy,"unbounded stream");}
Check("CHAT-01 successful Unicode stream and deterministic provider",()=> {
 var s=Session();s.Send("hello",MockScenario.Normal);Drain(s);Require(s.Status==ChatStatus.Completed);Require(s.Text.EndsWith("🌿"));
 var t=Session();t.Send("hello",MockScenario.Normal);Drain(t);Require(s.Text==t.Text&&s.TurnId==t.TurnId);
 Require(UnicodeText.Length("a🌿हिन्दी")==8);
});
Check("CHAT-02 cancellation during loading and late delivery",()=> {
 var s=Session();s.Send("hello",MockScenario.Normal);var es=new MockTextProvider().Create("hello",s.TurnId,MockScenario.Normal,0);s.Cancel();foreach(var e in es)s.Apply(e);Require(s.Status==ChatStatus.Cancelled&&s.Text=="");s.Retry();Drain(s);Require(s.Status==ChatStatus.Completed);
});
Check("CHAT-03 partial failure and explicit bounded retry",()=> {
 var s=Session();s.Send("hello",MockScenario.FailMidway);Drain(s);Require(s.Status==ChatStatus.Failed&&s.Text.Length>0);s.Retry();Drain(s);Require(s.Status==ChatStatus.Completed);
 var b=Session();b.Send("hello",MockScenario.FailBefore);Drain(b);Require(b.Status==ChatStatus.Failed&&b.Text.Length==0);b.Retry();b.Cancel();b.Retry();b.Cancel();try{b.Retry();throw new Exception("retry unbounded");}catch(InvalidOperationException){}
});
Check("EVENT-01 duplicate, gap and stale turn handling",()=> {
 var s=Session();s.Send("hello",MockScenario.Normal);var es=new MockTextProvider().Create("hello",s.TurnId,MockScenario.Normal,0);s.Apply(es[0]);s.Apply(es[0]);s.Apply(es[1]);string text=s.Text;s.Apply(es[1]);Require(s.Text==text);s.Apply(es[3]);Require(s.Status==ChatStatus.Failed);s.Retry();s.Apply(es[2]);Require(s.Text=="");
});
Check("EVENT-02 invalid offset and future schema fail recoverably",()=> {
 foreach(bool version in new[]{false,true}) {var s=Session();s.Send("hello",MockScenario.Normal);var es=new MockTextProvider().Create("hello",s.TurnId,MockScenario.Normal,0);s.Apply(es[0]);if(version)es[1].schema_version=9;else es[1].payload.offset=5;s.Apply(es[1]);Require(s.Status==ChatStatus.Failed&&s.Retryable);}
});
Check("CHAT-04 rejects blank, excessive input and concurrent sends",()=> {
 foreach(string input in new[]{" ",new string('a',8001)}) {try{Session().Send(input,MockScenario.Normal);throw new Exception("bad input accepted");}catch(ArgumentException){}}
 var s=Session();s.Send("hello",MockScenario.Normal);try{s.Send("second",MockScenario.Normal);throw new Exception("concurrent send");}catch(InvalidOperationException){}
});
Directory.CreateDirectory("artifacts");
FacialChecks.Run(Check);
Check("MOCK-01 fake identity, no entitlements/push and cancellable synthetic tone",()=> {
 var mocks=new LocalMockServices();Require(mocks.IsMock&&Guid.TryParse(mocks.Subject,out _));Require(!mocks.HasEntitlement("any"));Require(mocks.Purchase("any")=="mock_checkout_disabled");Require(mocks.Schedule("x")=="mock_not_delivered:x");
 var a=mocks.SyntheticAudio(16000,100,CancellationToken.None);var b=mocks.SyntheticAudio(16000,100,CancellationToken.None);Require(a.SequenceEqual(b)&&a.Length==1600);
 try{mocks.SyntheticAudio(16000,100,new CancellationToken(true));throw new Exception("cancel ignored");}catch(OperationCanceledException){}
});
var fixtures=new List<TextEvent>();var provider=new MockTextProvider();
foreach(var scenario in new[]{MockScenario.Normal,MockScenario.FailBefore,MockScenario.FailMidway})fixtures.AddRange(provider.Create("hello",MockTextProvider.StableId("fixture:"+scenario),scenario,0));
File.WriteAllText("artifacts/events.json",JsonSerializer.Serialize(fixtures,new JsonSerializerOptions{IncludeFields=true,DefaultIgnoreCondition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull}));
Console.WriteLine($"{passed} checks passed; emitted contract fixtures to artifacts/events.json");
