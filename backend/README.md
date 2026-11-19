# GroupFinance — Backend (API)

ASP.NET Core 8 Web API em Clean Architecture (`Domain` → `Application` → `Infrastructure` / `API`), EF Core 8 + PostgreSQL.
Contratos: [`../docs/API_CONTRACTS.md`](../docs/API_CONTRACTS.md) · Decisões: [`../docs/DECISIONS.md`](../docs/DECISIONS.md).

## Pré-requisitos
- .NET SDK 8 (ver `global.json`).
- PostgreSQL 16 (local em `localhost:5432` ou `docker compose up -d db` na raiz).
- Ferramentas locais: `dotnet tool restore` (instala o `dotnet-ef` 8.x do manifesto `.config/dotnet-tools.json`).

## Comandos

```bash
cd backend
dotnet tool restore                 # dotnet-ef local
dotnet build                        # warnings são tratados como erros nos projetos src
dotnet test                         # unitários + integração (PostgreSQL real, ver abaixo)

# Executar (Development: appsettings.Development.json, Swagger em /swagger, migrations aplicadas na inicialização)
dotnet run --project src/FinanceApp.API          # http://localhost:5000  → GET /health

# Migrations (DbContext em Infrastructure, pasta Persistence/Migrations)
dotnet ef migrations add <Nome> -p src/FinanceApp.Infrastructure -s src/FinanceApp.API -o Persistence/Migrations
dotnet ef database update      -p src/FinanceApp.Infrastructure -s src/FinanceApp.API
dotnet ef migrations script --idempotent -p src/FinanceApp.Infrastructure -s src/FinanceApp.API -o migrate.sql
```

As ferramentas `dotnet ef` usam a variável `ConnectionStrings__Default` ou, na falta dela,
`Host=localhost;Port=5432;Database=groupfinance;Username=postgres;Password=postgres`.

## Testes de integração (D-015)
Usam `WebApplicationFactory<Program>` contra um **PostgreSQL real**. Conexão administrativa em
`TEST_DATABASE_CONNECTION` (padrão `Host=localhost;Port=5432;Username=postgres;Password=postgres`).
Cada execução cria um banco `groupfinance_test_<guid>`, aplica as migrations e o remove ao final.
O envio de e-mail é substituído por um fake que captura as mensagens; o rate limiting é desativado
(exceto no teste específico de 429).

## Configuração (variáveis de ambiente)

| Variável | Descrição |
|---|---|
| `ConnectionStrings__Default` | Connection string do PostgreSQL (obrigatória) |
| `Database__ApplyMigrationsOnStartup` | `true` aplica migrations ao iniciar (D-012). Em produção, prefira `dotnet ef database update`/script |
| `Jwt__Secret` | Segredo HS256, **≥ 32 caracteres** (a API não inicia sem ele) |
| `Jwt__Issuer`, `Jwt__Audience` | Emissor e audiência do JWT |
| `Jwt__AccessTokenMinutes` / `Jwt__RefreshTokenDays` | Validade do access token (padrão 15 min) e do refresh token (padrão 7 dias) |
| `App__FrontendBaseUrl` | URL do frontend usada no link de redefinição de senha |
| `App__PasswordResetTokenMinutes` | Validade do token de redefinição (padrão 60) |
| `App__TimeZone` | Fuso IANA da aplicação (padrão `America/Sao_Paulo`, D-021). Define "hoje" (movimentações vencidas, datas futuras, saldo projetado). Validado na inicialização; a imagem Docker inclui `tzdata` |
| `Cors__AllowedOrigins__0..n` | Origens permitidas (com credenciais). Vazio = nenhuma |
| `Smtp__Host`, `Smtp__Port`, `Smtp__User`, `Smtp__Password`, `Smtp__UseSsl`, `Smtp__From` | SMTP (Mailpit em desenvolvimento) |
| `RateLimiting__Enabled`, `RateLimiting__Auth__PermitLimit`, `RateLimiting__Auth__WindowSeconds`, `RateLimiting__Refresh__*` | Rate limiting por IP (padrão: 10/min em login/cadastro/esqueci/redefinir; 30/min em refresh) |
| `ForwardedHeaders__KnownNetworks__0..n` | Redes (CIDR) confiáveis para `X-Forwarded-For` (padrão: redes privadas, ex.: Nginx na rede Docker) |

`appsettings.json` não contém segredos. `appsettings.Development.json` contém um segredo JWT **apenas para desenvolvimento**.

## Endpoints (Etapa 1)
- `GET /health`
- `POST /api/v1/auth/register | login | refresh | logout | forgot-password | reset-password`
- `GET | PUT | DELETE /api/v1/users/me`, `PUT /api/v1/users/me/password`

Erros sempre em `application/problem+json` com `code` e `traceId` (D-008).

## Docker
`Dockerfile` multi-stage (sdk:8.0 → aspnet:8.0), usuário não-root, porta 8080, `curl` para o healthcheck.
Na raiz do projeto: `docker compose up --build`.
Observação: o build trata avisos do NuGet Audit como erro — uma vulnerabilidade recém-publicada em um pacote quebra o build até a atualização da versão em `Directory.Packages.props`.

## Notas de segurança
- Senhas: PBKDF2-HMAC-SHA512 (100k iterações) via `PasswordHasher` do Identity (D-007).
- Refresh token opaco (64 bytes) em cookie `gf_refresh` (HttpOnly, SameSite=Strict, Path=/api/v1/auth, Secure fora de Development); somente o hash SHA-256 é persistido; rotação a cada uso com detecção de reutilização (revoga a família).
- Concorrência otimista (`xmin`) em `refresh_tokens` e `password_reset_tokens` garante rotação/uso único sob requisições simultâneas.
- Os logs não registram corpo, query string, senhas ou tokens.

## Endpoints (Etapa 2) — ver `../docs/API_CONTRACTS.md` §6–9
- Contas: `GET | POST /api/v1/accounts`, `GET | PUT /api/v1/accounts/{id}`, `POST .../deactivate | activate`, `GET | POST .../balance-snapshots`
- Categorias: `GET | POST /api/v1/categories`, `PUT /api/v1/categories/{id}`, `POST .../deactivate | activate`
- Movimentações: `GET | POST /api/v1/transactions`, `GET | PUT | DELETE /api/v1/transactions/{id}`, `POST .../cancel | reopen`, `GET .../history`, `POST .../payments`, `DELETE .../payments/{paymentId}`
- Dashboard: `GET /api/v1/dashboard?from=&to=`

Regras no domínio: `Transaction` (máquina de estados, `docs/STATE_MACHINE.md`), `Account.RecordBalance`, `Category`;
saldo projetado em `Application/Accounts/ProjectedBalanceCalculator` (D-019). A migration `Etapa2_FinancialCore` cria as tabelas,
as constraints de integridade, os índices únicos por expressão (`lower(name)`) e o seed das 14 categorias padrão (IDs fixos).
