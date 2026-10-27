using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
var results = new List<object>();
async Task Check(string name, Func<Task> test) { try { await test(); results.Add(new {name,passed=true,detail=""}); } catch(Exception ex) { results.Add(new {name,passed=false,detail=ex.Message}); } }
void Eq<T>(T expected,T actual) { if (!EqualityComparer<T>.Default.Equals(expected,actual)) throw new Exception($"Expected {expected}; got {actual}"); }
void Throws<T>(Action action) where T:Exception { try { action(); } catch(T) {return;} throw new Exception($"Expected {typeof(T).Name}"); }
async Task ThrowsAsync<T>(Func<Task> action) where T:Exception {try{await action();}catch(T){return;}throw new Exception($"Expected {typeof(T).Name}");}
await Check("ASCII и пусто", async () => { Eq(0,Kata.CountScalars(""));Eq(3,Kata.CountScalars("abc")); });
await Check("Суррогатная пара", async () => { Eq(1,Kata.CountScalars("😀"));Eq(3,Kata.CountScalars("a😀b")); });
await Check("Комбинируемый знак", async () => { Eq(2,Kata.CountScalars("é")); });
await Check("Армянский текст", async () => { Eq(3,Kata.CountScalars("բար")); });
await Check("Null", async () => { Throws<ArgumentNullException>(()=>Kata.CountScalars(null!)); });
Console.WriteLine(JsonSerializer.Serialize(results));
