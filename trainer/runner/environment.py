"""Writable per-container SDK state, separate from the read-only package cache."""
import os
from pathlib import Path


def configure_dotnet_environment():
    base = Path('/work/.sdk-state')
    paths = {
        'HOME': base / 'home',
        'DOTNET_CLI_HOME': base / 'cli',
        'XDG_DATA_HOME': base / 'data',
        'XDG_CACHE_HOME': base / 'cache',
        'XDG_RUNTIME_DIR': base / 'runtime',
        'TMPDIR': Path('/tmp'),
        'NUGET_HTTP_CACHE_PATH': base / 'http-cache',
        'NUGET_SCRATCH': base / 'scratch',
    }
    for key, path in paths.items():
        path.mkdir(parents=True, exist_ok=True, mode=0o700)
        if key == 'XDG_RUNTIME_DIR':
            path.chmod(0o700)
        os.environ[key] = str(path)
    os.environ.update(DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_NOLOGO='1',
                      DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1',
                      DOTNET_GENERATE_ASPNET_CERTIFICATE='false',
                      DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='false')


configure_dotnet_environment()
