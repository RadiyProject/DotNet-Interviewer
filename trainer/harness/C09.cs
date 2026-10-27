using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
var results = new List<object>();
async Task Check(string name, Func<Task> test) { try { await test(); results.Add(new {name,passed=true,detail=""}); } catch(Exception ex) { results.Add(new {name,passed=false,detail=ex.Message}); } }
void Eq<T>(T expected,T actual) { if (!EqualityComparer<T>.Default.Equals(expected,actual)) throw new Exception($"Expected {expected}; got {actual}"); }
await Check("Границы и статус", async () => { var d=DateTimeOffset.UnixEpoch;var r=Kata.Summarize(new[]{new Order(1,d,600,true),new Order(1,d.AddDays(1),500,true),new Order(1,d.AddDays(2),900,true),new Order(2,d,5000,false)},d,d.AddDays(2),1000);Eq(1,r.Length);Eq(1100m,r[0].Total);Eq(2,r[0].Count); });
await Check("Строгий порог", async () => { var d=DateTimeOffset.UnixEpoch;Eq(0,Kata.Summarize(new[]{new Order(1,d,1000,true)},d,d.AddDays(1),1000).Length); });
await Check("Порядок ties", async () => { var d=DateTimeOffset.UnixEpoch;Eq("1,2",string.Join(",",Kata.Summarize(new[]{new Order(2,d,5,true),new Order(1,d,5,true)},d,d.AddDays(1),0).Select(x=>x.CustomerId))); });
await Check("Пусто", async () => { Eq(0,Kata.Summarize(Array.Empty<Order>(),DateTimeOffset.MinValue,DateTimeOffset.MaxValue,0).Length); });
Console.WriteLine(JsonSerializer.Serialize(results));
