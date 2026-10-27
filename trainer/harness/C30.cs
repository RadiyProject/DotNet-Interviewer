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
await Check("Порядок и short circuit",async()=>{var seen=new List<int>();var c=new RuleChain(new IOrderRule[]{new RuleProbe(()=>{seen.Add(1);return null;}),new RuleProbe(()=>{seen.Add(2);return "denied";}),new RuleProbe(()=>{seen.Add(3);return null;})});Eq("denied",c.Validate(new(1,7)));Eq("1,2",string.Join(",",seen));});
await Check("Пустая цепочка",async()=>{Eq<string?>(null,new RuleChain(Array.Empty<IOrderRule>()).Validate(new(1,1)));});
await Check("Пустая строка тоже ошибка",async()=>{var c=new RuleChain(new[]{new RuleProbe(()=>""),new RuleProbe(()=>throw new Exception("must not run"))});Eq("",c.Validate(new(1,1)));});
await Check("Snapshot",async()=>{var xs=new List<IOrderRule>{new RuleProbe(()=>"blocked")};var c=new RuleChain(xs);xs.Clear();Eq("blocked",c.Validate(new(1,1)));});
Console.WriteLine(JsonSerializer.Serialize(results));
public sealed class RuleProbe(Func<string?> f):IOrderRule{public string? Check(OrderRequest r)=>f();}