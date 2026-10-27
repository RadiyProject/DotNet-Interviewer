# Transfer, load balancer, URL shortener, ledger

Дополнение 27.10.2026: 16 упражнений, всего 249. Четыре категории доступны в практике, теории и архитектурном ревью. Банк содержит 42 C#, 24 PostgreSQL, 18 SQLite, 110 вопросов и 55 письменных разборов. Сценарии авторские; они не выдаются за реальные внутренние вопросы конкретной компании.

## Насколько это релевантно

Для Senior .NET/backend особенно полезны балансировка ASP.NET Core, идемпотентные операции, конкуренция в PostgreSQL и согласование эффектов через очередь. URL shortener — компактная system design тренировка чтений, кешей и генерации ключей. Transfer и ledger углубляют эти навыки для платежей, кошельков, биллинга и внутренних учётных систем. Глубина бухгалтерского домена зависит от вакансии: сам по себе такой набор не означает, что любой .NET разработчик обязан проектировать банковскую платформу.

| Тема | Теория (2 минуты на вопрос) | Исполняемая практика | Design / 45 минут |
|---|---|---|---|
| Transfer service | Q103–Q104 | SQL23-PG: сверка двух движений и результата перевода | A35: атомарность, duplicate requests, deadlock, outbox, timeout после commit |
| Load balancer | Q105–Q106 | C39: детерминированный weighted round robin по health snapshot | A36: L7, YARP, неодинаковая длительность запросов, drain, trusted proxies |
| URL shortener | Q107–Q108 | C40: Base62 codec, canonical format, overflow | A37: unique IDs, collisions, redirects/cache, mutable targets, hot keys |
| Ledger service | Q109–Q110 | SQL24-PG: несбалансированные проводки по transaction и currency | A38: immutable posted journal, reversals, projections, reconciliation |

Пройди теорию, реши kata без подсказки и затем подготовь design с инвариантами, схемой БД, API и трассой сбоя. В связанных заданиях есть кнопка теста по соответствующей теме. Успех SELECT-проверки не является доказательством транзакционной корректности сервиса.

## Контракты и границы

C39 проверяет чистый выбор назначения по взвешенным последовательным слотам; это не smooth weighted round robin, не запущенный YARP и не тест реального сетевого balancer. C40 проверяет кодирование ulong, а не генератор глобально уникальных ID. Упражнения не требуют новых NuGet-пакетов.

SQL23-PG и SQL24-PG работают на четырёх независимых наборах данных каждое. Все ожидаемые правила записаны в requirements: число движений и их знак, исключённые статусы, порядок результата, границы валют и тип суммы. SQL-сверки работают с уже созданными данными, не проводят деньги и не моделируют гонки двух соединений.

В ledger-kata явно задана signed-delta модель. Положительный delta увеличивает баланс; это не утверждение, что дебет всегда уменьшает или всегда увеличивает любой бухгалтерский счёт. Атомарность posting, append-only enforcement и согласованный watermark reconciliation обсуждаются в A38. Данные и нагрузки в кейсах — условия тренировки, не норматив работодателя.

A35–A38 оцениваются по рубрике самостоятельно. Реальные transfer/ledger/proxy сервисы автоматически не разворачиваются; новые гарантии изоляции или exactly-once не заявляются.

## Источники

В каждом упражнении есть технический первоисточник. Дополнительно:

- [PostgreSQL locking](https://www.postgresql.org/docs/18/explicit-locking.html) — блокировки и deadlocks.
- [PostgreSQL constraints](https://www.postgresql.org/docs/18/ddl-constraints.html) — уникальность и пределы row CHECK.
- [Stripe idempotent requests](https://docs.stripe.com/api/idempotent_requests) — пример публичного контракта API; его детали не универсальный стандарт для всех сервисов.
- [Azure compensating transaction](https://learn.microsoft.com/en-us/azure/architecture/patterns/compensating-transaction) — восстановление распределённого бизнес-процесса.
- [YARP load balancing](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/yarp/load-balancing?view=aspnetcore-10.0) и [ASP.NET Core proxy configuration](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0).
- [RFC 9110](https://www.rfc-editor.org/rfc/rfc9110.html) — HTTP semantics, включая redirects.
- [TigerBeetle data modeling](https://docs.tigerbeetle.com/coding/data-modeling/) — конкретная ledger-модель для сравнения с собственным контрактом.

Проверены только метаданные, связи, пути harness и регистрация источников. Сборка и тесты не запускались по инструкции пользователя.
