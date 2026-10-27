"""Trusted dependency preparation only. This container never receives user code."""
import fcntl
import json
import os
from pathlib import Path
import subprocess
import sys
from xml.sax.saxutils import quoteattr

p=json.load(sys.stdin)
cache=Path('/nuget')
# The app and executing submissions mount this same volume read-only.
os.umask(0o022)
with (cache/'.forge-restore.lock').open('a') as lock:
    fcntl.flock(lock,fcntl.LOCK_EX)
    marker=cache/p['marker']
    if marker.is_file(): sys.exit(0)
    root=Path('/work')
    refs=''.join('<PackageReference Include='+quoteattr(k)+' Version='+quoteattr(v)+' />' for k,v in p['packages'].items())
    (root/'Restore.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><NuGetAudit>false</NuGetAudit></PropertyGroup><ItemGroup>'+refs+'</ItemGroup></Project>')
    (root/'NuGet.Config').write_text('<configuration><packageSources><clear/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></packageSources></configuration>')
    env=dict(os.environ,NUGET_PACKAGES='/nuget')
    result=subprocess.run(['dotnet','restore','/work/Restore.csproj','--disable-parallel'],env=env,timeout=210)
    if result.returncode: sys.exit(result.returncode)
    marker.write_text(json.dumps(p['packages'],sort_keys=True))
