using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
var results = new List<object>();
async Task Check(string name, Func<Task> test) { try { await test(); results.Add(new {name,passed=true,detail=""}); } catch(Exception ex) { results.Add(new {name,passed=false,detail=ex.Message}); } }
void Eq<T>(T expected,T actual) { if (!EqualityComparer<T>.Default.Equals(expected,actual)) throw new Exception($"Expected {expected}; got {actual}"); }
void Throws<T>(Action action) where T:Exception { try { action(); } catch(T) {return;} throw new Exception($"Expected {typeof(T).Name}"); }
async Task ThrowsAsync<T>(Func<Task> action) where T:Exception {try{await action();}catch(T){return;}throw new Exception($"Expected {typeof(T).Name}");}
await Check("404", async () => { using var services=new ServiceCollection().AddOptions().BuildServiceProvider();var ctx=new DefaultHttpContext{RequestServices=services};ctx.Response.Body=new MemoryStream();await Kata.Errors(_=>throw new KeyNotFoundException("secret"))(ctx);Eq(404,ctx.Response.StatusCode);ctx.Response.Body.Position=0;using var doc=await JsonDocument.ParseAsync(ctx.Response.Body);Eq(404,doc.RootElement.GetProperty("status").GetInt32());Eq(true,doc.RootElement.GetProperty("title").GetString()!.Length>0);Eq(true,ctx.Response.ContentType!.StartsWith("application/problem+json")); });
await Check("400", async () => { using var services=new ServiceCollection().AddOptions().BuildServiceProvider();var ctx=new DefaultHttpContext{RequestServices=services};ctx.Response.Body=new MemoryStream();await Kata.Errors(_=>throw new ArgumentException("secret"))(ctx);Eq(400,ctx.Response.StatusCode); });
await Check("500 без утечки", async () => { using var services=new ServiceCollection().AddOptions().BuildServiceProvider();var ctx=new DefaultHttpContext{RequestServices=services};ctx.Response.Body=new MemoryStream();await Kata.Errors(_=>throw new InvalidOperationException("PASSWORD_SECRET"))(ctx);Eq(500,ctx.Response.StatusCode);ctx.Response.Body.Position=0;var text=await new StreamReader(ctx.Response.Body).ReadToEndAsync();Eq(false,text.Contains("PASSWORD_SECRET"));using var doc=JsonDocument.Parse(text);Eq(500,doc.RootElement.GetProperty("status").GetInt32()); });
await Check("Успех без изменений", async () => { using var services=new ServiceCollection().AddOptions().BuildServiceProvider();var ctx=new DefaultHttpContext{RequestServices=services};ctx.Response.Body=new MemoryStream();await Kata.Errors(c=>{c.Response.StatusCode=204;return Task.CompletedTask;})(ctx);Eq(204,ctx.Response.StatusCode); });
await Check("Отмена", async () => { var ctx=new DefaultHttpContext();using var cts=new CancellationTokenSource();cts.Cancel();ctx.RequestAborted=cts.Token;await ThrowsAsync<OperationCanceledException>(()=>Kata.Errors(_=>throw new OperationCanceledException(cts.Token))(ctx)); });
Console.WriteLine(JsonSerializer.Serialize(results));
