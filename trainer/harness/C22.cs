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
await Check("Фильтрация metadata", async () => { Eq("Id,Name",string.Join(",",Kata.ExportNames(typeof(ExportProbe)))); });
await Check("Наследование", async () => { Eq("Id,Name,Version",string.Join(",",Kata.ExportNames(typeof(ExportDerived)))); });
await Check("Без атрибутов", async () => { Eq(0,Kata.ExportNames(typeof(string)).Length); });
await Check("Null", async () => { Throws<ArgumentNullException>(()=>Kata.ExportNames(null!)); });
Console.WriteLine(JsonSerializer.Serialize(results));
public class ExportProbe {
 [Export]public int Id{get;set;} [Export]public string Name{get;set;}="";
 public string Secret{get;set;}="";[Export]public static int Global{get;set;}
 [Export]public int this[int i]=>i; [Export]public int WriteOnly{set{}} [Export]public int HiddenGetter{private get;set;}
}
public sealed class ExportDerived:ExportProbe{[Export]public int Version{get;set;}}