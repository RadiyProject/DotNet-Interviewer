# Senior Backend: геймификация, Kafka и CDC

Добавлены 17 вопросов Q173–Q189 и 8 письменных практических заданий A68–A75 на русском и английском. Это авторские сценарии по требованиям предоставленной вакансии, а не реконструкция вопросов конкретной компании.

## Где найти

- Теория: темы `CDC / Debezium`, `Kafka`, `Architecture`, `AI engineering`, а также EF Core, Security и Concurrency.
- Практические сценарии и ревью: вкладка «Архитектура и ревью»; фильтры `CDC / Debezium`, `Apache Kafka`, `Геймификация`.
- Теория связана с практикой через `related`. Теги доступны конструктору и настройкам AI-интервью.

| Практика | Что отрабатывается | Связанная теория |
|---|---|---|
| A68 | Ревью Debezium JSON: slot, snapshot, restart | Q173–Q175, Q177 |
| A69 | SQL/псевдокод CDC-проекции: snapshot, replay, delete | Q175, Q176, Q178 |
| A70 | Ревью C# Kafka consumer: offset, inbox, конкурентность | Q179–Q183 |
| A71 | Схема и транзакция баланса: повторы, ledger, валюты | Q186, Q187 |
| A72 | C4 context/container и UML sequence платформы | Q184, Q185 |
| A73 | Runbook CDC-инцидента: lag, WAL, восстановление | Q174, Q178 |
| A74 | Ревью AI-generated admin API: tenant и авторизация | Q189 |
| A75 | Спецификация DailyReward до AI-генерации | Q188, Q186 |

В каждом сценарии есть условие, проверяемая рубрика, разбор и первоисточник. A68/A70/A74 содержат отдельный фрагмент для ревью. Остальные требуют проектирования, SQL, псевдокода или схем в текстовом редакторе. **Это письменные задания:** инфраструктура Kafka/Debezium не поднимается и решения не выполняются автоматически. Можно получить Codex-разбор и обсуждать решение голосом через существующую интеграцию. Самооценка не является функциональным тестом.

## Основные источники

- [Debezium PostgreSQL connector](https://debezium.io/documentation/reference/stable/connectors/postgresql.html)
- [Debezium Outbox Event Router](https://debezium.io/documentation/reference/stable/transformations/outbox-event-router.html)
- [Debezium delete/unwrap transformations](https://debezium.io/documentation/reference/stable/transformations/event-flattening.html)
- [SQL Server CDC](https://learn.microsoft.com/en-us/sql/relational-databases/track-changes/about-change-data-capture-sql-server)
- [Kafka design](https://kafka.apache.org/41/design/design/), [consumer configuration](https://kafka.apache.org/41/configuration/consumer-configs/), [producer configuration](https://kafka.apache.org/41/configuration/producer-configs/)
- [C4 diagrams](https://c4model.com/diagrams), [UML 2.5.1](https://www.omg.org/spec/UML/2.5.1/About-UML)
- [EF Core concurrency](https://learn.microsoft.com/en-us/ef/core/saving/concurrency), [Redis locks](https://redis.io/docs/latest/develop/clients/patterns/distributed-locks/)
- [GitHub Spec Kit](https://github.com/github/spec-kit), [ASP.NET resource authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resourcebased)

Документация просмотрена 29 октября 2026 года. Kafka-ссылки закреплены за веткой 4.1, Debezium stable меняется: версии коннектора/БД и параметры необходимо фиксировать при реальном развёртывании. Задания не требуют обновления пакетов проекта.
