using System.Text.Json;

var results = new List<object>();
async Task Check(string name, Func<Task> test)
{
	try { await test().WaitAsync(TimeSpan.FromSeconds(2)); results.Add(new { name, passed = true, detail = "" }); }
	catch (Exception ex) { results.Add(new { name, passed = false, detail = ex.Message }); }
}
void Eq<T>(T expected, T actual)
{
	if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}; got {actual}");
}
void Throws<T>(Action action) where T : Exception
{
	try { action(); } catch (T) { return; }
	throw new Exception($"Expected {typeof(T).Name}");
}
async Task ThrowsAsync<T>(Func<Task> action) where T : Exception
{
	try { await action().WaitAsync(TimeSpan.FromSeconds(1)); } catch (T) { return; }
	throw new Exception($"Expected {typeof(T).Name}");
}
TaskCompletionSource<bool> Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
var ttl = TimeSpan.FromMinutes(5);
var wait = TimeSpan.FromSeconds(30);

await Check("Validation", async () =>
{
	var clock = new ManualClock();
	Func<string, CancellationToken, Task<decimal?>> load = (_, _) => Task.FromResult<decimal?>(1m);
	Throws<ArgumentNullException>(() => new AsyncRateCache(null!, clock, ttl, wait));
	Throws<ArgumentNullException>(() => new AsyncRateCache(load, null!, ttl, wait));
	Throws<ArgumentOutOfRangeException>(() => new AsyncRateCache(load, clock, TimeSpan.Zero, wait));
	Throws<ArgumentOutOfRangeException>(() => new AsyncRateCache(load, clock, ttl, TimeSpan.FromTicks(-1)));
	var cache = new AsyncRateCache(load, clock, ttl, wait);
	await ThrowsAsync<ArgumentNullException>(() => cache.GetAsync(null!));
	foreach (var key in new[] { "", " ", "EU", "EURO", "EU1", "ЕUR" })
		await ThrowsAsync<ArgumentException>(() => cache.GetAsync(key));
});
await Check("Normalization before loading and cache lookup", async () =>
{
	int calls = 0;
	var cache = new AsyncRateCache((key, _) => { Eq("EUR", key); calls++; return Task.FromResult<decimal?>(1.25m); }, new ManualClock(), ttl, wait);
	Eq<decimal?>(1.25m, await cache.GetAsync(" eur "));
	Eq<decimal?>(1.25m, await cache.GetAsync("EUR"));
	Eq(1, calls);
});
await Check("Fresh hit and exact TTL boundary", async () =>
{
	var clock = new ManualClock(); int calls = 0;
	var cache = new AsyncRateCache((_, _) => Task.FromResult<decimal?>(++calls), clock, ttl, wait);
	Eq<decimal?>(1m, await cache.GetAsync("EUR"));
	clock.Advance(ttl - TimeSpan.FromTicks(1));
	Eq<decimal?>(1m, await cache.GetAsync("EUR"));
	clock.Advance(TimeSpan.FromTicks(1));
	Eq<decimal?>(2m, await cache.GetAsync("EUR"));
	Eq(2, calls);
});
await Check("TTL starts after loader completion", async () =>
{
	var clock = new ManualClock(); int calls = 0;
	var cache = new AsyncRateCache((_, _) => { calls++; clock.Advance(TimeSpan.FromMinutes(2)); return Task.FromResult<decimal?>(3m); }, clock, ttl, wait);
	await cache.GetAsync("EUR"); clock.Advance(TimeSpan.FromMinutes(4));
	Eq<decimal?>(3m, await cache.GetAsync("EUR")); Eq(1, calls);
});
await Check("Same normalized key: coordinated successful refresh", async () =>
{
	var entered = Signal(); var release = Signal(); int calls = 0;
	var cache = new AsyncRateCache(async (key, _) => { Eq("EUR", key); Interlocked.Increment(ref calls); entered.TrySetResult(true); await release.Task; return 7m; }, new ManualClock(), ttl, wait);
	var owner = cache.GetAsync("eur");
	try
	{
		await entered.Task.WaitAsync(TimeSpan.FromSeconds(1));
		var followers = Enumerable.Range(0, 12).Select(i => cache.GetAsync(i % 2 == 0 ? " EUR " : "eur")).ToArray();
		release.TrySetResult(true);
		foreach (var value in await Task.WhenAll(followers.Append(owner))) Eq<decimal?>(7m, value);
		Eq(1, calls);
	}
	finally { release.TrySetResult(true); }
});
await Check("Different keys are independent", async () =>
{
	var entered = Signal(); var release = Signal();
	var cache = new AsyncRateCache(async (key, _) => { if (key == "EUR") { entered.TrySetResult(true); await release.Task; } return key == "EUR" ? 1m : 2m; }, new ManualClock(), ttl, wait);
	var eur = cache.GetAsync("EUR");
	try { await entered.Task.WaitAsync(TimeSpan.FromSeconds(1)); Eq<decimal?>(2m, await cache.GetAsync("JPY").WaitAsync(TimeSpan.FromSeconds(1))); }
	finally { release.TrySetResult(true); }
	Eq<decimal?>(1m, await eur);
});
await Check("Zero wait timeout does not release another caller's permit", async () =>
{
	var entered = Signal(); var release = Signal(); int calls = 0;
	var cache = new AsyncRateCache(async (_, _) => { Interlocked.Increment(ref calls); entered.TrySetResult(true); await release.Task; return 8m; }, new ManualClock(), ttl, TimeSpan.Zero);
	var owner = cache.GetAsync("EUR");
	try
	{
		await entered.Task.WaitAsync(TimeSpan.FromSeconds(1));
		await ThrowsAsync<TimeoutException>(() => cache.GetAsync("EUR"));
		await ThrowsAsync<TimeoutException>(() => cache.GetAsync("eur"));
		Eq(1, calls);
	}
	finally { release.TrySetResult(true); }
	Eq<decimal?>(8m, await owner); Eq<decimal?>(8m, await cache.GetAsync("EUR"));
});
await Check("Canceled waiter does not cancel the owner", async () =>
{
	var entered = Signal(); var release = Signal();
	var cache = new AsyncRateCache(async (_, ct) => { entered.TrySetResult(true); await release.Task.WaitAsync(ct); return 9m; }, new ManualClock(), ttl, wait);
	var owner = cache.GetAsync("EUR"); using var cts = new CancellationTokenSource();
	try
	{
		await entered.Task.WaitAsync(TimeSpan.FromSeconds(1));
		var follower = cache.GetAsync("eur", cts.Token); cts.Cancel();
		await ThrowsAsync<OperationCanceledException>(() => follower);
	}
	finally { release.TrySetResult(true); }
	Eq<decimal?>(9m, await owner); Eq<decimal?>(9m, await cache.GetAsync("EUR"));
});
await Check("Pre-canceled token is honored even on a fresh hit", async () =>
{
	int calls = 0;
	var cache = new AsyncRateCache((_, _) => { calls++; return Task.FromResult<decimal?>(1m); }, new ManualClock(), ttl, wait);
	await cache.GetAsync("EUR"); using var cts = new CancellationTokenSource(); cts.Cancel();
	await ThrowsAsync<OperationCanceledException>(() => cache.GetAsync("EUR", cts.Token)); Eq(1, calls);
});
await Check("Owner cancellation reaches the loader and permits retry", async () =>
{
	var entered = Signal(); int calls = 0;
	var cache = new AsyncRateCache(async (_, ct) => { if (Interlocked.Increment(ref calls) == 1) { entered.TrySetResult(true); await Task.Delay(Timeout.Infinite, ct); } return 4m; }, new ManualClock(), ttl, wait);
	using var cts = new CancellationTokenSource(); var owner = cache.GetAsync("EUR", cts.Token);
	await entered.Task.WaitAsync(TimeSpan.FromSeconds(1)); cts.Cancel();
	await ThrowsAsync<OperationCanceledException>(() => owner);
	Eq<decimal?>(4m, await cache.GetAsync("EUR")); Eq(2, calls);
});
await Check("Canceled result is not published even if loader ignores cancellation", async () =>
{
	using var cts = new CancellationTokenSource(); int calls = 0;
	var cache = new AsyncRateCache((_, _) => { if (++calls == 1) cts.Cancel(); return Task.FromResult<decimal?>(calls); }, new ManualClock(), ttl, wait);
	await ThrowsAsync<OperationCanceledException>(() => cache.GetAsync("EUR", cts.Token));
	Eq<decimal?>(2m, await cache.GetAsync("EUR")); Eq(2, calls);
});
await Check("Null and loader failures are not cached", async () =>
{
	int calls = 0;
	var cache = new AsyncRateCache((_, _) => { calls++; if (calls == 1) return Task.FromResult<decimal?>(null); if (calls == 2) throw new InvalidOperationException("provider failure"); return Task.FromResult<decimal?>(6m); }, new ManualClock(), ttl, wait);
	Eq<decimal?>(null, await cache.GetAsync("EUR"));
	await ThrowsAsync<InvalidOperationException>(() => cache.GetAsync("EUR"));
	Eq<decimal?>(6m, await cache.GetAsync("EUR")); Eq<decimal?>(6m, await cache.GetAsync("EUR")); Eq(3, calls);
});
await Check("Expired data is not a silent fallback on failure", async () =>
{
	var clock = new ManualClock(); int calls = 0;
	var cache = new AsyncRateCache((_, _) => { if (++calls == 2) throw new InvalidOperationException("offline"); return Task.FromResult<decimal?>(calls); }, clock, ttl, wait);
	await cache.GetAsync("EUR"); clock.Advance(ttl);
	await ThrowsAsync<InvalidOperationException>(() => cache.GetAsync("EUR"));
	Eq<decimal?>(3m, await cache.GetAsync("EUR"));
});
Console.WriteLine(JsonSerializer.Serialize(results));

sealed class ManualClock : TimeProvider
{
	private long ticks = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).Ticks;
	public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref ticks), TimeSpan.Zero);
	public void Advance(TimeSpan value) => Interlocked.Add(ref ticks, value.Ticks);
}
