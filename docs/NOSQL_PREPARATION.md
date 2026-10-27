# NoSQL: MongoDB, графовые, key-value и wide-column СУБД

27.10.2026: добавлены 16 упражнений. Общий банк — 306: 47 C#, 42 SQL, 152 вопроса, 65 письменных разборов.

| Тема | Вопросы | Практика |
|---|---|---|
| MongoDB | Q143–Q146 | A45: модель, aggregation pipeline, compound index и version filter; C45: агрегация вложенных документов в памяти |
| Graph / Neo4j / Cypher | Q147–Q148 | A46: параметризованные запросы, tenant, duplicates и OPTIONAL MATCH; C44: bounded BFS |
| Redis | Q149–Q150 | A47: leaderboard, cache-aside, invalidation race и восстановление |
| Cassandra | Q151–Q152 | A48: CQL, partition/clustering keys, buckets, hot partitions и retention |

Все четыре темы доступны отдельно в выборе теоретических тестов, в библиотеке и в разделе архитектуры. Тематические переходы соединяют вопросы, практику и ранее существующие упражнения. История и таймеры используют существующую модель.

## Что действительно запускается

C44 и C45 — исполняемые C# kata с harness-проверками: глубина/направление/циклы/дубли графа; tenant/status, повторные строки документа, ordinal sorting и checked arithmetic. Входные контракты и ошибки перечислены в условиях.

MongoDB aggregation, Cypher, Redis-команды и CQL в A45–A48 пишутся в редакторе как письменная практика и оцениваются по рубрике. Эти запросы автоматически не выполняются и не валидируются настоящей СУБД. Модель ответа не доказывает корректность реального query plan. C45 не эмулирует BSON numeric semantics; C44 не является Neo4j integration test.

MongoDB, Neo4j, Redis и Cassandra серверами в Compose этим дополнением не добавлены. Это не требует новых NuGet dependencies, скачивания образов или Docker build. Для полноценного live SQL-подобного запуска NoSQL понадобятся отдельные executors и изолированные fixtures; текущая версия не заявляет такой возможности.

## Как заниматься

1. Пройди тест выбранной темы, затем связанную практику.
2. Для MongoDB выпиши document boundary, запросы чтения/записи и условия конфликта до выбора индекса. Для графа реши, возвращаешь узлы или пути.
3. Для Redis назови authoritative источник и допустимую staleness. Для Cassandra начни с access patterns и верхней границы partition.
4. В design сравни выбранную СУБД с PostgreSQL: конкретный инвариант, failure case и стоимость эксплуатации полезнее лозунга «NoSQL масштабируется».

В учебных кейсах нет универсального предположения «NoSQL без схемы/транзакций/consistency». Гарантии определяются продуктом, topology и настройками. Pipeline без ограничений роста документа, graph traversal без bounds и partition без оценки объёма требуют отдельного разбора.

## Первоисточники

- [MongoDB data modeling](https://www.mongodb.com/docs/manual/data-modeling/), [atomicity](https://www.mongodb.com/docs/manual/core/write-operations-atomicity/), [compound indexes](https://www.mongodb.com/docs/manual/core/indexes/index-types/index-compound/), [aggregation pipeline](https://www.mongodb.com/docs/manual/core/aggregation-pipeline/), [TTL indexes](https://www.mongodb.com/docs/manual/core/index-ttl/).
- [Neo4j MATCH](https://neo4j.com/docs/cypher-manual/current/clauses/match/), [OPTIONAL MATCH](https://neo4j.com/docs/cypher-manual/current/clauses/optional-match/).
- [Redis data types](https://redis.io/docs/latest/develop/data-types/).
- [Cassandra data modeling](https://cassandra.apache.org/doc/latest/cassandra/developing/data-modeling/intro.html), [CQL schema](https://cassandra.apache.org/doc/latest/cassandra/developing/cql/ddl.html).

Упражнения авторские, не заявления о реальных вопросах конкретной компании. Проверены метаданные, related-связи, индексы правильных ответов и пути harness. Сборка и тесты не запускались по инструкции пользователя.
