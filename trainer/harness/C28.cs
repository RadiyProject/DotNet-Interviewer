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
await Check("Fee",async()=>{var p=new PriceProbe();Eq(105m,new FeeRule(p,5).Apply(100));Eq(1,p.Calls);});
await Check("Discount",async()=>{var p=new PriceProbe();Eq(90m,new DiscountRule(p,.1m).Apply(100));Eq(1,p.Calls);});
await Check("Порядок имеет значение",async()=>{Eq(94.5m,new DiscountRule(new FeeRule(new PriceProbe(),5),.1m).Apply(100));Eq(95m,new FeeRule(new DiscountRule(new PriceProbe(),.1m),5).Apply(100));});
await Check("Границы",async()=>{Eq(0m,new DiscountRule(new PriceProbe(),1).Apply(7));Throws<ArgumentOutOfRangeException>(()=>new DiscountRule(new PriceProbe(),1.1m));Throws<ArgumentOutOfRangeException>(()=>new FeeRule(new PriceProbe(),-1));Throws<ArgumentOutOfRangeException>(()=>new FeeRule(new PriceProbe(),0).Apply(-1));});
await Check("Null",async()=>{Throws<ArgumentNullException>(()=>new FeeRule(null!,0));Throws<ArgumentNullException>(()=>new DiscountRule(null!,0));});
Console.WriteLine(JsonSerializer.Serialize(results));
public sealed class PriceProbe:IPriceRule{public int Calls;public decimal Apply(decimal p){Calls++;return p;}}