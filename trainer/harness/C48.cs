using System.Net;
using System.Text.Json;

var results = new List<object>();
bool controlPassed = true;
async Task Check(string name, string mode, bool shouldPass)
{
	Exception? failure = null;
	try { await Kata.Verify((client, ct) => Fetch(client, ct, mode)); }
	catch (Exception ex) { failure = ex; }
	if (shouldPass && failure is not null) controlPassed = false;
	bool passed = shouldPass ? failure is null : controlPassed && failure is not null && failure is not NotImplementedException;
	results.Add(new { name, passed, detail = passed ? "" : !shouldPass && !controlPassed ? "Корректная реализация отвергнута; обнаружение дефектов не засчитывается" : failure is null ? "Дефект не обнаружен" : failure.GetType().Name + ": " + failure.Message });
}
async Task<string> Fetch(HttpClient client, CancellationToken token, string mode)
{
	try
	{
		var ct = mode == "ignore-cancel" ? CancellationToken.None : token;
		ct.ThrowIfCancellationRequested();
		int limit = mode == "two-attempts" ? 2 : mode == "four-attempts" ? 4 : 3;
		for (int attempt = 0; attempt < limit; attempt++)
		{
			using var response = await client.GetAsync("resource", ct);
			if (response.IsSuccessStatusCode)
				return mode == "wrong-body" ? "constant" : await response.Content.ReadAsStringAsync(ct);
			bool retry = mode == "retry-all" || response.StatusCode == HttpStatusCode.ServiceUnavailable;
			if (!retry || attempt == limit - 1) response.EnsureSuccessStatusCode();
		}
		throw new InvalidOperationException("Unreachable");
	}
	catch (OperationCanceledException) when (mode == "swallow-cancel") { return ""; }
	finally { if (mode == "dispose-client") client.Dispose(); }
}
await Check("Принимает корректное HTTP-поведение", "correct", true);
await Check("Ловит повтор после 400", "retry-all", false);
await Check("Ловит только две попытки", "two-attempts", false);
await Check("Ловит четвёртую попытку", "four-attempts", false);
await Check("Ловит игнорирование отмены", "ignore-cancel", false);
await Check("Ловит проглатывание отмены", "swallow-cancel", false);
await Check("Ловит подмену тела ответа", "wrong-body", false);
await Check("Ловит закрытие чужого HttpClient", "dispose-client", false);
Console.WriteLine(JsonSerializer.Serialize(results));
