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
await Check("Порядок",async()=>{Eq("6,2,4",string.Join(",",await Kata.TransformAsync(new[]{3,1,2},2,(x,ct)=>Task.FromResult(x*2),default)));});
await Check("Больше bounded capacity",async()=>{var xs=Enumerable.Range(0,40).ToArray();Eq(string.Join(",",xs.Select(x=>x+1)),string.Join(",",await Kata.TransformAsync(xs,2,(x,ct)=>Task.FromResult(x+1),default)));});
await Check("Пусто",async()=>{Eq(0,(await Kata.TransformAsync(Array.Empty<int>(),1,(x,ct)=>Task.FromResult(x),default)).Length);});
await Check("Степень и отмена",async()=>{await ThrowsAsync<ArgumentOutOfRangeException>(()=>Kata.TransformAsync(new[]{1},0,(x,ct)=>Task.FromResult(x),default));using var c=new CancellationTokenSource();c.Cancel();await ThrowsAsync<OperationCanceledException>(()=>Kata.TransformAsync(new[]{1},1,(x,ct)=>Task.FromResult(x),c.Token));});
await Check("Ошибка stage",async()=>{bool failed=false;try{await Kata.TransformAsync(new[]{1,2},1,(x,ct)=>Task.FromException<int>(new InvalidOperationException("stage")),default);}catch(InvalidOperationException){failed=true;}catch(AggregateException e)when(e.Flatten().InnerExceptions.Any(x=>x is InvalidOperationException)){failed=true;}Eq(true,failed);});
Console.WriteLine(JsonSerializer.Serialize(results));
