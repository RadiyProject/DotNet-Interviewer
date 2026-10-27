using System.Text.Json;
using System.Collections.Generic;
var results=new List<object>();
void Check(string name,Action test){try{test();results.Add(new{name,passed=true,detail=""});}catch(Exception e){results.Add(new{name,passed=false,detail=e.Message});}}
void Eq<T>(T a,T b){if(!EqualityComparer<T>.Default.Equals(a,b))throw new Exception($"Expected {a}; got {b}");}
void Throws<T>(Action f) where T:Exception{try{f();}catch(T){return;}throw new Exception($"Expected {typeof(T).Name}");}
Check("Значения и ссылки",()=>{var a=new Order{Id=9,Lines=new(){new Line{Sku="A",Quantity=2}}};var b=Kata.Copy(a);Eq(9,b.Id);Eq("A",b.Lines[0].Sku);Eq(2,b.Lines[0].Quantity);Eq(false,ReferenceEquals(a,b));Eq(false,ReferenceEquals(a.Lines,b.Lines));Eq(false,ReferenceEquals(a.Lines[0],b.Lines[0]));});
Check("Мутация исходника",()=>{var a=new Order{Id=1,Lines=new(){new Line{Sku="A",Quantity=2}}};var b=Kata.Copy(a);a.Id=7;a.Lines[0].Sku="B";a.Lines[0].Quantity=99;a.Lines.Clear();Eq(1,b.Id);Eq(1,b.Lines.Count);Eq("A",b.Lines[0].Sku);Eq(2,b.Lines[0].Quantity);});
Check("Мутация копии",()=>{var a=new Order{Lines=new(){new Line{Sku="A",Quantity=2}}};var b=Kata.Copy(a);b.Lines[0].Quantity=9;b.Lines.Add(new Line());Eq(2,a.Lines[0].Quantity);Eq(1,a.Lines.Count);});
Check("Повторная ссылка",()=>{var line=new Line{Sku="A",Quantity=3};var b=Kata.Copy(new Order{Lines=new(){line,line}});Eq(2,b.Lines.Count);Eq(false,ReferenceEquals(b.Lines[0],b.Lines[1]));b.Lines[0].Quantity=10;Eq(3,b.Lines[1].Quantity);});
Check("Контракт",()=>{Eq(0,Kata.Copy(new Order()).Lines.Count);Throws<ArgumentNullException>(()=>Kata.Copy(null!));Throws<ArgumentException>(()=>Kata.Copy(new Order{Lines=null!}));var a=new Order{Lines=new(){new Line{Sku="keep",Quantity=7},null!}};Throws<ArgumentException>(()=>Kata.Copy(a));Eq(2,a.Lines.Count);Eq("keep",a.Lines[0].Sku);Eq(7,a.Lines[0].Quantity);});
Console.WriteLine(JsonSerializer.Serialize(results));
