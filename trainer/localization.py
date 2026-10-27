"""Offline English content overlays; grading and persistence keep canonical IDs."""
import json
import re
from functools import lru_cache
from pathlib import Path

ROOT = Path(__file__).parent
CHUNK = re.compile(r'[\u0400-\u04ff][\u0400-\u04ffA-Za-z0-9 \t·…:!?.,;()«»≥≤%→/=+–—\-#]*')


@lru_cache(maxsize=8)
def _read(path, stamp):
    return json.loads(Path(path).read_text())


def read_json(path):
    path = Path(path)
    return _read(str(path), path.stat().st_mtime_ns)


def localize_task(task, language):
    if not task or language != 'en':
        return task
    overlay = read_json(ROOT / 'content/tasks.en.json').get(task['id'])
    if overlay is None:
        raise ValueError('English content is missing for task ' + task['id'])
    # Answer indices, fixtures, task IDs and code harnesses are never translated.
    return {**task, **overlay}


def translate_text(value, language, dictionary=None):
    if language != 'en' or not isinstance(value, str) or not CHUNK.search(value):
        return value
    if dictionary is None:
        dictionary = read_json(ROOT / 'content/translations.en.json')
    if value in dictionary:
        return dictionary[value]
    return CHUNK.sub(lambda match: dictionary.get(match[0], match[0]), value)


def localize_payload(value, language):
    if language != 'en':
        return value
    dictionary = read_json(ROOT / 'content/translations.en.json')
    def visit(item):
        if isinstance(item, str):
            return translate_text(item, language, dictionary)
        if isinstance(item, list):
            return [visit(child) for child in item]
        if isinstance(item, dict):
            # User source, persisted drafts and structural identifiers are verbatim.
            return {key: child if key in ('code', 'drafts', 'starter', 'id', 'task_id', 'tags', 'cat')
                    else visit(child) for key, child in item.items()}
        return item
    return visit(value)
