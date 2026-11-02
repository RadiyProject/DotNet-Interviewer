# Библиотеки .NET: вопросы и практика

Добавлены 11 вопросов и 11 письменных практических задач. Все карточки имеют RU/EN версии, ответ, сценарии и критерии разбора. Практика требует эскиза кода/схемы и конкретных проверок; автоматического исполнения библиотечного кода нет. Пакеты, контейнеры и сборки не устанавливались и не запускались.

| Тема | Вопрос | Практика | Официальный источник |
|---|---|---|---|
| EF Core | Q231 | A106 | [Документация](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/) |
| MediatR | Q232 | A107 | [Документация](https://github.com/LuckyPennySoftware/MediatR) |
| FluentValidation | Q233 | A108 | [Документация](https://docs.fluentvalidation.net/en/latest/async.html) |
| StackExchange.Redis | Q234 | A109 | [Документация](https://seredis.dev/Basics.html) |
| Confluent.Kafka | Q235 | A110 | [Документация](https://docs.confluent.io/kafka-clients/dotnet/current/overview.html) |
| Polly | Q236 | A111 | [Документация](https://www.pollydocs.org/strategies/timeout.html) |
| Serilog | Q237 | A112 | [Документация](https://github.com/serilog/serilog/wiki/Writing-Log-Events) |
| OpenTelemetry | Q238 | A113 | [Документация](https://opentelemetry.io/docs/languages/dotnet/instrumentation/) |
| xUnit | Q239 | A114 | [Документация](https://xunit.net/docs/shared-context) |
| Moq | Q240 | A115 | [Документация](https://github.com/devlooped/moq/wiki/Quickstart) |
| Testcontainers | Q241 | A116 | [Документация](https://dotnet.testcontainers.org/test_frameworks/xunit_net/) |

Рекомендуемый маршрут: EF Core → FluentValidation → MediatR → Redis → Kafka → Polly → Serilog → OpenTelemetry → xUnit → Moq → Testcontainers.

Для Polly продолжение — Q225–Q230, C52–C53, A104–A105. Для каждого письменного разбора сначала предложи своё решение, затем сравни гарантии и сценарии с ответом. Версию xUnit в эскизах нужно указывать явно: async lifecycle v2 и v3 отличается.
