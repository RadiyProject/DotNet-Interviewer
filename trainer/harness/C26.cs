using Dapper;
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
await Check("Tenant и exact name", async () => { await using var cn=new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");await cn.OpenAsync();await cn.ExecuteAsync("CREATE TABLE customers(id INTEGER PRIMARY KEY,tenant_id INTEGER,name TEXT);");await cn.ExecuteAsync("INSERT INTO customers VALUES(3,1,'Ada'),(1,1,'Ada'),(2,2,'Ada'),(4,1,'Other')");Eq("1,3",string.Join(",",await Kata.FindByNameAsync(cn,1,"Ada",default)));Eq(System.Data.ConnectionState.Open,cn.State); });
await Check("SQL-символы как данные", async () => { await using var cn=new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");await cn.OpenAsync();await cn.ExecuteAsync("CREATE TABLE customers(id INTEGER PRIMARY KEY,tenant_id INTEGER,name TEXT);");var name="' OR 1=1 --";await cn.ExecuteAsync("INSERT INTO customers VALUES(1,1,@Name),(2,1,'Other'),(3,2,@Name)",new {Name=name});Eq("1",string.Join(",",await Kata.FindByNameAsync(cn,1,name,default))); });
await Check("Нет совпадения", async () => { await using var cn=new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");await cn.OpenAsync();await cn.ExecuteAsync("CREATE TABLE customers(id INTEGER PRIMARY KEY,tenant_id INTEGER,name TEXT);");Eq(0,(await Kata.FindByNameAsync(cn,1,"missing",default)).Length); });
await Check("Отмена", async () => { await using var cn=new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");await cn.OpenAsync();await cn.ExecuteAsync("CREATE TABLE customers(id INTEGER PRIMARY KEY,tenant_id INTEGER,name TEXT);");using var cts=new CancellationTokenSource();cts.Cancel();await ThrowsAsync<OperationCanceledException>(async()=>await Kata.FindByNameAsync(cn,1,"Ada",cts.Token)); });
Console.WriteLine(JsonSerializer.Serialize(results));
