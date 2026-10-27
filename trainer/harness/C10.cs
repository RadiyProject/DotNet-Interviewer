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
await Check("Кеширование", async () => { var c=new OnceCache();int n=0;Eq(7,c.Get("a",()=>{n++;return 7;}));Eq(7,c.Get("a",()=>99));Eq(1,n); });
await Check("Изоляция и регистр", async () => { var c=new OnceCache();Eq(1,c.Get("a",()=>1));Eq(2,c.Get("A",()=>2)); });
await Check("Параллельный старт", async () => { var c=new OnceCache();int n=0;using var gate=new ManualResetEventSlim();var jobs=Enumerable.Range(0,12).Select(_=>Task.Run(()=>{gate.Wait();return c.Get("x",()=>{Interlocked.Increment(ref n);return 42;});})).ToArray();gate.Set();var values=await Task.WhenAll(jobs);Eq(true,values.All(x=>x==42));Eq(1,n); });
await Check("Кеш исключения", async () => { var c=new OnceCache();int n=0;Throws<InvalidOperationException>(()=>c.Get("x",()=>{n++;throw new InvalidOperationException();}));Throws<InvalidOperationException>(()=>c.Get("x",()=>{n++;return 1;}));Eq(1,n); });
await Check("Null", async () => { var c=new OnceCache();Throws<ArgumentNullException>(()=>c.Get(null!,()=>1));Throws<ArgumentNullException>(()=>c.Get("a",null!)); });
Console.WriteLine(JsonSerializer.Serialize(results));
