using Microsoft.EntityFrameworkCore;
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
await Check("Фильтрация и порядок", async () => { await using var cn=new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");await cn.OpenAsync();await using var db=new ShopDb(new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<ShopDb>().UseSqlite(cn).Options);await db.Database.EnsureCreatedAsync();db.Products.AddRange(new Product{Id=1,TenantId=1,Name="A",PriceCents=100,Active=true},new Product{Id=2,TenantId=1,Name="B",PriceCents=200,Active=true},new Product{Id=3,TenantId=2,Name="OTHER",PriceCents=1000,Active=true},new Product{Id=4,TenantId=1,Name="OFF",PriceCents=900,Active=false},new Product{Id=5,TenantId=1,Name="C",PriceCents=200,Active=true});await db.SaveChangesAsync();db.ChangeTracker.Clear();var r=await Kata.FindAsync(db,1,100,default);Eq("2,5,1",string.Join(",",r.Select(x=>x.Id)));Eq("B",r[0].Name);Eq(0,db.ChangeTracker.Entries().Count()); });
await Check("Пустой tenant", async () => { await using var cn=new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");await cn.OpenAsync();await using var db=new ShopDb(new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<ShopDb>().UseSqlite(cn).Options);await db.Database.EnsureCreatedAsync();Eq(0,(await Kata.FindAsync(db,99,0,default)).Length); });
await Check("Граница цены", async () => { await using var cn=new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");await cn.OpenAsync();await using var db=new ShopDb(new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<ShopDb>().UseSqlite(cn).Options);await db.Database.EnsureCreatedAsync();db.Products.Add(new Product{Id=1,TenantId=1,Name="Exact",PriceCents=100,Active=true});await db.SaveChangesAsync();db.ChangeTracker.Clear();Eq(1,(await Kata.FindAsync(db,1,100,default)).Length);Eq(0,(await Kata.FindAsync(db,1,101,default)).Length); });
await Check("Отмена", async () => { await using var cn=new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");await cn.OpenAsync();await using var db=new ShopDb(new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<ShopDb>().UseSqlite(cn).Options);await db.Database.EnsureCreatedAsync();using var cts=new CancellationTokenSource();cts.Cancel();await ThrowsAsync<OperationCanceledException>(async()=>await Kata.FindAsync(db,1,0,cts.Token)); });
Console.WriteLine(JsonSerializer.Serialize(results));
