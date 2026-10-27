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
await Check("Начало и update", async () => { var s=new VersionedStore(7);Eq(new Snapshot(7,0),s.Read());Eq(true,s.TryWrite(0,8));Eq(new Snapshot(8,1),s.Read()); });
await Check("Конфликт без мутации", async () => { var s=new VersionedStore(7);Eq(false,s.TryWrite(-1,99));s.TryWrite(0,8);Eq(false,s.TryWrite(0,99));Eq(new Snapshot(8,1),s.Read()); });
await Check("Один конкурентный победитель", async () => { var s=new VersionedStore(0);using var gate=new ManualResetEventSlim();var jobs=Enumerable.Range(1,16).Select(x=>Task.Run(()=>{gate.Wait();return s.TryWrite(0,x);})).ToArray();gate.Set();var r=await Task.WhenAll(jobs);Eq(1,r.Count(x=>x));Eq(1L,s.Read().Version); });
await Check("Следующая версия", async () => { var s=new VersionedStore(1);Eq(true,s.TryWrite(0,2));Eq(true,s.TryWrite(1,3));Eq(new Snapshot(3,2),s.Read()); });
Console.WriteLine(JsonSerializer.Serialize(results));
