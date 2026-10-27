using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
var results = new List<object>();
async Task Check(string name, Func<Task> test) { try { await test(); results.Add(new {name,passed=true,detail=""}); } catch(Exception ex) { results.Add(new {name,passed=false,detail=ex.Message}); } }
void Eq<T>(T expected,T actual) { if (!EqualityComparer<T>.Default.Equals(expected,actual)) throw new Exception($"Expected {expected}; got {actual}"); }
await Check("Пусто", async () => { Eq(true,Kata.Balanced("")); });
await Check("Вложенные", async () => { Eq(true,Kata.Balanced("a{[()]}b")); });
await Check("Перекрёстные", async () => { Eq(false,Kata.Balanced("([)]")); });
await Check("Лишняя закрывающая", async () => { Eq(false,Kata.Balanced(")")); });
await Check("Незакрытая", async () => { Eq(false,Kata.Balanced("(()")); });
Console.WriteLine(JsonSerializer.Serialize(results));
