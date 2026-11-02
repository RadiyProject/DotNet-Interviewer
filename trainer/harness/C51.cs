using Microsoft.Data.Sqlite;
using System.Text.Json;

var results = new List<object>();
bool controlPassed = true;
async Task Check(string name, string mode, bool shouldPass)
{
	Exception? failure = null;
	try { await Kata.Verify(connection => Transfer(connection, mode)); }
	catch (Exception ex) { failure = ex; }
	if (shouldPass && failure is not null) controlPassed = false;
	bool passed = shouldPass ? failure is null : controlPassed && failure is not null && failure is not NotImplementedException;
	results.Add(new { name, passed, detail = passed ? "" : !shouldPass && !controlPassed ? "Корректная реализация отвергнута; обнаружение дефектов не засчитывается" : failure is null ? "Дефект не обнаружен" : failure.GetType().Name + ": " + failure.Message });
}
async Task Transfer(SqliteConnection connection, string mode)
{
	if (mode == "always-fail") throw new InvalidOperationException("unconditional failure");
	await using var transaction = mode == "no-transaction" ? null : await connection.BeginTransactionAsync();
	try
	{
		if (mode == "credit-first")
		{
			await Run("UPDATE accounts SET balance = balance + 10 WHERE id = 2;");
			await Run("UPDATE accounts SET balance = balance - 10 WHERE id = 1;");
		}
		else
		{
			await Run("UPDATE accounts SET balance = balance - 10 WHERE id = 1;");
			if (mode != "skip-credit") await Run("UPDATE accounts SET balance = balance + 10 WHERE id = 2;");
		}
		if (mode != "skip-ledger") await Run("INSERT INTO ledger(id) VALUES ('transfer-1');");
		if (transaction is not null) await transaction.CommitAsync();
	}
	catch (SqliteException) when (mode == "swallow-error") { }
	finally { if (mode == "close-connection") await connection.CloseAsync(); }
	async Task Run(string sql)
	{
		await using var command = connection.CreateCommand();
		command.CommandText = sql;
		command.Transaction = (SqliteTransaction?)transaction;
		await command.ExecuteNonQueryAsync();
	}
}
await Check("Принимает корректный атомарный перевод", "correct", true);
await Check("Принимает credit-first атомарный перевод", "credit-first", true);
await Check("Ловит частичный commit без транзакции", "no-transaction", false);
await Check("Ловит проглоченный constraint failure", "swallow-error", false);
await Check("Ловит пропуск начисления", "skip-credit", false);
await Check("Ловит пропуск ledger", "skip-ledger", false);
await Check("Ловит безусловное исключение", "always-fail", false);
await Check("Ловит закрытие чужого соединения", "close-connection", false);
Console.WriteLine(JsonSerializer.Serialize(results));
