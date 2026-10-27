using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
var results=new List<object>();
async Task Check(string name,Func<Task> test){try{await test();results.Add(new{name,passed=true,detail=""});}catch(Exception e){results.Add(new{name,passed=false,detail=e.Message});}}
void Eq<T>(T expected,T actual){if(!EqualityComparer<T>.Default.Equals(expected,actual))throw new Exception($"Expected {expected}; got {actual}");}
void Throws<T>(Action action) where T:Exception{try{action();}catch(T){return;}throw new Exception($"Expected {typeof(T).Name}");}
HttpContext Context(params System.Security.Claims.ClaimsIdentity[] identities)=>new DefaultHttpContext{User=new System.Security.Claims.ClaimsPrincipal(identities)};
await Check("Anonymous",async()=>{var c=new DefaultHttpContext();var count=0;await new PermissionMiddleware(_=>{count++;return Task.CompletedTask;}).InvokeAsync(c);Eq(401,c.Response.StatusCode);Eq(0,count);});
await Check("Forbidden",async()=>{var c=Context(new System.Security.Claims.ClaimsIdentity([],"test"));var count=0;await new PermissionMiddleware(_=>{count++;return Task.CompletedTask;}).InvokeAsync(c);Eq(403,c.Response.StatusCode);Eq(0,count);});
await Check("Allowed",async()=>{var c=Context(new System.Security.Claims.ClaimsIdentity(new[]{new System.Security.Claims.Claim("permission","orders.read")},"test"));var count=0;await new PermissionMiddleware(x=>{count++;x.Response.StatusCode=204;return Task.CompletedTask;}).InvokeAsync(c);Eq(1,count);Eq(204,c.Response.StatusCode);});
await Check("Только authenticated claims",async()=>{var c=Context(new System.Security.Claims.ClaimsIdentity(new[]{new System.Security.Claims.Claim("permission","orders.read")}),new System.Security.Claims.ClaimsIdentity([],"test"));await new PermissionMiddleware(_=>throw new Exception("next called")).InvokeAsync(c);Eq(403,c.Response.StatusCode);});
await Check("Вторая identity и case",async()=>{var c=Context(new System.Security.Claims.ClaimsIdentity(),new System.Security.Claims.ClaimsIdentity(new[]{new System.Security.Claims.Claim("permission","orders.read")},"test"));await new PermissionMiddleware(x=>{x.Response.StatusCode=202;return Task.CompletedTask;}).InvokeAsync(c);Eq(202,c.Response.StatusCode);var denied=Context(new System.Security.Claims.ClaimsIdentity(new[]{new System.Security.Claims.Claim("permission","Orders.Read")},"test"));await new PermissionMiddleware(_=>throw new Exception("next called")).InvokeAsync(denied);Eq(403,denied.Response.StatusCode);});
await Check("Downstream exception",async()=>{var c=Context(new System.Security.Claims.ClaimsIdentity(new[]{new System.Security.Claims.Claim("permission","orders.read")},"test"));var expected=new InvalidOperationException("probe");try{await new PermissionMiddleware(_=>Task.FromException(expected)).InvokeAsync(c);throw new Exception("Exception swallowed");}catch(InvalidOperationException e){Eq(true,ReferenceEquals(e,expected));}});
await Check("Null",async()=>{Throws<ArgumentNullException>(()=>new PermissionMiddleware(null!));try{await new PermissionMiddleware(_=>Task.CompletedTask).InvokeAsync(null!);throw new Exception("Expected ArgumentNullException");}catch(ArgumentNullException){}});
Console.WriteLine(JsonSerializer.Serialize(results));
