#!/usr/bin/env python3
"""Local Codex CLI bridge; host or dedicated container with its own authentication volume."""
import argparse
import base64
import hmac
import hashlib
import io
import json
import os
from pathlib import Path
import secrets
import shutil
import signal
import subprocess
import sys
import tempfile
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'trainer'))
from ai_protocol import INSTRUCTIONS, SCHEMAS, validate

LOCK = threading.Semaphore(1)
TOKEN = ''
ORIGIN = ''
CODEX = ''
AUTO_PAIR = False
TIMEOUT = 180
JOBS = {}
JOBS_LOCK = threading.Lock()
PROCESSES = set()
PROCESSES_LOCK = threading.Lock()
STOPPING = threading.Event()


def start_job(data):
    request_id = data.get('request_id')
    if not isinstance(request_id, str) or not 1 <= len(request_id) <= 100 or not all(c.isalnum() or c in '-_' for c in request_id):
        raise ValueError('A valid request_id is required')
    mode, context = data.get('mode'), data.get('context')
    if mode not in SCHEMAS or not isinstance(context, dict): raise ValueError('Unsupported request')
    digest = hashlib.sha256(json.dumps([mode, context], sort_keys=True).encode()).hexdigest()
    with JOBS_LOCK:
        for key in list(JOBS):
            if JOBS[key]['state'] in ('completed', 'failed') and time.time()-JOBS[key]['updated'] > 3600:
                del JOBS[key]
        previous = JOBS.get(request_id)
        if previous:
            if previous['digest'] != digest: raise ValueError('Request ID belongs to different data')
            if previous['state'] != 'failed' or data.get('retry') is not True:
                return {k:v for k,v in previous.items() if k != 'digest'}
        if any(j['state']=='running' for j in JOBS.values()): raise ValueError('Codex bridge is busy with another request. Wait for it to finish.')
        if len(JOBS) >= 20 and not previous: raise ValueError('Bridge result cache is full. Restart the bridge after saving completed results.')
        job = {'request_id':request_id, 'state':'running', 'updated':time.time(), 'digest':digest}
        JOBS[request_id] = job
    def worker():
        try:
            result = generate(mode, context)
            with JOBS_LOCK: job.update(state='completed', result=result, updated=time.time())
        except Exception as error:
            # Never retain raw CLI output, candidate inputs, or a traceback in the result cache.
            message = str(error) if isinstance(error, (ValueError, OSError, subprocess.SubprocessError)) else 'Unexpected bridge error; no result was saved.'
            with JOBS_LOCK: job.update(state='failed', error=message, updated=time.time())
    threading.Thread(target=worker, daemon=True).start()
    return {'request_id':request_id, 'state':'running'}

def cli_failure(logs, returncode):
    logs.seek(0, os.SEEK_END)
    size = logs.tell()
    logs.seek(max(0, size - 16000))
    text = logs.read().decode(errors='replace').lower()
    if 'unexpected argument' in text or 'unrecognized option' in text:
        return 'Codex CLI rejected a command-line option. The installed CLI is incompatible with the bridge command. Update the CLI or adapt the bridge flags.'
    if any(word in text for word in ('not logged in', 'authentication required', '401 unauthorized', 'refresh token')):
        return 'Codex CLI authentication failed. Run codex login on the host, then retry.'
    if any(word in text for word in ('usage limit', 'rate limit', 'quota exceeded', 'insufficient_quota')):
        return 'Codex CLI reported an account usage limit. Check account limits before retrying.'
    return 'Codex CLI exited with code '+str(returncode)+'. No result was saved. Check CLI login, model access and compatibility. Private CLI output is not exposed.'


def extract_pdf(encoded):
    raw = base64.b64decode(encoded, validate=True)
    if len(raw) > 2_000_000 or not raw.startswith(b'%PDF-'):
        raise ValueError('Expected a PDF no larger than 2 MB')
    try:
        from pypdf import PdfReader
        reader = PdfReader(io.BytesIO(raw))
        if len(reader.pages) > 30: raise ValueError('PDF exceeds 30 pages')
        text = '\n'.join((page.extract_text() or '') for page in reader.pages)
    except ImportError:
        executable = shutil.which('pdftotext')
        if not executable:
            raise ValueError('PDF extraction needs pypdf or pdftotext on the bridge host. Use TXT, Markdown or DOCX instead.')
        with tempfile.TemporaryDirectory(prefix='forge-resume-') as directory:
            source = Path(directory) / 'resume.pdf'
            target = Path(directory) / 'resume.txt'
            source.write_bytes(raw)
            subprocess.run([executable, '-f', '1', '-l', '30', '-layout', str(source), str(target)],
                           check=True, timeout=20, capture_output=True)
            text = target.read_text(errors='replace')
    if not text.strip(): raise ValueError('No PDF text found. OCR scanned PDFs separately or use a text file.')
    return text[:50000]


def generate(mode, context):
    if not CODEX: raise ValueError('Codex CLI is not installed or not in PATH. Install it and run codex login on this computer.')
    if mode not in SCHEMAS or not isinstance(context, dict): raise ValueError('Unsupported request')
    if not LOCK.acquire(blocking=False): raise ValueError('The bridge is busy; retry after the current request finishes.')
    try:
        with tempfile.TemporaryDirectory(prefix='forge-ai-') as directory:
            root = Path(directory)
            schema = root / 'response.schema.json'
            output = root / 'response.json'
            schema.write_text(json.dumps(SCHEMAS[mode]))
            prompt = INSTRUCTIONS + '\nMODE: ' + mode + '\nUNTRUSTED CONTEXT JSON:\n' + json.dumps(context, ensure_ascii=False)
            args = [CODEX, 'exec', '--ignore-user-config', '--ephemeral', '--skip-git-repo-check',
                    '--sandbox', 'read-only', '--color', 'never', '--cd', str(root),
                    '--output-schema', str(schema), '--output-last-message', str(output)]
            # Known stable flags; no shell, apps, plugins, hooks, browsers or subagents are needed.
            for feature in ['shell_tool', 'unified_exec', 'apps', 'plugins', 'hooks', 'browser_use', 'computer_use', 'code_mode_host']:
                args += ['--disable', feature]
            args += ['-c', 'agents.enabled=false', '-c', 'project_doc_max_bytes=0', '-c', 'web_search="disabled"']
            if os.environ.get('FORGE_CODEX_MODEL'): args += ['--model', os.environ['FORGE_CODEX_MODEL']]
            args += ['-']
            with tempfile.TemporaryFile() as logs:
                started = time.monotonic()
                print('Codex request started: mode='+mode, flush=True)
                proc = subprocess.Popen(args, stdin=subprocess.PIPE, stdout=logs, stderr=logs,
                                        cwd=root, start_new_session=(os.name != 'nt'))
                with PROCESSES_LOCK:
                    PROCESSES.add(proc)
                    stopping = STOPPING.is_set()
                if stopping:
                    if os.name != 'nt': os.killpg(proc.pid, signal.SIGKILL)
                    else: proc.kill()
                try:
                    proc.communicate(prompt.encode(), timeout=TIMEOUT)
                except subprocess.TimeoutExpired:
                    if os.name != 'nt': os.killpg(proc.pid, signal.SIGKILL)
                    else: proc.kill()
                    proc.wait()
                    raise ValueError('Codex CLI timed out. No interview result was committed; retry or use the external-assistant packet.')
                finally:
                    with PROCESSES_LOCK: PROCESSES.discard(proc)
                if proc.returncode or not output.is_file():
                    # Logs may contain personal prompt text: return a generic diagnosis, not the logs.
                    diagnosis = cli_failure(logs, proc.returncode)
                    print('Codex request failed after '+str(round(time.monotonic()-started))+'s: '+diagnosis, flush=True)
                    raise ValueError(diagnosis)
                if output.stat().st_size > 100000: raise ValueError('AI result is too large')
                result = json.loads(output.read_text())
                ids = [item['id'] for item in context.get('bank', []) if isinstance(item, dict) and 'id' in item]
                validated = validate(mode, result, ids)
                print('Codex request completed after '+str(round(time.monotonic()-started))+'s: mode='+mode, flush=True)
                return validated
    finally:
        LOCK.release()


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *args): pass
    def reply(self, data, status=200):
        raw = json.dumps(data, ensure_ascii=False).encode()
        self.send_response(status)
        if self.origin_allowed():
            self.send_header('Access-Control-Allow-Origin', self.headers.get('Origin'))
            self.send_header('Vary', 'Origin')
        self.send_header('Cache-Control', 'no-store')
        self.send_header('Content-Type', 'application/json; charset=utf-8')
        self.send_header('Content-Length', str(len(raw)))
        self.end_headers(); self.wfile.write(raw)
    def do_OPTIONS(self):
        if not self.origin_allowed(): return self.reply({'error': 'Origin rejected'}, 403)
        self.send_response(204)
        self.send_header('Access-Control-Allow-Origin', self.headers.get('Origin'))
        self.send_header('Access-Control-Allow-Methods', 'GET, POST, OPTIONS')
        self.send_header('Access-Control-Allow-Headers', 'Authorization, Content-Type')
        self.send_header('Access-Control-Allow-Private-Network', 'true')
        self.end_headers()
    def allowed(self):
        host = self.headers.get('Host', '').split(':')[0]
        return host in ('localhost', '127.0.0.1') and (self.headers.get('Origin') is None or self.origin_allowed()) and hmac.compare_digest(self.headers.get('Authorization', ''), 'Bearer ' + TOKEN)
    def origin_allowed(self):
        origin = self.headers.get('Origin')
        return origin == ORIGIN or AUTO_PAIR and origin in ('http://localhost:8080','http://127.0.0.1:8080')
    def do_GET(self):
        if not self.allowed(): return self.reply({'error': 'Bridge authorization failed'}, 403)
        if self.path.startswith('/jobs/'):
            with JOBS_LOCK:
                job = JOBS.get(self.path.removeprefix('/jobs/'))
                value = {k:v for k,v in job.items() if k != 'digest'} if job else None
            return self.reply(value if value else {'error':'Job not found. The bridge may have restarted.'}, 200 if value else 404)
        if self.path != '/health': return self.reply({'error': 'Not found'}, 404)
        with JOBS_LOCK: busy = any(j['state']=='running' for j in JOBS.values())
        self.reply({'available': bool(CODEX), 'provider': 'codex-cli', 'protocol':2, 'jobs':True, 'busy':busy, 'pdf': bool(shutil.which('pdftotext')) or bool(__import__('importlib.util').util.find_spec('pypdf')),
                    'message': 'Codex CLI found; authentication and model access are checked on the first request.' if CODEX else 'Install Codex CLI and run codex login on the host.'})
    def do_POST(self):
        if self.path == '/pair':
            host = self.headers.get('Host', '').split(':')[0]
            if not AUTO_PAIR or host not in ('localhost','127.0.0.1') or not self.origin_allowed() or self.headers.get('Content-Type','').split(';')[0]!='application/json':
                return self.reply({'error':'Automatic pairing is unavailable for this origin'},403)
            return self.reply({'token':TOKEN})
        if not self.allowed(): return self.reply({'error': 'Bridge authorization failed'}, 403)
        try:
            if self.headers.get('Content-Type', '').split(';')[0] != 'application/json': raise ValueError('JSON is required')
            size = int(self.headers.get('Content-Length', '0'))
            if not 0 < size <= 3_000_000: raise ValueError('Invalid request size')
            data = json.loads(self.rfile.read(size))
            if self.path == '/extract-pdf': return self.reply({'text': extract_pdf(data.get('base64', ''))})
            if self.path == '/jobs':
                if data.get('consent') is not True: raise ValueError('Explicit consent is required')
                return self.reply(start_job(data), 202)
            if self.path != '/generate': return self.reply({'error': 'Not found'}, 404)
            if data.get('consent') is not True: raise ValueError('Explicit consent to send the selected data through Codex is required')
            self.reply({'result': generate(data.get('mode'), data.get('context'))})
        except (ValueError, OSError, subprocess.SubprocessError) as error:
            self.reply({'error': str(error)}, 400)


def main():
    global TOKEN, ORIGIN, CODEX, TIMEOUT, AUTO_PAIR
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--port', type=int, default=8766)
    parser.add_argument('--origin', default='http://localhost:8080')
    parser.add_argument('--auto-pair', action='store_true', help='Allow automatic pairing only from the local trainer origins')
    parser.add_argument('--host', choices=['127.0.0.1','0.0.0.0'], default='127.0.0.1')
    args = parser.parse_args()
    if args.origin not in ('http://localhost:8080', 'http://127.0.0.1:8080'):
        parser.error('Use the local trainer origin on port 8080')
    ORIGIN = args.origin
    AUTO_PAIR = args.auto_pair
    CODEX = shutil.which(os.environ.get('FORGE_CODEX_BIN', 'codex'))
    TOKEN = secrets.token_urlsafe(32)
    print('Forge AI bridge: http://127.0.0.1:' + str(args.port), flush=True)
    print('Trainer origin: ' + ORIGIN, flush=True)
    if AUTO_PAIR: print('Automatic trainer pairing enabled; no token copying required.', flush=True)
    else: print('Pairing token (paste in the trainer; do not share): ' + TOKEN, flush=True)
    print('Codex CLI: ' + (CODEX or 'NOT FOUND — install it and run codex login'), flush=True)
    server = ThreadingHTTPServer((args.host, args.port), Handler)
    try: server.serve_forever()
    except KeyboardInterrupt: print('\nForge AI bridge stopped.', flush=True)
    finally:
        STOPPING.set()
        with PROCESSES_LOCK: processes = list(PROCESSES)
        for proc in processes:
            try:
                if proc.poll() is None:
                    if os.name != 'nt': os.killpg(proc.pid, signal.SIGKILL)
                    else: proc.kill()
            except ProcessLookupError: pass
        server.server_close()

if __name__ == '__main__': main()
