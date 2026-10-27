# Сопоставление: Stop Making This EF Core Transaction Mistake

27.10.2026. Видео: [Milan Jovanović, YouTube](https://www.youtube.com/watch?v=cUszBUYSFzw).

## Что удалось проверить

Прочитаны title и полное публичное описание YouTube, а также [статья автора, связанная из описания](https://www.milanjovanovic.tech/blog/working-with-transactions-in-ef-core). Автосубтитры заявлены в metadata, но текст получить не удалось: endpoint вернул пустой ответ. Аудиовизуальный ряд и полный transcript не проверены. Поэтому ниже сопоставлены доступные заявленные темы, а не каждая реплика или пример из ролика. Статья опубликована отдельно; она не выдаётся за расшифровку видео.

## Покрытие банка

| Тема | Что было | Что добавлено |
|---|---|---|
| Общая атомарность, outbox и transfer | T14, A35, Q104 | Привязка к EF API в Q153/Q155 и C46 |
| ExecuteUpdate и conditional writes | C25 | Q154: immediate execution и stale tracker; C46: общий rollback |
| EF raw SQL и параметры | C35 | A49: raw SQL не включается задним числом в поздний SaveChanges |
| Два SaveChanges в одной бизнес-операции | Общие transaction/design кейсы | Q155, A49, исполняемая failure kata C46 |
| Savepoints и состояние tracker | Отдельного вопроса не было | Q156, A49 |
| Retrying strategy + user transaction | Общие retry-вопросы | Q157, A49 |
| Неизвестный commit outcome | A35/Q103 на уровне API | Q158 на уровне EF и connection failure |

Вывод: существующий банк был релевантен, но не давал достаточно прямой практики границ EF-команд. Не нужно дублировать весь EF-раздел; добавлен целевой набор из 8 упражнений: Q153–Q158, C46, A49. Общий банк теперь 314.

## Практика и ограничения

C46 использует существующий Microsoft.EntityFrameworkCore.Sqlite 10.0.10 и реальную SQLite in-memory connection. Harness проверяет успешные debit+credit+audit, отказ после debit, отсутствие получателя, нехватку средств, pre-cancellation и ошибку INSERT через SQLite trigger. Это тесты продукта, подготовленные для решения пользователем; при работе над дополнением они не запускались.

Инвариант: при success в БД сохранены обе стороны и audit; при failure ни один из трёх эффектов не остаётся. В условии перечислены test hook, affected rows, null/argument errors, границы tracker ownership и cancellation. Использование конкретного bulk API частично требует review, поскольку behavior tests сами по себе не доказывают структуру реализации.

SQLite не проверяет MARS, PostgreSQL locking, реальную сетевую connection resiliency или неизвестный commit outcome. Эти части вынесены в вопросы и A49. No network side effect включён в C46: callback — только точка тестового отказа, не платёж или публикация.

## Важные уточнения

Атомарность SaveChanges действует при поддержке transactions и соответствующих настройках. При default WhenNeeded EF не обязан делать отдельный BeginTransaction для каждого одиночного statement. Read-before-write concurrency и атомарность сохранения — разные свойства. Общая DB transaction не refresh-ит tracker и не делает HTTP/broker participant той же атомарной операции.

Savepoints и retries рассматриваются с provider-specific ограничениями. Execution strategy должна повторять подходящую целую единицу; при потере связи на commit нельзя автоматически объявить rollback. Пример статьи с перехваченным исключением не принимается как универсальная рекомендация молча возвращать успех.

## Первоисточники

- [EF Core transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions).
- [ExecuteUpdate/ExecuteDelete](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete).
- [Connection resiliency](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency).

Проверены метаданные, related, индексы ответов и наличие harness. Сборка и тесты не запускались по инструкции пользователя. Изменения контента и runner harness подхватываются без Docker build.
