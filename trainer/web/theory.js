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

function testingPage(){
 const items=tasks.filter(t=>(t.tags||[]).includes('testing'));
 $('main').innerHTML=`<section class="panel"><div class="eyebrow">.NET · БД · HIGH-LOAD</div><h1>Тестирование</h1><p>Подготовка к устному интервью: объясни, какой риск проверяет тест, где проходит граница системы и почему тест поймает реальный дефект.</p><h2>Что разобрать</h2><ul><li>Unit: Arrange–Act–Assert, граничные случаи, параметризация, xUnit/NUnit, тестовые двойники; проверка поведения вместо внутренних вызовов.</li><li>Интеграционные: WebApplicationFactory, настоящая PostgreSQL в контейнере, миграции, изоляция данных. EF InMemory не проверяет SQL и транзакции PostgreSQL.</li><li>Кошелёк: атомарность, rollback, параллельное списание, отрицательный баланс, deadlock/retry, повтор idempotency key.</li><li>Kafka/CDC: дубликаты, replay, порядок по ключу, сбой между commit БД и публикацией; eventual consistency с ограниченным ожиданием.</li><li>API: коды ошибок, валидация, авторизация и tenant isolation; совместимость контрактов.</li><li>Нагрузка: throughput, p95/p99, насыщение пула соединений; профиль нагрузки и измеримый критерий успеха.</li><li>Надёжность тестов: управляемое время, отсутствие Thread.Sleep, детерминированные данные, cleanup, flaky tests; coverage не доказывает корректность.</li></ul><h2>План на 30 минут</h2><p>10 минут — теория ниже; 10 минут — тесты перевода средств; 10 минут — интеграционные проверки БД и повторной доставки события. На собеседовании обсуждай проверки устно, без написания кода.</p></section>${['quiz','review','dotnet'].map(engine=>{const group=items.filter(t=>engine==='dotnet'?!['quiz','review'].includes(t.engine):t.engine===engine);return group.length?`<section class="panel"><h2>${engine==='quiz'?'Основы и вопросы':engine==='review'?'Разбор сценариев':'Практика'}</h2>${group.map(t=>`<button class="recommendation" data-open="${esc(t.id)}"><span class="pill">${esc(t.id)} · ${t.minutes} мин</span><strong>${esc(t.title)}</strong></button>`).join('')}</section>`:'';}).join('')}`;
 bindCommon();
}
