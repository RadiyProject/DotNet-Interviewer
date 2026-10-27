# .NET roadmap: карта подготовки

[Roadmap Milan Jovanović](https://milanjovanovic.tech/roadmaps/2026_dotnet_roadmap.png) просмотрена как изображение. Пять основных блоков: .NET/C#, Database, Messaging, Cloud, AI Tooling. Full-stack с React/Angular и TypeScript указан как bonus.

## Покрытие тренажёра

| Направление roadmap | Практика и теория | Следующий шаг |
|---|---|---|
| C#, ASP.NET Core, DI, auth, integration testing | T01, C16–C18, C24–C25, C37, C41–C42, A28, A40 | Тренировать целостный HTTP pipeline и проверять persistence на выбранной реальной БД |
| SQL, data modeling, indexing, plans | 24 PostgreSQL и 18 SQLite задач, T09, A26, A35, Q164 | Читать estimates vs actual rows, loops и buffers; выбирать индекс под запрос |
| PostgreSQL JSON и vector | SQL19-PG, A45, Q163, A51 | Разделять exact correctness, ANN recall и контроль доступа |
| RabbitMQ, queues/topics, retries, dedup, DLQ, outbox/inbox | T14–T15, A29–A31, Q097–Q098 | По шагам разбирать crash до/после commit и ack |
| Azure Service Bus, AWS SQS/SNS | Q159–Q161, A50 | Сравнить delivery/settlement и независимых подписчиков |
| Cloud deployment и GitHub Actions | A32–A34, Q162, A50 | Выбрать одно облако и подготовить pipeline, migration и rollback plan |
| AI tooling | T18, Q048–Q049, A10, A39 | Проверять изменения, ограничивать права инструментов и измерять результат |
| Frontend bonus | Отдельного frontend-курса в банке нет | Углублять под full-stack вакансии; для backend это не обязательная первая ветка |

Карта является навигацией по темам, а не заявлением о полном покрытии каждого продукта. В частности, SQL Server integration runner, реальные cloud deployments и pgvector execution не включены. Общие EF/SQL навыки не заменяют проверку деталей конкретного provider.

## Дополнение

Добавлено 8 упражнений: Q159–Q164 и A50–A51. Общий банк: 322, из них 48 C#, 42 SQL, 164 теоретических вопроса и 68 письменных разборов.

- Q159–Q161: Service Bus lock/settlement, SQS Standard, SNS fanout.
- Q162: GitHub Actions OIDC и границы cloud permissions.
- Q163: pgvector approximate search, фильтры и recall.
- Q164: фактическое исполнение EXPLAIN ANALYZE.
- A50: pipeline ASP.NET Core в одном облаке с artifact identity, OIDC, совместимой миграцией и consumer recovery.
- A51: JSONB/pgvector, tenant/ACL isolation, exact baseline и оценка approximate индекса.

A50/A51 — письменные задания с рубрикой самооценки, без исполнения YAML/SQL или создания ресурсов. Первоисточники находятся в каждом упражнении. Визуальная roadmap добавлена ссылкой в библиотеку; копия картинки на диск не скачивалась.

## Порядок подготовки

Сначала добейся уверенности в C#/HTTP, SQL и транзакциях. Затем отработай брокер, повторную доставку и наблюдаемость. После этого выбери Azure либо AWS и собери собственный end-to-end deployment. Углублённые vector search, несколько облаков и frontend выбирай по роли, а не ради количества технологий. Версии и поддержку платформ проверяй по документации при выборе runtime: подпись в roadmap не заменяет актуальную support policy.

Проверены метаданные, связи и синтаксис изменённого JavaScript. Сборка и тесты не запускались. Новых пакетов, временных файлов и Docker build для этого дополнения не требуется.
