# Происхождение базы и источники

Дата исследовательской сессии: **21 октября 2026**. Формулировки заданий и объяснения написаны для этого проекта. Тексты вопросов из чужих репозиториев массово не копировались; ссылки на community-материалы служат навигацией. Нет утверждения, что конкретное упражнение — внутренний вопрос конкретной компании.

## Релевантность интервью

Открыты и изучены публичные hiring guides:

- [Microsoft: Technical interviewing](https://careers.microsoft.com/v2/global/en/hiring-tips/technical-interviewing.html): решение задач, технические принципы и применение опыта. Это основание включить кодирование, проверку решений и обсуждение архитектуры, а не источник численных нормативов.
- [Amazon: Software development interview topics](https://amazon.jobs/content/en/how-we-hire/interview-prep/software-development-topics): coding и system design, применение фундаментальных знаний. Конкретный процесс и стек следует уточнять у рекрутера.

Тайминги, границы баллов и алгоритм рекомендаций — настройки этого тренажёра, не официальные критерии конкретного работодателя.

## Технические первоисточники

В ходе исследования отдельно открыты:

- [HttpClient guidelines](https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient-guidelines): lifetime клиентов, connection pooling и DNS.
- [Implement Dispose](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose): детерминированная очистка и unmanaged resources.
- [DI guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines): scope, ownership и design рекомендаций.
- [Options pattern](https://learn.microsoft.com/en-us/dotnet/core/extensions/options): типизированная конфигурация и validation.
- [HTTP resilience](https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience): resilience pipeline и обработка сбоев.
- [EF Core testing strategy](https://learn.microsoft.com/en-us/ef/core/testing/choosing-a-testing-strategy): ограничения test doubles и важность реального provider.
- [PostgreSQL transaction isolation](https://www.postgresql.org/docs/18/transaction-iso.html): concurrency, guarantees и serialization failures.
- [PostgreSQL multicolumn indexes](https://www.postgresql.org/docs/18/indexes-multicolumn.html): ведущие ключи и ограничения применимости индекса.
- [Azure Functions hosting](https://learn.microsoft.com/en-us/azure/azure-functions/functions-scale): зависимость ограничений от плана. Не заучивай одно число timeout.
- [Google SRE](https://sre.google/sre-book/table-of-contents/): SLO, monitoring, overload и надёжность.

У каждого задания есть дополнительная тематическая ссылка на документацию. Не все отдельные страницы библиотеки вручную перечитывались в этой сессии; curated ссылка не означает независимый аудит всей документации. При изменении cloud service или версии API перепроверь актуальную страницу.

## Публичные репозитории

- [System Design Primer](https://github.com/donnemartin/system-design-primer) — обзор тем, альтернатив и архитектурных компромиссов.
- [Tech Interview Handbook](https://github.com/yangshun/tech-interview-handbook) — организация подготовки и темы алгоритмов.
- [StefanTheCode/dotnet_interview_questions](https://github.com/StefanTheCode/dotnet_interview_questions) — дополнительные .NET/SQL темы. Ответы community нужно сверять с первоисточниками.

Репозитории не использованы как доказательство точной частоты вопросов в компаниях. Stories из Reddit и маркетинговые подборки не использованы как основание для company-specific утверждений.

## Литература и порядок

1. [C# in Depth — Jon Skeet](https://csharpindepth.com/): language mechanisms. Новые версии языка дополнительно сверяй с Microsoft Learn.
2. [PostgreSQL manual](https://www.postgresql.org/docs/18/): SQL, типы, constraints, индексы и транзакции. Отдельно изучай SQL Server при подготовке на T-SQL позицию.
3. [Designing Data-Intensive Applications](https://dataintensive.net/): storage, replication, consistency и distributed systems; сайт авторов содержит информацию об изданиях.
4. [Site Reliability Engineering](https://sre.google/sre-book/table-of-contents/): бесплатная книга Google, особенно SLO, monitoring, overload и cascading failures.
5. [Azure Architecture Center](https://learn.microsoft.com/en-us/azure/architecture/): практические архитектурные паттерны и их ограничения.

Не читай подряд всё до практики: задача → конкретный пробел → небольшой раздел источника → собственный пример → повтор без подсказки → перенос в design кейс.


## Дополнение от 24 октября 2026

Исследованы предоставленный PDF The .NET Senior Playbook, CodeWithMukesh и Hirebee; дополнительно PostgreSQL Exercises, Ardalis kata catalog, dotnet/eShop и тематические первоисточники. Карта добавлений, осторожность к спорным утверждениям и механизм NuGet описаны в [BANK_UPDATE_20261024.md](BANK_UPDATE_20261024.md). Старые вопросы не переименованы и не заменены новыми.
