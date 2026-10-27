using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
var results=new List<object>();
void Check(string name,Action test){try{test();results.Add(new{name,passed=true,detail=""});}catch(Exception e){results.Add(new{name,passed=false,detail=e.Message});}}
void Eq<T>(T expected,T actual){if(!EqualityComparer<T>.Default.Equals(expected,actual))throw new Exception($"Expected {expected}; got {actual}");}
void Throws<T>(Action f) where T:Exception{try{f();}catch(T){return;}throw new Exception($"Expected {typeof(T).Name}");}
Check("Глубина и направление",()=>{var e=new[]{new Edge(1,2),new Edge(2,3),new Edge(3,4),new Edge(5,1)};Eq("2,3",string.Join(",",Kata.Reachable(e,1,2)));Eq("2",string.Join(",",Kata.Reachable(e,1,1)));});
Check("Циклы и дубли",()=>{var e=new[]{new Edge(1,1),new Edge(1,2),new Edge(1,2),new Edge(2,1),new Edge(2,3)};Eq("2,3",string.Join(",",Kata.Reachable(e,1,10)));});
Check("Разные пути",()=>{var e=new[]{new Edge(1,2),new Edge(1,4),new Edge(2,3),new Edge(4,3),new Edge(3,5)};Eq("2,3,4",string.Join(",",Kata.Reachable(e,1,2)));Eq("2,3,4,5",string.Join(",",Kata.Reachable(e,1,3)));});
Check("Пустота и контракт",()=>{Eq(0,Kata.Reachable([],0,10).Length);Eq(0,Kata.Reachable(new[]{new Edge(1,2)},1,0).Length);Eq("-2",string.Join(",",Kata.Reachable(new[]{new Edge(-1,-2)},-1,1)));Throws<ArgumentNullException>(()=>Kata.Reachable(null!,0,1));Throws<ArgumentOutOfRangeException>(()=>Kata.Reachable([],0,-1));});
Console.WriteLine(JsonSerializer.Serialize(results));
