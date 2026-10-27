using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
var results = new List<object>();
async Task Check(string name, Func<Task> test) { try { await test(); results.Add(new {name,passed=true,detail=""}); } catch(Exception ex) { results.Add(new {name,passed=false,detail=ex.Message}); } }
void Eq<T>(T expected,T actual) { if (!EqualityComparer<T>.Default.Equals(expected,actual)) throw new Exception($"Expected {expected}; got {actual}"); }
await Check("Пусто", async () => { Eq(0,Kata.Merge(Array.Empty<SpanRange>()).Length); });
await Check("Пересечение", async () => { Eq("1:6;8:10",string.Join(";",Kata.Merge(new[]{new SpanRange(2,6),new SpanRange(1,3),new SpanRange(8,10)}).Select(x=>$"{x.Start}:{x.End}"))); });
await Check("Касание", async () => { Eq(1,Kata.Merge(new[]{new SpanRange(1,2),new SpanRange(2,3)}).Length); });
await Check("Вложение", async () => { Eq(10,Kata.Merge(new[]{new SpanRange(1,10),new SpanRange(2,3)}).Single().End); });
await Check("Некорректный", async () => { try { Kata.Merge(new[]{new SpanRange(3,2)}); throw new Exception("Expected ArgumentException"); } catch(ArgumentException) {} });
await Check("Не мутировать", async () => { var a=new[]{new SpanRange(5,6),new SpanRange(1,2)}; Kata.Merge(a); Eq(5,a[0].Start); });
Console.WriteLine(JsonSerializer.Serialize(results));
