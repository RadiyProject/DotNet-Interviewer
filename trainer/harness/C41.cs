using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
var results=new List<object>();
async Task Check(string name,Func<Task> test){try{await test();results.Add(new{name,passed=true,detail=""});}catch(Exception e){results.Add(new{name,passed=false,detail=e.Message});}}
void Eq<T>(T expected,T actual){if(!EqualityComparer<T>.Default.Equals(expected,actual))throw new Exception($"Expected {expected}; got {actual}");}
void Throws<T>(Action action) where T:Exception{try{action();}catch(T){return;}throw new Exception($"Expected {typeof(T).Name}");}
await Check("Override и секция",async()=>{var a=new Dictionary<string,string?>{{"Api:Host","one"},{"Api:Timeout","10"}};var b=new Dictionary<string,string?>{{"Api:Host","two"}};Eq("two",Kata.Read(a,b,"Api:Host"));Eq("10",Kata.Read(a,b,"Api:Timeout"));Eq("one",a["Api:Host"]);Eq(1,b.Count);});
await Check("Регистр",async()=>{Eq("v",Kata.Read(new(){{"API:KEY","v"}},new(),"api:key"));});
await Check("Пустые значения",async()=>{Eq("",Kata.Read(new(){{"A","x"}},new(){{"A",""}},"A"));Eq<string?>(null,Kata.Read(new(),new(),"Missing"));});
await Check("Контракт аргументов",async()=>{Throws<ArgumentNullException>(()=>Kata.Read(null!,new(),"k"));Throws<ArgumentNullException>(()=>Kata.Read(new(),null!,"k"));Throws<ArgumentNullException>(()=>Kata.Read(new(),new(),null!));Throws<ArgumentException>(()=>Kata.Read(new(),new(),"  "));});
Console.WriteLine(JsonSerializer.Serialize(results));
