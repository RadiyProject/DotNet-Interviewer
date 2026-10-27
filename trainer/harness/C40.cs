using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
var results=new List<object>();
void Check(string name,Action test){try{test();results.Add(new{name,passed=true,detail=""});}catch(Exception e){results.Add(new{name,passed=false,detail=e.Message});}}
void Eq<T>(T expected,T actual){if(!EqualityComparer<T>.Default.Equals(expected,actual))throw new Exception($"Expected {expected}; got {actual}");}
void Throws<T>(Action action) where T:Exception{try{action();}catch(T){return;}throw new Exception($"Expected {typeof(T).Name}");}
Check("Алфавит и границы",()=>{Eq("0",Kata.Encode(0));Eq("9",Kata.Encode(9));Eq("A",Kata.Encode(10));Eq("a",Kata.Encode(36));Eq("z",Kata.Encode(61));Eq("10",Kata.Encode(62));Eq(36UL,Kata.Decode("a"));});
Check("Roundtrip",()=>{foreach(var n in new ulong[]{0,1,61,62,3843,3844,999999,ulong.MaxValue})Eq(n,Kata.Decode(Kata.Encode(n)));});
Check("Канонический формат",()=>{Throws<ArgumentNullException>(()=>Kata.Decode(null!));foreach(var x in new[]{"","00","01","+1","a-b","я"," 1"})Throws<FormatException>(()=>Kata.Decode(x));});
Check("Overflow",()=>{Throws<OverflowException>(()=>Kata.Decode("zzzzzzzzzzz"));});
Console.WriteLine(JsonSerializer.Serialize(results));
