FROM node:22-bookworm-slim
RUN apt-get update && apt-get install -y --no-install-recommends python3 ca-certificates \
    && rm -rf /var/lib/apt/lists/* \
    && npm install --global @openai/codex@0.163.0 \
    && mkdir -p /home/node/.codex && chown node:node /home/node/.codex
WORKDIR /workspace
COPY tools/ai_bridge.py tools/ai_bridge.py
COPY trainer/ai_protocol.py trainer/ai_protocol.py
ENV CODEX_HOME=/home/node/.codex
USER node
EXPOSE 8766
CMD ["python3", "tools/ai_bridge.py", "--auto-pair", "--host", "0.0.0.0"]
