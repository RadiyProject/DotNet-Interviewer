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
await Check("Хвост",async()=>{var r=await Kata.BatchesAsync(new[]{1,2,3,4,5},2,default);Eq("1,2|3,4|5",string.Join("|",r.Select(a=>string.Join(",",a))));});
await Check("Пусто",async()=>{Eq(0,(await Kata.BatchesAsync(Array.Empty<int>(),3,default)).Length);});
await Check("Полные группы",async()=>{Eq(2,(await Kata.BatchesAsync(new[]{1,2,3,4},2,default)).Length);});
await Check("Размер",async()=>{await ThrowsAsync<ArgumentOutOfRangeException>(()=>Kata.BatchesAsync(new[]{1},0,default));});
await Check("Отмена",async()=>{using var c=new CancellationTokenSource();c.Cancel();await ThrowsAsync<OperationCanceledException>(()=>Kata.BatchesAsync(new[]{1},2,c.Token));});
Console.WriteLine(JsonSerializer.Serialize(results));
