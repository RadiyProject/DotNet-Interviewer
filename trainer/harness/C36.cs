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
await Check("Регистр и null",async()=>{var r=Kata.CountWords(new string?[]{"A","a","B",null,""},2);Eq(3,r.Count);Eq(2,r["a"]);Eq(1,r["b"]);Eq(1,r[""]);});
await Check("Contention",async()=>{var xs=Enumerable.Range(0,20000).Select(i=>(string?)(i%2==0?"key":"KEY")).ToArray();var r=Kata.CountWords(xs,4);Eq(1,r.Count);Eq(20000,r["key"]);});
await Check("Пусто",async()=>{Eq(0,Kata.CountWords(Array.Empty<string?>(),1).Count);});
await Check("Аргументы",async()=>{Throws<ArgumentNullException>(()=>Kata.CountWords(null!,1));Throws<ArgumentOutOfRangeException>(()=>Kata.CountWords(new[]{"a"},0));});
Console.WriteLine(JsonSerializer.Serialize(results));
