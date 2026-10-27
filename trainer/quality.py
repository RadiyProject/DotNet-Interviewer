"""Conservative static signals, NOT a compiler, proof of complexity or a quality grade."""
import hashlib
import re

def analyze(task, source):
    engine = task.get('engine')
    lines = source.splitlines()
    # Mask comments and string literals without moving line numbers.
    pattern = r'/\*[\s\S]*?\*/|//[^\n]*|--[^\n]*|@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\''
    if engine == 'csharp': pattern = pattern.replace(r'|--[^\n]*', '')
    masked = re.sub(pattern, lambda m: ''.join('\n' if ch == '\n' else ' ' for ch in m[0]), source)
    findings = []
    def add(rule, severity, match, en, ru):
        findings.append({'rule': rule, 'severity': severity, 'line': source.count('\n', 0, match.start()) + 1,
                         'message': {'en': en, 'ru': ru}, 'confidence': 'heuristic'})
    rules = []
    if engine == 'csharp':
        rules = [
            ('sync-over-async', 'important', r'\.Result\b|\.Wait\s*\(|GetAwaiter\s*\(\s*\)\s*\.GetResult\s*\(', 'Check whether this blocks asynchronous work. Explain cancellation and thread-pool impact.', 'Проверь блокировку асинхронной работы, отмену и влияние на thread pool.'),
            ('async-void', 'warning', r'\basync\s+void\b', 'Is this an event handler? Otherwise, how can the caller await it and observe failures?', 'Это обработчик события? Иначе как вызывающий дождётся завершения и увидит ошибку?'),
            ('httpclient-lifetime', 'warning', r'\bnew\s+HttpClient\s*\(', 'Explain HttpClient ownership and reuse; local test setup may legitimately construct it.', 'Объясни ownership и повторное использование HttpClient; в тестовой настройке создание может быть уместно.'),
            ('empty-catch', 'important', r'\bcatch\s*(?:\([^)]*\))?\s*\{\s*\}', 'Which failure is being swallowed, and how will the caller distinguish failure from success?', 'Какую ошибку скрывает catch и как вызывающий отличит сбой от успеха?'),
            ('throw-ex', 'warning', r'\bthrow\s+ex\s*;', 'Does rethrowing the exception variable preserve the original stack trace?', 'Сохраняется ли исходный stack trace при таком повторном выбрасывании?'),
            ('blocking-sleep', 'warning', r'\bThread\s*\.\s*Sleep\s*\(', 'Is blocking required by the contract, or should waiting support cancellation asynchronously?', 'Требует ли контракт блокирующее ожидание или нужно отменяемое асинхронное?')]
    elif engine in ('postgres', 'sqlite'):
        rules = [('select-star', 'info', r'\bSELECT\s+\*', 'Check the required output contract and whether an explicit projection would be clearer.', 'Сверь контракт вывода: будет ли явная проекция понятнее?'),
                 ('not-in-null', 'warning', r'\bNOT\s+IN\s*\(', 'Can this subquery contain NULL? Explain the resulting three-valued logic.', 'Может ли подзапрос вернуть NULL? Объясни результат трёхзначной логики.'),
                 ('offset-pagination', 'info', r'\bOFFSET\b', 'Discuss deterministic ordering, changing data and whether keyset pagination fits this task.', 'Обсуди детерминированный порядок, изменение данных и применимость keyset pagination.')]
    for rule, severity, expr, en, ru in rules:
        for match in list(re.finditer(expr, masked, re.I if engine != 'csharp' else 0))[:3]:
            add(rule, severity, match, en, ru)
    depth = peak = 0
    for ch in masked:
        if ch == '{': depth += 1; peak = max(peak, depth)
        elif ch == '}': depth = max(0, depth - 1)
    branches = len(re.findall(r'\b(if|for|foreach|while|case|catch)\b|&&|\|\|', masked))
    return {'source_hash': hashlib.sha256(source.encode()).hexdigest(), 'engine': engine,
            'metrics': {'lines': len(lines), 'nonblank_lines': sum(bool(line.strip()) for line in lines),
                        'branch_signals': branches, 'brace_depth': peak if engine == 'csharp' else None},
            'findings': findings[:20], 'score': None,
            'limitations': {'ru': 'Статические эвристики: возможны пропуски и ложные срабатывания. Это не оценка грейда, доказательство Big O или замена тестов/анализатора компилятора.',
                            'en': 'Static heuristics can miss issues or flag valid code. This is not a grade, a Big O proof, or a substitute for tests/compiler analysis.'}}
