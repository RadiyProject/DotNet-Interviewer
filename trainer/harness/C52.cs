using System.Text.Json;
var results = new List<object>();
void Check(string name, Action test)
{
	try { test(); results.Add(new { name, passed = true, detail = "" }); }
	catch (Exception ex) { results.Add(new { name, passed = false, detail = ex.Message }); }
}
void Eq(int expected, int actual) { if (expected != actual) throw new Exception($"Expected {expected}, got {actual}"); }
void Throws(Action action)
{
	try { action(); } catch (ArgumentOutOfRangeException) { return; }
	throw new Exception("Expected ArgumentOutOfRangeException");
}
Check("Первая задержка и удвоение", () => {
	Eq(50, Kata.BackoffMs(1, 100, 1000, 0.5));
	Eq(100, Kata.BackoffMs(2, 100, 1000, 0.5));
	Eq(200, Kata.BackoffMs(3, 100, 1000, 0.5));
	Eq(400, Kata.BackoffMs(4, 100, 1000, 0.5));
});
Check("Cap применяется до jitter", () => {
	Eq(500, Kata.BackoffMs(5, 100, 1000, 0.5));
	Eq(125, Kata.BackoffMs(20, 100, 1000, 0.125));
	Eq(10, Kata.BackoffMs(1, 1000, 40, 0.25));
});
Check("Ноль и округление вниз", () => {
	Eq(0, Kata.BackoffMs(20, 100, 1000, 0));
	Eq(1, Kata.BackoffMs(1, 3, 10, 0.5));
	Eq(9, Kata.BackoffMs(5, 2, 10, 0.999));
});
Check("Большой номер retry без overflow", () => {
	Eq(500, Kata.BackoffMs(int.MaxValue, 1, 1000, 0.5));
	Eq(1073741823, Kata.BackoffMs(32, int.MaxValue, int.MaxValue, 0.5));
});
Check("Аргументы задержки", () => {
	Throws(() => Kata.BackoffMs(0, 1, 1, 0.5));
	Throws(() => Kata.BackoffMs(-1, 1, 1, 0.5));
	Throws(() => Kata.BackoffMs(1, 0, 1, 0.5));
	Throws(() => Kata.BackoffMs(1, 1, 0, 0.5));
	Throws(() => Kata.BackoffMs(1, -1, 10, 0.5));
});
Check("Аргументы jitter", () => {
	foreach (var u in new[] { -0.1, 1.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
		Throws(() => Kata.BackoffMs(1, 1, 10, u));
});
Console.WriteLine(JsonSerializer.Serialize(results));
