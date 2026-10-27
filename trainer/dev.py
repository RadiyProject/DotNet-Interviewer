"""Reload the stdlib API on saved source changes, without image rebuilds or packages."""
import signal
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).parent
stopping = False


def stop_requested(signum, frame):
    global stopping
    stopping = True


def fingerprint():
    # Content, web files, harnesses and runner.py are read per request already.
    return tuple((p.name, p.stat().st_mtime_ns, p.stat().st_size)
                 for p in sorted(ROOT.glob('*.py')))


def stop_child(child):
    if child.poll() is None:
        child.terminate()
        try:
            child.wait(timeout=10)
        except subprocess.TimeoutExpired:
            child.kill()
            child.wait()


signal.signal(signal.SIGTERM, stop_requested)
signal.signal(signal.SIGINT, stop_requested)
child = None
try:
    previous = fingerprint()
    child = subprocess.Popen([sys.executable, '-u', str(ROOT / 'server.py')], cwd=ROOT)
    while not stopping:
        time.sleep(0.7)
        current = fingerprint()
        if current != previous:
            previous = current
            print('Python source changed; restarting API.', flush=True)
            stop_child(child)
            child = subprocess.Popen([sys.executable, '-u', str(ROOT / 'server.py')], cwd=ROOT)
finally:
    if child is not None:
        stop_child(child)
