using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
var results = new List<object>();
async Task Check(string name, Func<Task> test) { try { await test(); results.Add(new {name,passed=true,detail=""}); } catch(Exception ex) { results.Add(new {name,passed=false,detail=ex.Message}); } }
void Eq<T>(T expected,T actual) {if(!EqualityComparer<T>.Default.Equals(expected,actual))throw new Exception($"Expected {expected}, got {actual}");}
void Throws<T>(Action f) where T:Exception {try{f();}catch(T){return;}throw new Exception($"Expected {typeof(T).Name}");}
async Task ThrowsAsync<T>(Func<Task> f) where T:Exception {try{await f();}catch(T){return;}throw new Exception($"Expected {typeof(T).Name}");}
await Check("Результат и no tracking",async()=>{await using var cn=new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");await cn.OpenAsync();await using var db=new LookupDb(new DbContextOptionsBuilder<LookupDb>().UseSqlite(cn).Options);await db.Database.EnsureCreatedAsync();db.Catalog.AddRange(new CatalogRow{Id=1,TenantId=1,Name="A",Price=20},new CatalogRow{Id=2,TenantId=1,Name="B",Price=10},new CatalogRow{Id=3,TenantId=2,Name="A",Price=30});await db.SaveChangesAsync();db.ChangeTracker.Clear();var ids=new List<int>();await foreach(var x in Kata.Query(db,1,10))ids.Add(x.Id);Eq("1,2",string.Join(",",ids));Eq(0,db.ChangeTracker.Entries().Count());});
await Check("Другие параметры",async()=>{await using var cn=new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");await cn.OpenAsync();await using var db=new LookupDb(new DbContextOptionsBuilder<LookupDb>().UseSqlite(cn).Options);await db.Database.EnsureCreatedAsync();db.Catalog.AddRange(new CatalogRow{Id=1,TenantId=1,Name="A",Price=20},new CatalogRow{Id=2,TenantId=1,Name="B",Price=10},new CatalogRow{Id=3,TenantId=2,Name="A",Price=30});await db.SaveChangesAsync();db.ChangeTracker.Clear();var ids=new List<int>();await foreach(var x in Kata.Query(db,2,0))ids.Add(x.Id);Eq("3",string.Join(",",ids));ids.Clear();await foreach(var x in Kata.Query(db,1,21))ids.Add(x.Id);Eq(0,ids.Count);});
await Check("Не закеширован первый tenant",async()=>{await using var cn=new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");await cn.OpenAsync();await using var db=new LookupDb(new DbContextOptionsBuilder<LookupDb>().UseSqlite(cn).Options);await db.Database.EnsureCreatedAsync();db.Catalog.AddRange(new CatalogRow{Id=1,TenantId=1,Name="A",Price=20},new CatalogRow{Id=2,TenantId=1,Name="B",Price=10},new CatalogRow{Id=3,TenantId=2,Name="A",Price=30});await db.SaveChangesAsync();db.ChangeTracker.Clear();await foreach(var x in Kata.Query(db,1,0)){}var ids=new List<int>();await foreach(var x in Kata.Query(db,2,30))ids.Add(x.Id);Eq("3",string.Join(",",ids));});
Console.WriteLine(JsonSerializer.Serialize(results));
