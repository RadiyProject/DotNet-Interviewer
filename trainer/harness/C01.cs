using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
var results = new List<object>();
async Task Check(string name, Func<Task> test) { try { await test(); results.Add(new {name,passed=true,detail=""}); } catch(Exception ex) { results.Add(new {name,passed=false,detail=ex.Message}); } }
void Eq<T>(T expected,T actual) { if (!EqualityComparer<T>.Default.Equals(expected,actual)) throw new Exception($"Expected {expected}; got {actual}"); }
await Check("Обычный", async () => { Eq("0,1",string.Join(",",Kata.TwoSum(new[]{2,7,11,15},9))); });
await Check("Повтор", async () => { Eq("0,1",string.Join(",",Kata.TwoSum(new[]{3,3},6))); });
await Check("Нет решения", async () => { Eq(0,Kata.TwoSum(new[]{2},4).Length); });
await Check("Отрицательные", async () => { Eq("0,2",string.Join(",",Kata.TwoSum(new[]{-3,4,3},0))); });
await Check("Переполнение", async () => { Eq(0,Kata.TwoSum(new[]{int.MaxValue,1},int.MinValue).Length); });
await Check("Первый левый", async () => { Eq("0,2",string.Join(",",Kata.TwoSum(new[]{1,1,3},4))); });
Console.WriteLine(JsonSerializer.Serialize(results));
