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
await Check("Порядок и нормализация", async () => { var id=Guid.NewGuid();var ctx=new DefaultHttpContext();ctx.Request.Headers["X-Correlation-ID"]=id.ToString("N");int n=0;var m=Kata.Correlation(c=>{n++;Eq(id.ToString("D"),c.Request.Headers["X-Correlation-ID"].ToString());Eq(id.ToString("D"),c.Response.Headers["X-Correlation-ID"].ToString());return Task.CompletedTask;});await m(ctx);Eq(1,n); });
await Check("Нет заголовка", async () => { var ctx=new DefaultHttpContext();await Kata.Correlation(_=>Task.CompletedTask)(ctx);Eq(true,Guid.TryParse(ctx.Response.Headers["X-Correlation-ID"].ToString(),out _));Eq(ctx.Request.Headers["X-Correlation-ID"].ToString(),ctx.Response.Headers["X-Correlation-ID"].ToString()); });
await Check("Невалидное значение", async () => { var ctx=new DefaultHttpContext();ctx.Request.Headers["X-Correlation-ID"]="untrusted text";await Kata.Correlation(_=>Task.CompletedTask)(ctx);Eq(true,Guid.TryParse(ctx.Response.Headers["X-Correlation-ID"].ToString(),out _)); });
await Check("Исключение next", async () => { await ThrowsAsync<InvalidOperationException>(()=>Kata.Correlation(_=>throw new InvalidOperationException("next"))(new DefaultHttpContext())); });
Console.WriteLine(JsonSerializer.Serialize(results));
