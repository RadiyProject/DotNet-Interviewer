'use strict';
(()=>{
 if(window.__forgeCompanionInstalled)return;
 window.__forgeCompanionInstalled=true;
 const markerPattern=/^[\s\u200b\ufeff]*FORGE_CONTEXT:\s*([a-f0-9]{32}):\s*([a-f0-9]{32})/;
 function messages(){const root=document;const result=[];const ids=new Set();
  for(const node of root.querySelectorAll('[data-message-author-role],[data-testid^="conversation-turn-"],[data-chatgpt-search-unit-key]')){
   if(!node.hasAttribute('data-message-author-role')&&node.querySelector('[data-message-author-role]'))continue;
   const role=node.getAttribute('data-message-author-role')||node.getAttribute('data-turn')||node.getAttribute('data-chatgpt-search-unit-key')?.match(/:(user|assistant)$/)?.[1]||(/^(You said|Вы сказали)\s*:/i.test(node.querySelector('h4,h5,h6')?.textContent?.trim()||'')?'user':/^(ChatGPT said|ChatGPT сказал)\s*:/i.test(node.querySelector('h4,h5,h6')?.textContent?.trim()||'')?'assistant':null);if(!['user','assistant'].includes(role))continue;
   const id=node.getAttribute('data-message-id')||node.getAttribute('data-chatgpt-search-message-ids')||node.closest('[data-message-id]')?.getAttribute('data-message-id')||node.closest('[data-testid^="conversation-turn-"]')?.getAttribute('data-testid');
   // Missing legacy IDs must not prevent the semantic-heading fallback below.
   if(!id)continue;
   if(ids.has(id))continue;ids.add(id);const clone=node.cloneNode(true);clone.querySelectorAll('button,script,style,nav,[aria-hidden="true"]').forEach(x=>x.remove());
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
 function snapshot(sessionId,anchorNonce=null){const all=messages();const first=all.findIndex(m=>m.role==='user'&&markerPattern.exec(m.text)?.[1]===sessionId);const detected=deliveryMarkers(sessionId),markers=[...detected.markers];
  if(first<0){
   // The new chat UI has semantic turn headings but no legacy data-message attributes.
   // Delivery needs the exact nonce in a user turn, not a transcript message ID.
   const headings=[...document.querySelectorAll('h4,h5,h6')].filter(h=>/^(You said|Вы сказали|ChatGPT said|ChatGPT сказал):$/.test(h.textContent.trim()));
   const turns=headings.map((h,i)=>{
    const range=document.createRange();range.setStartAfter(h);
    if(headings[i+1])range.setEndBefore(headings[i+1]);else{const editor=composer();if(editor&&(h.compareDocumentPosition(editor)&Node.DOCUMENT_POSITION_FOLLOWING))range.setEndBefore(editor);else range.setEndAfter(h.parentElement);}
    const fragment=range.cloneContents();fragment.querySelectorAll('button,script,style,nav,[aria-hidden="true"],textarea,[contenteditable="true"]').forEach(e=>e.remove());
    return {role:/^(You said|Вы сказали):$/.test(h.textContent.trim())?'user':'assistant',text:fragment.textContent.trim()};
   });
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
 async function deliver(m){let mayHaveSent=false;try{
  if(typeof m.prompt!=='string'||!m.prompt.startsWith(`FORGE_CONTEXT:${m.sessionId}:${m.nonce}\n`))return {error:'Only a marked Forge context or start notification can be sent. Candidate answers must be entered by the human.',code:'INVALID_CONTEXT_MESSAGE',mayHaveSent:false};
  const before=snapshot(m.sessionId);if(before.markers.some(x=>x.nonce===m.nonce))return {state:'visible_in_chat',url:before.url};
  const e=composer();if(!e)return {state:'composer_unavailable'};if(editorText(e))return {state:'draft_busy'};
  e.focus();if(e instanceof HTMLTextAreaElement){Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype,'value').set.call(e,m.prompt);e.dispatchEvent(new Event('input',{bubbles:true}));}
  else{const inserted=document.execCommand('insertText',false,m.prompt);if(!inserted||!sameText(editorText(e),m.prompt)){e.textContent=m.prompt;e.dispatchEvent(new InputEvent('input',{bubbles:true,inputType:'insertText',data:m.prompt}));}}
  if(!sameText(editorText(e),m.prompt))return {error:'The ChatGPT composer did not accept the exact prompt. Inspect the prepared draft and send it manually.',mayHaveSent:false};
  let send;for(let i=0;i<30;i++){send=sendButton();if(send&&!send.disabled)break;await sleep(200);}
  if(!send||send.disabled)return {error:'Context is prepared in ChatGPT. Click its Send button manually, then Check delivery in the companion. Do not send the context again.',code:'CHATGPT_SEND_BUTTON_UNAVAILABLE',mayHaveSent:true};
  mayHaveSent=true;send.click();
  for(let i=0;i<60;i++){await sleep(200);const s=snapshot(m.sessionId);const mark=s.markers.find(x=>x.nonce===m.nonce);if(mark&&location.pathname.includes('/c/'))return {state:'visible_in_chat',url:s.url,confirmed:mark.confirmed};}
  return {state:'sent_unconfirmed'};
 }catch(e){return {error:e.message,mayHaveSent};}}
 chrome.runtime.onMessage.addListener((m,sender,reply)=>{
  if(sender.id!==chrome.runtime.id||!m.forge)return false;
  if(m.action==='ping'){reply({ok:true});return false;}
  if(m.action==='check-composer'){reply({state:composer()?'tab_ready':'composer_unavailable'});return false;}
  if(m.action==='read'){try{reply(snapshot(m.sessionId,m.anchorNonce));}catch(e){reply({error:e.message});}return false;}
  if(m.action==='deliver'){deliver(m).then(reply);return true;}
  if(m.action==='voice'){
   const buttons=[...document.querySelectorAll('button')].filter(b=>!b.disabled&&/^(Start voice mode|Start voice chat|Start voice conversation|Use voice mode|Start Voice|Начать голосовой чат|Начать голосовой разговор|Запустить голосовой режим|Использовать голосовой режим)$/i.test(b.getAttribute('aria-label')||''));
   if(buttons.length===1){buttons[0].click();reply({state:'voice_button_clicked'});}else reply({error:'Click the native ChatGPT Voice button in the prompt bar. The companion could not identify it unambiguously.'});return false;
  }return false;
 });
 let timer;const observer=new MutationObserver(()=>{clearTimeout(timer);timer=setTimeout(()=>{try{chrome.runtime.sendMessage({action:'changed'}).catch(()=>{});}catch{observer.disconnect();}},2500);});
 observer.observe(document.body,{subtree:true,childList:true,characterData:true});
 // A site-side Start click must reach ChatGPT even when the popup is closed.
 const startPoll=setInterval(()=>{try{chrome.runtime.sendMessage({action:'poll-start'}).catch(()=>{});}catch{clearInterval(startPoll);}},3000);
})();
