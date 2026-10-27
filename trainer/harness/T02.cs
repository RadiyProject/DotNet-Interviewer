using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
var results = new List<object>();
async Task Check(string name, Func<Task> test) { try { await test(); results.Add(new {name,passed=true,detail=""}); } catch(Exception ex) { results.Add(new {name,passed=false,detail=ex.Message}); } }
void Eq<T>(T expected,T actual) { if (!EqualityComparer<T>.Default.Equals(expected,actual)) throw new Exception($"Expected {expected}; got {actual}"); }
await Check("Пустой набор", async () => { Eq(0,Kata.LastTwo(Array.Empty<Item>()).Count); });
await Check("Один активный", async () => { Eq(4,Kata.LastTwo(new[]{new Item(4,DateTimeOffset.UnixEpoch,true)}).Single().Id); });
await Check("Фильтрация и сортировка", async () => { Eq("2,3",string.Join(",",Kata.LastTwo(new[]{new Item(3,DateTimeOffset.UnixEpoch.AddDays(3),true),new Item(4,DateTimeOffset.MaxValue,false),new Item(1,DateTimeOffset.UnixEpoch,true),new Item(2,DateTimeOffset.UnixEpoch.AddDays(2),true)}).Select(x=>x.Id))); });
await Check("Одинаковая дата", async () => { Eq("2,3",string.Join(",",Kata.LastTwo(new[]{new Item(3,DateTimeOffset.UnixEpoch,true),new Item(1,DateTimeOffset.UnixEpoch,true),new Item(2,DateTimeOffset.UnixEpoch,true)}).Select(x=>x.Id))); });
await Check("Все неактивны", async () => { Eq(0,Kata.LastTwo(new[]{new Item(1,DateTimeOffset.UnixEpoch,false)}).Count); });
await Check("Null", async () => { try { Kata.LastTwo(null!); throw new Exception("Expected ArgumentNullException"); } catch(ArgumentNullException) {} });
Console.WriteLine(JsonSerializer.Serialize(results));
