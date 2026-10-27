FROM python:3.13-slim
RUN apt-get update && apt-get install -y --no-install-recommends docker-cli && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY trainer/server.py trainer/localization.py trainer/chatgpt_companion.py trainer/interview_builder.py trainer/mock_interviews.py trainer/ai_protocol.py trainer/quality.py ./
COPY trainer/content content
COPY trainer/harness harness
COPY trainer/web web
EXPOSE 8080
CMD ["python", "server.py"]
