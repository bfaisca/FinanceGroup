# GroupFinance

Gestão financeira **pessoal** com, como diferencial, **grupos financeiros** para consolidar despesas e receitas de várias pessoas — com transparência, sem divisão de dívidas.

- Backend: .NET 8 · ASP.NET Core · EF Core · PostgreSQL · Clean Architecture
- Frontend: Vue 3 · TypeScript · Vite · Pinia · PrimeVue 4 · ECharts
- Infra: Docker Compose (PostgreSQL, API, Web, Mailpit)

## Início rápido

```bash
cp .env.example .env        # ajuste POSTGRES_PASSWORD e JWT_SECRET
docker compose up --build
```

| Serviço | URL / Porta | Variável |
|---|---|---|
| Aplicação web | http://localhost:8080 | `WEB_PORT` |
| API (Swagger em Development) | http://localhost:5000/swagger | `API_PORT` |
| Health check | http://localhost:5000/health | — |
| Mailpit (e-mails de dev) | http://localhost:8025 | `MAILPIT_UI_PORT` |
| PostgreSQL | localhost:5432 | `DB_PORT` |

Os dados do PostgreSQL ficam no volume `groupfinance_pgdata` e persistem entre reinicializações (`docker compose down` mantém; `docker compose down -v` apaga).

As migrations são aplicadas automaticamente na inicialização da API em desenvolvimento (`Database__ApplyMigrationsOnStartup=true`).

Para gerar um `JWT_SECRET`: `openssl rand -base64 48`.

## Desenvolvimento sem Docker (hot reload)

```bash
docker compose up -d db mailpit           # só banco e e-mail
cd backend && dotnet run --project src/FinanceApp.API      # API em http://localhost:5000
cd frontend && npm install && npm run dev                  # Web em http://localhost:5173 (proxy /api → :5000)
```

## Testes

```bash
cd backend && dotnet test        # requer PostgreSQL acessível (TEST_DATABASE_CONNECTION)
cd frontend && npm run test -- --run
```

## Bot do Telegram (opcional)

Permite lançar despesas pessoais enviando, por exemplo, `45,90 mercado` ao bot e escolhendo a categoria (D-053/D-054, contrato §18).

1. No Telegram, converse com o **@BotFather**, use `/newbot` e guarde o **token** e o **nome de usuário** do bot.
2. Defina as variáveis `TELEGRAM_BOT_TOKEN`, `TELEGRAM_WEBHOOK_SECRET` (16+ caracteres: letras, números, `_`, `-`) e `TELEGRAM_BOT_USERNAME` (sem `@`). No Render, são `Telegram__BotToken`, `Telegram__WebhookSecret` e `Telegram__BotUsername`.
3. Registre o webhook (uma vez; a URL precisa ser HTTPS e pública):
   ```bash
   curl -X POST "https://api.telegram.org/bot<TOKEN>/setWebhook" \
     -d "url=https://<url-da-api>/api/v1/telegram/webhook" \
     -d "secret_token=<TELEGRAM_WEBHOOK_SECRET>"
   ```
4. No site, abra **Configurações → Telegram**, gere o código e envie `/vincular CÓDIGO` ao bot.

## Documentação

- [`CLAUDE.md`](CLAUDE.md) — regras arquiteturais e de negócio
- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — arquitetura, pastas e estrutura do banco
- [`docs/API_CONTRACTS.md`](docs/API_CONTRACTS.md) — contrato frontend ↔ backend
- [`docs/DECISIONS.md`](docs/DECISIONS.md) — registro de decisões
- [`docs/PLAN.md`](docs/PLAN.md) — plano por etapas e status
- [`backend/README.md`](backend/README.md) · [`frontend/README.md`](frontend/README.md)
