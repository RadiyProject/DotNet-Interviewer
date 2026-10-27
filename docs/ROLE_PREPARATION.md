# Подготовка по требованиям Senior .NET роли

Дополнение от 24.10.2026: 48 авторских упражнений, всего 233. Это учебные сценарии по перечисленным требованиям, а не подборка подтверждённых внутренних вопросов работодателя. Основные технические объяснения сверяются с документацией разработчиков технологий; каталог GoF — вторичный учебный материал.

## Карта занятий

| Требование | Практика и разбор | Что дополнительно объяснить вслух |
|---|---|---|
| GoF и применение | C27–C31, A23–A25, Q083–Q087 | Альтернативы паттерну, цена усложнения, границы ответственности |
| C# 8–12+ / ASP.NET Core | C12, C16–C20, C22–C23, Q096, T04 | NRT, records/init/required, async streams, middleware ordering, DI lifetime |
| async/await, TAP | C06, C11–C12, C38, Q094, T07 | Cancellation, синхронный callback, exception propagation, cleanup |
| Память / GC | C07, C13, C23, A27, Q095, T13 | Managed heap vs process memory; retention vs allocation; ownership |
| Threading, Parallel, TPL, concurrent collections, Dataflow | C10–C12, C32–C33, C36, Q088–Q089, Q092, T05–T06 | CPU vs I/O, bounded queues, contention, fault/completion, starvation |
| EF Core | C24–C25, C34–C35, A26, Q090–Q091, Q101, T10 | IQueryable vs IEnumerable, tracking, query plan, compiled query limits, expand/contract migration |
| Unit / integration testing | C37, A08, A28, Q093, T08 | Assertions против дефектов, real provider, isolation, transaction boundaries |
| PostgreSQL | SQL01-PG–SQL22-PG, T09, A13, A21 | EXPLAIN, indexes, locks/isolation, JSONB, time zones, ranges, tie-breaking |
| RabbitMQ / Kafka | A29–A31, Q097–Q098, T14–T15 | Confirm vs ack vs offset; crash points, dedup, retry/DLQ, ordering |
| Docker | A32, Q099 | Layer cache, dependency restore, secrets, runtime user, signal handling |
| Kubernetes | A33–A34, Q100, Q102 | Requests/limits, probes, rollout, drain, events, OOM investigation |

Сначала ответь на связанные вопросы за 2 минуты. Затем реши практическую задачу без просмотра ответа за её лимит. После отправки прочитай разбор и объясни один контрпример, одну альтернативу и один сценарий отказа. Повторяй слабые темы с новым решением, а не запоминанием текста. Цвет освоения в интерфейсе отражает записанные результаты тренировки, а не независимую сертификацию senior-уровня.

## Все 23 GoF: где прорабатывать

| Группа | Паттерны | Упражнения |
|---|---|---|
| Порождающие | Factory Method, Abstract Factory, Builder, Prototype, Singleton | A23: выбор и контрпримеры; для Singleton отдельно обсуди lifetime и testability |
| Структурные | Adapter, Bridge, Composite, Decorator, Facade, Flyweight, Proxy | A24; Composite — C29; Decorator — C28 |
| Поведенческие | Chain of Responsibility, Command, Iterator, Mediator, Memento, Observer, State, Strategy, Template Method, Visitor | A25; Chain — C30; State — C31; Strategy — C27 |

В review-заданиях назови участников, нарисуй взаимодействия, напиши минимальный пример выбранного решения и покажи, когда простой код лучше. У всех 23 паттернов есть тематическое покрытие, но отдельные исполняемые kata пока есть у пяти. Поведенческие проверки не доказывают, что внутри действительно применён GoF или соблюдена нужная асимптотика.

## Границы проверки

- C27–C38 компилируются и запускаются существующим C# runner с проверками наблюдаемого поведения. C32–C33 используют Dataflow; C34–C35 — настоящий EF Core с SQLite. SQLite не заменяет PostgreSQL integration tests.
- C37 запускает написанные пользователем NUnit assertions против правильного делегата и дефектных вариантов. Это практика качества assertions, а не запуск NUnit discovery через `dotnet test`.
- SQL19-PG–SQL22-PG проверяют SELECT на четырёх независимых fixtures: JSONB/LATERAL, TIMESTAMPTZ, DISTINCT ON и диапазоны. Эквивалентный корректный запрос допускается.
- A23–A34 — письменные задания с рубрикой и разбором. RabbitMQ, Kafka и Kubernetes в этом дополнении не поднимаются; YAML, миграции, профилирование и диагностические команды автоматически не исполняются. Сохраняемый результат рубрики является самооценкой.
- Runner использует имеющийся .NET 10 SDK. Вопросы охватывают C# 8–12+, но отдельная матрица компиляторов и target frameworks не запускается.
- Время ограничивается существующим таймером. Банк и связи расширены без изменения формата истории попыток.

## Материалы и источники

Начинай с конкретной страницы `source` в задании и библиотеки в интерфейсе. Для углубления:

- GoF, *Design Patterns: Elements of Reusable Object-Oriented Software*: [издатель](https://www.informit.com/store/design-patterns-elements-of-reusable-object-oriented-9780201633610), [учебный каталог](https://refactoring.guru/design-patterns/catalog). Не заучивай паттерн без ситуации, где он оправдан.
- [C# version history](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-version-history), [TAP interoperability](https://learn.microsoft.com/en-us/dotnet/standard/asynchronous-programming-patterns/interop-with-other-asynchronous-patterns-and-types), [GC fundamentals](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/fundamentals).
- [Dataflow](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/dataflow-task-parallel-library): рисуй потоки данных, завершения и ошибок отдельно; начинай с маленьких bounded capacities.
- [EF performance](https://learn.microsoft.com/en-us/ef/core/performance/advanced-performance-topics), [raw SQL](https://learn.microsoft.com/en-us/ef/core/querying/sql-queries), [testing strategy](https://learn.microsoft.com/en-us/ef/core/testing/choosing-a-testing-strategy). Смотри сгенерированный SQL и план до выбора оптимизации.
- [PostgreSQL JSONB](https://www.postgresql.org/docs/18/datatype-json.html), [date/time](https://www.postgresql.org/docs/18/datatype-datetime.html), [ranges](https://www.postgresql.org/docs/18/rangetypes.html). Разделяй SQL NULL, JSON null, пустой массив и отсутствие строки.
- [RabbitMQ confirms](https://www.rabbitmq.com/docs/confirms), [DLX](https://www.rabbitmq.com/docs/dlx), [Kafka design](https://kafka.apache.org/43/design/design/). Для любой гарантии доставки укажи её границу и crash между двумя действиями.
- [Docker multi-stage](https://docs.docker.com/build/building/multi-stage/), [Kubernetes probes](https://kubernetes.io/docs/concepts/workloads/pods/probes/), [resources](https://kubernetes.io/docs/concepts/configuration/manage-resources-containers/), [debugging](https://kubernetes.io/docs/tasks/debug/debug-application/debug-running-pod/). Подкрепляй диагноз наблюдаемыми фактами.

Новые закреплённые NuGet зависимости: System.Threading.Tasks.Dataflow 10.0.10 и NUnit 4.3.2. Существующий механизм restore автоматически подтягивает их в persistent volume при первой проверке соответствующего задания. Это требует сети; выполнение пользовательского кода остаётся без сети. Обновление контента не требует Docker build.

При подготовке дополнения проверены только структура JSON, ссылки между заданиями, наличие harness-файлов и регистрация пакетов. Сборка, скачивание зависимостей и запуск тестов не выполнялись по инструкции пользователя.
