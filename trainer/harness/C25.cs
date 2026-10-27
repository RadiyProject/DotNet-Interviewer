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
await Check("Обновление и версия", async () => { await using var cn=new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");await cn.OpenAsync();await using var db=new DocDb(new DbContextOptionsBuilder<DocDb>().UseSqlite(cn).Options);await db.Database.EnsureCreatedAsync();db.Documents.Add(new Document{Id=1,Name="old",Version=0});await db.SaveChangesAsync();db.ChangeTracker.Clear();Eq(true,await Kata.TryRenameAsync(db,1,0,"new",default));Eq(0,db.ChangeTracker.Entries().Count());var row=await db.Documents.AsNoTracking().SingleAsync();Eq("new",row.Name);Eq(1L,row.Version); });
await Check("Stale не перезаписывает", async () => { await using var cn=new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");await cn.OpenAsync();await using var db=new DocDb(new DbContextOptionsBuilder<DocDb>().UseSqlite(cn).Options);await db.Database.EnsureCreatedAsync();db.Documents.Add(new Document{Id=1,Name="old",Version=0});await db.SaveChangesAsync();db.ChangeTracker.Clear();Eq(true,await Kata.TryRenameAsync(db,1,0,"first",default));Eq(false,await Kata.TryRenameAsync(db,1,0,"stale",default));var row=await db.Documents.AsNoTracking().SingleAsync();Eq("first",row.Name);Eq(1L,row.Version); });
await Check("Отсутствующий id", async () => { await using var cn=new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");await cn.OpenAsync();await using var db=new DocDb(new DbContextOptionsBuilder<DocDb>().UseSqlite(cn).Options);await db.Database.EnsureCreatedAsync();db.Documents.Add(new Document{Id=1,Name="old",Version=0});await db.SaveChangesAsync();db.ChangeTracker.Clear();Eq(false,await Kata.TryRenameAsync(db,99,0,"other",default));Eq("old",(await db.Documents.AsNoTracking().SingleAsync()).Name); });
await Check("Отмена", async () => { await using var cn=new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");await cn.OpenAsync();await using var db=new DocDb(new DbContextOptionsBuilder<DocDb>().UseSqlite(cn).Options);await db.Database.EnsureCreatedAsync();db.Documents.Add(new Document{Id=1,Name="old",Version=0});await db.SaveChangesAsync();db.ChangeTracker.Clear();using var cts=new CancellationTokenSource();cts.Cancel();await ThrowsAsync<OperationCanceledException>(async()=>await Kata.TryRenameAsync(db,1,0,"x",cts.Token)); });
Console.WriteLine(JsonSerializer.Serialize(results));
