'use strict';
window.ForgePDF=(()=>{
 const tr=(ru,en)=>I18n.language==='en'?en:ru;
 const MAX_BYTES=2000000,MAX_PAGES=30,MAX_TEXT=50000;
 let library;
 async function readPageItems(page){
  // PDF.js getTextContent() uses ReadableStream[Symbol.asyncIterator],
  // which is absent in some Safari versions, including with the legacy build.
  const reader=page.streamTextContent().getReader();
  const items=[];let complete=false,characters=0;
  try{
   while(true){
    const {done,value}=await reader.read();
    if(done){complete=true;return items;}
    for(const item of value.items){
     if(typeof item.str==='string')characters+=item.str.length;
     if(characters>MAX_TEXT)throw Error(tr('В PDF больше 50 000 символов. Загрузи только резюме.','PDF text exceeds 50,000 characters. Upload only the resume.'));
     items.push(item);
    }
   }
  }finally{
   if(!complete)reader.cancel().catch(()=>{});
   reader.releaseLock();
  }
 }
 function pageText(items){
  let text='',last=null;
  for(const item of items){
   if(typeof item.str!=='string')continue;
   // Preserve PDF stream order; never guess a multi-column reading order.
   if(last&&item.str){
    const dy=Math.abs(item.transform[5]-last.transform[5]);
    const height=Math.max(1,Math.abs(item.transform[3]),Math.abs(last.transform[3]));
    if(dy>height*.55&&!text.endsWith('\n'))text+='\n';
    else if(text&&!/\s$/.test(text)&&!/^\s/.test(item.str)){const end=last.transform[4]+(last.dir==='rtl'?-last.width:last.width);if(Math.abs(item.transform[4]-end)>height*.12)text+=' ';}
   }
   text+=item.str;
   if(item.hasEOL&&!text.endsWith('\n'))text+='\n';
   last=item;
   if(text.length>MAX_TEXT)throw Error(tr('В PDF слишком много текста: предел — 50 000 символов. Загрузи только резюме.','PDF text exceeds 50,000 characters. Upload only the resume.'));
  }
  return text.replace(/[ \t]+\n/g,'\n').replace(/\n{3,}/g,'\n\n').trim();
 }
 async function extract(file,onProgress=()=>{}){
  if(file.size>MAX_BYTES)throw Error(tr('Максимальный размер резюме — 2 МБ.','Maximum resume size is 2 MB.'));
  let task=null,stopped=null,timer,abort,destroyed=false,passwordAttempts=0;
  const destroy=()=>{if(task&&!destroyed){destroyed=true;task.destroy().catch(()=>{});}};
  const timeout=new Promise((_,reject)=>{abort=error=>{stopped=error;destroy();reject(error);};timer=setTimeout(()=>abort(Error(tr('Обработка PDF заняла больше 30 секунд. Попробуй сохранить документ как текстовый PDF или TXT.','PDF extraction exceeded 30 seconds. Try saving it as a text-based PDF or TXT.'))),30000);});
  const work=async()=>{
   onProgress(tr('Загрузка локального PDF-модуля…','Loading the local PDF reader…'));
   library??=import('/vendor/pdfjs/pdf.min.mjs').catch(()=>{library=null;throw Error(tr('Не удалось загрузить локальный PDF-модуль. Обнови страницу тренажёра.','Could not load the local PDF reader. Refresh the trainer page.'));});
   const pdfjs=await library;if(stopped)throw stopped;
   pdfjs.GlobalWorkerOptions.workerSrc='/vendor/pdfjs/pdf.worker.min.mjs';
   const bytes=new Uint8Array(await file.arrayBuffer());if(stopped)throw stopped;
   if(!String.fromCharCode(...bytes.subarray(0,1024)).includes('%PDF-'))throw Error(tr('Файл не содержит корректного заголовка PDF.','The file does not have a valid PDF header.'));
   task=pdfjs.getDocument({data:bytes,disableFontFace:true,useWorkerFetch:false,useWasm:false,stopAtErrors:true});
   task.onPassword=(update,reason)=>{
    if(stopped)return;
    if(passwordAttempts++>=3){abort(Error(tr('Не удалось открыть PDF: неверный пароль.','Could not open PDF: incorrect password.')));return;}
    const password=window.prompt(reason===pdfjs.PasswordResponses.INCORRECT_PASSWORD?tr('Неверный пароль. Введи пароль PDF ещё раз:','Incorrect password. Enter the PDF password again:'):tr('PDF защищён паролем. Введи пароль для извлечения текста:','This PDF is password protected. Enter its password to extract text:'));
    if(password===null){abort(Error(tr('Загрузка PDF отменена.','PDF import cancelled.')));return;}
    update(password);
   };
   const doc=await task.promise;if(stopped)throw stopped;
   if(doc.numPages>MAX_PAGES)throw Error(tr('В резюме больше 30 страниц. Загрузи сокращённый PDF.','The resume exceeds 30 pages. Upload a shorter PDF.'));
   const pages=[];let characters=0;
   for(let i=1;i<=doc.numPages;i++){
    if(stopped)throw stopped;onProgress(tr('Извлечение текста: страница ','Extracting text: page ')+i+' / '+doc.numPages);
    const page=await doc.getPage(i);
    try{const text=pageText(await readPageItems(page));characters+=text.length+(i>1?2:0);if(characters>MAX_TEXT)throw Error(tr('В PDF больше 50 000 символов. Загрузи только резюме.','PDF text exceeds 50,000 characters. Upload only the resume.'));pages.push(text);}finally{page.cleanup();}
   }
   const text=pages.join('\n\n').trim();
   if(!text)throw Error(tr('В PDF нет текстового слоя. Похоже, это скан: сначала распознай его через OCR или вставь текст резюме вручную.','This PDF has no text layer and may be scanned. Run OCR first or paste the resume text manually.'));
   return {text,pages:doc.numPages,emptyPages:pages.filter(p=>!p).length};
  };
  try{return await Promise.race([work(),timeout]);}
  catch(error){if(stopped)throw stopped;if(error instanceof TypeError){console.error('PDF text extraction compatibility error:',error);throw Error(tr('Не удалось обработать PDF из-за ошибки совместимости браузера. Текст резюме не изменён. Подробности доступны в консоли браузера.','PDF processing failed due to a browser compatibility error. The resume text was not changed. Details are available in the browser console.'));}if(['InvalidPDFException','UnknownErrorException','MissingPDFException','FormatError'].includes(error.name))throw Error(tr('Не удалось прочитать PDF. Файл повреждён или имеет неподдерживаемое шифрование. Попробуй пересохранить его как PDF с текстом.','Could not read the PDF. It may be damaged or use unsupported encryption. Try re-saving it as a text-based PDF.'));throw error;}
  finally{clearTimeout(timer);destroy();}
 }
 return {extract};
})();
