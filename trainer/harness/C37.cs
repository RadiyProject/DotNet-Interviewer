using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
void Reject(Func<int,int,bool> f){try{Kata.Verify(f);}catch(NUnit.Framework.AssertionException){return;}throw new Exception("Mutant accepted");}
var results = new List<object>();
async Task Check(string name, Func<Task> test) { try { await test(); results.Add(new {name,passed=true,detail=""}); } catch(Exception ex) { results.Add(new {name,passed=false,detail=ex.Message}); } }
void Eq<T>(T expected,T actual) {if(!EqualityComparer<T>.Default.Equals(expected,actual))throw new Exception($"Expected {expected}, got {actual}");}
void Throws<T>(Action f) where T:Exception {try{f();}catch(T){return;}throw new Exception($"Expected {typeof(T).Name}");}
async Task ThrowsAsync<T>(Func<Task> f) where T:Exception {try{await f();}catch(T){return;}throw new Exception($"Expected {typeof(T).Name}");}
await Check("Принимает корректную реализацию",async()=>{Kata.Verify((b,a)=>a>0 && b>=a);});
await Check("Отвергает always true",async()=>{Reject((b,a)=>true);});
await Check("Отвергает always false",async()=>{Reject((b,a)=>false);});
await Check("Ловит > вместо >=",async()=>{Reject((b,a)=>a>0 && b>a);});
await Check("Ловит zero amount",async()=>{Reject((b,a)=>a>=0 && b>=a);});
await Check("Ловит negative amount",async()=>{Reject((b,a)=>a!=0 && b>=a);});
await Check("Ловит отсутствие funds check",async()=>{Reject((b,a)=>a>0);});
Console.WriteLine(JsonSerializer.Serialize(results));
