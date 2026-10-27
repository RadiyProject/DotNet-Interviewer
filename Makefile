.DEFAULT_GOAL := help

COMPOSE := docker compose

.PHONY: help setup up down status logs restart recreate bridge-up bridge-restart bridge-logs codex-login codex-status

help:
	@printf '%s\n' \
	  'Команды запускаются из папки проекта:' \
	  '  make setup          Первый запуск или обновление образов: сборка и запуск' \
	  '  make up             Запуск всего проекта без сборки' \
	  '  make down           Остановка с сохранением прогресса и авторизации' \
	  '  make status         Состояние контейнеров' \
	  '  make logs           Логи приложения и Codex-моста (Ctrl+C — выход)' \
	  '  make restart        Перезапуск приложения без сборки' \
	  '  make recreate       Пересоздание приложения после изменения Compose без сборки' \
	  '  make bridge-up      Запуск только Codex-моста без сборки' \
	  '  make bridge-restart Перезапуск Codex-моста без сборки' \
	  '  make bridge-logs    Логи Codex-моста (Ctrl+C — выход)' \
	  '  make codex-login    Вход в Codex через адрес и код в браузере' \
	  '  make codex-status   Проверка авторизации Codex в контейнере' \
	  '' \
	  'Первый раз: make setup, затем make codex-login.' \
	  'Далее: make up. Тренажёр: http://localhost:8080' \
	  'Авторизация хранится в codex-auth; токен подключения сайт получает автоматически.'

setup:
	$(COMPOSE) up -d --build

up:
	$(COMPOSE) up -d --no-build

down:
	$(COMPOSE) down

status:
	$(COMPOSE) ps -a

logs:
	$(COMPOSE) logs --follow --tail=100 app ai-bridge

restart:
	$(COMPOSE) restart app

recreate:
	$(COMPOSE) up -d --no-build --no-deps --force-recreate app

bridge-up:
	$(COMPOSE) up -d --no-build ai-bridge

bridge-restart:
	$(COMPOSE) restart ai-bridge

bridge-logs:
	$(COMPOSE) logs --follow --tail=100 ai-bridge

codex-login: bridge-up
	$(COMPOSE) exec ai-bridge codex login --device-auth

codex-status:
	$(COMPOSE) exec ai-bridge codex login status
