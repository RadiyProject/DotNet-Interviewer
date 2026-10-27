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
await Check("Точный public контракт", async () => { var a=new Account(7,"Ada","hash","token","note");using var d=JsonDocument.Parse(Kata.ToPublicJson(a));Eq("displayName,id",string.Join(",",d.RootElement.EnumerateObject().Select(p=>p.Name).OrderBy(x=>x,StringComparer.Ordinal)));Eq(7,d.RootElement.GetProperty("id").GetInt32());Eq("Ada",d.RootElement.GetProperty("displayName").GetString()); });
await Check("Нет утечки", async () => { var text=Kata.ToPublicJson(new Account(1,"A","HASH_SECRET","TOKEN_SECRET","NOTE_SECRET"));Eq(false,text.Contains("HASH_SECRET"));Eq(false,text.Contains("TOKEN_SECRET"));Eq(false,text.Contains("NOTE_SECRET")); });
await Check("Escaping", async () => { using var d=JsonDocument.Parse(Kata.ToPublicJson(new Account(1,"<admin>"","h","t","n")));Eq("<admin>"",d.RootElement.GetProperty("displayName").GetString()); });
await Check("Null", async () => { using var d=JsonDocument.Parse(Kata.ToPublicJson(new Account(1,null,"h","t","n")));Eq(JsonValueKind.Null,d.RootElement.GetProperty("displayName").ValueKind); });
Console.WriteLine(JsonSerializer.Serialize(results));
