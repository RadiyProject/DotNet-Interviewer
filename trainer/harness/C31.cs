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
await Check("Happy path",async()=>{var o=new OrderLifecycle();Eq(Stage.New,o.Current);Eq(true,o.TryApply(Signal.Pay));Eq(Stage.Paid,o.Current);Eq(true,o.TryApply(Signal.Ship));Eq(Stage.Shipped,o.Current);});
await Check("Запрет перехода",async()=>{var o=new OrderLifecycle();Eq(false,o.TryApply(Signal.Ship));Eq(Stage.New,o.Current);o.TryApply(Signal.Pay);o.TryApply(Signal.Ship);Eq(false,o.TryApply(Signal.Cancel));Eq(Stage.Shipped,o.Current);});
await Check("Повтор действия",async()=>{var o=new OrderLifecycle();o.TryApply(Signal.Pay);Eq(true,o.TryApply(Signal.Pay));Eq(Stage.Paid,o.Current);o.TryApply(Signal.Ship);Eq(true,o.TryApply(Signal.Ship));});
await Check("Cancel",async()=>{var o=new OrderLifecycle();Eq(true,o.TryApply(Signal.Cancel));Eq(true,o.TryApply(Signal.Cancel));Eq(false,o.TryApply(Signal.Pay));Eq(Stage.Cancelled,o.Current);});
Console.WriteLine(JsonSerializer.Serialize(results));
