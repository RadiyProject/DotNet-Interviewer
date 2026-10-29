'use strict';
(()=>{
 if(window.__forgeCompanionVersion==='1.5.8')return;
 window.__forgeCompanionVersion='1.5.8';
 window.__forgeCompanionInstalled=true;
 const markerPattern=/^[\s\u200b\ufeff]*FORGE_CONTEXT:\s*([a-f0-9]{32}):\s*([a-f0-9]{32})/;
 function messages(){const root=document;const result=[];const ids=new Set();
  for(const node of root.querySelectorAll('[data-message-author-role],[data-turn="user"],[data-turn="assistant"],[data-testid^="conversation-turn-"],[data-chatgpt-search-unit-key]')){
   if(!node.hasAttribute('data-message-author-role')&&node.querySelector('[data-message-author-role]'))continue;
   const role=node.getAttribute('data-message-author-role')||node.getAttribute('data-turn')||node.getAttribute('data-chatgpt-search-unit-key')?.match(/:(user|assistant)$/)?.[1]||(/^(You said|Вы сказали)\s*:/i.test(node.querySelector('h4,h5,h6')?.textContent?.trim()||'')?'user':/^(ChatGPT said|ChatGPT сказал)\s*:/i.test(node.querySelector('h4,h5,h6')?.textContent?.trim()||'')?'assistant':null);if(!['user','assistant'].includes(role))continue;
   const id=node.getAttribute('data-message-id')||node.getAttribute('data-chatgpt-search-message-ids')||node.closest('[data-message-id]')?.getAttribute('data-message-id')||node.closest('[data-testid^="conversation-turn-"]')?.getAttribute('data-testid');
   // Missing legacy IDs must not prevent the semantic-heading fallback below.
   // Turns without legacy IDs are still readable; snapshot uses anchored offsets.
   if(id&&ids.has(id))continue;if(id)ids.add(id);const clone=node.cloneNode(true);clone.querySelectorAll('button,script,style,nav,[aria-hidden="true"]').forEach(x=>x.remove());
   if(!node.hasAttribute('data-message-author-role'))clone.querySelectorAll('h4,h5,h6').forEach(e=>{if(/^(You said|Вы сказали|ChatGPT said|ChatGPT сказал)\s*:/i.test(e.textContent.trim()))e.remove();});
   clone.querySelectorAll('br').forEach(e=>e.replaceWith(document.createTextNode('\n')));
   clone.querySelectorAll('p,pre,li,h1,h2,h3,h4,blockquote').forEach(e=>e.append(document.createTextNode('\n')));const text=(clone.textContent||'').trim();if(text)result.push({id,role,text});
  }return result;
 }
 function deliveryMarkers(sessionId){
  const root=document,markers=[],detected_session_ids=[];
  // Collapsed user messages can keep their marker in hidden DOM text. Read only
  // explicit user turns, never the composer, assistant quotations or page text.
  const nodes=[...root.querySelectorAll('[data-message-author-role="user"],[data-turn="user"],[data-user-message-bubble="true"],article')];
  for(const node of nodes){
   if(node.matches('article')&&!node.matches('[data-turn="user"]')&&!node.querySelector('[data-message-author-role="user"]')&&!/^(You said|Вы сказали)\s*[:：]$/i.test(node.querySelector('h4,h5,h6')?.textContent?.trim()||''))continue;
   const clone=node.cloneNode(true);clone.querySelectorAll('button,script,style,nav,textarea,[contenteditable="true"],h4,h5,h6').forEach(e=>e.remove());
   const marker=markerPattern.exec(clone.textContent||'');if(!marker)continue;
   detected_session_ids.push(marker[1]);if(marker[1]===sessionId&&!markers.some(m=>m.nonce===marker[2]))markers.push({nonce:marker[2],confirmed:false});
  }
  return {markers,detected_session_ids};
 }
 function visibleTurns(){
   const headings=[...document.querySelectorAll('h1,h2,h3,h4,h5,h6,[role="heading"]')].filter(h=>/^(You said|Вы сказали|ChatGPT said|ChatGPT сказал)\s*[:：]?$/i.test(h.textContent.trim()));
   const native=messages();if(native.length&&(!headings.length||native.length>=headings.length))return native;
   return headings.map((h,i)=>{
    const range=document.createRange();range.setStartAfter(h);
    if(headings[i+1])range.setEndBefore(headings[i+1]);else{const editor=composer();if(editor&&(h.compareDocumentPosition(editor)&Node.DOCUMENT_POSITION_FOLLOWING))range.setEndBefore(editor);else range.setEndAfter(h.parentElement);}
    const fragment=range.cloneContents();fragment.querySelectorAll('button,script,style,nav,[aria-hidden="true"],textarea,[contenteditable="true"]').forEach(e=>e.remove());
    return {role:/^(You said|Вы сказали)\s*[:：]?$/i.test(h.textContent.trim())?'user':'assistant',text:fragment.textContent.trim()};
   });
 }
 function snapshot(sessionId,anchorNonce=null,collected=null){const all=collected?(collected.every(t=>t.id)?collected:[]):messages().filter(m=>m.id);const first=all.findIndex(m=>m.role==='user'&&markerPattern.exec(m.text)?.[1]===sessionId);const detected=deliveryMarkers(sessionId),markers=[...detected.markers];
  if(first<0){
   // The new chat UI has semantic turn headings but no legacy data-message attributes.
   // Delivery needs the exact nonce in a user turn, not a transcript message ID.
   const turns=collected||visibleTurns();
   const detected_session_ids=[...detected.detected_session_ids];
   for(let i=0;i<turns.length;i++){
    if(turns[i].role!=='user')continue;
    const marker=markerPattern.exec(turns[i].text);if(!marker)continue;
    detected_session_ids.push(marker[1]);if(marker[1]!==sessionId)continue;
    markers.push({nonce:marker[2],confirmed:turns.slice(i+1).some(t=>t.role==='assistant'&&t.text.includes('FORGE_READY'))});
   }
   const anchor=turns.findIndex(t=>{const m=t.role==='user'&&markerPattern.exec(t.text);return m&&m[1]===sessionId&&(!anchorNonce||m[2]===anchorNonce);});
   if(anchor<0)return {url:location.origin+location.pathname,messages:[],markers,detected_session_ids,transcript_error:anchorNonce||markers.length?'Context delivery is visible, but transcript turns could not be read. Expand the context message and load the conversation history, then sync again.':null};
   const base=markerPattern.exec(turns[anchor].text)[2],transcript=[];
   // IDs are offsets from a fixed context anchor, never from the current viewport.
   // Keep the anchor across syncs; refuse a shifted/truncated window rather than duplicate turns.
   for(let i=anchor+1;i<turns.length;i++){
    const t=turns[i],marker=t.role==='user'&&markerPattern.exec(t.text);
    if(marker){if(marker[1]!==sessionId)throw Error('Another Forge interview is present in this conversation. Use a separate ChatGPT chat.');continue;}
    if(!t.text||t.role==='assistant'&&t.text.length<500&&t.text.includes('FORGE_READY'))continue;
    transcript.push({id:`visible:${base}:${i-anchor}:${t.role}`,role:t.role,text:t.text});
   }
   return {url:location.origin+location.pathname,messages:transcript,markers,detected_session_ids,transcript_anchor:base};
  }
  const selected=all.slice(first);const transcript=[];
  selected.forEach((m,i)=>{const marker=markerPattern.exec(m.text);if(marker){if(marker[1]!==sessionId)throw Error('Another Forge interview is present in this conversation. Use a separate ChatGPT chat.');markers.push({nonce:marker[2],confirmed:selected.slice(i+1).some(n=>n.role==='assistant'&&n.text.includes('FORGE_READY'))});return;}if(m.text.startsWith('FORGE_READY'))return;transcript.push(m);});
  return {url:location.origin+location.pathname,messages:transcript,markers};
 }
 function mergeTranscriptTurns(previous,current){
  if(!previous.length)return current.slice();
  const equal=(a,b)=>a.role===b.role&&(a.id&&b.id?a.id===b.id:a.text===b.text);
  if(current.length){for(let i=0;i<=previous.length-current.length;i++){if(current.every((turn,j)=>equal(previous[i+j],turn))&&(current.length>1||previous.filter(turn=>equal(turn,current[0])).length===1)){const merged=previous.slice();merged.splice(i,current.length,...current);return merged;}}}
  for(let overlap=Math.min(previous.length,current.length);overlap>0;overlap--){
   if(!current.slice(0,overlap).every((turn,i)=>equal(previous[previous.length-overlap+i],turn)))continue;
   // A single repeated short answer cannot establish a position in history.
   if(overlap===1&&previous.filter(turn=>equal(turn,current[0])).length!==1)continue;
   return previous.slice(0,previous.length-overlap).concat(current);
  }
  throw Error('Transcript history has a gap. Load the conversation history and retry; no partial transcript was marked complete.');
 }
 const transcriptHistory=new Map();
 function readSnapshot(sessionId,anchorNonce){
  const key=sessionId+'|'+location.origin+location.pathname,visible=visibleTurns();
  const previous=transcriptHistory.get(key);
  const hasContext=visible.some(t=>{const m=t.role==='user'&&markerPattern.exec(t.text);return m&&m[1]===sessionId;});
  if(previous||hasContext){
   try{
    const merged=previous&&visible.length?mergeTranscriptTurns(previous,visible):previous||visible;
    const result=snapshot(sessionId,anchorNonce,merged);
    if(!result.transcript_error)transcriptHistory.set(key,merged);
    return result;
   }catch(e){return {url:location.origin+location.pathname,messages:[],markers:[],transcript_error:e.message};}
  }
  return snapshot(sessionId,anchorNonce);
 }
 function historyLoading(){
  return [...document.querySelectorAll('main [role="status"],main [aria-busy="true"],main span,main p,main div')].some(e=>e.getClientRects().length>0&&(e.getAttribute('aria-busy')==='true'||(!e.children.length&&/^(Загрузка более ранних сообщений|Loading (earlier|older) messages)[….\s]*$/i.test(e.textContent.trim()))));
 }
 async function collectTranscript(sessionId,anchorNonce){
  const activity=transcriptActivity();
  if(!activity.voice_idle||!activity.transcript_stable)throw Error('End Voice and wait for the visible transcript to settle before saving the full history.');
  const heading=document.querySelector('main [data-message-author-role],main [data-turn],main [data-testid^="conversation-turn-"],main article,main h4,main h5,main h6')||document.querySelector('[data-message-author-role],article,h4,h5,h6');
  let scroller=heading;
  while(scroller&&!(scroller.scrollHeight>scroller.clientHeight+40&&/auto|scroll/.test(getComputedStyle(scroller).overflowY)))scroller=scroller.parentElement;
  scroller=scroller||document.scrollingElement;
  if(!scroller)throw Error('ChatGPT history scroller was not found. Scroll to the original Forge context and retry.');
  const original=scroller.scrollTop,wasBottom=original+scroller.clientHeight>=scroller.scrollHeight-8;
  let turns=[],complete=false;
  try{
   // Manual save/finish only: load the original context, never send another one.
   for(let i=0;i<40;i++){
    // Lazy history loading can take several seconds per page. Re-enter the
    // top threshold after each prepend instead of treating scrollTop=0 as proof.
    scroller.scrollTop=Math.min(80,Math.max(0,scroller.scrollHeight-scroller.clientHeight));
    scroller.dispatchEvent(new Event('scroll',{bubbles:true}));
    scroller.scrollTop=0;scroller.dispatchEvent(new Event('scroll',{bubbles:true}));
    await sleep(750);
    const visible=visibleTurns();
    if(visible.some(t=>{const m=t.role==='user'&&markerPattern.exec(t.text);return m&&m[1]===sessionId&&(!anchorNonce||m[2]===anchorNonce);})){turns=visible;break;}
   }
   if(!turns.length){const found=visibleTurns();const contexts=found.filter(t=>t.role==='user'&&markerPattern.exec(t.text));throw Error('The original Forge context could not be loaded. Visible turns: '+found.length+'; context markers: '+contexts.length+'; scroll position: '+Math.round(scroller.scrollTop)+'/'+Math.round(scroller.scrollHeight-scroller.clientHeight)+'. Scroll to the beginning of this ChatGPT conversation and retry saving; do not resend context.');}
   let bottomPasses=0;
   for(let i=0;i<80;i++){
    const visible=visibleTurns();
    if(visible.length)turns=mergeTranscriptTurns(turns,visible);
    const atBottom=scroller.scrollTop+scroller.clientHeight>=scroller.scrollHeight-8;
    if(atBottom&&!historyLoading()){if(++bottomPasses>=3){complete=true;break;}}
    else bottomPasses=0;
    scroller.scrollTop=Math.min(scroller.scrollHeight,scroller.scrollTop+Math.max(150,scroller.clientHeight*0.65));
    await sleep(historyLoading()?750:250);
   }
   if(!complete)throw Error('Full history loading did not finish. Retry saving; the transcript remains incomplete.');
   const result=snapshot(sessionId,anchorNonce,turns);
   if(result.transcript_error)throw Error(result.transcript_error);
   transcriptHistory.set(sessionId+'|'+location.origin+location.pathname,turns);
   return {...result,...transcriptActivity(),history_loaded:true};
  }finally{if(complete)scroller.scrollTop=wasBottom?scroller.scrollHeight:original;}
 }
 let lastTranscriptMutation=Date.now(),lastCandidateInput=0;
 document.addEventListener('input',event=>{if(event.target.matches?.('textarea,[contenteditable="true"],[role="textbox"]'))lastCandidateInput=Date.now();},true);
 function transcriptActivity(){const buttons=[...document.querySelectorAll('button')].filter(b=>b.getClientRects().length>0);const idle=buttons.some(b=>/^(Start voice mode|Start voice chat|Start voice conversation|Use voice mode|Start Voice|Начать голосовой чат|Начать голосовой разговор|Запустить голосовой режим|Использовать голосовой режим)$/i.test(b.getAttribute('aria-label')||''));const speaking=buttons.some(b=>/^(End voice|End voice chat|End voice conversation|Exit voice|Close voice|Завершить голосовой чат|Завершить голосовой разговор|Выйти из голосового режима)$/i.test(b.getAttribute('aria-label')||''));const stable=Date.now()-lastTranscriptMutation>5000&&!document.querySelector('button[data-testid="stop-button"],button[data-testid="stop-thinking-button"]');const editor=composer(),last=visibleTurns().filter(t=>t.text).at(-1);return {voice_idle:idle&&!speaking&&!!editor,transcript_stable:stable,last_visible_role:last?.role||null,last_turn_key:last?last.role+':'+last.text:null,coaching_ready:stable&&last?.role==='assistant'&&Date.now()-lastCandidateInput>5000&&!!editor&&!editorText(editor)};}
 const sleep=ms=>new Promise(r=>setTimeout(r,ms));
 function composer(){
  const visible=e=>e.getClientRects().length>0&&getComputedStyle(e).visibility!=='hidden'&&!e.disabled&&!e.readOnly;
  const editable=e=>e instanceof HTMLTextAreaElement||e.isContentEditable;
  const known=[...document.querySelectorAll('#prompt-textarea,#prompt-textarea [contenteditable="true"],[data-testid="composer-input"],[data-testid="composer-input"] [contenteditable="true"]')].filter(e=>editable(e)&&visible(e));
  if(known.length===1)return known[0];
  // ChatGPT also exposes the composer without its former ID (Lexical/ProseMirror).
  const candidates=[...document.querySelectorAll('main [contenteditable="true"],main textarea,[role="textbox"][contenteditable="true"],textarea[placeholder="Спросить ChatGPT"],textarea[placeholder="Ask anything"]')].filter(e=>editable(e)&&visible(e));
  return candidates.length===1?candidates[0]:null;
 }
 function sameText(a,b){return a.replace(/\s+/g,' ').trim()===b.replace(/\s+/g,' ').trim();}
 function sendButton(){return document.querySelector('button[data-testid="send-button"],button[data-testid="composer-submit-button"]')||[...document.querySelectorAll('button')].find(b=>/^(Send|Send prompt|Send message|Отправить|Отправить запрос|Отправить сообщение)$/i.test(b.getAttribute('aria-label')||''));}
 function editorText(e){return ('value' in e?e.value:(e.innerText||e.textContent||'')).replace(/\r\n/g,'\n').trim();}
 function stableConversationURL(value){try{const u=new URL(value);return u.origin==='https://chatgpt.com'&&/^\/(?:g\/[A-Za-z0-9_-]+\/)?c\/[A-Za-z0-9_-]+\/?$/.test(u.pathname);}catch{return false;}}
 async function deliver(m){let mayHaveSent=false;try{
  if(typeof m.prompt!=='string'||!m.prompt.startsWith(`FORGE_CONTEXT:${m.sessionId}:${m.nonce}\n`))return {error:'Only a marked Forge context or start notification can be sent. Candidate answers must be entered by the human.',code:'INVALID_CONTEXT_MESSAGE',mayHaveSent:false};
  if(m.safeCoaching){const activity=transcriptActivity();if(!activity.coaching_ready||m.coachingAnchor!==activity.last_turn_key)return {state:'waiting_for_safe_coaching_delivery'};}
  const before=snapshot(m.sessionId);if(before.markers.some(x=>x.nonce===m.nonce))return stableConversationURL(before.url)?{state:'visible_in_chat',url:before.url}:{state:'sent_unconfirmed'};
  const e=composer();if(!e)return {state:'composer_unavailable'};if(editorText(e))return {state:'draft_busy'};
  e.focus();if(e instanceof HTMLTextAreaElement){Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype,'value').set.call(e,m.prompt);e.dispatchEvent(new Event('input',{bubbles:true}));}
  else{const inserted=document.execCommand('insertText',false,m.prompt);if(!inserted||!sameText(editorText(e),m.prompt)){e.textContent=m.prompt;e.dispatchEvent(new InputEvent('input',{bubbles:true,inputType:'insertText',data:m.prompt}));}}
  if(!sameText(editorText(e),m.prompt))return {error:'The ChatGPT composer did not accept the exact prompt. Inspect the prepared draft and send it manually.',mayHaveSent:false};
  let send;for(let i=0;i<30;i++){send=sendButton();if(send&&!send.disabled)break;await sleep(200);}
  if(!send||send.disabled)return {error:'Context is prepared in ChatGPT. Click its Send button manually, then Check delivery in the companion. Do not send the context again.',code:'CHATGPT_SEND_BUTTON_UNAVAILABLE',mayHaveSent:true};
  if(m.safeCoaching&&(!transcriptActivity().coaching_ready||m.coachingAnchor!==transcriptActivity().last_turn_key))return {error:'The conversation changed. Director advice remains a draft; wait for the next stable assistant turn.',code:'COACHING_VOICE_ACTIVE',mayHaveSent:true};
  mayHaveSent=true;send.click();
  for(let i=0;i<60;i++){await sleep(200);const s=snapshot(m.sessionId);const mark=s.markers.find(x=>x.nonce===m.nonce);if(mark&&stableConversationURL(s.url))return {state:'visible_in_chat',url:s.url,confirmed:mark.confirmed};}
  return {state:'sent_unconfirmed'};
 }catch(e){return {error:e.message,mayHaveSent};}}
 chrome.runtime.onMessage.addListener((m,sender,reply)=>{
  if(sender.id!==chrome.runtime.id||!m.forge)return false;
  if(m.action==='ping'){reply({ok:true});return false;}
  if(m.action==='check-composer'){reply({state:composer()?'tab_ready':'composer_unavailable'});return false;}
  if(m.action==='read'&&m.loadHistory){collectTranscript(m.sessionId,m.anchorNonce).then(reply).catch(e=>reply({error:e.message}));return true;}
  if(m.action==='read'){try{reply({...readSnapshot(m.sessionId,m.anchorNonce),...transcriptActivity()});}catch(e){reply({error:e.message});}return false;}
  if(m.action==='deliver'){deliver(m).then(reply);return true;}
  if(m.action==='voice'){
   const buttons=[...document.querySelectorAll('button')].filter(b=>!b.disabled&&/^(Start voice mode|Start voice chat|Start voice conversation|Use voice mode|Start Voice|Начать голосовой чат|Начать голосовой разговор|Запустить голосовой режим|Использовать голосовой режим)$/i.test(b.getAttribute('aria-label')||''));
   if(buttons.length===1){buttons[0].click();reply({state:'voice_button_clicked'});}else reply({error:'Click the native ChatGPT Voice button in the prompt bar. The companion could not identify it unambiguously.'});return false;
  }return false;
 });
 let timer;const observer=new MutationObserver(()=>{lastTranscriptMutation=Date.now();clearTimeout(timer);timer=setTimeout(()=>{try{chrome.runtime.sendMessage({action:'changed'}).catch(()=>{});}catch{observer.disconnect();}},2500);});
 observer.observe(document.body,{subtree:true,childList:true,characterData:true});
 // A site-side Start click must reach ChatGPT even when the popup is closed.
 const startPoll=setInterval(()=>{try{chrome.runtime.sendMessage({action:'poll-start'}).catch(()=>{});}catch{clearInterval(startPoll);}},3000);
})();
