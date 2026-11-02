/* Topic-based theory navigation; attempts and deadlines remain server-owned. */
let theoryTopic = null;
let theoryTag=I18n.navigation?.theoryTag||'all';
function topicOf(t) {
 const groups = {
  'C# / OOP':'C# и платформа .NET','C# / Runtime':'C# и платформа .NET','C#':'C# и платформа .NET','.NET Tooling':'C# и платформа .NET',
  'C# / Resources':'Память и ресурсы','Memory / GC':'Память и ресурсы',
  'Concurrency':'Async и многопоточность','Concurrency / Dataflow':'Async и многопоточность','Async / TAP':'Async и многопоточность',
  'C# / HTTP':'HTTP и ASP.NET Core','ASP.NET Core':'HTTP и ASP.NET Core','DI / Configuration':'HTTP и ASP.NET Core',
  'EF Core':'EF Core и доступ к данным','EF Core / PostgreSQL':'EF Core и доступ к данным',
  'Messaging / Cloud':'Очереди и брокеры','Messaging / RabbitMQ':'Очереди и брокеры','Messaging / Kafka':'Очереди и брокеры',
  'Operations':'Эксплуатация и облако','Production':'Эксплуатация и облако','Azure':'Эксплуатация и облако',
  'Docker':'Docker и Kubernetes','Kubernetes':'Docker и Kubernetes',
 };
 return groups[t.cat] || t.cat;
}
function topicQuestions(topic) { return tasks.filter(t=>t.engine==='quiz' && topicOf(t)===topic && (theoryTag==='all'||(t.tags||[]).includes(theoryTag))); }
function openTheory(topic) {
 const questions=topicQuestions(topic); if(!questions.length)return;
 theoryTopic=topic;
 openTask((questions.find(t=>history(t.id)[0]?.result.status!=='passed')||questions[0]).id);
}
function theoryPage() {
 const quizzes=tasks.filter(t=>t.engine==='quiz'&&(theoryTag==='all'||(t.tags||[]).includes(theoryTag)));
 if(!quizzes.length){active=null;$('main').innerHTML=tagFilterHTML('theoryTag',theoryTag)+'<div class="panel">Нет вопросов с выбранным тегом.</div>';bindTheoryTag();return;}
 const chosen=quizzes.find(t=>t.id===selected);
 theoryTopic=chosen?topicOf(chosen):(quizzes.some(t=>topicOf(t)===theoryTopic)?theoryTopic:topicOf(quizzes[0]));
 const questions=topicQuestions(theoryTopic), t=chosen||questions[0];
 selected=t.id;active=progress.attempts.find(a=>a.task_id===t.id&&!a.finished)||null;
 const topics=[...new Set(quizzes.map(topicOf))];
 $('main').innerHTML=`<div class="theory-heading"><div class="eyebrow">ТЕОРЕТИЧЕСКИЕ ТЕСТЫ</div><h1>Проверка по темам</h1><p class="small">Выбери тему и проходи вопросы по порядку. У каждого вопроса свой таймер и сохраняемый результат; переход не останавливает начатую попытку.</p></div>${tagFilterHTML('theoryTag',theoryTag)}<div class="theory-topics" aria-label="Темы тестов">${topics.map(topic=>{const qs=topicQuestions(topic),passed=qs.filter(q=>history(q.id)[0]?.result.status==='passed').length;return `<button class="secondary ${topic===theoryTopic?'active':''}" data-topic="${esc(topic)}" ${topic===theoryTopic?'aria-current="true"':''}>${esc(topic)} <span class="small">${passed}/${qs.length}</span></button>`;}).join('')}</div><div class="theory-workspace"><aside class="panel theory-outline" id="tasklist" aria-label="Вопросы текущей темы"></aside><article class="taskdetail" id="detail"></article></div>`;
 theoryOutline(t);detail(t);bindTheoryTag();
}
function bindTheoryTag(){$('theoryTag').onchange=e=>{captureDraft();theoryTag=e.target.value;selected=null;theoryPage();I18n.localize(document.body);};}
function theoryOutline(t) {
 const questions=topicQuestions(topicOf(t));
 const passed=questions.filter(q=>history(q.id)[0]?.result.status==='passed').length;
 $('tasklist').innerHTML=`<h2>${esc(topicOf(t))}</h2><p class="small">Пройдено ${passed} из ${questions.length}</p><div class="track"><span style="width:${passed/questions.length*100}%"></span></div><div class="question-outline">${questions.map((q,i)=>{const h=history(q.id)[0];return `<button class="task ${q.id===t.id?'selected':''}" data-open="${q.id}" ${q.id===t.id?'aria-current="step"':''}><span class="small">${i+1}. ${q.id} · ${q.minutes} мин</span><strong>${esc(q.title)}</strong><span class="small ${h?.result.status==='passed'?'green':'muted'}">${h?.result.status==='passed'?'✓ Пройдено':h?'Есть попытка':'Не отвечено'}</span></button>`;}).join('')}</div>`;
}
function quizNavigation(t) {
 const qs=topicQuestions(topicOf(t)), index=qs.findIndex(q=>q.id===t.id);
 return `<div class="quiz-navigation"><button class="secondary" ${index?'data-open="'+qs[index-1].id+'"':'disabled'}>← Назад</button><span class="small">Вопрос ${index+1} из ${qs.length}</span><button class="secondary" ${index<qs.length-1?'data-open="'+qs[index+1].id+'"':'disabled'}>Далее →</button></div>`;
}
function quizDetailHTML(t,draft,latest,remaining) {
 const qs=topicQuestions(topicOf(t));
 const done=qs.filter(q=>history(q.id)[0]?.result.status==='passed').length;
 return `<section class="panel quiz-card">${quizNavigation(t)}<div class="row"><span class="pill">${esc(topicOf(t))}</span><span class="pill">${t.id}</span><span class="pill ${level(t.id).color}">${level(t.id).text}</span></div><h1>${esc(t.title)}</h1>${taskTagsHTML(t)}${t.desc!==t.title?`<p>${esc(t.desc)}</p>`:''}<div class="quiz-timer row between"><div><span class="small">${active?'До конца попытки':'Лимит ответа'}</span><div id="clock" class="clock">${formatTime(remaining)}</div></div><button id="start" class="primary" ${active?'disabled':''}>${active?'Таймер запущен':'Начать ответ →'}</button></div>${quizChoices(t,draft)}<div class="row between"><span class="small" id="saved">Ответ сохраняется автоматически</span><button class="primary" id="submit" ${active&&!busy?'':'disabled'}>${busy?'Проверка…':'Ответить'}</button></div><div id="result">${latest?resultHTML(latest.result):''}</div><div id="revealed"></div><details class="quiz-help"><summary>Правила, подсказки и источники</summary><ul>${t.requirements.map(x=>`<li>${esc(x)}</li>`).join('')}</ul><a href="${esc(t.source[1])}" target="_blank" rel="noopener noreferrer">${esc(t.source[0])} ↗</a>${taskResources(t)}<p><button id="reveal" class="secondary" ${active?'':'disabled'}>Открыть разбор / подсказки</button></p></details>${quizNavigation(t)}</section>${done===qs.length?'<div class="panel"><h2>Тема пройдена</h2><p>Все вопросы темы имеют успешный сохранённый ответ. Можно выбрать следующую тему или повторить вопросы.</p></div>':''}${relatedHTML(t)}${history(t.id).length?'<details class="panel"><summary>История ответов на этот вопрос</summary>'+historyTable(history(t.id).slice(0,12))+'</details>':''}`;
}

let testingMode='practice',testingQuery='';
function testingPage(){
 const tr=(ru,en)=>I18n.language==='en'?en:ru;
 const items=tasks.filter(t=>(t.tags||[]).includes('testing'));
 const route=['C37','C50','C49','C48','C51'];
 const groups=[
  {key:'practice',title:tr('Практика','Practice'),label:'C# · SQL',description:tr('Пиши тесты и проверяй, какие дефекты они находят.','Write tests and check which defects they detect.'),items:items.filter(isRunnable).sort((a,b)=>(route.indexOf(a.id)<0?999:route.indexOf(a.id))-(route.indexOf(b.id)<0?999:route.indexOf(b.id)))},
  {key:'theory',title:tr('Теория','Theory'),label:tr('ВОПРОСЫ','QUESTIONS'),description:tr('Повтори основы и проверь понимание механизмов.','Review the fundamentals and check your understanding.'),items:items.filter(t=>t.engine==='quiz')},
  {key:'review',title:tr('Разборы','Scenarios'),label:tr('СЦЕНАРИИ','SCENARIOS'),description:tr('Спланируй проверки и защити выбранную стратегию.','Plan checks and defend your testing strategy.'),items:items.filter(t=>t.engine==='review')}
 ];
 const group=groups.find(g=>g.key===testingMode)||groups[0];
 $('main').innerHTML=`<div class="row between"><div><div class="eyebrow">DELIBERATE PRACTICE</div><h1>${tr('Тестирование','Testing')}</h1><p class="intro">${tr('От граничных случаев до транзакций и сбоев. Выбери формат и начни с конкретной задачи.','From boundary cases to transactions and failures. Choose a format and start with a task.')}</p></div><span class="small">${items.length} ${tr('заданий по тестированию','testing exercises')}</span></div>
 <div class="section-links testing-modes" role="group" aria-label="${tr('Формат подготовки','Practice format')}">${groups.map(g=>{const seen=g.items.filter(t=>progress.attempts.some(a=>a.task_id===t.id)).length;return `<button class="panel section-link testing-mode" data-testing-mode="${g.key}" aria-pressed="${g.key===group.key}"><span class="eyebrow">${g.label}</span><h2>${g.title} <span class="pill">${g.items.length}</span></h2><span class="small">${g.description}</span><span class="testing-mode-progress small">${tr('Есть попытки','Attempted')}: ${seen} / ${g.items.length}</span><span class="track"><span style="width:${g.items.length?seen/g.items.length*100:0}%"></span></span></button>`;}).join('')}</div>
 <div class="filters"><input type="search" id="testingSearch" aria-label="${tr('Поиск заданий по тестированию','Search testing exercises')}" placeholder="${tr('Название, тема или ID…','Title, topic or ID…')}" value="${esc(testingQuery)}"></div>
 <section class="panel testing-catalog"><div class="row between testing-catalog-heading"><h2>${group.title}</h2><span class="small" id="testingCount" aria-live="polite"></span></div><div id="testingList"></div></section>
 <details class="panel testing-guide"><summary>${tr('Темы и порядок подготовки','Topics and practice order')}</summary><p class="small">${tr('Начни с C37, затем C50 (свойства), C49 (время), C48 (HTTP) и C51 (транзакции). На одну практическую задачу отведи 25–40 минут. Сначала собственные тесты, затем проверка и разбор.','Start with C37, then C50 (properties), C49 (time), C48 (HTTP) and C51 (transactions). Allow 25–40 minutes per practical task. Write your tests before checking and reading the explanation.')}</p><ul>
 <li>${tr('Unit: Arrange–Act–Assert, граничные случаи, параметризация, NUnit и тестовые двойники. Проверяй поведение, а не внутренние вызовы.','Unit: Arrange–Act–Assert, boundaries, parameterization, NUnit and test doubles. Verify behavior rather than internal calls.')}</li>
 <li>${tr('Интеграционные: настоящий provider, миграции, изоляция данных, rollback и ограничения БД. SQLite и EF InMemory не доказывают поведение PostgreSQL.','Integration: real providers, migrations, data isolation, rollback and constraints. SQLite and EF InMemory do not prove PostgreSQL behavior.')}</li>
 <li>${tr('API: ошибки, валидация, авторизация, tenant isolation и совместимость контрактов.','API: errors, validation, authorization, tenant isolation and contract compatibility.')}</li>
 <li>${tr('Кошелёк и Kafka/CDC: конкуренция, идемпотентность, дубликаты, replay и сбой между commit и публикацией.','Wallets and Kafka/CDC: concurrency, idempotency, duplicates, replay and crashes between commit and publication.')}</li>
 <li>${tr('Надёжность и нагрузка: управляемое время, детерминированные данные, cleanup, throughput, p95/p99 и насыщение ресурсов. Coverage не доказывает корректность.','Reliability and load: controlled time, deterministic data, cleanup, throughput, p95/p99 and saturation. Coverage does not prove correctness.')}</li>
 </ul><p class="small">${tr('Практические задачи проверяются в песочнице; разборы сценариев — по рубрике самооценки.','Practical tasks are checked in the sandbox; scenario reviews use self-assessment rubrics.')}</p></details>`;
 function renderItems(){
  const query=testingQuery.trim().toLocaleLowerCase();
  const visible=group.items.filter(t=>`${t.id} ${t.title} ${t.cat} ${t.desc}`.toLocaleLowerCase().includes(query));
  $('testingCount').textContent=`${visible.length} / ${group.items.length}`;
  $('testingList').innerHTML=visible.length?visible.map(t=>{
   const running=progress.attempts.some(a=>a.task_id===t.id&&!a.finished),latest=history(t.id)[0],state=level(t.id);
   const status=running?tr('В процессе','In progress'):latest?.result.status==='self_review'?tr('Самооценка','Self-assessed'):latest?I18n.text(state.text):tr('Не начато','Not started');
   return `<button class="recommendation testing-task" data-open="${esc(t.id)}"><span class="testing-task-copy"><span class="row"><span class="pill">${esc(t.id)}</span><span class="small">${esc(I18n.text(labels[t.engine]))} · ${t.minutes} ${tr('мин','min')}</span></span><strong>${esc(t.title)}</strong><span class="small">${esc(t.cat)}</span></span><span class="testing-task-status"><span class="small ${running?'green':state.color}">${esc(status)}</span><span class="testing-task-open">${running?tr('Продолжить →','Continue →'):tr('Открыть →','Open →')}</span></span></button>`;
  }).join(''):`<div class="testing-empty"><h3>${tr('Ничего не найдено','No matching exercises')}</h3><p class="small">${tr('Попробуй другой запрос или выбери другой формат.','Try another search or choose a different format.')}</p></div>`;
  bindCommon();
 }
 renderItems();
 $('testingSearch').oninput=e=>{testingQuery=e.target.value;renderItems();};
 document.querySelectorAll('[data-testing-mode]').forEach(button=>button.onclick=()=>{
  testingMode=button.dataset.testingMode;testingPage();
  document.querySelector(`[data-testing-mode="${testingMode}"]`)?.focus();
 });
}
