/* Offline UI translations. Exercise IDs, answers and drafts are language-independent. */
window.I18n = (() => {
 let language = 'ru';
 try { language = localStorage.getItem('forge-language') === 'en' ? 'en' : 'ru'; } catch {}
 const chunk = /[\u0400-\u04ff][\u0400-\u04ffA-Za-z0-9 \t·…:!?.,;()«»≥≤%→/=+–—\-#]*/g;
 let dictionary = {};
 let phrases = null;
 function text(value) {
  const input = String(value ?? '');
  if (language !== 'en') return input;
  if (Object.hasOwn(dictionary, input)) return dictionary[input];
  const exactFragments=input.replace(chunk, part => dictionary[part] ?? part);
  return phrases?exactFragments.replace(phrases, part=>dictionary[part]):exactFragments;
 }
 function localize(root) {
  if (language !== 'en' || !root || ![Node.ELEMENT_NODE,Node.DOCUMENT_NODE].includes(root.nodeType)) return;
  const elements = root.nodeType === Node.ELEMENT_NODE ? [root, ...root.querySelectorAll('*')] : [...root.querySelectorAll('*')];
  for (const element of elements) {
   if (element.closest('script,style,textarea,pre,code,[data-user-content]')) continue;
   for (const name of ['placeholder','aria-label','title','label']) {
    if (element.hasAttribute(name)) {
     const before=element.getAttribute(name), after=text(before);
     if(before!==after)element.setAttribute(name,after);
    }
   }
   for (const node of element.childNodes) if (node.nodeType===Node.TEXT_NODE) {
    const before=node.textContent, after=text(before);
    if(before!==after)node.textContent=after;
   }
  }
 }
 document.documentElement.lang=language;
 const ready=language==='en'?fetch('/locales/en.json').then(r=>{if(!r.ok)throw new Error('English UI translations are unavailable');return r.json();}).then(d=>{
  dictionary=d;
  const keys=Object.keys(d).filter(key=>/[\u0400-\u04ff]/.test(key)).sort((a,b)=>b.length-a.length);
  const escaped=keys.map(key=>key.replace(/[.*+?^${}()|[\]\\]/g,'\\$&'));
  phrases=new RegExp('(?<![\\u0400-\\u04ff])(?:'+escaped.join('|')+')(?![\\u0400-\\u04ff])','g');
  localize(document.body);
 }):Promise.resolve();
 const observer=new MutationObserver(records=>{
  for(const record of records) {
   if(record.type==='characterData')localize(record.target.parentElement);
   else if(record.type==='attributes')localize(record.target);
   else for(const node of record.addedNodes)localize(node.nodeType===Node.TEXT_NODE?node.parentElement:node);
  }
 });
 observer.observe(document.body,{subtree:true,childList:true,characterData:true,attributes:true,attributeFilter:['placeholder','aria-label','title','label']});
 const navigation=(()=>{try{const value=JSON.parse(sessionStorage.getItem('forge-navigation')||'null');sessionStorage.removeItem('forge-navigation');return value;}catch{return null;}})();
 return {language,ready,text,localize,navigation};
})();
