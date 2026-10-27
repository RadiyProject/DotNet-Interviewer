"""Deterministic interview plans built from the existing task bank; no AI required."""
import json
import random
import time
import uuid

class InterviewBuilder:
 def __init__(self,connect,catalog,public_task):
    self.connect,self.catalog,self.public_task=connect,catalog,public_task
    with connect() as db:
        db.execute('CREATE TABLE IF NOT EXISTS interview_plans(id TEXT PRIMARY KEY,created REAL NOT NULL,state TEXT NOT NULL)')
 def read(self,id,db):
    row=db.execute('SELECT * FROM interview_plans WHERE id=?',(id,)).fetchone()
    if not row:raise ValueError('Unknown interview plan')
    return json.loads(row['state'])
 def write(self,s,db):
    db.execute('UPDATE interview_plans SET state=? WHERE id=?',(json.dumps(s,ensure_ascii=False),s['id']))
 def public(self,s):
    from localization import localize_task
    bank={t['id']:t for t in self.catalog()}
    with self.connect() as db:
        attempts={r['id']:dict(r) for r in db.execute('SELECT id,task_id,finished,result,revealed FROM attempts') if r['id'] in s['attempts'].values()}
    items=[]
    for id in s['task_ids']:
        a=attempts.get(s['attempts'].get(id));r=json.loads(a['result']) if a and a['result'] else None
        items.append({'task':self.public_task(localize_task(bank[id],s['language'])),'attempt_id':a['id'] if a else None,
                      'finished':a['finished'] if a else None,'result':{k:r.get(k) for k in ['status','score','revealed']} if r else None,
                      'assisted':bool(a and (a['revealed'] or (r or {}).get('revealed'))),'self_assessed':bank[id]['engine']=='review'})
    completed=[x for x in items if x['finished'] and x['result'] and x['result']['status']!='checking']
    checked=[x for x in completed if not x['self_assessed'] and not x['assisted'] and x['result']['status'] not in ('infrastructure_error','busy','checking')]
    return {**s,'items':items,'stats':{'completed':len(completed),'total':len(items),'independent_checked':len(checked),
       'passed':sum(x['result']['status']=='passed' for x in checked),'assisted':sum(x['assisted'] for x in completed),
       'self_assessed':sum(x['result']['status']=='self_review' for x in completed)}}
 def route(self,method,path,p,language):
    endpoint=path.removeprefix('/api/interviews/')
    if method=='GET':
        if endpoint=='list':
            with self.connect() as db:rows=db.execute('SELECT state FROM interview_plans ORDER BY created DESC LIMIT 100').fetchall()
            return {'plans':[{k:s.get(k) for k in ['id','title','created','started','finished','minutes']} for s in map(lambda r:json.loads(r['state']),rows)]}
        with self.connect() as db:s=self.read(endpoint,db)
        return {'plan':self.public(s)}
    if endpoint=='generate':
        minutes=p.get('minutes',60);counts=p.get('counts',{});tags=p.get('tags',[]);categories=p.get('categories',[])
        if type(minutes) is not int or not 10<=minutes<=120:raise ValueError('Duration must be 10..120 minutes')
        if not isinstance(counts,dict) or set(counts)!={'quiz','csharp','sql','review'} or any(type(v) is not int or not 0<=v<=30 for v in counts.values()) or not 1<=sum(counts.values())<=40:raise ValueError('Choose 1..40 tasks')
        if not isinstance(tags,list) or len(tags)>6 or not all(isinstance(x,str) for x in tags):raise ValueError('Choose up to 6 tags')
        if not isinstance(categories,list) or not all(isinstance(x,str) for x in categories):raise ValueError('Invalid categories')
        strategy=p.get('strategy','balanced')
        if strategy not in ('balanced','weak','random'):raise ValueError('Invalid selection strategy')
        title=p.get('title','Interview')
        if not isinstance(title,str) or len(title)>120:raise ValueError('Title is too long')
        seed=uuid.uuid4().hex;rng=random.Random(seed)
        pool=[t for t in self.catalog() if (not tags or set(tags)&set(t.get('tags',[]))) and (not categories or t['cat'] in categories)]
        with self.connect() as db:rows=db.execute('SELECT task_id,result,revealed FROM attempts WHERE finished IS NOT NULL ORDER BY started DESC').fetchall()
        latest={}
        for a in rows:
            if a['task_id'] in latest or not a['result'] or a['revealed']:continue
            r=json.loads(a['result'])
            if not r.get('revealed') and not r.get('imported') and r.get('status') in ('passed','failed','expired','timeout','compile_error','runtime_error'):latest[a['task_id']]=r.get('score',0) or 0
        rng.shuffle(pool);selected=[];used=0;actual={k:0 for k in counts};seen={}
        group=lambda t:'sql' if t['engine'] in ('postgres','sqlite') else t['engine']
        # Round robin prevents theory questions from consuming the entire budget first.
        while True:
            added=False
            for kind in ('csharp','sql','review','quiz'):
                if actual[kind]>=counts[kind]:continue
                candidates=[t for t in pool if group(t)==kind and t['id'] not in selected and used+t['minutes']<=minutes]
                if not candidates:continue
                if strategy=='weak':candidates.sort(key=lambda t:(latest.get(t['id'],35),seen.get(t['cat'],0)))
                elif strategy=='balanced':candidates.sort(key=lambda t:(seen.get(t['cat'],0),t.get('prio')!='P0'))
                t=candidates[0];selected.append(t['id']);used+=t['minutes'];actual[kind]+=1;seen[t['cat']]=seen.get(t['cat'],0)+1;added=True
            if not added:break
        if not selected:raise ValueError('No matching task fits the time budget; increase the budget or change filters')
        s={'id':seed,'title':title.strip() or 'Interview','created':time.time(),'language':language,'minutes':minutes,'estimated_minutes':used,
           'counts':counts,'actual_counts':actual,'tags':tags,'categories':categories,'strategy':strategy,'task_ids':selected,'attempts':{},'started':None,'deadline':None,'finished':None}
        with self.connect() as db:db.execute('INSERT INTO interview_plans VALUES(?,?,?)',(seed,s['created'],json.dumps(s,ensure_ascii=False)))
        return {'plan':self.public(s)}
    with self.connect() as db:
        db.execute('BEGIN IMMEDIATE');s=self.read(p.get('id'),db)
        if endpoint=='delete':
            if s['started'] and not s['finished']:raise ValueError('Finish the interview before deleting its plan')
            db.execute('DELETE FROM interview_plans WHERE id=?',(s['id'],));return {'ok':True}
        if endpoint=='start-task':
            id=p.get('task_id');now=time.time()
            if id not in s['task_ids']:raise ValueError('Task is not part of this plan')
            if s['finished'] or (s['deadline'] and now>=s['deadline']):raise ValueError('Interview finished or time expired')
            if not s['started']:s['started']=now;s['deadline']=now+s['minutes']*60
            if id not in s['attempts']:
                existing=db.execute('SELECT id FROM attempts WHERE task_id=? AND finished IS NULL',(id,)).fetchone()
                if existing:raise ValueError('Finish the existing open attempt for this task first')
                task=next(t for t in self.catalog() if t['id']==id);aid=uuid.uuid4().hex
                db.execute('INSERT INTO attempts(id,task_id,started,deadline) VALUES(?,?,?,?)',(aid,id,now,min(s['deadline'],now+task['minutes']*60)))
                s['attempts'][id]=aid
        elif endpoint=='finish':
            s['finished']=s['finished'] or time.time()
            for aid in s['attempts'].values():
                a=db.execute('SELECT result,finished,started,task_id,code FROM attempts WHERE id=?',(aid,)).fetchone()
                if a and a['result'] and json.loads(a['result']).get('status')=='checking':raise ValueError('Wait for the running check before finishing')
                if a and not a['finished']:
                    draft=db.execute('SELECT code FROM drafts WHERE task_id=?',(a['task_id'],)).fetchone()
                    db.execute('UPDATE attempts SET finished=?,code=?,result=? WHERE id=?',(s['finished'],draft['code'] if draft else a['code'],json.dumps({'status':'expired','score':0,'elapsed':round(s['finished']-a['started']),'output':'Interview finished before submission'}),aid))
        else:raise ValueError('Unknown interview action')
        self.write(s,db)
    return {'plan':self.public(s)}
