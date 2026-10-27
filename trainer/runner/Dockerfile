FROM mcr.microsoft.com/dotnet/sdk:10.0
RUN apt-get update && apt-get install -y --no-install-recommends python3 postgresql && rm -rf /var/lib/apt/lists/* && useradd -u 10001 -m runner
COPY trainer/runner/runner.py /runner.py
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_CLI_HOME=/tmp DOTNET_NOLOGO=1
USER 10001:10001
WORKDIR /work
ENTRYPOINT ["python3", "/runner.py"]
