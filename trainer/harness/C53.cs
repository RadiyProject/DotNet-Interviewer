using System.Text.Json;
var results = new List<object>();
void Check(string name, Action test)
{
	try { test(); results.Add(new { name, passed = true, detail = "" }); }
	catch (Exception ex) { results.Add(new { name, passed = false, detail = ex.Message }); }
}
void Eq(int expected, int actual) { if (expected != actual) throw new Exception($"Expected {expected}, got {actual}"); }
void Failure(CircuitBreaker breaker, Exception error)
{
	try { breaker.Execute(() => throw error); }
	catch (Exception ex) when (ReferenceEquals(ex, error)) { return; }
	throw new Exception("Original callback exception must propagate");
}
void Blocked(CircuitBreaker breaker)
{
	int calls = 0;
	try { breaker.Execute(() => { calls++; return 1; }); }
	catch (CircuitOpenException) { Eq(0, calls); return; }
	throw new Exception("Expected CircuitOpenException");
}
var start = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
Check("Успех возвращает результат", () => {
	var b = new CircuitBreaker(2, TimeSpan.FromSeconds(5), () => start);
	Eq(-7, b.Execute(() => -7));
});
Check("Порог и исходное исключение", () => {
	var b = new CircuitBreaker(3, TimeSpan.FromSeconds(5), () => start);
	for (int i = 0; i < 3; i++) Failure(b, new InvalidOperationException("failure-" + i));
	Blocked(b);
});
Check("Успех сбрасывает consecutive failures", () => {
	var b = new CircuitBreaker(2, TimeSpan.FromSeconds(5), () => start);
	Failure(b, new Exception("first")); Eq(1, b.Execute(() => 1));
	Failure(b, new Exception("second")); Eq(2, b.Execute(() => 2));
});
Check("Open не продлевается отказанными вызовами", () => {
	var now = start;
	var b = new CircuitBreaker(1, TimeSpan.FromSeconds(5), () => now);
	Failure(b, new Exception("open"));
	now += TimeSpan.FromSeconds(4); Blocked(b);
	now += TimeSpan.FromSeconds(1); Eq(9, b.Execute(() => 9));
});
Check("Проба на точной границе cooldown", () => {
	var now = start;
	var b = new CircuitBreaker(1, TimeSpan.FromTicks(10), () => now);
	Failure(b, new Exception("open"));
	now += TimeSpan.FromTicks(9); Blocked(b);
	now += TimeSpan.FromTicks(1); Eq(3, b.Execute(() => 3));
	Eq(4, b.Execute(() => 4));
});
Check("Неудачная проба снова открывает circuit", () => {
	var now = start;
	var b = new CircuitBreaker(3, TimeSpan.FromSeconds(5), () => now);
	for (int i = 0; i < 3; i++) Failure(b, new Exception("closed failure"));
	now += TimeSpan.FromSeconds(5); Failure(b, new Exception("probe"));
	now += TimeSpan.FromSeconds(4); Blocked(b);
	now += TimeSpan.FromSeconds(1); Eq(8, b.Execute(() => 8));
	Failure(b, new Exception("after recovery")); Eq(9, b.Execute(() => 9));
});
Check("Независимость экземпляров", () => {
	var a = new CircuitBreaker(1, TimeSpan.FromSeconds(5), () => start);
	var b = new CircuitBreaker(1, TimeSpan.FromSeconds(5), () => start);
	Failure(a, new Exception("a")); Blocked(a); Eq(10, b.Execute(() => 10));
});
Check("Большой cooldown без переполнения даты", () => {
	var now = start;
	var b = new CircuitBreaker(1, TimeSpan.MaxValue, () => now);
	Failure(b, new Exception("open"));
	now += TimeSpan.FromDays(10); Blocked(b);
});
Check("Cooldown начинается с момента отказа", () => {
	var now = start;
	var b = new CircuitBreaker(1, TimeSpan.FromSeconds(5), () => now);
	var error = new Exception("slow failure");
	try { b.Execute(() => { now += TimeSpan.FromSeconds(10); throw error; }); }
	catch (Exception ex) when (ReferenceEquals(ex, error)) { }
	Blocked(b);
	now += TimeSpan.FromSeconds(5); Eq(1, b.Execute(() => 1));
});
Check("Аргументы", () => {
	void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
	Throws<ArgumentOutOfRangeException>(() => new CircuitBreaker(0, TimeSpan.FromSeconds(1), () => start));
	Throws<ArgumentOutOfRangeException>(() => new CircuitBreaker(1, TimeSpan.Zero, () => start));
	Throws<ArgumentOutOfRangeException>(() => new CircuitBreaker(1, TimeSpan.FromTicks(-1), () => start));
	Throws<ArgumentNullException>(() => new CircuitBreaker(1, TimeSpan.FromSeconds(1), null!));
	var b = new CircuitBreaker(1, TimeSpan.FromSeconds(1), () => start);
	Throws<ArgumentNullException>(() => b.Execute(null!));
});
Console.WriteLine(JsonSerializer.Serialize(results));
