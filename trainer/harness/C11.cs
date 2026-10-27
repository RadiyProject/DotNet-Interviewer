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
await Check("Завершённый пустой", async () => { var ch=System.Threading.Channels.Channel.CreateUnbounded<int>();ch.Writer.Complete();Eq(0L,await Kata.SumAsync(ch.Reader,default)); });
await Check("Сумма и long", async () => { var ch=System.Threading.Channels.Channel.CreateUnbounded<int>();ch.Writer.TryWrite(int.MaxValue);ch.Writer.TryWrite(int.MaxValue);ch.Writer.TryWrite(-1);ch.Writer.Complete();Eq(4294967293L,await Kata.SumAsync(ch.Reader,default)); });
await Check("Живой producer", async () => { var ch=System.Threading.Channels.Channel.CreateBounded<int>(1);var sum=Kata.SumAsync(ch.Reader,default);await ch.Writer.WriteAsync(4);await ch.Writer.WriteAsync(6);ch.Writer.Complete();Eq(10L,await sum); });
await Check("Предварительная отмена", async () => { var ch=System.Threading.Channels.Channel.CreateUnbounded<int>();using var cts=new CancellationTokenSource();cts.Cancel();await ThrowsAsync<OperationCanceledException>(async()=>await Kata.SumAsync(ch.Reader,cts.Token)); });
await Check("Ошибка completion", async () => { var ch=System.Threading.Channels.Channel.CreateUnbounded<int>();ch.Writer.Complete(new InvalidOperationException("producer"));await ThrowsAsync<InvalidOperationException>(async()=>await Kata.SumAsync(ch.Reader,default)); });
Console.WriteLine(JsonSerializer.Serialize(results));
