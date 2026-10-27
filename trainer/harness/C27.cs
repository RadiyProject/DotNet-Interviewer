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
await Check("Делегирование",async()=>{var s=new ShippingProbe(900);var q=new ShippingQuote(s);Eq(900L,q.Quote(120));Eq(1,s.Calls);Eq(120,s.Last);});
await Check("Сменяемые стратегии",async()=>{Eq(7L,new ShippingQuote(new ShippingProbe(7)).Quote(1));Eq(11L,new ShippingQuote(new ShippingProbe(11)).Quote(1));});
await Check("Не вызывать при плохом входе",async()=>{var s=new ShippingProbe(1);var q=new ShippingQuote(s);Throws<ArgumentOutOfRangeException>(()=>q.Quote(0));Throws<ArgumentOutOfRangeException>(()=>q.Quote(-1));Eq(0,s.Calls);});
await Check("Null dependency",async()=>{Throws<ArgumentNullException>(()=>new ShippingQuote(null!));});
Console.WriteLine(JsonSerializer.Serialize(results));
public sealed class ShippingProbe(long result):IShippingStrategy {public int Calls,Last;public long QuoteCents(int w){Calls++;Last=w;return result;}}