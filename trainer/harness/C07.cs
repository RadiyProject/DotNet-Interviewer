using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
var results = new List<object>();
async Task Check(string name, Func<Task> test) { try { await test(); results.Add(new {name,passed=true,detail=""}); } catch(Exception ex) { results.Add(new {name,passed=false,detail=ex.Message}); } }
void Eq<T>(T expected,T actual) { if (!EqualityComparer<T>.Default.Equals(expected,actual)) throw new Exception($"Expected {expected}; got {actual}"); }
await Check("Первая строка", async () => { Eq("first",Kata.ReadFirstLine(()=>new System.IO.StringReader("first second"))); });
await Check("Пусто", async () => { Eq<string?>(null,Kata.ReadFirstLine(()=>new System.IO.StringReader(""))); });
await Check("Dispose при успехе", async () => { var r=new ProbeReader(false); Eq("ok",Kata.ReadFirstLine(()=>r));Eq(true,r.Disposed); });
await Check("Dispose при ошибке", async () => { var r=new ProbeReader(true);try {Kata.ReadFirstLine(()=>r);throw new Exception("Expected failure");}catch(System.IO.IOException){} Eq(true,r.Disposed); });
Console.WriteLine(JsonSerializer.Serialize(results));
public sealed class ProbeReader(bool fail): System.IO.TextReader {
 public bool Disposed; public override string? ReadLine() => fail ? throw new System.IO.IOException("read") : "ok";
 protected override void Dispose(bool disposing) { Disposed=true; base.Dispose(disposing); }
}