# Arquitetura

## 1. Visão geral

```text
┌──────────────┐  /api (proxy)  ┌──────────────────┐   EF Core   ┌─────────────┐
│ web (Nginx)  │ ─────────────▶ │ api (ASP.NET 8)  │ ──────────▶ │ PostgreSQL  │
│ Vue 3 SPA    │                │ Clean Arch.      │             │ 16 (volume) │
└──────────────┘                └──────────────────┘             └─────────────┘
                                         │ SMTP
                                         ▼
                                  ┌────────────┐
                                  │ mailpit    │ (somente dev)
                                  └────────────┘
```

Frontend e API são servidos na **mesma origem** (D-006), o que permite refresh token em cookie `SameSite=Strict`.

## 2. Estrutura de pastas

```text
GroupFinance/
├── backend/
│   ├── FinanceApp.sln
│   ├── Dockerfile
│   ├── src/
│   │   ├── FinanceApp.Domain/          # Entidades, enums, invariantes, exceções de domínio (sem dependências)
│   │   ├── FinanceApp.Application/     # Casos de uso, DTOs, interfaces, validadores (FluentValidation)
│   │   ├── FinanceApp.Infrastructure/  # EF Core, DbContext, configurações, migrations, JWT, hash, e-mail
│   │   └── FinanceApp.API/             # Controllers, middlewares, autenticação, Swagger, DI
│   └── tests/
│       ├── FinanceApp.UnitTests/
│       └── FinanceApp.IntegrationTests/ # WebApplicationFactory + PostgreSQL real
├── frontend/
│   ├── Dockerfile
│   ├── nginx.conf
│   └── src/
│       ├── components/   # Componentes reutilizáveis (cards, tabelas, estados vazios, loading…)
│       ├── composables/
│       ├── layouts/      # AuthLayout, AppLayout
│       ├── modules/      # auth, dashboard, accounts, credit-cards, categories, transactions,
│       │                 # installments, recurrence, groups, reports, settings
│       ├── router/
│       ├── services/     # http.ts (Axios central + interceptors)
│       ├── stores/       # Pinia
│       ├── types/        # Tipos espelhando docs/API_CONTRACTS.md
│       └── utils/
├── docs/
│   ├── ARCHITECTURE.md   # este arquivo
│   ├── API_CONTRACTS.md  # contrato de integração (fonte da verdade com o Swagger)
│   ├── DECISIONS.md      # registro de decisões
│   └── PLAN.md           # plano por etapas e status
├── docker-compose.yml
├── .env.example
├── CLAUDE.md
└── README.md
```

## 3. Backend — dependências entre camadas

```text
API ──▶ Application ──▶ Domain
 │           ▲
 └──▶ Infrastructure ──┘
```

- **Domain** não referencia nenhum pacote de infraestrutura.
- **Application** define interfaces (`IAppDbContext`, `IPasswordHasher`, `ITokenService`, `IEmailSender`, `ICurrentUser`, `IDateTimeProvider`) e implementa os casos de uso como serviços (`AuthService`, `UserService`…). Sem MediatR, para evitar abstração prematura.
- **Infrastructure** implementa as interfaces. `IAppDbContext` expõe `DbSet`s — aceitável e pragmático; repositórios só quando houver ganho real.
- **API**: controllers finos, `ExceptionHandlingMiddleware` → ProblemDetails, `ICurrentUser` lido do JWT (`sub`). Nunca aceitar `userId` do corpo/rota para o usuário atual.

### Autorização (padrão para todas as etapas)
- Recursos individuais: toda query filtra por `UserId == currentUser.Id`. Recurso de outro usuário → **404** (não revela existência).
- Recursos de grupo: verificar `GroupMember` com `Status == Active` para o usuário atual. Não membro → 404; membro sem permissão para a ação → 403.
- DTOs de grupo **não** incluem `accountId`, `creditCardId`, nomes de contas/cartões ou instituições do autor.

## 4. Estrutura do banco (visão completa do MVP)

Tabelas em `snake_case`. Valores monetários `numeric(18,2)`. PKs UUID. Etapa em que cada tabela é criada:

| Tabela | Etapa | Observações principais |
|---|---|---|
| `users` | 1 | `email` único (minúsculas), `password_hash`, `is_active` |
| `refresh_tokens` | 1 | `token_hash` único, `family_id`, `expires_at`, `revoked_at`, `replaced_by_token_id`, `created_by_ip` |
| `password_reset_tokens` | 1 | `token_hash` único, `expires_at`, `used_at` |
| `accounts` | 2 | FK `user_id`; índice (`user_id`, `is_active`) |
| `account_balance_snapshots` | 2 | FK `account_id`; índice (`account_id`, `reference_date` desc) |
| `categories` | 2 | `user_id` nulo = padrão do sistema; `parent_category_id`; seed idempotente por chave fixa |
| `transactions` | 2 | `created_by_user_id`, `paid_by_user_id`, `account_id?`, `credit_card_id?`, `category_id`, `group_id?`, `installment_plan_id?`, `installment_number?`, `recurrence_rule_id?`, `occurrence_date?`; índices por (`created_by_user_id`, `transaction_date`), (`group_id`, `transaction_date`); único (`recurrence_rule_id`, `occurrence_date`) e (`installment_plan_id`, `installment_number`) |
| `transaction_payments` | 2 | FK `transaction_id`; check `amount > 0` |
| `transaction_audit_entries` | 2 | histórico de alterações relevantes (campo, valor anterior/novo, usuário, data) |
| `credit_cards` | 3 | FK `user_id`, `account_id?`; checks 1–31 para `closing_day`/`due_day` |
| `installment_plans` | 3 | `total_amount`, `installment_count`, `first_due_date` |
| `recurrence_rules` | 3 | `frequency`, `interval`, `start_date`, `end_date?`, `next_occurrence`, template da movimentação |
| `groups` | 4 | `created_by_user_id`, `is_active` |
| `group_members` | 4 | único (`group_id`, `user_id`) — reativação reutiliza a linha e preserva histórico em `group_member_events` |
| `group_invitations` | 4 | `token_hash` único, `expires_at`, `status` |

## 5. Frontend

- **Estado**: Pinia (`auth` store mantém usuário e access token em memória; demais módulos com stores próprios quando o estado é compartilhado).
- **HTTP**: `services/http.ts` — instância Axios única, `withCredentials`, interceptor que injeta Bearer e, em `401`, chama `/auth/refresh` uma única vez (requisições concorrentes aguardam o mesmo refresh) e repete a requisição; falha → logout e redireciona para login.
- **Erros**: `utils/problem.ts` converte ProblemDetails em mensagem + erros por campo.
- **Bootstrap de sessão**: ao iniciar, a app tenta `/auth/refresh` para restaurar a sessão a partir do cookie.
- **UI**: PrimeVue 4 (preset Aura customizado), ECharts via vue-echarts, layout com sidebar fixa no desktop, recolhível no tablet e topbar + drawer com todos os itens no mobile (D-017).
- **Testes**: Vitest + Vue Test Utils.
