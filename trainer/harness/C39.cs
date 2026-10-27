using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
var results=new List<object>();
void Check(string name,Action test){try{test();results.Add(new{name,passed=true,detail=""});}catch(Exception e){results.Add(new{name,passed=false,detail=e.Message});}}
void Eq<T>(T expected,T actual){if(!EqualityComparer<T>.Default.Equals(expected,actual))throw new Exception($"Expected {expected}; got {actual}");}
void Throws<T>(Action action) where T:Exception{try{action();}catch(T){return;}throw new Exception($"Expected {typeof(T).Name}");}
Check("Взвешенный цикл",()=>{var b=new[]{new Backend("A",2,true),new Backend("B",1,true)};Eq("A,A,B,A,A,B",string.Join(",",Enumerable.Range(0,6).Select(i=>Kata.Pick(b,i))));});
Check("Health и большой sequence",()=>{var b=new[]{new Backend("X",10,false),new Backend("A",1,true)};Eq("A",Kata.Pick(b,long.MaxValue));Eq("X",b[0].Id);});
Check("Нет доступных",()=>{Eq<string?>(null,Kata.Pick(Array.Empty<Backend>(),0));Eq<string?>(null,Kata.Pick(new[]{new Backend("A",1,false)},0));});
Check("Сумма не int",()=>{Eq("B",Kata.Pick(new[]{new Backend("A",int.MaxValue,true),new Backend("B",int.MaxValue,true)},int.MaxValue));});
Check("Некорректный вход",()=>{Throws<ArgumentNullException>(()=>Kata.Pick(null!,0));Throws<ArgumentOutOfRangeException>(()=>Kata.Pick([], -1));Throws<ArgumentException>(()=>Kata.Pick(new[]{new Backend("A",0,false)},0));Throws<ArgumentException>(()=>Kata.Pick(new[]{new Backend("A",1,true),new Backend("A",1,false)},0));Throws<ArgumentException>(()=>Kata.Pick(new[]{new Backend("",1,true)},0));});
Console.WriteLine(JsonSerializer.Serialize(results));
