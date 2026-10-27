# Дополнение по двум публичным подборкам

27.10.2026: добавлены 16 упражнений. Всего 266: C# 44, PostgreSQL 24, SQLite 18, теория 122, review 58.

## Доступ и способ использования

Прочитаны доступная статья [ZTM](https://zerotomastery.io/blog/dot-net-interview-questions/) с 32 темами и [ASP.NET Core-файл репозитория aam9063](https://github.com/aam9063/dotnet-interview-questions/blob/master/dotnet_questions.md) с индексом из 75 вопросов. Отдельный csharp-_questions.md через web не дал читаемого содержимого; полнота его покрытия не заявляется. Это не полный импорт всех исходных вопросов. Совпадающие темы используют уже существующие упражнения, новые формулировки и кейсы написаны для тренажёра, подробные ответы из сторонних материалов не копируются.

Подборки не являются техническим первоисточником или доказательством частоты вопроса на интервью. В новых заданиях source ведёт на Microsoft Learn, references — на подборки тем. Утверждения о guaranteed performance или свойствах всех приложений не переносятся.

## Что добавлено

| Материал | Упражнения |
|---|---|
| Современные interfaces, unboxing, исполнение async и граница Enumerable/Queryable | Q111–Q114 |
| Configuration precedence, validation, PeriodicTimer | Q115–Q117 |
| Output cache, CSRF, JWT, overposting, HttpContext.Items | Q118–Q122 |
| Настоящий ConfigurationBuilder с двумя providers | C41, исполняемая C# kata |
| Permission middleware на DefaultHttpContext и claims identities | C42, исполняемая C# kata |
| Security review с негативными сценариями | A40, письменный разбор |
| Multi-tenant cache и configuration rollout | A41, письменный разбор |

C41 проверяет поведение configuration providers; использование конкретного API отдельно проверяется при review. C42 не заменяет стандартный authorization framework и не проверяет криптографию JWT: его вход уже прошёл authentication. Это явно указано в условиях. Зависимости доступны в existing ASP.NET Core shared framework, новые NuGet-пакеты не нужны.

## Что уже покрыто

| Группа тем источников | Существующие упражнения |
|---|---|
| DI, options, pipeline и worker lifecycle | T04, C16–C18, Q030–Q031, Q064–Q066 |
| Memory, records, Span/Memory, reflection, AOT и ValueTask | C07, C13, C22–C23, Q063, Q072–Q078, A27 |
| LINQ, EF tracking, raw SQL, concurrency, migrations | C09, C24–C26, C34–C35, Q013–Q015, Q027–Q029, Q090–Q091, A26 |
| Async streams, TAP и bounded work | C06, C11–C12, C32–C33, C38 |
| Caching, rate limits и system design | C10, C14–C15, Q038, Q068–Q069, A37 |
| Testing и integration boundaries | T08, A08, A28, C37, Q025–Q026, Q093 |
| Архитектурные паттерны и распределённые эффекты | T14–T15, A23–A25, A29–A30, A35–A38 |

Таблица отражает тематическое совпадение, а не один-к-одному покрытие каждой формулировки. API versioning, конкретные сторонние пакеты, MVC/Razor и Testcontainers deployment не объявляются полностью реализованными благодаря наличию общей архитектурной темы.

## Уточнения к упрощённым объяснениям

- Сам тип struct не определяет, что любое его значение находится только на stack; записи также не гарантируют deep immutability.
- У interfaces есть default implementations и static abstract members в современных версиях C#.
- Присваивание IQueryable переменной IEnumerable не выполняет запрос и не удаляет ранее построенный серверный фильтр.
- Task описывает завершение, не обязательную работу на thread pool. Обычный async-метод начинает работу синхронно, завершённый await не обязан приостанавливать выполнение.
- Общие рекомендации по кешированию, performance и AOT требуют условий и измерений; добавление инструмента само по себе не даёт гарантий.

Ссылки для проверки этих механизмов находятся непосредственно в соответствующих заданиях. Новые контракты, включая 401/403, порядок providers и отсутствие network runtime, открыты до проверки решения.

Проверены только структура банка, уникальность ID, индексы правильных вариантов, ссылки related и наличие harness-файлов. Сборка и тесты не запускались по инструкции пользователя.
