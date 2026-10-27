using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
var results = new List<object>();
async Task Check(string name, Func<Task> test) { try { await test(); results.Add(new {name,passed=true,detail=""}); } catch(Exception ex) { results.Add(new {name,passed=false,detail=ex.Message}); } }
void Eq<T>(T expected,T actual) { if (!EqualityComparer<T>.Default.Equals(expected,actual)) throw new Exception($"Expected {expected}; got {actual}"); }
await Check("Пустой набор", async () => { Eq(0,(await Kata.MapAsync(Array.Empty<int>(),2,(x,ct)=>Task.FromResult(x),default)).Length); });
await Check("Порядок", async () => { Eq("6,2,4",string.Join(",",await Kata.MapAsync(new[]{3,1,2},2,async (x,ct)=>{await Task.Delay(x*5,ct);return x*2;},default))); });
await Check("Предел конкуренции", async () => { int current=0,max=0; await Kata.MapAsync(Enumerable.Range(0,12).ToArray(),2,async (x,ct)=>{int n=Interlocked.Increment(ref current);lock(typeof(Kata)){max=Math.Max(max,n);}await Task.Delay(10,ct);Interlocked.Decrement(ref current);return x;},default); Eq(true,max<=2);Eq(0,current); });
await Check("Некорректный degree", async () => { try { await Kata.MapAsync(new[]{1},0,(x,ct)=>Task.FromResult(x),default);throw new Exception("Expected argument"); }catch(ArgumentOutOfRangeException) {} });
await Check("Отмена", async () => { using var cts=new CancellationTokenSource();cts.Cancel();try { await Kata.MapAsync(new[]{1},1,(x,ct)=>Task.FromResult(x),cts.Token);throw new Exception("Expected cancellation"); }catch(OperationCanceledException) {} });
await Check("Ошибка operation", async () => { try { await Kata.MapAsync(new[]{1},1,(x,ct)=>throw new InvalidOperationException("boom"),default);throw new Exception("Expected operation error"); }catch(InvalidOperationException ex){Eq("boom",ex.Message);} });
Console.WriteLine(JsonSerializer.Serialize(results));
