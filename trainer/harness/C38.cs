using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
var results = new List<object>();
async Task Check(string name, Func<Task> test) { try { await test(); results.Add(new {name,passed=true,detail=""}); } catch(Exception ex) { results.Add(new {name,passed=false,detail=ex.Message}); } }
void Eq<T>(T expected,T actual) {if(!EqualityComparer<T>.Default.Equals(expected,actual))throw new Exception($"Expected {expected}, got {actual}");}
void Throws<T>(Action f) where T:Exception {try{f();}catch(T){return;}throw new Exception($"Expected {typeof(T).Name}");}
async Task ThrowsAsync<T>(Func<Task> f) where T:Exception {try{await f();}catch(T){return;}throw new Exception($"Expected {typeof(T).Name}");}
await Check("Синхронный callback",async()=>{var s=new SignalProbe(true);Eq(7,await Kata.FirstAsync(s,default));Eq(1,s.Disposals);});
await Check("Первый результат",async()=>{var s=new SignalProbe(false);var t=Kata.FirstAsync(s,default);s.Fire(4);s.Fire(9);Eq(4,await t);Eq(1,s.Disposals);});
await Check("Отмена после подписки",async()=>{var s=new SignalProbe(false);using var c=new CancellationTokenSource();var t=Kata.FirstAsync(s,c.Token);c.Cancel();await ThrowsAsync<OperationCanceledException>(async()=>await t);Eq(1,s.Disposals);});
await Check("Предварительная отмена",async()=>{var s=new SignalProbe(false);using var c=new CancellationTokenSource();c.Cancel();await ThrowsAsync<OperationCanceledException>(()=>Kata.FirstAsync(s,c.Token));Eq(0,s.Subscriptions);});
Console.WriteLine(JsonSerializer.Serialize(results));
public sealed class SignalProbe(bool synchronous):ISignal{public int Subscriptions,Disposals;Action<int>? callback;public IDisposable Subscribe(Action<int> cb){Subscriptions++;callback=cb;if(synchronous)cb(7);return new Lease(()=>Disposals++);}public void Fire(int x)=>callback?.Invoke(x);sealed class Lease(Action close):IDisposable{bool closed;public void Dispose(){if(!closed){closed=true;close();}}}}