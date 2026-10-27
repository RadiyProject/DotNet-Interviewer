using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
static async IAsyncEnumerable<int> Source(int[] values,[System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct=default) { foreach(var x in values){ct.ThrowIfCancellationRequested();yield return x;await Task.Yield();} }
var results = new List<object>();
async Task Check(string name, Func<Task> test) { try { await test(); results.Add(new {name,passed=true,detail=""}); } catch(Exception ex) { results.Add(new {name,passed=false,detail=ex.Message}); } }
void Eq<T>(T expected,T actual) { if (!EqualityComparer<T>.Default.Equals(expected,actual)) throw new Exception($"Expected {expected}; got {actual}"); }
void Throws<T>(Action action) where T:Exception { try { action(); } catch(T) {return;} throw new Exception($"Expected {typeof(T).Name}"); }
async Task ThrowsAsync<T>(Func<Task> action) where T:Exception {try{await action();}catch(T){return;}throw new Exception($"Expected {typeof(T).Name}");}
await Check("Хвост", async () => { var chunks=new List<int[]>();await foreach(var a in Kata.BatchAsync(Source(new[]{1,2,3,4,5}),2))chunks.Add(a);Eq("1,2|3,4|5",string.Join("|",chunks.Select(a=>string.Join(",",a)))); });
await Check("Пусто", async () => { int n=0;await foreach(var a in Kata.BatchAsync(Source(Array.Empty<int>()),3))n++;Eq(0,n); });
await Check("Независимые массивы", async () => { var result=new List<int[]>();await foreach(var a in Kata.BatchAsync(Source(new[]{1,2,3,4}),2))result.Add(a);Eq(false,ReferenceEquals(result[0],result[1]));result[0][0]=99;Eq(3,result[1][0]); });
await Check("Неверный size", async () => { await ThrowsAsync<ArgumentOutOfRangeException>(async()=>{await foreach(var a in Kata.BatchAsync(Source(new[]{1}),0)){} }); });
await Check("Отмена источника", async () => { using var cts=new CancellationTokenSource();cts.Cancel();await ThrowsAsync<OperationCanceledException>(async()=>{await foreach(var a in Kata.BatchAsync(Source(new[]{1}),2,cts.Token)){} }); });
Console.WriteLine(JsonSerializer.Serialize(results));

