using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
var results = new List<object>();
async Task Check(string name, Func<Task> test) { try { await test(); results.Add(new {name,passed=true,detail=""}); } catch(Exception ex) { results.Add(new {name,passed=false,detail=ex.Message}); } }
void Eq<T>(T expected,T actual) { if (!EqualityComparer<T>.Default.Equals(expected,actual)) throw new Exception($"Expected {expected}; got {actual}"); }
await Check("Успех сразу", async () => { int n=0; Eq(7,await Kata.RetryAsync<int>(_=>{n++;return Task.FromResult(7);},3,default)); Eq(1,n); });
await Check("Сбой и успех", async () => { int n=0; Eq(9,await Kata.RetryAsync<int>(_=>++n<3?Task.FromException<int>(new HttpRequestException()):Task.FromResult(9),3,default)); Eq(3,n); });
await Check("Лимит", async () => { int n=0; try { await Kata.RetryAsync<int>(_=>{n++;throw new HttpRequestException();},2,default); throw new Exception("Expected failure"); } catch(HttpRequestException) {} Eq(2,n); });
await Check("Не повторять бизнес-ошибку", async () => { int n=0; try { await Kata.RetryAsync<int>(_=>{n++;throw new InvalidOperationException();},3,default); throw new Exception("Expected InvalidOperationException"); } catch(InvalidOperationException) {} Eq(1,n); });
await Check("Отмена до старта", async () => { int n=0; using var cts=new CancellationTokenSource();cts.Cancel();try { await Kata.RetryAsync<int>(_=>{n++;return Task.FromResult(1);},3,cts.Token);throw new Exception("Expected cancellation"); } catch(OperationCanceledException) {} Eq(0,n); });
await Check("Нулевой лимит", async () => { try { await Kata.RetryAsync<int>(_=>Task.FromResult(1),0,default);throw new Exception("Expected invalid argument"); }catch(ArgumentOutOfRangeException) {} });
Console.WriteLine(JsonSerializer.Serialize(results));
