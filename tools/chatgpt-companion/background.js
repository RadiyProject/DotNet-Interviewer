'use strict';
let busy=false;
const passiveRetry=new Map();
async function diagnostic(action,state){
 const {diagnostics=[]}=await chrome.storage.session.get('diagnostics');
 diagnostics.push({time:new Date().toISOString(),action,state});
 await chrome.storage.session.set({diagnostics:diagnostics.slice(-80)});
}
const get=()=>chrome.storage.session.get('connection').then(x=>x.connection||null);
const put=connection=>chrome.storage.session.set({connection});
function failure(code,message){return Object.assign(Error(message),{code});}
function safeOrigin(value){if(!['http://localhost:8080','http://127.0.0.1:8080'].includes(value))throw Error('Use http://localhost:8080 or http://127.0.0.1:8080');return value;}
function chatURL(value){const u=new URL(value);if(u.origin!=='https://chatgpt.com')throw Error('Select a ChatGPT tab');return u.origin+u.pathname;}
function stableConversationURL(value){try{const u=new URL(value);return u.origin==='https://chatgpt.com'&&/^\/(?:g\/[A-Za-z0-9_-]+\/)?c\/[A-Za-z0-9_-]+\/?$/.test(u.pathname);}catch{return false;}}
function waitingForConversationURL(){return failure('CHATGPT_URL_PENDING','ChatGPT is still assigning a permanent conversation URL. No context was resent.');}
async function local(c,path,data){if(['delivered','transcript'].includes(path)&&!stableConversationURL(data?.url))throw waitingForConversationURL();const r=await fetch(safeOrigin(c.origin)+'/api/companion/'+path,{method:data?'POST':'GET',headers:{Authorization:'Bearer '+c.token,...(data?{'Content-Type':'application/json'}:{})},...(data?{body:JSON.stringify(data)}:{}),signal:AbortSignal.timeout(15000)});const p=await r.json();if(!r.ok){const error=Error(p.error||r.statusText);error.code='HTTP_'+r.status+'_'+path;throw error;}if(path==='delivered'){c.waitingConversationURL=false;c.lastError=null;}if(path==='status'&&p.deadline&&!p.finished){const when=Date.now()+Math.max(1,(p.deadline-(p.server_time||Date.now()/1000))*1000);await chrome.alarms.create('forge-deadline',{when});}return p;}
async function bound(c){if(!c?.tabId)throw Error('Bind a ChatGPT tab first');const tab=await chrome.tabs.get(c.tabId);const url=chatURL(tab.url);if(c.url&&c.url!==url)throw Error('The bound tab switched conversations. Return to '+c.url);return tab;}
async function content(c,action,data={}){
 await bound(c);
 // Probe before delivery: never retry a possibly sent prompt.
 try{await chrome.tabs.sendMessage(c.tabId,{forge:true,action:'ping'});}
 catch{
  try{await chrome.scripting.executeScript({target:{tabId:c.tabId},files:['chatgpt.js']});}
  catch{throw Error('Cannot load the companion in ChatGPT. Wait for the tab to load and allow the extension access to chatgpt.com.');}
 }
 try{return await chrome.tabs.sendMessage(c.tabId,{forge:true,action,sessionId:c.sessionId,...data});}
 catch{throw Error('ChatGPT tab disconnected. Return to that tab, wait for it to load and retry.');}
}
async function read(c,loadHistory=false){const r=await content(c,'read',{anchorNonce:c.transcriptAnchor||null,loadHistory});if(r.error)throw failure('CHATGPT_READ',r.error);if(!stableConversationURL(r.url)&&(r.markers?.length||new URL(r.url).pathname.includes('/c/')))throw waitingForConversationURL();return r;}
async function acknowledge(c){
 const pending=await local(c,'status');
 const r=await read(c),known=pending.known_nonces||[pending.pending_nonce].filter(Boolean);
 const marker=[...r.markers].reverse().find(m=>known.includes(m.nonce));
 if(!marker){
  if(pending.url===r.url)return;
  if(r.detected_session_ids?.some(id=>id!==c.sessionId))throw failure('WRONG_INTERVIEW','В чате найден контекст другого раунда. Открой в тренажёре тот сохранённый раунд, для которого отправлял контекст, и подключись к нему. Повторная отправка не нужна.');
  if(r.markers.length)throw failure('UNKNOWN_CONTEXT','Контекст этого раунда виден, но сервер больше не хранит его идентификатор. Не отправляй его повторно: требуется восстановить связанный раунд.');
  throw failure('CONTEXT_NOT_VISIBLE','В выбранной вкладке не виден маркер FORGE_CONTEXT. Открой чат с отправленным контекстом и прокрути к этому сообщению, затем повтори проверку доставки.');
 }
 if(pending.url&&pending.url!==r.url)throw failure('WRONG_CHAT','Этот раунд уже связан с другим чатом. Открой связанный чат вместо повторной отправки контекста.');
 await local(c,'delivered',{nonce:marker.nonce,url:r.url,confirmed:marker.confirmed});
 c.url=r.url;c.uncertain=null;await put(c);
 if(pending.pending_nonce&&c.startNonce===pending.pending_nonce){c.startDelivered=true;await put(c);}
 await diagnostic('delivery','visible_in_chat');
}
async function sync(c,final=false,loadHistory=false){const r=await read(c,loadHistory);if(r.transcript_error)throw failure('TRANSCRIPT_INCOMPLETE',r.transcript_error);const pending=await local(c,'status');let newest=null;
 for(const marker of r.markers){if(pending.known_nonces&&!pending.known_nonces.includes(marker.nonce))continue;if(pending.pending_nonce===marker.nonce){await local(c,'delivered',{nonce:marker.nonce,url:r.url,confirmed:marker.confirmed});c.url=r.url;c.uncertain=null;await put(c);}newest=marker;}
 if(!newest)throw failure('CONTEXT_NOT_LOADED',c.url?'The original context is not loaded in this paired conversation. Use Sync transcript now to load its history; do not resend context.':'The interview marker is not visible. Pair the original ChatGPT conversation before saving.');
 if(!c.url){const status=await local(c,'status');c.url=status.url;if(!c.url)throw Error('Context delivery is not confirmed yet');await put(c);}
 if(r.transcript_error)throw failure('TRANSCRIPT_INCOMPLETE',r.transcript_error);
 const fingerprint=Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256',new TextEncoder().encode(JSON.stringify(r.messages)))),b=>b.toString(16).padStart(2,'0')).join('');
 if(r.coaching_ready===true){if(c.coachingSafeHash!==fingerprint){c.coachingSafeHash=fingerprint;c.coachingSafeSince=Date.now();}}else{c.coachingSafeSince=null;c.coachingSafeHash=null;}
 let settled=false;if(r.voice_idle===true&&r.transcript_stable===true){if(c.idleTranscriptHash===fingerprint)settled=Date.now()-(c.idleTranscriptSince||Date.now())>=5000;else{c.idleTranscriptHash=fingerprint;c.idleTranscriptSince=Date.now();}}else{c.idleTranscriptHash=null;c.idleTranscriptSince=null;}
 const result=await local(c,'transcript',{context_nonce:newest.nonce,url:r.url,messages:r.messages,transcript_anchor:r.transcript_anchor||null,transcript_complete:final||(settled&&r.history_loaded===true),coaching_ready:r.coaching_ready===true});
 c.lastSync=result.last_sync;c.lastError=null;if(r.transcript_anchor)c.transcriptAnchor=r.transcript_anchor;await put(c);return result;
}
async function deliver(c,manual=false){const p=await local(c,'pending');if(p.finished)throw Error('This interview is finished');if(!p.update)return {state:'nothing_pending'};
 const already=await read(c);if(already.markers.some(m=>m.nonce===p.update.nonce)){await local(c,'delivered',{nonce:p.update.nonce,url:already.url});c.url=already.url;c.uncertain=null;await put(c);return {state:'visible_in_chat'};}
 if(p.update.action==='coaching'&&(already.coaching_ready!==true||!c.coachingSafeSince||Date.now()-c.coachingSafeSince<5000))return {state:'waiting_for_safe_coaching_delivery'};
 if(c.uncertain===p.update.nonce)throw Error('Delivery is unconfirmed. Inspect the ChatGPT tab and sync again. No duplicate message was sent.');
 if(!manual&&!c.autoUpdates)return {state:'waiting_for_send'};
 c.uncertain=p.update.nonce;await put(c);
 const r=await content(c,'deliver',{prompt:p.update.prompt,nonce:p.update.nonce,safeCoaching:p.update.action==='coaching',coachingAnchor:already.last_turn_key});
 if(r.error){if(!r.mayHaveSent){c.uncertain=null;await put(c);}const error=Error(r.error);error.code=r.code||'CHATGPT_DELIVERY';throw error;}
 if(r.state==='draft_busy'||r.state==='composer_unavailable'||r.state==='waiting_for_safe_coaching_delivery'){c.uncertain=null;await put(c);return r;}
 if(r.state==='visible_in_chat'){await local(c,'delivered',{nonce:p.update.nonce,url:r.url,confirmed:r.confirmed});c.url=r.url;c.uncertain=null;await put(c);}
 if(p.update.action==='start'){c.startNonce=p.update.nonce;c.startDelivered=r.state==='visible_in_chat';await put(c);}
 return r;
}
async function connect(origin,token){
 const next={origin:safeOrigin(origin),token,consent:false,autoSync:true,autoUpdates:true};
 const status=await local(next,'status');if(status.finished&&!status.can_recover)throw Error('Pair an unfinished interview or an incomplete transcript to recover');
 const previous=await get();
 if(previous?.sessionId===status.session_id&&previous.origin===next.origin){next.consent=previous.consent===true;next.autoSync=previous.autoSync;next.autoUpdates=previous.autoUpdates;next.tabId=previous.tabId;}
 next.transcriptAnchor=status.transcript_anchor||null;next.lastStatus=status;next.sessionId=status.session_id;next.url=status.url||null;next.name=status.name;await put(next);await chrome.storage.session.remove('autoPair');
 chrome.alarms.create('forge-sync',{periodInMinutes:0.5});return {state:'connected',connection:publicConnection(next),status};
}
async function automaticPair(origin,force=false){
 let {autoPair}=await chrome.storage.session.get('autoPair');
 // Reopening/clicking the popup must claim the approved request, not discard it.
 if(!autoPair&&!force){const c=await get();if(c){const status=await local(c,'status');if(!status.finished||status.can_recover)return {state:'connected',connection:publicConnection(c),status};}}
 if(!autoPair){
  const secret=Array.from(crypto.getRandomValues(new Uint8Array(32)),x=>x.toString(16).padStart(2,'0')).join('');
  const requested=await local({origin:safeOrigin(origin),token:''},'auto-request',{secret});
  autoPair={origin,secret,request_id:requested.request_id};await chrome.storage.session.set({autoPair});
  return {state:'waiting'};
 }
 let claim;try{claim=await local({origin:autoPair.origin,token:''},'auto-claim',{request_id:autoPair.request_id,secret:autoPair.secret});}
 catch(error){await chrome.storage.session.remove('autoPair');throw error;}
 if(claim.status!=='approved')return {state:'waiting'};
 const result=await connect(autoPair.origin,claim.token);await chrome.storage.session.remove('autoPair');return result;
}
async function work(message){let c=await get();
 switch(message.action){
 case 'check-delivery':{
  if(!c?.consent)throw Error('Pair the interview and allow sharing first');
  await acknowledge(c);const status=await local(c,'status');
  if(!status.url)throw Error('The selected chat has no matching interview context. Select the chat where you already sent it. No context was resent.');
  return {state:'visible_in_chat'};
 }
 case 'diagnostics':return {events:(await chrome.storage.session.get('diagnostics')).diagnostics||[]};
 case 'auto-pair':return await automaticPair(message.origin||'http://localhost:8080',message.force===true);
 case 'connect':return await connect(message.origin,String(message.token||'').trim());
 case 'status':{
  const pairingPending=!!(await chrome.storage.session.get('autoPair')).autoPair;
  if(!c)return {connection:null,status:null,pairingPending};
  try{
   let status=await local(c,'status');
   if(c.consent&&c.tabId&&(c.waitingConversationURL||c.uncertain)){
    try{await acknowledge(c);c=await get();status=await local(c,'status');}
    catch(e){if(e.code!=='CHATGPT_URL_PENDING')throw e;c.waitingConversationURL=true;c.lastError=null;}
   }
   if(status.url&&c.url===status.url&&!c.uncertain&&c.lastError==='Unknown context or invalid ChatGPT conversation URL'){c.lastError=null;c.waitingConversationURL=false;}
   c.lastStatus=status;await put(c);return {connection:publicConnection(c),status,pairingPending};}

  catch(e){return {connection:publicConnection(c),status:c.lastStatus||{phase:'preparing',url:c.url},remoteError:e.message,pairingPending};}
 }
 case 'disconnect':await chrome.storage.session.remove(['connection','autoPair']);await chrome.alarms.clear('forge-sync');await chrome.alarms.clear('forge-deadline');return {ok:true};
 }
 if(!c){if(['poll-start','changed'].includes(message.action))return {ignored:true};throw failure('NOT_PAIRED','Сначала подключи интервью в расширении.');}
 switch(message.action){
 case 'poll-start':{
  if(message.tabId!==c.tabId||!c.consent)return {ignored:true};
  const status=await local(c,'status');if(status.deadline_reached)return await finishAtDeadline(c,status);if(c.startDelivered)return {ignored:true};
  const p=await local(c,'pending');
  if(p.update?.action!=='start'||c.uncertain===p.update.nonce)return {ignored:true};
  return await deliver(c,true);
 }
 case 'check-tab':return await content(c,'check-composer');
 case 'new-tab':{const tab=await chrome.tabs.create({url:c.url||'https://chatgpt.com/'});c.tabId=tab.id;await put(c);return {ok:true};}
 case 'bind-current':{const [tab]=await chrome.tabs.query({active:true,currentWindow:true});const url=chatURL(tab.url);if(c.url&&url!==c.url)throw Error('This interview belongs to '+c.url+'. Open that conversation or start a new interview.');c.tabId=tab.id;await put(c);return {ok:true};}
 case 'preview':{await bound(c);return await local(c,'pending');}
 case 'send':if(!c.consent)throw Error('Allow sharing in the companion first');return await deliver(c,true);
 case 'sync':{if(!c.consent)throw Error('Allow sharing in the companion first');const status=await local(c,'status');return await sync(c,!!status.finished,true);}
 case 'voice':{const check=await read(c);if(!check.markers.length)throw Error('Send the interview context before starting Voice');const r=await content(c,'voice');if(r.error)throw Error(r.error);return r;}
 case 'consent':if(message.allowed===true&&!c.consent){c.autoSync=true;c.autoUpdates=true;}c.consent=message.allowed===true;if(!c.consent){c.autoSync=false;c.autoUpdates=false;}await put(c);return {ok:true};
 case 'settings':if(!c.consent)throw Error('Allow sharing in the companion first');c.autoSync=!!message.autoSync;c.autoUpdates=!!message.autoUpdates;await put(c);return {ok:true};
 case 'begin':{let s=await local(c,'status');if(!s.url){if(!c.consent)throw Error('Allow sharing and send context before starting');await acknowledge(c);s=await local(c,'status');}if(!s.url)throw Error('Send the interview context to ChatGPT before starting the timer');await local(c,'begin',{});await local(c,'status');try{const delivery=await deliver(c,true);return {state:'interview_started',delivery:delivery.state};}catch{return {state:'interview_started',delivery:'needs_manual_start'};}}
 case 'finish':{
  if(!c.consent)throw failure('CONSENT_REQUIRED','Разреши сохранение расшифровки галочкой в расширении, затем повтори сохранение. / Allow transcript sharing in the companion first.');
  let syncError=null;
  try{await sync(c,true,true);}catch(e){syncError=e.message;}
  const result=await local(c,'finish',{allow_incomplete:true,transcript_complete:!syncError});
  return {...result,sync_error:syncError};
 }
 case 'changed':if(message.tabId!==c.tabId||!c.consent)return {ignored:true};{const status=await local(c,'status');if(status.finished&&status.transcript_complete)return {ignored:true};}if(c.autoSync)return await sync(c);if(c.uncertain||!c.url)await acknowledge(c);return {ok:true};
 default:throw Error('Unknown companion action');
 }
}
function publicConnection(c){return {sessionId:c.sessionId,origin:c.origin,name:c.name,tabId:c.tabId,url:c.url,consent:!!c.consent,autoSync:c.autoSync,autoUpdates:c.autoUpdates,lastSync:c.lastSync,lastError:c.lastError,uncertain:!!c.uncertain,waitingConversationURL:!!c.waitingConversationURL};}
async function guarded(m){
 if(m.action==='diagnostics')return await work(m);
 const passive=['poll-start','changed'].includes(m.action),retryKey=String(m.tabId);
 if(passive&&(passiveRetry.get(retryKey)||0)>Date.now())return {ignored:true};
 if(busy)return {error:'Companion is busy; retry shortly'};
 busy=true;
 try{
  if(m.action!=='poll-start')await diagnostic(m.action,'started');
  const result=await work(m);
  if(m.action!=='poll-start'||result.state)await diagnostic(m.action,result.state||'completed');
  await chrome.action.setBadgeText({text:''});return result;
 }catch(e){
  if(e.code==='CHATGPT_URL_PENDING'){
   const c=await get();if(c){c.waitingConversationURL=true;c.lastError=null;await put(c);}
   passiveRetry.delete(retryKey);await chrome.action.setBadgeText({text:''});
   return {state:'waiting_for_conversation_url'};
  }
  if(passive)passiveRetry.set(retryKey,Date.now()+60000);
  // Record the failed step, never credentials, URLs, prompts or transcripts.
  await diagnostic(m.action,'failed'+(e.code?' · '+e.code: e.name==='TimeoutError'?' · timeout':' · '+e.name));
  const c=await get();if(c){c.lastError=e.message;await put(c);}
  console.error('[Forge companion]',m.action,'failed');
  await chrome.action.setBadgeText({text:'!'});return {error:e.message};
 }finally{busy=false;}
}
chrome.runtime.onMessage.addListener((message,sender,reply)=>{
 if(sender.id!==chrome.runtime.id)return false;
 if(sender.tab&&sender.url!==chrome.runtime.getURL('popup.html')){if(!['changed','poll-start'].includes(message.action))return false;message={action:message.action,tabId:sender.tab.id};}
 guarded(message).then(reply).catch(()=>reply({error:'Companion failed. Open extension diagnostics or its service worker console.'}));return true;
});
async function finishAtDeadline(c,status){
 if(!status.deadline_reached||!c.consent)return {ignored:true};
 if(status.transcript_complete){await chrome.alarms.clear('forge-deadline');return {state:'deadline_saved'};}
 if(Date.now()-(c.deadlineLastAttempt||0)<15000)return {ignored:true};c.deadlineLastAttempt=Date.now();await put(c);
 let complete=false,error=null;
 try{const result=await sync(c);complete=result.transcript_complete===true;}catch(e){error=e.message;}
 await local(c,'finish',{allow_incomplete:true,transcript_complete:complete});
 await diagnostic('deadline',complete?'transcript_saved':'awaiting_full_transcript');
 if(error){c=await get();if(c){c.lastError=error;await put(c);}}
 return {state:complete?'deadline_saved':'deadline_partial',sync_error:error};
}
chrome.alarms.onAlarm.addListener(async alarm=>{if(!['forge-sync','forge-deadline'].includes(alarm.name)||busy)return;busy=true;let c;
 try{c=await get();if(!c?.consent)return;const s=await local(c,'status');
  if(s.finished){if(s.can_recover&&!s.transcript_complete&&c.tabId&&c.autoSync)await sync(c);return;}
  if(s.deadline_reached){await finishAtDeadline(c,s);return;}
  if(!c.tabId)return;
  if(c.autoSync)await sync(c);else if(s.pending_nonce)await acknowledge(c);
  c=await get();if(c?.consent&&c.autoUpdates)await deliver(c);
 }catch(e){if(e.code==='CHATGPT_URL_PENDING'){c=await get();if(c){c.waitingConversationURL=true;c.lastError=null;await put(c);}await chrome.action.setBadgeText({text:''});return;}await diagnostic(alarm.name==='forge-deadline'?'deadline':'background-sync','failed');if(c){c.lastError=e.message;await put(c);}await chrome.action.setBadgeText({text:'!'});}finally{busy=false;}
});
