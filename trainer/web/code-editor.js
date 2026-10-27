/* Local Prism-backed editor: native textarea retains editing/accessibility/IME behavior. */
window.ForgeCode = (() => {
 const escape = value => String(value ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
 function language(task) {
  if (!task) return 'text';
  if (['postgres','sqlite'].includes(task.engine)) return 'sql';
  if (task.engine === 'csharp') return 'csharp';
  if (task.engine === 'review' && /^\s*(?:\/\/|public\s|using\s|app\.)/.test(task.starter)) return 'csharp';
  if (task.engine === 'review' && /^\s*(?:--|SELECT\b)/i.test(task.starter)) return 'sql';
  return 'text';
 }
 function highlight(text, lang) {
  const grammar = window.Prism?.languages[lang];
  return grammar ? Prism.highlight(text, grammar, lang) : escape(text);
 }
 function block(text, lang) {
  if (lang === 'text') return '<pre class="pre">' + escape(text) + '</pre>';
  return '<pre class="syntax-block" tabindex="0"><code class="language-' + lang + '">' + highlight(text, lang) + '</code></pre>';
 }
 function editor(task, text, enabled) {
  const lang = language(task);
  if (lang === 'text') return `<textarea id="code" data-task="${escape(task.id)}" class="editor" spellcheck="false" aria-label="Редактор решения" ${enabled?'':'disabled'}>${escape(text)}</textarea>`;
  const tabLabel = window.I18n?.language === 'en' ? 'Tab stops: every 4 columns' : 'Табуляция: шаг 4 колонки';
  return `<div class="code-toolbar"><span>${lang==='csharp'?'C#':'SQL'} · Dark+ · ${tabLabel}</span><span>Tab / Shift+Tab: отступ · Enter: автоотступ · Esc, Tab: выйти</span></div><div class="code-editor" data-language="${lang}"><div class="code-gutter" aria-hidden="true"><pre></pre></div><pre class="code-highlight" aria-hidden="true"><code></code></pre><textarea id="code" data-task="${escape(task.id)}" class="editor" spellcheck="false" autocapitalize="off" autocomplete="off" autocorrect="off" wrap="off" aria-label="Редактор решения ${lang==='csharp'?'C#':'SQL'}" ${enabled?'':'disabled'}>${escape(text)}</textarea></div>`;
 }
 function starterDraft(task, text) {
  // Only migrate an untouched legacy template: never reformat a user's solution.
  if (!['csharp','postgres','sqlite'].includes(task.engine) || !task.starter.includes('\t')) return text;
  const legacy = task.starter.replace(/^\t+/gm, tabs => ' '.repeat(tabs.length));
  return text === legacy ? task.starter : text;
 }
 function bindEditing(textarea) {
  let releaseTab = false;
  function replace(start, end, text, selectionStart, selectionEnd, direction = 'none') {
   const top = textarea.scrollTop, left = textarea.scrollLeft;
   textarea.setSelectionRange(start, end);
   // Native insertion retains the browser's undo stack where supported.
   const before = textarea.value;
   const inserted = document.execCommand('insertText', false, text);
   if (!inserted && textarea.value === before) textarea.setRangeText(text, start, end, 'end');
   textarea.setSelectionRange(selectionStart, selectionEnd, direction);
   textarea.scrollTop = top; textarea.scrollLeft = left;
   textarea.dispatchEvent(new Event('input', {bubbles:true}));
  }
  textarea.addEventListener('keydown', event => {
   if (event.isComposing || textarea.disabled || event.ctrlKey || event.metaKey || event.altKey) return;
   if (event.key === 'Escape') { releaseTab = true; return; }
   if (event.key === 'Tab' && releaseTab) { releaseTab = false; return; }
   releaseTab = false;
   const value = textarea.value, start = textarea.selectionStart, end = textarea.selectionEnd;
   const lineStart = start === 0 ? 0 : value.lastIndexOf('\n', start - 1) + 1;
   if (event.key === 'Tab') {
    event.preventDefault();
    if (start === end && !event.shiftKey) {
     const prefix = value.slice(lineStart, start);
     if (/^[\t ]*$/.test(prefix)) {
      let column = 0;
      for (const character of prefix) column += character === '\t' ? 4 - column % 4 : 1;
      const indent = '\t'.repeat(Math.floor(column / 4) + 1), caret = lineStart + indent.length;
      replace(lineStart, start, indent, caret, caret);
     } else replace(start, end, '\t', start + 1, start + 1);
     return;
    }
    // A selection ending at the next line's column 0 excludes that line.
    const last = end > start && value[end - 1] === '\n' ? end - 1 : end;
    const nextBreak = value.indexOf('\n', last);
    const blockEnd = nextBreak < 0 ? value.length : nextBreak;
    const edits = []; let cursor = lineStart;
    const text = value.slice(lineStart, blockEnd).split('\n').map(line => {
     const remove = event.shiftKey ? (line.startsWith('\t') ? 1 : (line.match(/^ {1,4}/)?.[0].length || 0)) : 0;
     edits.push({at:cursor, remove, add:event.shiftKey ? 0 : 1});
     cursor += line.length + 1;
     return event.shiftKey ? line.slice(remove) : '\t' + line;
    }).join('\n');
    function moved(position) {
     let delta = 0;
     for (const edit of edits) {
      if (position < edit.at) break;
      if (position < edit.at + edit.remove) return edit.at + delta;
      delta += edit.add - edit.remove;
     }
     return position + delta;
    }
    replace(lineStart, blockEnd, text, moved(start), moved(end), textarea.selectionDirection);
   } else if (event.key === 'Enter') {
    event.preventDefault();
    const before = value.slice(lineStart, start), indent = before.match(/^[\t ]*/)[0];
    const text = '\n' + indent;
    replace(start, end, text, start + text.length, start + text.length);
   } else if (event.key === 'Backspace' && start === end && start > lineStart && /^ +$/.test(value.slice(lineStart, start))) {
    event.preventDefault();
    const count = (start - lineStart) % 4 || 4;
    replace(start - count, start, '', start - count, start - count);
   }
  });
 }
 function mount(textarea) {
  if (!textarea) return;
  bindEditing(textarea);
  const host = textarea?.closest('.code-editor');
  if (!host) return;
  const pre = host.querySelector('.code-highlight'), code = pre.querySelector('code'), numbers = host.querySelector('.code-gutter pre');
  function syncScroll() {
   // One offset for both layers, including at elastic-scroll boundaries.
   const top = Math.max(0, Math.min(textarea.scrollTop, textarea.scrollHeight - textarea.clientHeight));
   const left = Math.max(0, Math.min(textarea.scrollLeft, textarea.scrollWidth - textarea.clientWidth));
   code.style.transform = `translate3d(${-left}px, ${-top}px, 0)`;
   numbers.style.transform = `translate3d(0, ${-top}px, 0)`;
  }
  function paint() {
   code.innerHTML = highlight(textarea.value + '\n ', host.dataset.language);
   numbers.textContent = Array.from({length:textarea.value.split('\n').length},(_,i)=>i+1).join('\n');
   host.classList.add('highlighted');
   syncScroll();
  }
  textarea.addEventListener('input', paint);
  textarea.addEventListener('scroll', syncScroll);
  paint();
 }
 return {language, block, editor, mount, starterDraft};
})();
