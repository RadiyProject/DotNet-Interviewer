using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
var results = new List<object>();
async Task Check(string name, Func<Task> test) { try { await test(); results.Add(new {name,passed=true,detail=""}); } catch(Exception ex) { results.Add(new {name,passed=false,detail=ex.Message}); } }
void Eq<T>(T expected,T actual) { if (!EqualityComparer<T>.Default.Equals(expected,actual)) throw new Exception($"Expected {expected}; got {actual}"); }
await Check("Пусто", async () => { Eq(0,Kata.LongestUnique("")); });
await Check("Повторы", async () => { Eq(3,Kata.LongestUnique("abcabcbb")); });
await Check("Одинаковые", async () => { Eq(1,Kata.LongestUnique("bbbb")); });
await Check("Граница окна", async () => { Eq(2,Kata.LongestUnique("abba")); });
await Check("Не подпоследовательность", async () => { Eq(3,Kata.LongestUnique("pwwkew")); });
await Check("Уникальные", async () => { Eq(6,Kata.LongestUnique("abcdef")); });
Console.WriteLine(JsonSerializer.Serialize(results));
