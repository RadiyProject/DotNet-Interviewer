using System.Text.Json;

var results = new List<object>();
bool controlPassed = true;
void Check(string name, string mode, bool shouldPass)
{
	Exception? failure = null;
	try { Kata.Verify(clock => new Store(clock, mode)); }
	catch (Exception ex) { failure = ex; }
	if (shouldPass && failure is not null) controlPassed = false;
	bool passed = shouldPass ? failure is null : controlPassed && failure is not null && failure is not NotImplementedException;
	results.Add(new { name, passed, detail = passed ? "" : !shouldPass && !controlPassed ? "Корректная реализация отвергнута; обнаружение дефектов не засчитывается" : failure is null ? "Дефект не обнаружен" : failure.GetType().Name + ": " + failure.Message });
}
Check("Принимает корректный absolute TTL", "correct", true);
Check("Ловит включённую границу истечения", "late-expiry", false);
Check("Ловит преждевременное истечение", "early-expiry", false);
Check("Ловит sliding вместо absolute TTL", "sliding", false);
Check("Ловит игнорирование переданных часов", "frozen-clock", false);
Check("Ловит удаление соседнего ключа", "clear-all", false);
Check("Ловит игнорирование перезаписи", "ignore-overwrite", false);
Console.WriteLine(JsonSerializer.Serialize(results));

sealed class Store(TimeProvider clock, string mode) : ITtlStore
{
	readonly Dictionary<string, (string Value, DateTimeOffset End, TimeSpan Ttl)> entries = new();
	readonly DateTimeOffset initial = clock.GetUtcNow();
	DateTimeOffset Now => mode == "frozen-clock" ? initial : clock.GetUtcNow();
	public void Put(string key, string value, TimeSpan ttl)
	{
		if (mode == "ignore-overwrite" && entries.ContainsKey(key)) return;
		entries[key] = (value, Now + ttl, ttl);
	}
	public string? Get(string key)
	{
		if (!entries.TryGetValue(key, out var entry)) return null;
		bool expired = mode == "late-expiry" ? Now > entry.End
			: mode == "early-expiry" ? Now >= entry.End - TimeSpan.FromTicks(1)
			: Now >= entry.End;
		if (expired)
		{
			if (mode == "clear-all") entries.Clear(); else entries.Remove(key);
			return null;
		}
		if (mode == "sliding") entries[key] = (entry.Value, Now + entry.Ttl, entry.Ttl);
		return entry.Value;
	}
}
