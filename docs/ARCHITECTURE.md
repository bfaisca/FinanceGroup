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
| `transaction_payments` | 2 (4c) | FK `transaction_id`; `amount` = principal (check `> 0`); 4c: `interest_amount` NUMERIC(18,2) default 0 (check `>= 0`, D-048) e `updated_at` (edição do próprio pagamento) |
| `transaction_audit_entries` | 2 | histórico de alterações relevantes (campo, valor anterior/novo, usuário, data) |
| `credit_cards` | 3 | FK `user_id`, `account_id?`; checks 1–31 para `closing_day`/`due_day` |
| `installment_plans` | 3 | `total_amount`, `installment_count`, `first_due_date` |
| `recurrence_rules` | 3 | `frequency`, `interval`, `start_date`, `end_date?`, `next_occurrence`, template da movimentação |
| `groups` | 4 | `created_by_user_id`, `is_active` |
| `group_members` | 4 | único (`group_id`, `user_id`) — reativação reutiliza a mesma linha (papel volta a `Member`, `joined_at` renovado, `left_at` limpo); o histórico de participação fica nas próprias linhas (`status`, `left_at`) — a tabela `group_member_events` não foi criada |
| `group_invitations` | 4 | `token_hash` único (SHA-256 hex), `expires_at`, `status`; único parcial (`group_id`, `email`) onde `status = 'Pending'` |
| `group_transaction_series` | 4 (4c) | Série de despesa recorrente do grupo (D-049): `group_id` (FK), `created_by_user_id` (FK), `frequency` (check: Weekly…Annual, sem Daily/Custom), `occurrences` (check 2–60; total ORIGINAL), `created_at`. Imutável (sem `xmin`) |
| `notifications` | 4 (4b/4c) | Notificação dentro do app (D-041 a D-044): `recipient_user_id` (FK cascade), `type` (`GroupTransactionCreated`/`GroupPaymentRegistered`/`GroupInvitationReceived`), `group_id` (FK), `actor_user_id` (FK set null; nome lido da conta na consulta), instantâneo `transaction_id` (FK cascade)/`transaction_description`/`amount` (NUMERIC(18,2); no pagamento = total principal + juros), `series_occurrences` (só na criação de série, D-049; check 2–60), `invitation_id` (FK cascade), `created_at`, `read_at`, `xmin`. Checks `ck_notifications_shape` e `ck_notifications_series_occurrences`. Índices: (`recipient_user_id`, `created_at` desc); parcial `read_at IS NULL` (contador); único parcial (`recipient_user_id`, `invitation_id`) (D-043); parcial `read_at IS NOT NULL` (retenção). O tipo da movimentação não é gravado: grupos só têm despesas (D-046) |

**Colunas e regras de grupo em `transactions`** (todas na migration `Etapa4_Groups`): `group_id` (FK restrict), `group_series_id` (FK restrict) e `series_number` (1–60). Checks: `ck_transactions_group_rules` (`group_id IS NULL OR (type = 'Expense' AND account_id/credit_card_id/installment_plan_id/recurrence_rule_id IS NULL)` — D-046: receita em grupo é barrada pelo banco), `ck_transactions_group_series` (série e número andam juntos e só existem com grupo) e `ck_transactions_due_date_after_date` (vencimento ≥ data só para movimentações SEM grupo; o vencimento do grupo é livre, D-047). Índices: parciais por (`group_id`, `transaction_date`) e (`group_id`, `created_by_user_id`); único parcial (`group_series_id`, `series_number`).

**Migrations**: Etapas 1–3 inalteradas; a Etapa 4 inteira (grupos, convites, membros, séries, notificações e as colunas acima) é UMA migration, `Etapa4_Groups` (D-051, pré-lançamento: quem rodou versões anteriores deve recriar o banco com `docker compose down -v`).

## 5. Frontend

- **Estado**: Pinia (`auth` store mantém usuário e access token em memória; demais módulos com stores próprios quando o estado é compartilhado).
- **HTTP**: `services/http.ts` — instância Axios única, `withCredentials`, interceptor que injeta Bearer e, em `401`, chama `/auth/refresh` uma única vez (requisições concorrentes aguardam o mesmo refresh) e repete a requisição; falha → logout e redireciona para login.
- **Erros**: `utils/problem.ts` converte ProblemDetails em mensagem + erros por campo.
- **Bootstrap de sessão**: ao iniciar, a app tenta `/auth/refresh` para restaurar a sessão a partir do cookie.
- **UI**: PrimeVue 4 (preset Aura customizado), ECharts via vue-echarts, layout com sidebar fixa no desktop, recolhível no tablet e topbar + drawer com todos os itens no mobile (D-017).
- **Testes**: Vitest + Vue Test Utils.
