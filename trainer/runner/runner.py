"""Ephemeral, networkless executor. No Docker socket or host mounts here."""
import json, os, pathlib, signal, subprocess, sys

# Independent watchdog survives disconnection/restart of the API container.
signal.alarm(65)


def command(args, **kwargs):
    return subprocess.run(args, text=True, capture_output=True, timeout=35, **kwargs)


def csharp(p):
    from xml.sax.saxutils import quoteattr
    root = pathlib.Path('/work')
    # Mirror Web SDK implicit usings without changing the console harness entry point.
    web_usings = (
        'System.Net.Http.Json', 'Microsoft.AspNetCore.Builder',
        'Microsoft.AspNetCore.Hosting', 'Microsoft.AspNetCore.Http',
        'Microsoft.AspNetCore.Routing', 'Microsoft.Extensions.Configuration',
        'Microsoft.Extensions.DependencyInjection', 'Microsoft.Extensions.Hosting',
        'Microsoft.Extensions.Logging',
    )
    usings = ''.join('<Using Include='+quoteattr(name)+' />' for name in web_usings)
    references=''.join('<PackageReference Include='+quoteattr(k)+' Version='+quoteattr(v)+' />' for k,v in p.get('packages',{}).items())
    (root/'Lab.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><NuGetAudit>false</NuGetAudit></PropertyGroup><ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App" />'+usings+references+'</ItemGroup></Project>')
    (root/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
    (root/'Solution.cs').write_text(p['code'])
    (root/'Program.cs').write_text(p['harness'])
    build = command(['dotnet','build','--nologo','--disable-build-servers','-o','/work/out'])
    if build.returncode:
        output = (build.stdout+build.stderr)[-14000:]
        # Compiler diagnostics include CSxxxx; SDK/bootstrap and restore failures are infrastructure.
        import re
        status = 'compile_error' if re.search(r'\berror CS\d+\b', output) else 'infrastructure_error'
        return {'status':status,'score':0,'output':output}
    run = command(['dotnet','/work/out/Lab.dll'])
    if run.returncode:
        return {'status':'runtime_error','score':0,'output':(run.stdout+run.stderr)[-12000:]}
    try:
        lines=run.stdout.strip().splitlines()
        cases = json.loads(lines[-1])
        if not isinstance(cases,list) or not cases: raise ValueError()
        return {'status':'passed' if all(c['passed'] for c in cases) else 'failed', 'score':round(100*sum(c['passed'] for c in cases)/len(cases)), 'cases':cases, 'output':('\n'.join(lines[:-1])+'\n'+run.stderr).strip()[-12000:]}
    except (ValueError,KeyError,IndexError,TypeError):
        return {'status':'runtime_error','score':0,'output':(run.stdout+run.stderr)[-12000:]}


def sql(p):
    import sqlite3
    engine = p['engine']
    cases=[]
    if engine == 'postgres':
        bindir = command(['pg_config','--bindir']).stdout.strip()
        init = command([bindir+'/initdb','-D','/work/pg','-A','trust','--no-locale','-E','UTF8'])
        if init.returncode: raise RuntimeError(init.stderr)
        start = command([bindir+'/pg_ctl','-D','/work/pg','-l','/work/pg.log','-o',"-k /tmp -h '' -c fsync=off -c statement_timeout=2500",'-w','start'])
        if start.returncode: raise RuntimeError(start.stderr)
        def query(q):
            r=command([bindir+'/psql','-h','/tmp','-U','runner','-d','postgres','-X','-q','-v','ON_ERROR_STOP=1','--csv','-c',q])
            if r.returncode: raise ValueError(r.stderr)
            import csv, io
            rows=list(csv.reader(io.StringIO(r.stdout)))
            return rows[1:]
    else:
        db=sqlite3.connect(':memory:')
        def query(q):
            return [[None if x is None else str(x) for x in row] for row in db.execute(q).fetchall()]
    for fixture in p['fixtures']:
        if engine == 'postgres': query(fixture['setup'])
        else: db.executescript(fixture['setup'])
        expected=query(p['reference'])
        try:
            actual=query(p['code'])
            # Column order and row order are part of the exercise contract.
            ok=actual==expected
            detail='' if ok else ('Ожидалось '+json.dumps(expected[:8],ensure_ascii=False)[:1500]+'; получено '+json.dumps(actual[:8],ensure_ascii=False)[:1500]+'. Проверь границы, дубликаты, порядок и агрегаты.')
            cases.append({'name':fixture['name'],'passed':ok,'detail':detail})
        except Exception as exc:
            cases.append({'name':fixture['name'],'passed':False,'detail':str(exc)[:2000]})
    return {'status':'passed' if all(c['passed'] for c in cases) else 'failed','score':round(100*sum(c['passed'] for c in cases)/len(cases)), 'cases':cases}

try:
    payload=json.load(sys.stdin)
    print(json.dumps(csharp(payload) if payload['engine']=='csharp' else sql(payload),ensure_ascii=False))
except subprocess.TimeoutExpired:
    print(json.dumps({'status':'timeout','score':0,'output':'Превышен лимит исполнения/компиляции.'}))
except Exception as exc:
    print(json.dumps({'status':'runner_error','score':0,'output':str(exc)[:4000]}))
