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
await Check("Несколько чтений", async () => { using var src=new ChunkStream(new byte[]{1,2,3,4,5});using var dst=new MemoryStream();Eq(5L,await Kata.CopyAsync(src,dst,4,default));Eq("1,2,3,4,5",string.Join(",",dst.ToArray())); });
await Check("Пустой stream", async () => { using var src=new MemoryStream();using var dst=new MemoryStream();Eq(0L,await Kata.CopyAsync(src,dst,1,default)); });
await Check("Оба stream открыты", async () => { using var src=new MemoryStream(new byte[]{1,2});using var dst=new MemoryStream();await Kata.CopyAsync(src,dst,1,default);Eq(true,src.CanRead);Eq(true,dst.CanWrite); });
await Check("Отмена", async () => { using var src=new MemoryStream(new byte[]{1});using var dst=new MemoryStream();using var cts=new CancellationTokenSource();cts.Cancel();await ThrowsAsync<OperationCanceledException>(async()=>await Kata.CopyAsync(src,dst,2,cts.Token)); });
await Check("Неверный буфер", async () => { using var src=new MemoryStream();using var dst=new MemoryStream();await ThrowsAsync<ArgumentOutOfRangeException>(async()=>await Kata.CopyAsync(src,dst,0,default)); });
Console.WriteLine(JsonSerializer.Serialize(results));
public sealed class ChunkStream(byte[] bytes):MemoryStream(bytes) {
 public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken ct=default)=>base.ReadAsync(buffer[..Math.Min(2,buffer.Length)],ct);
 public override Task<int> ReadAsync(byte[] buffer,int offset,int count,CancellationToken ct)=>base.ReadAsync(buffer,offset,Math.Min(2,count),ct);
}