using System.Text.Json;

var results = new List<object>();
bool controlPassed = true;
void Check(string name, Func<int[], int[]> sort, bool shouldPass)
{
	Exception? failure = null;
	try { Kata.Verify(sort); }
	catch (Exception ex) { failure = ex; }
	if (shouldPass && failure is not null) controlPassed = false;
	bool passed = shouldPass ? failure is null : controlPassed && failure is not null && failure is not NotImplementedException;
	results.Add(new { name, passed, detail = passed ? "" : !shouldPass && !controlPassed ? "Корректная реализация отвергнута; обнаружение дефектов не засчитывается" : failure is null ? "Дефект не обнаружен" : failure.GetType().Name + ": " + failure.Message });
}
Check("Принимает корректную LINQ-сортировку", xs => xs.OrderBy(x => x).ToArray(), true);
Check("Принимает сортировку копии массива", xs => { var copy = (int[])xs.Clone(); Array.Sort(copy); return copy; }, true);
Check("Принимает reuse отсортированного входа", xs => xs.SequenceEqual(xs.OrderBy(x => x)) ? xs : xs.OrderBy(x => x).ToArray(), true);
Check("Ловит несортированный результат", xs => (int[])xs.Clone(), false);
Check("Ловит удалённые дубликаты", xs => xs.Distinct().OrderBy(x => x).ToArray(), false);
Check("Ловит потерю элемента", xs => xs.OrderBy(x => x).Skip(1).ToArray(), false);
Check("Ловит обратный порядок", xs => xs.OrderByDescending(x => x).ToArray(), false);
Check("Ловит изменение входа", xs => { Array.Sort(xs); return xs; }, false);
Check("Ловит отказ на пустом массиве", xs => { if (xs.Length == 0) throw new ArgumentException("empty"); return xs.OrderBy(x => x).ToArray(); }, false);
Check("Ловит потерю крайних int", xs => xs.Where(x => x != int.MinValue && x != int.MaxValue).OrderBy(x => x).ToArray(), false);
Console.WriteLine(JsonSerializer.Serialize(results));
