using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
var results=new List<object>();
void Check(string name,Action test){try{test();results.Add(new{name,passed=true,detail=""});}catch(Exception e){results.Add(new{name,passed=false,detail=e.Message});}}
void Eq<T>(T expected,T actual){if(!EqualityComparer<T>.Default.Equals(expected,actual))throw new Exception($"Expected {expected}; got {actual}");}
void Throws<T>(Action f) where T:Exception{try{f();}catch(T){return;}throw new Exception($"Expected {typeof(T).Name}");}
string Show(SkuTotal[] xs)=>string.Join(",",xs.Select(x=>$"{x.Sku}:{x.Cents}"));
Check("Tenant и status",()=>{var o=new[]{new OrderDoc(1,"paid",new[]{new LineDoc("A",2,5)}),new OrderDoc(2,"paid",new[]{new LineDoc("A",9,9)}),new OrderDoc(1,"pending",new[]{new LineDoc("A",9,9)}),new OrderDoc(1,"Paid",new[]{new LineDoc("A",9,9)})};Eq("A:10",Show(Kata.Totals(o,1)));});
Check("Вложенные строки и ordinal",()=>{var o=new[]{new OrderDoc(1,"paid",new[]{new LineDoc("a",2,3),new LineDoc("A",1,2),new LineDoc("a",1,4),new LineDoc("Z",0,8)})};Eq("A:2,Z:0,a:10",Show(Kata.Totals(o,1)));Eq(4,o[0].Items.Length);});
Check("Пустые документы",()=>{Eq(0,Kata.Totals([],1).Length);Eq(0,Kata.Totals(new[]{new OrderDoc(1,"paid",[])},1).Length);Throws<ArgumentNullException>(()=>Kata.Totals(null!,1));});
Check("Точность и overflow",()=>{Eq(4294967294L,Kata.Totals(new[]{new OrderDoc(1,"paid",new[]{new LineDoc("A",2,int.MaxValue)})},1)[0].Cents);var lines=Enumerable.Range(0,3).Select(_=>new LineDoc("A",int.MaxValue,int.MaxValue)).ToArray();Throws<OverflowException>(()=>Kata.Totals(new[]{new OrderDoc(1,"paid",lines)},1));});
Console.WriteLine(JsonSerializer.Serialize(results));
