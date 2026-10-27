# Расширение банка · 24 октября 2026

Добавлено **73 задания**, всего **185**. Старые ID и контракты сохранены; у старых заданий дополнены только тематические связи.

| Раздел | Добавлено | ID |
|---|---:|---|
| C# / .NET с harness | 17 | C10–C26 |
| SQL с независимыми fixtures | 20 | SQL09–SQL18, отдельно PG и SQ |
| Архитектура / engineering review | 12 | A11–A22 |
| Теория, один или несколько ответов | 24 | Q059–Q082 |

Общий состав: **28 C#/.NET + 36 SQL + 39 review/design + 82 quiz**. SQL задачи в двух диалектах — варианты одних 18 постановок, не 36 разных бизнес-кейсов. Манифест новых ID: BANK_UPDATE_20261024.json.

## Откуда взяты направления

- Предоставленный PDF **The .NET Senior Playbook, Anton Martyniuk**, 22 страницы. Тематическая часть и вопросы: с. 7–17. Использованы темы async, memory, reflection, ASP.NET pipeline, DI, serialization, EF, caching, scheduling, messaging, telemetry и deployment. Готовых подробных ответов в PDF нет; рекламные страницы и заявления о гарантированной готовности к интервью не приняты за технические критерии.
- [CodeWithMukesh: Senior .NET developer interview questions](https://codewithmukesh.com/blog/senior-dotnet-developer-interview-questions/) — ориентир по эксплуатационным темам: shared factories, memory и диагностика, cache/data consistency. Формулировки и решения не скопированы.
- [Hirebee: Senior .NET role topics](https://hirebee.ai/hr-tool-and-template/senior-net-developer-2) — ориентир по фундаменту языка/runtime. Активность в социальных сетях и side projects не превращена в критерий знаний.
- [PostgreSQL Exercises](https://pgexercises.com/questions/aggregates/) — дополнительная публичная практика по агрегациям и окнам; схемы и наборы данных в банке собственные.
- [Ardalis kata catalog](https://github.com/ardalis/kata-catalog) — дополнительный каталог для самостоятельной code-kata практики, ссылка доступна в библиотеке.
- [dotnet/eShop](https://github.com/dotnet/eShop) — открытая reference architecture .NET, не подтверждение, что конкретный кейс задавался на интервью.

Новые условия, C# harness, SQL fixtures, рубрики и объяснения написаны для этого проекта. Цифры нагрузок в design кейсах — явно заданные учебные ограничения. Не заявляются company-specific вопросы или частота их появления на реальных интервью.

## Проверка спорных утверждений

В подборках встречаются упрощения. Для ответов предпочтены Microsoft Learn, PostgreSQL/SQLite docs, RFC и документация проектов. У каждой карточки есть основной технический источник; дополнительные ссылки отображаются отдельно.

- `int[20000]` содержит около 80000 bytes данных: это не корректный универсальный пример превышения стандартного LOH threshold около 85000 bytes. Учитываются заголовок объекта и настройки runtime. [LOH](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/large-object-heap).
- `ConcurrentDictionary.GetOrAdd` защищает вставку, но не гарантирует однократный вызов valueFactory. Задача C10 отделяет создание Lazy-обёртки от выполнения бизнес-фабрики. [GetOrAdd](https://learn.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2.getoradd).
- Нельзя переносить утверждения о lock escalation или переписывании таблицы между SQL Server/PostgreSQL и версиями движка без условий. A13 привязан к PostgreSQL и требует выбора стратегии по его документации.
- SQLite integration test не доказывает поведение SQL Server или PostgreSQL. Для EF задач явно указан provider; деньги заданы целыми minor units. [SQLite limitations](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations).
- Оптимальная архитектура не определяется наличием всех модных компонентов: у кейсов есть инварианты, альтернативы, отказы и acceptance rubric. Результат review остаётся самооценкой.

## Что действительно исполняется

C10–C23 используют BCL/Microsoft.AspNetCore.App: например, реальный DI-контейнер и RequestDelegate на DefaultHttpContext, без открытия порта. C24–C25 используют настоящий EF Core + SQLite in-memory database. C26 использует Dapper и настоящую SQLite connection, включая SQL-символы во входных данных и cancellation.

SQL09–SQL18 выполняются на четырёх fixture-наборах в PostgreSQL или SQLite: ties, дубли, несколько дочерних строк, пустые связи, отрицательные значения и пустая БД. Это проверка конечного набора наблюдаемых свойств, не математическое доказательство правильности и не performance benchmark.

Для C25 запрет предварительного SELECT требует дополнительного review/инструментирования: включённые сценарии не заявляются полным доказательством отсутствия гонок. Для Span/parsing проверяется результат, а не отсутствие аллокаций. Архитектурные критерии не оцениваются regex-поиском ключевых слов.

Сборка образов, dotnet build, выполнение harness и SQL-проверок при подготовке расширения **не запускались по инструкции пользователя**. Проверена структура контента: уникальность новых ID, корректность choice indexes, разрешение связей и наличие harness-файлов.

## Автоматический NuGet cache

`trainer/content/packages.json` закрепляет версии прямых зависимостей. Сейчас:

- Microsoft.EntityFrameworkCore.Sqlite **10.0.10**;
- Dapper **2.1.66**.

Только задания с `packages` запускают подготовку. Отдельный доверенный контейнер, не получающий пользовательский исходник, выполняет restore с nuget.org в volume **interview-lab-nuget**. Успешная комбинация пакетов отмечается marker-файлом; при повторных проверках cache используется без повторной загрузки. Запись сериализована file lock.

В контейнер исполнения этот volume монтируется **read-only**, `NUGET_PACKAGES=/nuget`, источники NuGet очищены, сеть отключена. Новый пакет/новая версия в manifest автоматически вызывает подготовку при первом использовании, без rebuild image. SDK/системные пакеты и native prerequisites по-прежнему относятся к образу. Для полной фиксации транзитивного графа дополнительно нужен lock-file workflow; текущий manifest фиксирует прямые версии.

Подготовка ограничена 240 секундами, runtime sandbox — прежними лимитами. Время холодного restore входит в таймер промежуточной проверки. При недоступном NuGet подготовка не считается неверным решением; показывается ошибка окружения и попытка остаётся активной, но дедлайн не продлевается. Подготовку и работу новых зависимостей пользователь проверяет в своём окружении.

Один раз подключить volume к существующему приложению без сборки:

```sh
docker compose up -d --no-build --no-deps --force-recreate app
```

Дальнейшее расширение контента — обновление страницы. Volume `progress` сохраняется; `docker compose down -v` удалит и прогресс, и NuGet cache, после чего пакеты потребуется скачать снова.
