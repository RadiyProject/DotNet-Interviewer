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
await Check("Обрезка", async () => { Eq(true,Kata.TryPair("  city = Yerevan  ",out var k,out var v));Eq("city",k);Eq("Yerevan",v); });
await Check("Без разделителя", async () => { Eq(false,Kata.TryPair("abc",out var k,out var v));Eq("",k);Eq("",v); });
await Check("Несколько разделителей", async () => { Eq(false,Kata.TryPair("a=b=c",out var k,out var v));Eq("",k);Eq("",v); });
await Check("Пустая часть", async () => { Eq(false,Kata.TryPair(" =x",out _,out _));Eq(false,Kata.TryPair("a= ",out _,out _));Eq(false,Kata.TryPair("",out _,out _)); });
await Check("Внутренние пробелы", async () => { Eq(true,Kata.TryPair("display name=two words",out var k,out var v));Eq("display name",k);Eq("two words",v); });
Console.WriteLine(JsonSerializer.Serialize(results));
