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
await Check("Вложенность",async()=>{CostNode n=new GroupCost(new CostNode[]{new LineCost(10),new GroupCost(new CostNode[]{new LineCost(20),new LineCost(30)})});Eq(60L,n.TotalCents());});
await Check("Пусто и повтор узла",async()=>{var leaf=new LineCost(7);Eq(0L,new GroupCost(Array.Empty<CostNode>()).TotalCents());Eq(14L,new GroupCost(new CostNode[]{leaf,leaf}).TotalCents());});
await Check("Snapshot входа",async()=>{var xs=new List<CostNode>{new LineCost(5)};var g=new GroupCost(xs);xs.Clear();Eq(5L,g.TotalCents());});
await Check("Overflow",async()=>{Throws<OverflowException>(()=>new GroupCost(new CostNode[]{new LineCost(long.MaxValue),new LineCost(1)}).TotalCents());});
await Check("Аргументы",async()=>{Throws<ArgumentOutOfRangeException>(()=>new LineCost(-1));Throws<ArgumentNullException>(()=>new GroupCost(null!));Throws<ArgumentException>(()=>new GroupCost(new CostNode[]{null!}));});
Console.WriteLine(JsonSerializer.Serialize(results));
