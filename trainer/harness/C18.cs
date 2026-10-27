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
await Check("Новый scope каждый раз", async () => { JobProbe.Reset();await using var root=new ServiceCollection().AddScoped<IJob,JobProbe>().BuildServiceProvider(new ServiceProviderOptions{ValidateScopes=true});var scopes=root.GetRequiredService<IServiceScopeFactory>();await Kata.RunOnceAsync(scopes,default);await Kata.RunOnceAsync(scopes,default);Eq(2,JobProbe.Created);Eq(2,JobProbe.Runs);Eq(2,JobProbe.Disposed); });
await Check("Cleanup после ошибки", async () => { JobProbe.Reset();JobProbe.Fail=true;await using var root=new ServiceCollection().AddScoped<IJob,JobProbe>().BuildServiceProvider();await ThrowsAsync<InvalidOperationException>(()=>Kata.RunOnceAsync(root.GetRequiredService<IServiceScopeFactory>(),default));Eq(1,JobProbe.Disposed); });
await Check("Тот же token", async () => { JobProbe.Reset();using var cts=new CancellationTokenSource();await using var root=new ServiceCollection().AddScoped<IJob,JobProbe>().BuildServiceProvider();await Kata.RunOnceAsync(root.GetRequiredService<IServiceScopeFactory>(),cts.Token);Eq(cts.Token,JobProbe.LastToken); });
Console.WriteLine(JsonSerializer.Serialize(results));
public sealed class JobProbe:IJob,IAsyncDisposable {
 public static int Created,Runs,Disposed;public static bool Fail;public static CancellationToken LastToken;
 public JobProbe(){Created++;}public static void Reset(){Created=Runs=Disposed=0;Fail=false;LastToken=default;}
 public Task ExecuteAsync(CancellationToken ct){Runs++;LastToken=ct;return Fail?Task.FromException(new InvalidOperationException("job")):Task.CompletedTask;}
 public ValueTask DisposeAsync(){Disposed++;return ValueTask.CompletedTask;}
}