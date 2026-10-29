"""Single-user local trainer; stdlib HTTP + SQLite. Content is trusted repo data."""
import json
import hashlib
import os
import re
import shutil
from pathlib import Path
import sqlite3
import subprocess
import tempfile
import threading
import time
import uuid
from mock_interviews import MockStore
from interview_builder import InterviewBuilder
from chatgpt_companion import ChatGPTCompanion
from localization import localize_task, localize_payload, read_json
from contextlib import contextmanager
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlparse

ROOT=Path(__file__).parent
DATA=Path(os.environ.get('DATA_DIR',str(ROOT/'data')))
DATA.mkdir(parents=True,exist_ok=True)
LOCK=threading.Semaphore(1)
IMAGE=os.environ.get('RUNNER_IMAGE','interview-lab-runner:local')
NUGET_VOLUME=os.environ.get('NUGET_VOLUME','interview-lab-nuget')

@contextmanager
def connect():
    db=sqlite3.connect(DATA/'progress.sqlite',timeout=15)
    db.row_factory=sqlite3.Row
    try:
        with db:
            yield db
    finally:
        db.close()

with connect() as db:
    db.executescript('''
    PRAGMA journal_mode=WAL;
    CREATE TABLE IF NOT EXISTS attempts(id TEXT PRIMARY KEY, task_id TEXT NOT NULL, started REAL NOT NULL, deadline REAL NOT NULL, finished REAL, code TEXT NOT NULL DEFAULT '', result TEXT, revealed INTEGER NOT NULL DEFAULT 0);
    CREATE TABLE IF NOT EXISTS drafts(task_id TEXT PRIMARY KEY, code TEXT NOT NULL);
    ''')

def catalog():
    tasks=json.loads((ROOT/'content/tasks.json').read_text())
    if len({t['id'] for t in tasks})!=len(tasks): raise ValueError('Duplicate task IDs')
    return tasks

def find_task(task_id, language='ru'):
    return localize_task(next((t for t in catalog() if t['id']==task_id),None), language)

def public_task(t):
    result={k:v for k,v in t.items() if k not in ('answer','correct','reference','fixtures','harness')}
    if t['engine']=='quiz': result['multiple']=isinstance(t['correct'],list)
    if t.get('packages'): result['packageVersions']=package_versions(t)
    return result

def correct_indices(t):
    return t['correct'] if isinstance(t['correct'],list) else [t['correct']]

def grade_quiz(t,code):
    try:
        selected=json.loads(code) if code else []
    except ValueError:
        selected=None
    if type(selected) is int: selected=[selected]
    valid=isinstance(selected,list) and all(type(i) is int and 0<=i<len(t['choices']) for i in selected)
    if not valid: selected=[]
    expected=set(correct_indices(t))
    ok=valid and set(selected)==expected
    return {
        'status':'passed' if ok else 'failed','score':100 if ok else 0,
        'quizFeedback':[
            {'text':choice,'selected':i in selected,'correct':i in expected}
            for i,choice in enumerate(t['choices'])
        ],
        'output':'Ответ засчитывается только при полном совпадении: выбраны все правильные варианты и ни одного лишнего.'
    }

def explanation(t):
    if not t: return ''
    answer=t.get('answer','')
    if t['engine']=='quiz':
        prefix='Правильные ответы:' if isinstance(t['correct'],list) else 'Правильный ответ:'
        answer=prefix+'\n'+'\n'.join('• '+t['choices'][i] for i in correct_indices(t))+'\n\n'+answer
    return answer

def package_versions(t):
    requested=t.get('packages',[])
    if not requested: return {}
    allowed=json.loads((ROOT/'content/packages.json').read_text())
    result={}
    for name in requested:
        version=allowed.get(name)
        if not re.fullmatch(r'[A-Za-z0-9_.-]+',name) or not isinstance(version,str) or not re.fullmatch(r'\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?',version):
            raise ValueError('Пакет отсутствует в packages.json или версия не закреплена: '+str(name))
        result[name]=version
    return result

def prepare_packages(packages):
    if not packages: return
    key=hashlib.sha256(json.dumps({'packages':packages,'image':IMAGE,'tfm':'net10.0','format':1},sort_keys=True).encode()).hexdigest()
    marker='.forge-ready-'+key
    if (Path('/nuget')/marker).is_file(): return
    name='interview-lab-restore-'+uuid.uuid4().hex
    script=(ROOT/'runner/environment.py').read_text()+'\n'+(ROOT/'runner/restore.py').read_text()
    bootstrap='import io,json,sys; p=json.load(sys.stdin); sys.stdin=io.StringIO(json.dumps(p["payload"])); exec(compile(p["runner"],"restore.py","exec"))'
    try:
        restored=subprocess.run(['docker','run','--rm','-i','--name',name,'--user','0:0',
            '--cap-drop','ALL','--security-opt','no-new-privileges','--memory','768m','--cpus','1','--pids-limit','96',
            '--mount',f'type=volume,src={NUGET_VOLUME},dst=/nuget',
            '--tmpfs','/work:rw,exec,nosuid,size=256m','--tmpfs','/tmp:rw,exec,nosuid,size=96m',
            '--entrypoint','python3',IMAGE,'-c',bootstrap],
            input=json.dumps({'runner':script,'payload':{'packages':packages,'marker':marker}}),
            text=True,capture_output=True,timeout=240)
        if restored.returncode:
            raise RuntimeError('NuGet restore не завершён. Проверь доступ к api.nuget.org и volume: '+(restored.stdout+restored.stderr)[-10000:])
    finally:
        try:
            subprocess.run(['docker','rm','-f',name],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,timeout=10)
        except (OSError,subprocess.TimeoutExpired):
            pass

def execute(t, code):
    if not shutil.which('docker'):
        return {'status':'infrastructure_error','score':0,'output':'Процесс API не нашёл Docker CLI (docker) в PATH. Если API запущен локально, установи Docker CLI и запускай API из окружения, где доступен docker. Если API работает через Compose, один раз пересобери app: docker compose up -d --build --no-deps app. Это обновление системной зависимости образа; последующие изменения кода пересборки не требуют.'}
    if not LOCK.acquire(blocking=False):
        return {'status':'busy','score':0,'output':'Другая проверка ещё выполняется. Повтори отправку.'}
    name='interview-lab-'+uuid.uuid4().hex
    try:
        payload={'engine':t['engine'],'code':code}
        if t['engine']=='csharp':
            payload['harness']=(ROOT/'harness'/t['harness']).read_text()
            payload['packages']=package_versions(t)
            prepare_packages(payload['packages'])
        else: payload.update(fixtures=t['fixtures'],reference=t['reference'])
        # Read trusted executor source from the checkout so runner.py edits need no image rebuild.
        request={'runner':(ROOT/'runner/environment.py').read_text()+'\n'+(ROOT/'runner/runner.py').read_text(),'payload':payload}
        bootstrap='import io,json,sys; p=json.load(sys.stdin); sys.stdin=io.StringIO(json.dumps(p["payload"])); exec(compile(p["runner"],"/runner.py","exec"))'
        # Bound disk-backed output: malicious Console.Write cannot exhaust API RAM.
        with tempfile.TemporaryFile() as out, tempfile.TemporaryFile() as err:
            proc=subprocess.Popen(['docker','run','--rm','-i','--name',name,'--network','none','--read-only',
                '--user','10001:10001','--cap-drop','ALL','--security-opt','no-new-privileges',
                '--memory','768m','--memory-swap','768m','--cpus','1','--pids-limit','96',
                '--ulimit','fsize=67108864:67108864','--log-driver','none',
                *(['--mount',f'type=volume,src={NUGET_VOLUME},dst=/nuget,readonly','--env','NUGET_PACKAGES=/nuget'] if payload.get('packages') else []),
                '--tmpfs','/work:rw,exec,nosuid,size=256m,uid=10001,gid=10001',
                '--tmpfs','/tmp:rw,exec,nosuid,size=96m,uid=10001,gid=10001',
                '--entrypoint','python3',IMAGE,'-c',bootstrap],
                stdin=subprocess.PIPE,stdout=out,stderr=err)
            try: proc.communicate(json.dumps(request).encode(),timeout=75)
            except subprocess.TimeoutExpired:
                proc.kill(); proc.wait()
                return {'status':'timeout','score':0,'output':'Лимит песочницы: 75 секунд.'}
            out.seek(0); raw=out.read(65536).decode(errors='replace')
            err.seek(0); error=err.read(12000).decode(errors='replace')
        if proc.returncode:
            if proc.returncode in (137,142):
                return {'status':'timeout','score':0,'output':'Достигнут лимит памяти или времени песочницы.'}
            return {'status':'infrastructure_error','score':0,'output':error or raw or 'Контейнер завершился без результата.'}
        try:
            result=json.loads(raw.strip().splitlines()[-1])
            if result.get('status')=='runner_error': result['status']='infrastructure_error'
            return result
        except (ValueError,IndexError,AttributeError):
            return {'status':'infrastructure_error','score':0,'output':raw[-10000:] or error}
    except (OSError,RuntimeError,subprocess.TimeoutExpired) as exc:
        return {'status':'infrastructure_error','score':0,'output':str(exc)}
    finally:
        try:
            subprocess.run(['docker','rm','-f',name],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,timeout=10)
        except (OSError,subprocess.TimeoutExpired):
            pass  # Cleanup must not replace the execution result with another infrastructure error.
        finally:
            LOCK.release()

MOCK = MockStore(connect, catalog)
BUILDER = InterviewBuilder(connect, catalog, public_task)
COMPANION = ChatGPTCompanion(MOCK)

class Handler(SimpleHTTPRequestHandler):
    @property
    def language(self):
        return 'en' if self.headers.get('X-Forge-Language') == 'en' else 'ru'
    def __init__(self,*args,**kwargs): super().__init__(*args,directory=str(ROOT/'web'),**kwargs)
    def log_message(self,format,*args): pass
    def end_headers(self):
        origin=self.headers.get('Origin','')
        if urlparse(self.path).path.startswith('/api/companion/') and re.fullmatch(r'chrome-extension://[a-p]{32}',origin):
            self.send_header('Access-Control-Allow-Origin',origin)
            self.send_header('Vary','Origin')
        if not urlparse(self.path).path.startswith('/api/'):
            self.send_header('Cache-Control','no-store')
        super().end_headers()
    def reply(self,obj,status=200):
        body=json.dumps(obj if urlparse(self.path).path.startswith(('/api/mock/','/api/interviews/','/api/companion/')) else localize_payload(obj,self.language),ensure_ascii=False).encode()
        self.send_response(status); self.send_header('Content-Type','application/json; charset=utf-8'); self.send_header('Cache-Control','no-store'); self.send_header('Content-Length',str(len(body))); self.end_headers(); self.wfile.write(body)
    def do_OPTIONS(self):
        origin=self.headers.get('Origin','')
        if not urlparse(self.path).path.startswith('/api/companion/') or not re.fullmatch(r'chrome-extension://[a-p]{32}',origin):
            return self.reply({'error':'Origin rejected'},403)
        self.send_response(204)
        self.send_header('Access-Control-Allow-Methods','GET, POST, OPTIONS')
        self.send_header('Access-Control-Allow-Headers','Authorization, Content-Type')
        self.send_header('Access-Control-Allow-Private-Network','true')
        self.end_headers()
    def do_GET(self):
        if urlparse('//'+self.headers.get('Host','')).hostname not in ('localhost','127.0.0.1','::1'):
            return self.reply({'error':'Используй localhost или 127.0.0.1'},403)
        path=urlparse(self.path).path
        if path.startswith('/api/companion/'):
            try: return self.reply(COMPANION.route('GET',path.removeprefix('/api/companion/'),{},self.headers.get('Authorization','')))
            except ValueError as e: return self.reply({'error':str(e)},400)
        if path.startswith('/api/interviews/'):
            try: return self.reply(BUILDER.route('GET',path,{},self.language))
            except ValueError as e: return self.reply({'error':str(e)},400)
        if path.startswith('/api/mock/'):
            try: return self.reply(MOCK.route('GET',path,{},self.language))
            except ValueError as e: return self.reply({'error':str(e)},400)
        if path=='/api/tasks': return self.reply({'tasks':[public_task(localize_task(t,self.language)) for t in catalog()], 'tags':read_json(ROOT/'content/tags.json'), 'serverTime':time.time()})
        if path in ('/api/progress','/api/export'):
            with connect() as db:
                attempts=[dict(r) for r in db.execute('SELECT * FROM attempts ORDER BY started DESC')]
                drafts={r['task_id']:r['code'] for r in db.execute('SELECT * FROM drafts')}
            for a in attempts:
                a['result']=json.loads(a['result']) if a['result'] else None
                if a['result'] and a['finished']:
                    task=find_task(a['task_id'],self.language)
                    a['result']['answer']=explanation(task)
                    if task and task['engine']=='quiz' and a['result'].get('quizFeedback'):
                        for i,feedback in enumerate(a['result']['quizFeedback']):
                            if i<len(task['choices']): feedback['text']=task['choices'][i]
            return self.reply({'version':1,'attempts':attempts,'drafts':drafts,'serverTime':time.time()})
        if path.startswith('/api/'): return self.reply({'error':'Не найдено'},404)
        return super().do_GET()
    def do_POST(self):
        try:
            if urlparse('//'+self.headers.get('Host','')).hostname not in ('localhost','127.0.0.1','::1'):
                return self.reply({'error':'Используй localhost или 127.0.0.1'},403)
            # JSON-only, same-origin mutations; blocks browser cross-site requests to localhost.
            path=urlparse(self.path).path
            origin=self.headers.get('Origin')
            companion_request=path.startswith('/api/companion/')
            if origin and origin!=f'http://{self.headers.get("Host")}' and not (companion_request and re.fullmatch(r'chrome-extension://[a-p]{32}',origin)): return self.reply({'error':'Недопустимый Origin'},403)
            if self.headers.get('Content-Type','').split(';')[0]!='application/json': return self.reply({'error':'Нужен application/json'},415)
            length=int(self.headers.get('Content-Length','0'))
            if length<1 or length>3_000_000: return self.reply({'error':'Слишком большой запрос'},413)
            p=json.loads(self.rfile.read(length))
            path=urlparse(self.path).path
            if path.startswith('/api/companion/'): return self.reply(COMPANION.route('POST',path.removeprefix('/api/companion/'),p,self.headers.get('Authorization','')))
            if path=='/api/mock/begin':return self.reply(COMPANION.begin(p.get('session_id')))
            if path=='/api/mock/companion-manual':
                from chatgpt_companion import VOICE_INSTRUCTIONS
                session=MOCK.session(p.get('session_id'))
                return self.reply({'prompt':VOICE_INSTRUCTIONS+'\nUNTRUSTED INTERVIEW CONTEXT JSON:\n'+json.dumps(MOCK.context(session,'continue' if session['state']['messages'] else 'start'),ensure_ascii=False)})
            if path=='/api/mock/companion-active':return self.reply(COMPANION.active(p.get('page_id'),p.get('session_id'),p.get('focused') is True))
            if path=='/api/mock/companion-approve':return self.reply(COMPANION.approve_auto(p.get('page_id'),p.get('session_id'),p.get('request_id')))
            if path=='/api/mock/companion-revoke':
                COMPANION.revoke(p.get('session_id'))
                return self.reply({'ok':True})
            if path=='/api/mock/import-transcript':
                from chatgpt_companion import parse_transcript
                with MOCK.lock:
                    session=MOCK.session(p.get('session_id'));st=session['state'];version=st['version']
                    if session['finished'] and (st.get('summary') or {}).get('source') not in (None,'local_stats'):raise ValueError('An AI debrief already exists; its evidence cannot be changed')
                    incoming=parse_transcript(p.get('text'))
                    digest=hashlib.sha256(p['text'].encode()).hexdigest()
                    if digest not in st.get('transcript_imports',[]):
                        if sum(len(m['text']) for m in st['messages'])+sum(len(m['text']) for m in incoming)>200000 or len(st['messages'])+len(incoming)>650:raise ValueError('Transcript exceeds limits')
                        for m in incoming:st['messages'].append({'role':'candidate' if m['role']=='user' else 'interviewer','kind':'external_turn','text':m['text'],'at':time.time(),'source':'manual-chatgpt-transcript','external_import':True,'preparation':MOCK.phase(session)=='preparing'})
                        st.setdefault('transcript_imports',[]).append(digest);st['pending']=None;st.setdefault('companion',{})['transcript_complete']=True
                        if session['finished']:st['summary']=MOCK.simple_summary(session)
                        MOCK.save(session,version)
                return self.reply({'session':MOCK.public(session)})
            if path=='/api/mock/coaching/complete':
                with MOCK.lock:
                    result=MOCK.route('POST',path,p,self.language)
                    if result.get('advice'):
                        session=MOCK.session(p.get('session_id'));version=session['state']['version']
                        c=session['state'].get('companion',{})
                        for item in c.get('contexts',[]):
                            if item.get('action')=='coaching' and not item.get('delivered'):
                                item.update(delivered=time.time(),cancelled=True)
                        delivery='queue_full'
                        if sum(not x.get('delivered') for x in c.get('contexts',[]))<10:
                            COMPANION.queue(session,'coaching',result['advice']);delivery='queued'
                        session['state']['coaching']['delivery']=delivery
                        result['delivery']=delivery
                        MOCK.save(session,version)
                return self.reply(result)
            if path=='/api/mock/companion-pair': return self.reply(COMPANION.pair(p.get('session_id')))
            if path in ('/api/mock/companion-context','/api/mock/companion-cancel'):
                with MOCK.lock:
                    session=MOCK.session(p.get('session_id'));version=session['state']['version']
                    if session['finished']: raise ValueError('This interview is finished')
                    if path.endswith('cancel'):
                        for item in session['state'].get('companion',{}).get('contexts',[]):
                            if not item.get('delivered'):item['delivered']=time.time();item['cancelled']=True
                    else:
                        extra=None
                        if p.get('task_id'):
                            if p['task_id'] not in session['state']['bank_ids']: raise ValueError('Task is not in this interview plan')
                            code=p.get('code','')
                            if not isinstance(code,str) or not code.strip() or len(code)>100000: raise ValueError('Enter source text, up to 100,000 characters')
                            extra={'task_id':p['task_id'],'code':code,'shared_at':time.time(),'note':'Candidate explicitly shared this code as text for discussion. Review this exact version, ask about reasoning and edge cases. This is not an executed test result.'}
                            task=MOCK.task(p['task_id'],session['state']['language'])
                            if task.get('review_code'):extra.update(kind='candidate_review',note='The code field contains the candidate’s review comments. The immutable source fragment is review_code in the matching bank task. Discuss their findings, ask for evidence and avoid giving the full answer unasked.')
                            with connect() as db:
                                exists=db.execute('SELECT id FROM interview_artifacts WHERE session_id=? AND task_id=? AND code=?',(session['id'],p['task_id'],code)).fetchone()
                                if not exists:db.execute('INSERT INTO interview_artifacts VALUES(?,?,?,?,?)',(uuid.uuid4().hex,session['id'],p['task_id'],code,time.time()))
                            artifacts=session['state'].setdefault('shared_artifacts',[])
                            artifacts.append(extra)
                            while len(artifacts)>5 or sum(len(a['code']) for a in artifacts)>150000:artifacts.pop(0)
                        COMPANION.queue(session,p.get('action','continue') if p.get('action') in ('start','continue','hint','next') else 'continue',extra)
                    MOCK.save(session,version)
                    if path.endswith('context') and extra:
                        with connect() as db:db.execute('UPDATE attempts SET revealed=1 WHERE task_id=? AND finished IS NULL',(extra['task_id'],))
                return self.reply({'session':MOCK.public(session)})
            if path.startswith('/api/interviews/'): return self.reply(BUILDER.route('POST',path,p,self.language))
            if path.startswith('/api/mock/'): return self.reply(MOCK.route('POST',path,p,self.language))
            if path=='/api/import':
                if p.get('version')!=1 or not isinstance(p.get('attempts'),list) or not isinstance(p.get('drafts'),dict): raise ValueError('Неверный формат экспорта')
                valid={t['id'] for t in catalog()}
                with connect() as db:
                    for a in p['attempts']:
                        if not isinstance(a,dict) or not re.fullmatch(r'[a-f0-9]{32}',str(a.get('id',''))):
                            raise ValueError('Некорректная запись истории')
                        if a['task_id'] not in valid: continue
                        # Import only finished practice history; no active deadlines or trusted exams.
                        if not a.get('finished') or not isinstance(a.get('result'),dict): continue
                        r=dict(a['result']); r.pop('answer',None); r['imported']=True
                        db.execute('INSERT OR IGNORE INTO attempts VALUES(?,?,?,?,?,?,?,?,?)',(str(a['id']),a['task_id'],float(a['started']),float(a['deadline']),float(a['finished']),str(a['code'])[:100000],json.dumps(r),1))
                    for k,v in p['drafts'].items():
                        if k in valid and isinstance(v,str): db.execute('INSERT OR REPLACE INTO drafts VALUES(?,?)',(k,v[:100000]))
                return self.reply({'ok':True})
            t=find_task(p.get('taskId'),self.language)
            if not t: return self.reply({'error':'Неизвестная задача'},404)
            if path=='/api/draft':
                code=str(p.get('code',''))[:100000]
                with connect() as db:
                    db.execute('INSERT OR REPLACE INTO drafts VALUES(?,?)',(t['id'],code))
                    draft_attempt=db.execute('SELECT * FROM attempts WHERE id=? AND task_id=? AND finished IS NULL',(p.get('attemptId'),t['id'])).fetchone()
                if draft_attempt:MOCK.record_attempt(draft_attempt['id'],code)
                return self.reply({'ok':True})
            if path=='/api/start':
                now=time.time()
                with connect() as db:
                    db.execute('BEGIN IMMEDIATE')
                    a=db.execute('SELECT * FROM attempts WHERE task_id=? AND finished IS NULL ORDER BY started DESC LIMIT 1',(t['id'],)).fetchone()
                    if not a:
                        aid=uuid.uuid4().hex
                        db.execute('INSERT INTO attempts(id,task_id,started,deadline) VALUES(?,?,?,?)',(aid,t['id'],now,now+t['minutes']*60))
                        a=db.execute('SELECT * FROM attempts WHERE id=?',(aid,)).fetchone()
                MOCK.link_attempt(p.get('session_id'),a)
                return self.reply({'attempt':dict(a),'serverTime':now})
            with connect() as db:
                db.execute('BEGIN IMMEDIATE')
                a=db.execute('SELECT * FROM attempts WHERE id=? AND task_id=?',(p.get('attemptId',''),t['id'])).fetchone()
            if not a: return self.reply({'error':'Сначала начни попытку'},409)
            if a['finished']: return self.reply({'error':'Попытка уже завершена'},409)
            if p.get('session_id'):MOCK.link_attempt(p['session_id'],a)
            if path=='/api/check':
                if t['engine'] not in ('csharp','postgres','sqlite'):
                    return self.reply({'error':'Автопроверка доступна для задач C# и SQL'},400)
                if time.time()>a['deadline']:
                    return self.reply({'error':'Время попытки истекло. Отправь решение или начни новую попытку.'},409)
                code=str(p.get('code',''))[:100000]
                with connect() as db:
                    db.execute('INSERT OR REPLACE INTO drafts VALUES(?,?)',(t['id'],code))
                MOCK.record_attempt(a['id'],code,{'status':'checking','preview':True})
                result=execute(t,code)
                result['preview']=True
                MOCK.record_attempt(a['id'],code,result)
                return self.reply(result)
            if path=='/api/reveal':
                with connect() as db: db.execute('UPDATE attempts SET revealed=1 WHERE id=?',(a['id'],))
                return self.reply({'answer':explanation(t),'hints':t.get('hints',[])})
            if path!='/api/submit': return self.reply({'error':'Не найдено'},404)
            now=time.time(); code=str(p.get('code',''))[:100000]
            MOCK.record_attempt(a['id'],code,{'status':'checking'})
            # Claim attempt atomically before execution; prevents parallel duplicate submissions.
            with connect() as db:
                cur=db.execute('UPDATE attempts SET finished=?,code=?,result=? WHERE id=? AND finished IS NULL',(now,code,json.dumps({'status':'checking','score':0}),a['id']))
                if cur.rowcount!=1: return self.reply({'error':'Попытка уже отправлена'},409)
            late=now>a['deadline']
            try:
                if late: result={'status':'expired','score':0,'output':'Время истекло. Решение сохранено. Начни новую попытку.'}
                elif t['engine']=='quiz':
                    result=grade_quiz(t,code)
                elif t['engine']=='review':
                    selected=p.get('criteria',[])
                    if not isinstance(selected,list): selected=[]
                    score=round(100*sum(i in selected for i in range(len(t['review'])))/max(1,len(t['review'])))
                    result={'status':'self_review','score':score,'output':'Самооценка по рубрике: не автоматическая оценка архитектуры или английского.'}
                else: result=execute(t,code)
            except Exception as exc: result={'status':'infrastructure_error','score':0,'output':str(exc)[:2000]}
            result.update(elapsed=round(now-a['started']),revealed=bool(a['revealed']),answer=explanation(t))
            if result['status'] in ('busy','infrastructure_error'):
                result.pop('answer',None)
            with connect() as db:
                if result['status'] in ('busy','infrastructure_error'):
                    db.execute('UPDATE attempts SET finished=NULL,result=NULL WHERE id=?',(a['id'],))
                else: db.execute('UPDATE attempts SET result=? WHERE id=?',(json.dumps(result,ensure_ascii=False),a['id']))
                db.execute('INSERT OR REPLACE INTO drafts VALUES(?,?)',(t['id'],code))
            MOCK.record_attempt(a['id'],code,result)
            return self.reply(result)
        except (ValueError,KeyError,TypeError) as exc: return self.reply({'error':str(exc)},400)
        except Exception as exc: return self.reply({'error':'Ошибка сервера: '+str(exc)[:500]},500)

# Interrupted checks remain visible and retryable after container restart.
with connect() as db:
    db.execute("UPDATE attempts SET finished=NULL,result=NULL WHERE result LIKE '%\"checking\"%'")
ThreadingHTTPServer(('0.0.0.0',8080),Handler).serve_forever()
