'use strict';
const InterviewPresetsUI=(()=>{
 const tr=(ru,en)=>I18n.language==='en'?en:ru;
 let working=false;
 async function render(){
  $('main').innerHTML='<p>'+tr('Загрузка готовых вариантов…','Loading ready-made plans…')+'</p>';
  try{
   const {presets}=await api('interviews/presets');
   if(view!=='presets')return;
   $('main').innerHTML=`<h1>${tr('Готовые варианты','Ready-made plans')}</h1><p class="intro">${tr('Подготовка к Senior .NET / iGaming интервью с техлидом: упор на БД, high-load и CDC. Фиксированный состав без случайного подбора.','Prepare for a Senior .NET / iGaming interview with a tech lead: databases, high load and CDC. Fixed task sets without random selection.')}</p><p class="note">${tr('Авторские тренировочные варианты по вакансии, не вопросы конкретной компании. Без лайв-кодинга. Теория проверяется автоматически, письменные сценарии — по рубрике или с ассистентом. Существующие разборы доступны в заданиях после ответа.','Original practice plans based on the vacancy, not actual company questions. No live coding. Theory is graded automatically; written scenarios use rubrics or an assistant. Existing explanations are available in tasks after submission.')}</p><div class="preset-grid">${presets.map(p=>`<section class="panel preset-card"><h2>${esc(p.title)}</h2><p>${esc(p.description)}</p><p class="preset-meta">${p.minutes} ${tr("минут · без лайв-кодинга","minutes · no live coding")}</p><button class="primary" data-preset="${esc(p.id)}">${tr("Выбрать и настроить AI-интервью","Select and configure AI interview")}</button><details><summary>${tr("Сценарий и план разговора","Scenario and discussion agenda")}</summary><p>${esc(p.scenario.premise)}</p>${p.scenario.discussion.map(item=>`<p>${esc(item)}</p>`).join('')}<h3>${tr("План устного раунда","Verbal round agenda")}</h3><ul>${p.scenario.agenda.map(item=>`<li>${esc(item)}</li>`).join('')}</ul><p class="small">${tr("Это ориентир для разговора с ассистентом. В самостоятельном режиме действуют лимиты отдельных заданий.","This agenda guides the assistant discussion. Independent mode uses individual task deadlines.")}</p></details><p class="small">${p.tasks.length} ${tr('заданий','tasks')} · ${tr('лимит','limit')}: ${p.minutes} ${tr('мин','min')} · ${tr('оценка заданий','task estimate')}: ${p.estimated_minutes} ${tr('мин','min')}</p><details><summary>${tr('Состав варианта','Plan contents')}</summary><ol>${p.tasks.map(t=>`<li><strong>${esc(t.id)}</strong> · ${esc(t.title)} · ${t.minutes} ${tr('мин','min')} · ${t.engine==='quiz'?tr('тест','quiz'):tr('письменный разбор','written review')}</li>`).join('')}</ol></details><details><summary>${tr('Чек-лист и устные вопросы','Checklist and verbal questions')}</summary><ul>${p.guide.map(item=>`<li>${esc(item)}</li>`).join('')}</ul></details></section>`).join('')}</div><p class="small">${tr('Каждое открытие создаёт отдельный сохранённый вариант в конструкторе. Там можно проверить состав, продолжить попытку или подготовить разговорный раунд с ассистентом и своим профилем. Таймер самостоятельного режима начнётся при запуске первого задания.','Each opening creates a separate saved plan in the builder. Inspect the tasks, resume an attempt or prepare an assistant round with your profile. The independent timer starts with the first task.')}</p>`;
   document.querySelectorAll('[data-preset]').forEach(button=>button.onclick=async()=>{
    if(working)return;
    working=true;
    const buttons=[...document.querySelectorAll('[data-preset]')];buttons.forEach(b=>b.disabled=true);
    try{
     const {plan}=await api('interviews/from-preset',{preset_id:button.dataset.preset});
     sessionStorage.setItem('forge-builder-plan',plan.id);
     navigate('builder');
    }catch(e){notify(e.message);}finally{working=false;buttons.forEach(b=>b.disabled=false);}
   });
  }catch(e){if(view==='presets')$('main').innerHTML='<p>'+esc(e.message)+'</p>';}
 }
 return {render};
})();
