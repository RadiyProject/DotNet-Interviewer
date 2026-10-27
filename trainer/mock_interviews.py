"""Persistent candidate-owned practice sessions. No external calls or command execution here."""
import base64
import hashlib
import io
import json
from pathlib import Path
import random
import re
import time
import threading
import uuid
import zipfile
from xml.etree import ElementTree
from ai_protocol import SCHEMAS, INSTRUCTIONS, validate
from localization import localize_task
from quality import analyze

FORMATS = {
 'screening': {'label': {'ru':'HR / скрининг','en':'HR / screening'}, 'minutes':25,
   'focus': 'Resume, role motivation, individual contribution, career transitions, compensation expectations, availability and candidate questions. Follow up on ALL relevant resume claims without inventing facts.',
   'questions': {'ru':['Расскажи о последнем проекте и своей личной ответственности.','Какая роль и условия работы тебе подходят?','Как ты измерял результат своей работы?'],
                 'en':['Describe your last project and your individual responsibilities.','What role and working conditions are you looking for?','How did you measure the impact of your work?']}},
 'technical': {'label': {'ru':'Техническое интервью','en':'Technical interview'}, 'minutes':60,
   'focus':'Include both an oral theory segment from the quiz bank and practical code/SQL discussion. Probe C#/.NET, concurrency, data access, testing and tradeoffs within the selected topics. Ask why and when, not multiple-choice letters. Use linked practical tasks and ask the candidate to explain invariants and complexity.',
   'questions': {'ru':['Какой инвариант защищает твоё решение?','Какие крайние случаи ты проверишь?','Что изменится при нескольких репликах сервиса?'],
                 'en':['Which invariant does your solution protect?','Which edge cases will you check?','What changes when the service has several replicas?']}},
 'lead': {'label': {'ru':'Лид / hiring manager','en':'Lead / hiring manager'}, 'minutes':45,
   'focus':'Include oral theory from the quiz bank as well as architecture, delivery, incidents, prioritization, mentoring and stakeholder tradeoffs. Probe consistency, reliability, security, testing and operational decisions within the selected topics. Connect principles to concrete resume evidence and a selected design task.',
   'questions': {'ru':['Расскажи о решении, с которым команда не согласилась.','Как ты действовал при production-инциденте?','Как менял план при сокращении сроков?'],
                 'en':['Describe a technical decision your team disagreed with.','How did you handle a production incident?','How did you adapt a plan when the deadline changed?']}},
 'final': {'label': {'ru':'Финал / ценности и взаимные ожидания','en':'Final / values and mutual expectations'}, 'minutes':35,
   'focus':'Behavioral evidence, collaboration, feedback, values, ownership and mutual expectations. Candidate questions on salary, workload, on-call, processes and decision making are welcome. No personality or demographic inference.',
   'questions': {'ru':['Когда обратная связь заставила тебя изменить подход?','Что ты ожидаешь от руководителя и команды?','Как ты обсуждаешь неразумный срок или риск для качества?'],
                 'en':['When did feedback change your approach?','What do you expect from your manager and team?','How do you discuss an unrealistic deadline or quality risk?']}}
}
SOURCES = [
 ['Technical interviews','https://careers.microsoft.com/v2/global/en/hiring-tips/technical-interviewing'],
 ['Two-way interview and STAR(R)','https://careers.microsoft.com/v2/global/en/hiring-tips/interview-tips'],
 ['Behavioral and senior engineering interview examples','https://www.amazon.jobs/content/en/how-we-hire/sde-iii-interview-prep']]


def limited(value, limit=30000):
    if not isinstance(value,str) or len(value)>limit: raise ValueError('Text field is invalid or too long')
    return value

def interviewer_persona(seed, stage, language, gender='random'):
    rng=random.Random('interviewer:'+seed)
    gender=rng.choice(['female','male']) if gender=='random' else gender
    names={
        'ru':{'female':['Анна Волкова','Ирина Соколова','Мария Орлова','Елена Морозова'],
              'male':['Алексей Волков','Дмитрий Соколов','Михаил Орлов','Андрей Морозов']},
        'en':{'female':['Emma Bennett','Sarah Morgan','Olivia Carter','Sophie Reed'],
              'male':['James Bennett','Daniel Morgan','Oliver Carter','Michael Reed']}}
    roles={
        'ru':{'screening':['HR-менеджер','Специалист по подбору IT-персонала'],
              'technical':['Старший инженер .NET','Ведущий backend-инженер'],
              'lead':['Руководитель команды разработки','Engineering Manager'],
              'final':['Директор по разработке','Руководитель инженерного направления']},
        'en':{'screening':['Technical Recruiter','Talent Acquisition Partner'],
              'technical':['Senior .NET Engineer','Staff Backend Engineer'],
              'lead':['Engineering Manager','Technical Lead'],
              'final':['Director of Engineering','Head of Engineering']}}
    return {'name':rng.choice(names[language][gender]),'gender':gender,'role':rng.choice(roles[language][stage]),'virtual':True}

def employer_simulation(seed, profile):
    name=profile.get('company_name','').strip()
    if not name:return None
    rng=random.Random('employer:'+seed+':'+name)
    text=' '.join(str(profile.get(k,'')) for k in ('company_name','role','vacancy','company_scenario')).lower()
    domain='financial services' if any(k in text for k in ('fintech','bank','payment','платеж','банк')) else 'enterprise software'
    return {'company_name':name,'fictional':True,'domain':domain,
            'team_size':rng.choice([6,8,10,12]),
            'product':rng.choice(['a backend platform serving several product teams','a customer-facing API and asynchronous processing platform']),
            'engineering_process':rng.choice(['small releases with code review, automated checks and progressive rollout','weekly releases with review, staging checks and a rollback plan']),
            'team_priorities':rng.sample(['reliability','latency','delivery predictability','cost efficiency','clear ownership','mentoring'],3),
            'collaboration':'Product, engineering and operations agree priorities; engineers discuss tradeoffs and escalate delivery risks early.',
            'on_call':rng.choice(['a rotating primary and backup engineer, with a post-incident review','a separate operations rotation with engineers joining complex incidents']),
            'candidate_supplied_details':profile.get('company_scenario','')}


def extract_resume(filename, encoded):
    raw=base64.b64decode(encoded,validate=True)
    if len(raw)>2_000_000: raise ValueError('Resume exceeds 2 MB')
    suffix=Path(filename).suffix.lower()
    if suffix in ('.txt','.md'):
        text=raw.decode('utf-8-sig')
    elif suffix=='.docx':
        with zipfile.ZipFile(io.BytesIO(raw)) as archive:
            entry=archive.getinfo('word/document.xml')
            if entry.file_size>2_000_000 or len(archive.infolist())>2000: raise ValueError('DOCX content exceeds limits')
            document=ElementTree.fromstring(archive.read(entry))
            text='\n'.join(''.join(p.itertext()) for p in document.iter() if p.tag.endswith('}p'))
    else: raise ValueError('Use UTF-8 TXT, Markdown or DOCX with this endpoint. Import PDF using the resume file picker in the trainer.')
    if not text.strip(): raise ValueError('Resume has no readable text')
    return limited(text,50000)


class MockStore:
 def __init__(self,connect,catalog):
    self.connect=connect;self.catalog=catalog;self.lock=threading.RLock()
    with connect() as db:
        db.executescript('''CREATE TABLE IF NOT EXISTS mock_profiles(id TEXT PRIMARY KEY,name TEXT NOT NULL,data TEXT NOT NULL,updated REAL NOT NULL);
        CREATE TABLE IF NOT EXISTS mock_sessions(id TEXT PRIMARY KEY,profile_id TEXT NOT NULL,stage TEXT NOT NULL,started REAL NOT NULL,deadline REAL NOT NULL,finished REAL,state TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS code_reviews(id TEXT PRIMARY KEY,task_id TEXT NOT NULL,created REAL NOT NULL,state TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS ai_translations(id TEXT PRIMARY KEY,state TEXT NOT NULL);''')
        # Restore setup time only for empty, unpaired legacy ChatGPT rounds.
        for row in db.execute('SELECT * FROM mock_sessions WHERE finished IS NULL').fetchall():
            state=json.loads(row['state'])
            if 'began_at' not in state and state.get('mode')=='chatgpt' and not state.get('messages') and not state.get('attachments') and not state.get('companion',{}).get('contexts'):
                state.update(began_at=None,duration_seconds=max(300,round(row['deadline']-row['started'])),pending=None,version=state.get('version',0)+1)
                db.execute('UPDATE mock_sessions SET state=?,deadline=0 WHERE id=?',(json.dumps(state,ensure_ascii=False),row['id']))
 def task(self,id,language='ru'):
    task=next((t for t in self.catalog() if t['id']==id),None)
    if not task: raise ValueError('Unknown task')
    return localize_task(task,language)
 def session(self,id):
    with self.connect() as db: row=db.execute('SELECT * FROM mock_sessions WHERE id=?',(id,)).fetchone()
    if not row: raise ValueError('Unknown interview session')
    value=dict(row);value['state']=json.loads(value['state']);return value
 def save(self,session,old_version):
    session['state']['version']=old_version+1
    with self.connect() as db:
        row=db.execute('SELECT state FROM mock_sessions WHERE id=?',(session['id'],)).fetchone()
        if not row or json.loads(row['state'])['version']!=old_version: raise ValueError('Session changed; reload before retrying')
        changed=db.execute('UPDATE mock_sessions SET state=?,finished=?,deadline=? WHERE id=? AND state=?',(json.dumps(session['state'],ensure_ascii=False),session['finished'],session['deadline'],session['id'],row['state']))
        if changed.rowcount!=1: raise ValueError('Session changed; reload before retrying')
 def began_at(self,session):
    return session['state'].get('began_at',session['started'])
 def duration(self,session):
    return session['state'].get('duration_seconds',max(0,round(session['deadline']-session['started'])))
 def phase(self,session):
    return 'finished' if session['finished'] else 'running' if self.began_at(session) is not None else 'preparing'
 def remaining(self,session):
    if self.began_at(session) is None:return self.duration(session)
    return max(0,round(session['deadline']-(session['finished'] or time.time())))
 def public(self,session):
    state=session['state']
    bank=[]
    for id in state['bank_ids']:
        t=self.task(id,state['language'])
        bank.append({k:t.get(k) for k in ['id','title','engine','cat','tags','desc','requirements','cases','minutes']})
    return {k:v for k,v in session.items() if k!='state'}|{'company':state.get('company'),'interviewer':state.get('interviewer') or interviewer_persona(session['id'],session['stage'],state['language']),'language':state['language'],'strict_language':state['config'].get('strict_language',False),'mode':state['mode'],'tags':state['tags'],
        'messages':state['messages'],'summary':({**state['summary'],'stats':self.stats(session)} if state.get('summary') else None),'summary_translation':state.get('summary_translation'),'hints':state['hints'],'attachments':state['attachments'],
        'companion':{k:state.get('companion',{}).get(k) for k in ['last_sync','url','finished_from_tab','transcript_complete']},'companion_queued':sum(not x.get('delivered') for x in state.get('companion',{}).get('contexts',[])),'constructed_plan':state.get('constructor_plan'),'bank':bank,'pending':bool(state.get('pending')),'stats':self.stats(session),'context_name':state['profile']['name'],'phase':self.phase(session),'created':session['started'],'started':self.began_at(session),'deadline':session['deadline'] if self.began_at(session) is not None else None,'duration_seconds':self.duration(session)}
 def stats(self,session):
    st=session['state']
    turns=[(i,m) for i,m in enumerate(st['messages']) if m['role']=='candidate' and not m.get('preparation')]
    external={i:m for i,m in turns if m['kind']=='external_turn'}
    classified={}
    for item in (st.get('summary') or {}).get('turn_classification',[]):
        m=external.get(item.get('turn_index'))
        if m and item.get('evidence') and item['evidence'] in m['text']:classified[item['turn_index']]=item['kind']
    unknown=len(external)-len(classified)
    answers=sum(m['kind']=='answer' for _,m in turns)+sum(k=='answer' for k in classified.values())
    questions=sum(m['kind']=='ask' for _,m in turns)+sum(k=='candidate_question' for k in classified.values())
    hints=st['hints']+sum(k=='hint' for k in classified.values())
    return {'answers':None if unknown else answers,'candidate_questions':None if unknown else questions,
            'hints':None if unknown else hints,'candidate_turns':len(turns),'unclassified_turns':unknown,
            'setup_turns':None if unknown else sum(k=='setup' for k in classified.values()),'closing_turns':None if unknown else sum(k=='closing' for k in classified.values()),
            'other_turns':None if unknown else sum(k=='other' for k in classified.values()),'classification_source':'codex_or_external_ai' if classified else 'manual_tags_only',
            'elapsed':round((session['finished'] or time.time())-self.began_at(session)) if self.began_at(session) is not None else 0,
            'practice_attempts':len(st['attachments']),'external_transcript':any(m.get('companion_id') or m.get('external_import') for m in st['messages'])}
 def context(self,session,action):
    st=session['state'];bank=[]
    for id in st['bank_ids']:
        t=self.task(id,st['language'])
        bank.append({k:t.get(k) for k in ['id','title','engine','desc','requirements','cases','review','answer','choices','source']})
    attachments=[]
    with self.connect() as db:
        for id in st['attachments']:
            row=db.execute('SELECT * FROM attempts WHERE id=?',(id,)).fetchone()
            if row:
                a=dict(row);a['result']=json.loads(a['result']) if a['result'] else None
                attachments.append({k:a[k] for k in ['id','task_id','code','result','revealed']})
    return {'company':st.get('company'),'interviewer':st.get('interviewer') or interviewer_persona(session['id'],session['stage'],st['language']),'language':st['language'],'stage':session['stage'],'format':FORMATS[session['stage']],
            'action':action,'candidate_profile':st['profile'],'interview_config':st['config'],
            'constructed_plan':st.get('constructor_plan'),'phase':self.phase(session),'duration_seconds':self.duration(session),'remaining_seconds':self.remaining(session),
            'transcript':st['messages'],'bank':bank,'practice_evidence':attachments,'shared_artifacts':st.get('shared_artifacts',[]),
            'stats':self.stats(session),'previous_round_summary':st.get('previous_summary')}
 def request(self,session,action):
    mode='summary' if action=='finish' else 'turn'
    nonce=uuid.uuid4().hex
    session['state']['pending']={'id':nonce,'mode':mode,'action':action}
    return {'request_id':nonce,'mode':mode,'context':self.context(session,action),'schema':SCHEMAS[mode]}
 def simple_summary(self,session):
    en=session['state']['language']=='en';stats=self.stats(session)
    return {'overview':'Session saved. An evidence-based AI debrief is not available yet.' if en else 'Сессия сохранена. Содержательный AI-разбор пока не получен.',
            'strengths':[],'gaps':[],'comments':[('Candidate turns: ' if en else 'Реплик кандидата: ')+str(stats['candidate_turns']),('Unclassified turns: ' if en else 'Неклассифицированных реплик: ')+str(stats['unclassified_turns'])],
            'unanswered_questions':[],
            'dimensions':[],'next_steps':[],'source':'local_stats','stats':stats}
 def plan(self,stage,tags,seed):
    pool=[t for t in self.catalog() if not tags or set(tags)&set(t.get('tags',[]))]
    if not pool: raise ValueError('No tasks match the selected tags')
    rng=random.Random(seed);rng.shuffle(pool)
    if stage in ('technical','lead'):
        theory=self.theory_plan(stage,tags,seed)
        if stage=='lead':return theory+[t['id'] for t in pool if t['engine']=='review'][:2]
        return theory+[t['id'] for t in pool if t['engine']=='csharp'][:1]+[t['id'] for t in pool if t['engine'] in ('postgres','sqlite')][:1]+[t['id'] for t in pool if t['engine']=='review'][:1]
    return [t['id'] for t in pool if t['engine']=='review'][:4]+[t['id'] for t in pool if t['engine']=='quiz'][:3]
 def theory_plan(self,stage,tags,seed,exclude=(),limit=5):
    pool=[t for t in self.catalog() if t['engine']=='quiz' and t['id'] not in exclude and (not tags or set(tags)&set(t.get('tags',[])))]
    random.Random('theory:'+seed).shuffle(pool)
    preferred=(['Architecture','Production','Security','Testing','Operations','Messaging / Cloud','SQL','Concurrency'] if stage=='lead' else ['C# / Runtime','Concurrency','SQL','EF Core','Testing','ASP.NET Core','Memory / GC','Async / TAP'])
    pool.sort(key=lambda t:(preferred.index(t['cat']) if t['cat'] in preferred else len(preferred),t.get('prio')!='P0'))
    selected=[];seen=set()
    for t in pool:
        if t['cat'] not in seen:selected.append(t['id']);seen.add(t['cat'])
        if len(selected)>=limit:return selected
    return (selected+[t['id'] for t in pool if t['id'] not in selected])[:limit]
 def route(self,method,path,p,language):
    with self.lock:return self._route(method,path,p,language)
 def _route(self,method,path,p,language):
    endpoint=path.removeprefix('/api/mock/')
    if method=='GET':
        if endpoint=='meta': return {'formats':FORMATS,'sources':SOURCES}
        if endpoint.startswith('quality-history/'):
            task_id=endpoint.split('/',1)[1];self.task(task_id,language)
            with self.connect() as db: rows=db.execute('SELECT id,created,state FROM code_reviews WHERE task_id=? ORDER BY created DESC LIMIT 20',(task_id,)).fetchall()
            reviews=[]
            for row in rows:
                state=json.loads(row['state'])
                if state['status']=='complete':reviews.append({'id':row['id'],'created':row['created'],'source_hash':state['source_hash'],'code':state['context']['code'],'review':state['result'],'provider':state.get('provider','external')})
            return {'reviews':reviews}
        if endpoint=='profiles':
            with self.connect() as db: rows=db.execute('SELECT * FROM mock_profiles ORDER BY updated DESC').fetchall()
            return {'profiles':[dict(id=r['id'],name=r['name'],data=json.loads(r['data'])) for r in rows]}
        if endpoint=='sessions':
            with self.connect() as db: rows=db.execute('SELECT id,stage,profile_id,started,deadline,finished,state FROM mock_sessions ORDER BY started DESC LIMIT 100').fetchall()
            sessions=[]
            for row in rows:
                item=dict(row);state=json.loads(item.pop('state'));item['created']=item['started'];item['started']=state.get('began_at',item['started']);item['deadline']=item['deadline'] if item['started'] is not None else None;item['phase']='finished' if item['finished'] else 'running' if item['started'] is not None else 'preparing';sessions.append(item)
            return {'sessions':sessions}
        if endpoint.startswith('session/'):
            return {'session':self.public(self.session(endpoint.split('/',1)[1]))}
        if endpoint=='export':
            with self.connect() as db:
                profiles=[dict(r) for r in db.execute('SELECT * FROM mock_profiles')]
                sessions=[dict(r) for r in db.execute('SELECT * FROM mock_sessions')]
                reviews=[dict(r) for r in db.execute('SELECT * FROM code_reviews')]
                translations=[dict(r) for r in db.execute('SELECT * FROM ai_translations')]
            return {'format':'forge-mock-backup-v1','profiles':profiles,'sessions':sessions,'reviews':reviews,'translations':translations}
        raise ValueError('Unknown interview endpoint')
    if endpoint.startswith('translation/'):return self.translation(endpoint,p)
    if endpoint=='resume-text': return {'text':extract_resume(limited(p.get('filename',''),200),limited(p.get('base64',''),2_800_000))}
    if endpoint=='profile':
        data={k:limited(p.get(k,''),50000 if k=='resume' else 200 if k=='company_name' else 12000) for k in ['resume','narrative','role','vacancy','company_name','company_scenario']}
        name=limited(p.get('name','Candidate'),120).strip() or 'Candidate'
        if sum(map(len,data.values()))>70000: raise ValueError('Candidate context exceeds 70,000 characters')
        id=p.get('id') or uuid.uuid4().hex
        if not re.fullmatch('[a-f0-9]{32}',id): raise ValueError('Invalid profile ID')
        with self.connect() as db: db.execute('INSERT OR REPLACE INTO mock_profiles VALUES(?,?,?,?)',(id,name,json.dumps(data,ensure_ascii=False),time.time()))
        return {'id':id}
    if endpoint=='delete-profile':
        with self.connect() as db:
            db.execute('DELETE FROM mock_sessions WHERE profile_id=?',(p.get('id'),))
            db.execute('DELETE FROM mock_profiles WHERE id=?',(p.get('id'),))
        return {'ok':True}
    if endpoint=='delete-session':
        with self.connect() as db: db.execute('DELETE FROM mock_sessions WHERE id=?',(p.get('id'),))
        return {'ok':True}
    if endpoint=='start':
        language=p.get('language',language)
        if language not in ('ru','en'):raise ValueError('Interview language must be ru or en')
        stage=p.get('stage')
        if stage not in FORMATS: raise ValueError('Invalid interview stage')
        gender=p.get('interviewer_gender','random')
        if gender not in ('random','female','male'):raise ValueError('Invalid interviewer gender')
        with self.connect() as db: profile=db.execute('SELECT * FROM mock_profiles WHERE id=?',(p.get('profile_id'),)).fetchone()
        if not profile: raise ValueError('Save/select a candidate profile first')
        duration=p.get('minutes',FORMATS[stage]['minutes'])
        if type(duration) is not int or not 5<=duration<=120: raise ValueError('Duration must be 5..120 minutes')
        tags=p.get('tags',[])
        known={tag for t in self.catalog() for tag in t.get('tags',[])}
        if not isinstance(tags,list) or len(tags)>6 or any(tag not in known for tag in tags): raise ValueError('Invalid interview tags')
        mode=p.get('mode','codex')
        if mode not in ('codex','external','chatgpt'): raise ValueError('Unknown assistant mode')
        config={'difficulty':p.get('difficulty','standard'),'hints_allowed':p.get('hints_allowed',True) is True,'strict_language':p.get('strict_language',True) is True}
        if config['difficulty'] not in ('gentle','standard','strict'): raise ValueError('Invalid difficulty')
        id=uuid.uuid4().hex;now=time.time();previous=None
        if p.get('previous_id'):
            prev=self.session(p['previous_id'])
            if prev['profile_id']!=profile['id']: raise ValueError('Previous round belongs to a different profile')
            previous=prev['state'].get('summary')
        constructed=None
        if p.get('plan_id'):
            with self.connect() as db: planrow=db.execute('SELECT state FROM interview_plans WHERE id=?',(p['plan_id'],)).fetchone()
            if not planrow: raise ValueError('Unknown interview plan')
            constructed=json.loads(planrow['state'])
            duration=constructed['minutes'];tags=constructed['tags']
        state={'version':0,'interviewer':interviewer_persona(id,stage,language,gender),'began_at':None,'duration_seconds':duration*60,'language':language,'mode':mode,'tags':tags,'config':config,'profile':{'name':profile['name'],**json.loads(profile['data'])},
               'messages':[],'attachments':[],'hints':0,'summary':None,'bank_ids':constructed['task_ids'] if constructed else self.plan(stage,tags,id),'constructor_plan':{'id':constructed['id'],'title':constructed['title']} if constructed else None,'pending':None,'previous_summary':previous}
        state['company']=employer_simulation(id,state['profile'])
        with self.connect() as db: db.execute('INSERT INTO mock_sessions VALUES(?,?,?,?,?,?,?)',(id,profile['id'],stage,now,0,None,json.dumps(state,ensure_ascii=False)))
        session=self.session(id)
        return {'session':self.public(session),'request':None}
    if endpoint.startswith('quality/'):
        return self.quality(endpoint,p,language)
    session=self.session(p.get('session_id'));st=session['state'];version=st['version']
    if endpoint=='add-theory':
        if session['finished']:raise ValueError('This round is finished')
        if session['stage'] not in ('technical','lead'):raise ValueError('Theory expansion is available for technical and lead rounds')
        count=sum(self.task(id,st['language'])['engine']=='quiz' for id in st['bank_ids'])
        if count>=5:return {'session':self.public(session)}
        extra=self.theory_plan(session['stage'],st['tags'],session['id'],st['bank_ids'],min(5-count,40-len(st['bank_ids']))) if len(st['bank_ids'])<40 else []
        if not extra:raise ValueError('No additional theory questions match the round tags, or the plan is full')
        st['bank_ids']+=extra;st['pending']=None;self.save(session,version)
        return {'session':self.public(session)}
    if endpoint=='summary-translation':
        if not session['finished'] or not st.get('summary') or st['summary'].get('source')=='local_stats':raise ValueError('Generate the interview debrief before translating it')
        target=p.get('language')
        if target not in ('ru','en') or target==st['language']:raise ValueError('Choose the other debrief language')
        if st.get('pending'):raise ValueError('Finish the pending AI request first')
        nonce=uuid.uuid4().hex
        st['pending']={'id':nonce,'mode':'summary_translation','action':'translate','language':target}
        self.save(session,version)
        return {'request':{'request_id':nonce,'mode':'summary_translation','context':self.translation_context(session,target),'schema':SCHEMAS['summary_translation']},'session':self.public(session)}
    if endpoint=='begin':
        if session['finished']:raise ValueError('This round is finished')
        if self.began_at(session) is not None:return {'session':self.public(session),'request':None,'started_new':False}
        st['began_at']=time.time();session['deadline']=st['began_at']+self.duration(session)
        request=self.request(session,'start');self.save(session,version)
        return {'session':self.public(session),'request':request,'started_new':True}
    if endpoint=='attach':
        if session['finished']:raise ValueError('The interview is finished; attached evidence is frozen')
        with self.connect() as db: a=db.execute('SELECT * FROM attempts WHERE id=?',(p.get('attempt_id'),)).fetchone()
        if not a or not a['finished'] or not a['result'] or a['task_id'] not in st['bank_ids']: raise ValueError('Choose a submitted attempt for an interview task')
        result=json.loads(a['result'])
        if result.get('status') in ('checking','infrastructure_error','busy'): raise ValueError('Attempt has no usable result')
        if a['id'] not in st['attachments']:st['attachments'].append(a['id'])
        st['pending']=None;self.save(session,version)
        return {'session':self.public(session)}
    if endpoint=='action':
        action=p.get('action')
        if action not in ('answer','ask','hint','next','finish','retry'): raise ValueError('Unknown interview action')
        if len(st['messages'])>=120 and action not in ('finish','retry'): raise ValueError('Turn limit reached; finish this round')
        if self.began_at(session) is None and action!='finish':raise ValueError('Connect your assistant, then click Start interview')
        if action=='retry':
            action='finish' if session['finished'] else (st.get('pending') or {}).get('action','continue')
        elif session['finished']: raise ValueError('This round is finished')
        elif self.began_at(session) is not None and time.time()>session['deadline']:action='finish'
        if action=='hint':
            if not st['config']['hints_allowed']: raise ValueError('Hints are disabled for this round')
            st['hints']+=1
        text=limited(p.get('text',''),8000).strip()
        if action in ('answer','ask') and not text: raise ValueError('Enter your answer or question')
        if action in ('answer','ask','hint','next'):
            st['messages'].append({'role':'candidate','kind':action,'text':text or action,'at':time.time()})
        if sum(len(m['text']) for m in st['messages'])>200000:raise ValueError('Transcript limit reached')
        if action=='finish':
            session['finished']=session['finished'] or time.time()
            st['summary']=st.get('summary') or self.simple_summary(session)
        request=self.request(session,action);self.save(session,version)
        return {'session':self.public(session),'request':request}
    if endpoint=='packet':
        pending=st.get('pending')
        if not pending: request=self.request(session,'finish' if session['finished'] else 'prepare' if self.began_at(session) is None else 'continue');self.save(session,version)
        else: request={'request_id':pending['id'],'mode':pending['mode'],'context':self.translation_context(session,pending['language']) if pending['mode']=='summary_translation' else self.context(session,pending['action']),'schema':SCHEMAS[pending['mode']]}
        return {'request':request,'instructions':INSTRUCTIONS,'session':self.public(session)}
    if endpoint=='complete':
        pending=st.get('pending')
        if not pending or pending['id']!=p.get('request_id'): raise ValueError('This AI response is stale; prepare a new request')
        if pending['mode']=='turn' and self.began_at(session) is None:raise ValueError('Click Start interview before recording interview questions')
        result=validate(pending['mode'],p.get('result'),st['bank_ids'])
        if pending['mode']=='turn' and not any(m['role']=='interviewer' for m in st['messages']):
            person=st.get('interviewer') or interviewer_persona(session['id'],session['stage'],st['language'])
            if person['name'] not in result['message']:
                greeting=('Здравствуйте! Я ' if st['language']=='ru' else 'Hello! I’m ')+person['name']+', '+person['role']+'.'
                result={**result,'message':greeting+'\n\n'+result['message']}
                if len(result['message'])>16000:raise ValueError('Interviewer introduction exceeds response limit')
        provider=p.get('provider','external')
        if pending['mode']=='summary':
            seen=set()
            for item in result['turn_classification']:
                index=item['turn_index']
                if index in seen or not 0<=index<len(st['messages']):raise ValueError('Invalid or duplicate transcript classification index')
                m=st['messages'][index]
                if m['role']!='candidate' or m['kind']!='external_turn' or m.get('preparation') or not item['evidence'].strip() or item['evidence'] not in m['text']:raise ValueError('Classification must quote the matching candidate turn')
                seen.add(index)
            st['summary']={**result,'source':provider};st['summary']['stats']=self.stats(session)
            st.pop('summary_translation',None)
        elif pending['mode']=='summary_translation':
            original=st['summary']
            if [d['rating'] for d in result['dimensions']]!=[d['rating'] for d in original['dimensions']] or [d['rating'] for d in result['language_feedback']['dimensions']]!=[d['rating'] for d in original.get('language_feedback',{}).get('dimensions',[])]:raise ValueError('Translation must preserve all ratings')
            if [n['task_id'] for n in result['next_steps']]!=[n['task_id'] for n in original['next_steps']]:raise ValueError('Translation must preserve recommended tasks')
            feedback=original.get('language_feedback') or {}
            if result['language_feedback']['assessed']!=feedback.get('assessed',False) or [(i['original'],i['corrected']) for i in result['language_feedback']['issues']]!=[(i['original'],i['corrected']) for i in feedback.get('issues',[])]:raise ValueError('Translation must preserve the original language examples and assessment status')
            st['summary_translation']={'language':pending['language'],'result':result,'source':provider}
        else:
            st['messages'].append({'role':'interviewer','kind':result['kind'],'text':result['message'],'task_id':result['task_id'],'evidence_note':result['evidence_note'],'at':time.time(),'source':provider})
        st['pending']=None;self.save(session,version)
        return {'session':self.public(session)}
    raise ValueError('Unknown interview endpoint')
 def translation_context(self,session,language):
    summary=session['state']['summary']
    return {'language':language,'original_language':session['state']['language'],
            'original_summary':{k:summary.get(k) for k in SCHEMAS['summary_translation']['properties']},
            'bank':[{'id':id} for id in session['state']['bank_ids']]}
 def quality(self,endpoint,p,language):
    if endpoint in ('quality/static','quality/prepare'):
        task=self.task(p.get('task_id'),language)
        if task['engine'] not in ('csharp','postgres','sqlite'):raise ValueError('Code review supports C# and SQL tasks')
        code=limited(p.get('code',''),100000)
        result=analyze(task,code)
        if endpoint=='quality/static':return {'quality':result}
        if not code.strip():raise ValueError('Enter a solution before requesting AI review')
        evidence=None
        with self.connect() as db:
            rows=db.execute('SELECT * FROM attempts WHERE task_id=? AND code=? AND finished IS NOT NULL ORDER BY started DESC LIMIT 1',(task['id'],code)).fetchall()
            if rows:evidence=json.loads(rows[0]['result']) if rows[0]['result'] else None
        id=uuid.uuid4().hex
        context={'language':language,'bank':[{k:task.get(k) for k in ['id','title','desc','requirements','cases','review','source']}],
                 'code':code,'static_signals':result,'test_evidence':evidence,'instructions':'If test_evidence is null, this exact source has no submitted test result. Never claim it passes.'}
        state={'status':'pending','context':context,'source_hash':result['source_hash'],'result':None}
        with self.connect() as db:db.execute('INSERT INTO code_reviews VALUES(?,?,?,?)',(id,task['id'],time.time(),json.dumps(state,ensure_ascii=False)))
        return {'quality':result,'request':{'request_id':id,'mode':'review','context':context,'schema':SCHEMAS['review']},'instructions':INSTRUCTIONS}
    if endpoint=='quality/complete':
        with self.connect() as db:
            row=db.execute('SELECT * FROM code_reviews WHERE id=?',(p.get('request_id'),)).fetchone()
            if not row:raise ValueError('Unknown review request')
            state=json.loads(row['state'])
            if state['status']!='pending':raise ValueError('Review already completed')
            result=validate('review',p.get('result'),[row['task_id']])
            state.update(status='complete',result=result,provider=p.get('provider','external'))
            db.execute('UPDATE code_reviews SET state=? WHERE id=?',(json.dumps(state,ensure_ascii=False),row['id']))
            # Advice during an active attempt is assistance; previous independent submissions stay unchanged.
            db.execute('UPDATE attempts SET revealed=1 WHERE task_id=? AND finished IS NULL',(row['task_id'],))
        return {'review':result,'source_hash':state['source_hash'],'review_id':row['id']}
    raise ValueError('Unknown quality endpoint')
 def translation_source(self,kind,source_id):
    if kind=='message':
        session_id,separator,index=source_id.partition(':')
        session=self.session(session_id)
        if not separator or not index.isdigit() or int(index)>=len(session['state']['messages']):raise ValueError('Unknown transcript message')
        message=session['state']['messages'][int(index)]
        return message['text']+('\n\n'+message['evidence_note'] if message.get('evidence_note') else '')
    if kind=='review':
        with self.connect() as db:row=db.execute('SELECT state FROM code_reviews WHERE id=?',(source_id,)).fetchone()
        if not row:raise ValueError('Unknown code review')
        state=json.loads(row['state'])
        if state['status']!='complete':raise ValueError('Finish the code review first')
        q=state['result'];parts=[q['overview']]
        for f in q['findings']:
            parts.append(f"{f['severity']} · L{f['line'] or '—'} · {f['confidence']}\n{f['observation']}\nEvidence:\n{f['evidence']}\nHint:\n{f['hint']}")
        for label,key in [('Strengths','strengths'),('Questions','questions'),('Graduated hints','hint_ladder')]:
            parts.append(label+'\n'+'\n'.join(q[key]))
        parts.extend(['Complexity\n'+q['complexity'],'Limitations\n'+q['limitations']])
        return '\n\n'.join(parts)
    if kind=='context':
        context=self.context(self.session(source_id),'continue')
        # Remaining seconds and derived counters are volatile, not text to translate.
        for key in ('remaining_seconds','stats','phase'):context.pop(key,None)
        return json.dumps(context,ensure_ascii=False,indent=2)
    raise ValueError('Unsupported translation source')
 def translation(self,endpoint,p):
    if endpoint=='translation/prepare':
        kind=p.get('kind');source_id=limited(p.get('source_id',''),150)
        text=self.translation_source(kind,source_id)
        fragments=[];remaining=text
        while remaining:
            end=min(len(remaining),6000)
            if end<len(remaining):
                boundary=remaining.rfind('\n',3000,end)
                if boundary<0:boundary=remaining.rfind(' ',3000,end)
                if boundary>=0:end=boundary+1
            fragments.append(remaining[:end]);remaining=remaining[end:]
        if not fragments:fragments=['']
        part=p.get('part',0);total=len(fragments)
        if type(part) is not int or not 0<=part<total:raise ValueError('Invalid translation fragment')
        digest=hashlib.sha256(text.encode()).hexdigest()
        id=hashlib.sha256(f'{kind}:{source_id}:{digest}:ru:{part}'.encode()).hexdigest()
        with self.connect() as db:
            row=db.execute('SELECT state FROM ai_translations WHERE id=?',(id,)).fetchone()
            state=json.loads(row['state']) if row else {'request_id':uuid.uuid4().hex,'kind':kind,'source_id':source_id,'source_hash':digest,'result':None}
            if not row:db.execute('INSERT INTO ai_translations VALUES(?,?)',(id,json.dumps(state,ensure_ascii=False)))
        response={'translation_id':id,'parts':total,'source_hash':digest,'translation':state['result']}
        if state['result'] is None:response['request']={'request_id':state['request_id'],'mode':'translation','context':{'language':'ru','original_text':fragments[part]},'schema':SCHEMAS['translation']}
        return response
    if endpoint=='translation/complete':
        with self.connect() as db:
            row=db.execute('SELECT state FROM ai_translations WHERE id=?',(p.get('translation_id'),)).fetchone()
            if not row:raise ValueError('Unknown translation request')
            state=json.loads(row['state'])
            if state['request_id']!=p.get('request_id'):raise ValueError('Stale translation request')
            source=self.translation_source(state['kind'],state['source_id'])
            if hashlib.sha256(source.encode()).hexdigest()!=state['source_hash']:raise ValueError('Source changed; request its translation again')
            result=validate('translation',p.get('result'))
            if not result['text'].strip():raise ValueError('Empty translation')
            if state['result'] is None:
                state['result']=result['text'];db.execute('UPDATE ai_translations SET state=? WHERE id=?',(json.dumps(state,ensure_ascii=False),row['id']))
        return {'translation':state['result']}
    raise ValueError('Unknown translation endpoint')
