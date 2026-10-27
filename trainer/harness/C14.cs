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
await Check("Miss", async () => { var c=new LruCache(2);Eq(false,c.TryGet(1,out var v));Eq(0,v); });
await Check("Eviction", async () => { var c=new LruCache(2);c.Set(1,10);c.Set(2,20);c.TryGet(1,out _);c.Set(3,30);Eq(false,c.TryGet(2,out _));Eq(true,c.TryGet(1,out var v));Eq(10,v); });
await Check("Update не занимает слот", async () => { var c=new LruCache(2);c.Set(1,10);c.Set(2,20);c.Set(1,11);c.Set(3,30);Eq(false,c.TryGet(2,out _));Eq(true,c.TryGet(1,out var v));Eq(11,v); });
await Check("Capacity 1", async () => { var c=new LruCache(1);c.Set(1,1);c.Set(2,2);Eq(false,c.TryGet(1,out _));Eq(true,c.TryGet(2,out _)); });
await Check("Неверная capacity", async () => { Throws<ArgumentOutOfRangeException>(()=>new LruCache(0)); });
Console.WriteLine(JsonSerializer.Serialize(results));
