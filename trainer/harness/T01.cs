using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
var results = new List<object>();
async Task Check(string name, Func<Task> test) { try { await test(); results.Add(new {name,passed=true,detail=""}); } catch(Exception ex) { results.Add(new {name,passed=false,detail=ex.Message}); } }
void Eq<T>(T expected,T actual) { if (!EqualityComparer<T>.Default.Equals(expected,actual)) throw new Exception($"Expected {expected}; got {actual}"); }

HttpClient Client(ScriptedHandler h) => new HttpClient(h) { BaseAddress=new Uri("https://example.test/") };
await Check("200 JSON", async () => { using var h=new ScriptedHandler(200);using var c=Client(h); var w=await new WeatherClient(c).GetAsync("Yerevan",default);if(w is null)throw new Exception("Для 200 ожидался Weather, получен null");Eq("Yerevan",w.City);Eq(22m,w.Celsius); });
await Check("404 без повторов", async () => { using var h=new ScriptedHandler(404);using var c=Client(h);Eq<Weather?>(null,await new WeatherClient(c).GetAsync("Yerevan",default));Eq(1,h.Calls);Eq(true,h.Contents.All(x=>x.Disposed)); });
await Check("503,503,200", async () => { using var h=new ScriptedHandler(503,503,200);using var c=Client(h);var w=await new WeatherClient(c).GetAsync("Yerevan",default);if(w is null)throw new Exception("Ожидался Weather после успешного ответа; получен null. Проверь число попыток и обработку 200.");Eq(22m,w.Celsius);Eq(3,h.Calls);Eq(true,h.Contents.All(x=>x.Disposed)); });
await Check("429,200", async () => { using var h=new ScriptedHandler(429,200);using var c=Client(h);var w=await new WeatherClient(c).GetAsync("Yerevan",default);if(w is null)throw new Exception("Ожидался Weather после успешного ответа; получен null. Проверь число попыток и обработку 200.");Eq(22m,w.Celsius);Eq(2,h.Calls); });
await Check("400 без повторов", async () => { using var h=new ScriptedHandler(400);using var c=Client(h);try {await new WeatherClient(c).GetAsync("Yerevan",default);throw new Exception("400 должен выбрасывать HttpRequestException без повторов, а не возвращать null");}catch(HttpRequestException){} Eq(1,h.Calls);Eq(true,h.Contents.All(x=>x.Disposed)); });
await Check("Не более трёх", async () => { using var h=new ScriptedHandler(503,503,503,200);using var c=Client(h);try {await new WeatherClient(c).GetAsync("Yerevan",default);throw new Exception("После трёх неуспешных попыток ожидался HttpRequestException, а не null");}catch(HttpRequestException){} Eq(3,h.Calls); });
await Check("Отмена до старта", async () => { using var h=new ScriptedHandler(200);using var c=Client(h);using var ct=new CancellationTokenSource();ct.Cancel();try{await new WeatherClient(c).GetAsync("Yerevan",ct.Token);throw new Exception("Expected cancellation");}catch(OperationCanceledException){} Eq(0,h.Calls); });
await Check("Dispose успешного response", async () => { using var h=new ScriptedHandler(200);using var c=Client(h);await new WeatherClient(c).GetAsync("Yerevan",default);Eq(true,h.Contents.All(x=>x.Disposed)); });
await Check("URL encoding", async () => { using var h=new ScriptedHandler(200);using var c=Client(h);await new WeatherClient(c).GetAsync("A&B",default);Eq("?city=A%26B",h.Last!.Query); });
await Check("HttpClient остаётся у владельца", async () => { using var h=new ScriptedHandler(200);using var c=Client(h);await new WeatherClient(c).GetAsync("Yerevan",default);using var r=await c.GetAsync("ping");Eq(2,h.Calls); });
await Check("Отмена ожидающего HTTP", async () => { using var h=new BlockingHandler();using var c=new HttpClient(h){BaseAddress=new Uri("https://example.test/")};using var ct=new CancellationTokenSource();var call=new WeatherClient(c).GetAsync("Yerevan",ct.Token);await h.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));ct.Cancel();try{await call.WaitAsync(TimeSpan.FromSeconds(2));throw new Exception("Expected cancellation");}catch(OperationCanceledException){} });
Console.WriteLine(JsonSerializer.Serialize(results));
public sealed class ProbeContent : StringContent {
 public bool Disposed;
 public ProbeContent():base("{\"City\":\"Yerevan\",\"Celsius\":22}",System.Text.Encoding.UTF8,"application/json") {}
 protected override void Dispose(bool disposing) {Disposed=true;base.Dispose(disposing);}
}
public sealed class ScriptedHandler(params int[] statuses) : HttpMessageHandler {
 public int Calls; public Uri? Last; public List<ProbeContent> Contents=new();
 protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) {
  ct.ThrowIfCancellationRequested(); Last=request.RequestUri;int s=statuses[Math.Min(Calls,statuses.Length-1)];Calls++;
  var content=new ProbeContent();Contents.Add(content);return Task.FromResult(new HttpResponseMessage((System.Net.HttpStatusCode)s){Content=content});
 }
}
public sealed class BlockingHandler : HttpMessageHandler {
 public TaskCompletionSource<bool> Entered=new(TaskCreationOptions.RunContinuationsAsynchronously);
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) {
  Entered.TrySetResult(true);await Task.Delay(Timeout.Infinite,ct);return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
 }
}
