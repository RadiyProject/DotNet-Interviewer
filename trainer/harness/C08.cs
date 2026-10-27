using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
var results = new List<object>();
async Task Check(string name, Func<Task> test) { try { await test(); results.Add(new {name,passed=true,detail=""}); } catch(Exception ex) { results.Add(new {name,passed=false,detail=ex.Message}); } }
void Eq<T>(T expected,T actual) { if (!EqualityComparer<T>.Default.Equals(expected,actual)) throw new Exception($"Expected {expected}; got {actual}"); }
await Check("Пусто", async () => { Eq(0,Kata.BuildOrder(0,Array.Empty<(int,int)>()).Length); });
await Check("Цепь", async () => { Eq("0,1,2",string.Join(",",Kata.BuildOrder(3,new[]{(0,1),(1,2)}))); });
await Check("Приоритет минимального", async () => { Eq("0,1,2,3",string.Join(",",Kata.BuildOrder(4,new[]{(0,2),(1,2)}))); });
await Check("Цикл", async () => { try { Kata.BuildOrder(2,new[]{(0,1),(1,0)});throw new Exception("Expected cycle"); }catch(InvalidOperationException) {} });
await Check("Self-loop", async () => { try { Kata.BuildOrder(1,new[]{(0,0)});throw new Exception("Expected cycle"); }catch(InvalidOperationException) {} });
Console.WriteLine(JsonSerializer.Serialize(results));
