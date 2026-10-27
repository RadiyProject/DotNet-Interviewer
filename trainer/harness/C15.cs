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
await Check("Лимит", async () => { var l=new WindowLimiter(2,TimeSpan.FromMinutes(1));var d=DateTimeOffset.UnixEpoch;Eq(true,l.TryAcquire("a",d));Eq(true,l.TryAcquire("a",d));Eq(false,l.TryAcquire("a",d)); });
await Check("Точная граница", async () => { var l=new WindowLimiter(1,TimeSpan.FromSeconds(10));var d=DateTimeOffset.UnixEpoch;Eq(true,l.TryAcquire("a",d));Eq(false,l.TryAcquire("a",d.AddSeconds(9)));Eq(true,l.TryAcquire("a",d.AddSeconds(10))); });
await Check("Разные ключи", async () => { var l=new WindowLimiter(1,TimeSpan.FromMinutes(1));var d=DateTimeOffset.UnixEpoch;Eq(true,l.TryAcquire("a",d));Eq(true,l.TryAcquire("A",d));Eq(false,l.TryAcquire("a",d)); });
await Check("Время назад", async () => { var l=new WindowLimiter(1,TimeSpan.FromSeconds(10));var d=DateTimeOffset.UnixEpoch;l.TryAcquire("a",d);Throws<ArgumentOutOfRangeException>(()=>l.TryAcquire("a",d.AddSeconds(-1))); });
await Check("Параметры", async () => { Throws<ArgumentOutOfRangeException>(()=>new WindowLimiter(0,TimeSpan.FromSeconds(1)));Throws<ArgumentOutOfRangeException>(()=>new WindowLimiter(1,TimeSpan.Zero)); });
Console.WriteLine(JsonSerializer.Serialize(results));
