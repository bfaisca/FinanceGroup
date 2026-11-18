# Contratos de integração Frontend ↔ Backend

> **Fonte da verdade:** este documento + Swagger (`/swagger` em Development).
> O **backend** define e mantém os contratos. O **frontend** consome sem inventar estruturas.
> Qualquer mudança de contrato deve ser registrada no [Changelog](#changelog) antes da implementação.

## 1. Convenções gerais

| Item | Regra |
|---|---|
| Base URL | `/api/v1` |
| Formato | JSON, propriedades em `camelCase` |
| Enums | Serializados como **string** (`"Expense"`, `"Income"`) |
| Datas financeiras | `DateOnly` → `"YYYY-MM-DD"` |
| Carimbos de tempo | `DateTimeOffset` UTC ISO 8601 → `"2026-11-18T22:40:00Z"` |
| Valores monetários | `number` com 2 casas (decimal no C#, `NUMERIC(18,2)` no banco) |
| Identificadores | UUID (string) |
| Autenticação | `Authorization: Bearer <accessToken>` |
| Refresh token | Cookie `gf_refresh` (httpOnly) — enviado automaticamente pelo navegador; Axios usa `withCredentials: true` |

### 1.1 Paginação, ordenação e filtros (listagens)

Query string: `page` (padrão 1), `pageSize` (padrão 20, máximo 100), `sort` (campo; prefixo `-` para descendente, ex.: `sort=-transactionDate`).

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalCount": 0,
  "totalPages": 0
}
```

### 1.2 Erros — `application/problem+json`

```json
{
  "type": "https://httpstatuses.io/400",
  "title": "Dados inválidos.",
  "status": 400,
  "detail": "Texto opcional com detalhes para o usuário.",
  "code": "validation_error",
  "traceId": "00-…",
  "errors": {
    "email": ["E-mail inválido."],
    "password": ["A senha deve ter ao menos 8 caracteres."]
  }
}
```

`detail` é opcional. `errors` só existe em `validation_error`. Novos valores de `code` só podem surgir com atualização desta tabela e do Changelog. Chaves de `errors` em camelCase, iguais aos campos do request.

| HTTP | `code` | Quando |
|---|---|---|
| 400 | `validation_error` | Falha de validação (FluentValidation) |
| 400 | `invalid_token` | Token de redefinição inválido/expirado |
| 401 | `unauthorized` | Sem token / token inválido ou expirado |
| 401 | `invalid_credentials` | Login com e-mail ou senha incorretos |
| 401 | `invalid_refresh_token` | Refresh ausente, expirado, revogado ou reutilizado |
| 400 | `invalid_password` | Senha atual incorreta (troca de senha / exclusão de conta) |
| 403 | `forbidden` | Autenticado sem permissão no recurso |
| 404 | `not_found` | Recurso inexistente **ou** pertencente a outro usuário (não vazar existência) |
| 409 | `email_already_registered` | Cadastro com e-mail já existente |
| 409 | `conflict` | Conflito genérico de estado |
| 422 | `business_rule_violation` | Regra de domínio violada (mensagem em `detail`) |
| 429 | `too_many_requests` | Rate limit excedido |
| 500 | `internal_error` | Erro inesperado (sem detalhes internos fora de Development) |

O frontend deve exibir `detail` (quando presente) ou `title`, e mapear `errors` para os campos do formulário.

---

## 2. Health

### `GET /health` (fora de `/api/v1`)
`200 OK` → `{ "status": "Healthy" }`. Verifica conexão com o banco. Usado pelo Docker healthcheck.

---

## 3. Autenticação — `/api/v1/auth` (público)

### Tipos

```ts
interface UserDto {
  id: string;
  name: string;
  email: string;
  createdAt: string; // ISO UTC
}

interface AuthResponse {
  accessToken: string;
  accessTokenExpiresAt: string; // ISO UTC
  user: UserDto;
}
```

### `POST /auth/register`
```json
{ "name": "Maria Silva", "email": "maria@exemplo.com", "password": "Senha123" }
```
Validações: `name` 2–100 caracteres (trim); `email` válido, ≤ 254, normalizado para minúsculas; `password` 8–128, ao menos uma letra e um número.

- `201 Created` → `AuthResponse` + `Set-Cookie: gf_refresh`
- `400 validation_error` · `409 email_already_registered` · `429`

### `POST /auth/login`
```json
{ "email": "maria@exemplo.com", "password": "Senha123" }
```
- `200 OK` → `AuthResponse` + `Set-Cookie: gf_refresh`
- `400 validation_error` · `401 invalid_credentials` (mensagem genérica; também para usuário inativo) · `429`

### `POST /auth/refresh`
Sem corpo. Lê o cookie `gf_refresh`, **rotaciona** o token.
- `200 OK` → `AuthResponse` + novo `Set-Cookie: gf_refresh`
- `401 invalid_refresh_token` (cookie removido na resposta)

Reutilização de um refresh token já rotacionado revoga **todos** os tokens da mesma família.

### `POST /auth/logout`
Sem corpo, não exige access token. Revoga o refresh token do cookie (se houver) e remove o cookie.
- `204 No Content` (sempre)

### `POST /auth/forgot-password`
```json
{ "email": "maria@exemplo.com" }
```
- `202 Accepted` **sempre** (não revela se o e-mail existe). Se existir, envia e-mail com link `${App__FrontendBaseUrl}/reset-password?token=<token>`. Token válido por 1 hora, uso único, armazenado como hash.
- `400 validation_error` · `429`

### `POST /auth/reset-password`
```json
{ "token": "<token do e-mail>", "newPassword": "NovaSenha123" }
```
- `204 No Content` — senha alterada, **todos** os refresh tokens do usuário revogados.
- `400 validation_error` · `400 invalid_token` · `429`

---

## 4. Usuário autenticado — `/api/v1/users/me` (Bearer)

### `GET /users/me`
- `200 OK` → `UserDto` · `401`

### `PUT /users/me`
```json
{ "name": "Maria S. Silva" }
```
- `200 OK` → `UserDto` · `400 validation_error` · `401`

### `PUT /users/me/password`
```json
{ "currentPassword": "Senha123", "newPassword": "NovaSenha123" }
```
`newPassword` deve seguir a política e ser diferente da atual.
- `204 No Content` — revoga todos os refresh tokens **exceto** o da sessão atual. A sessão atual é identificada pelo claim `sid` do access token (família do refresh token), pois o cookie `gf_refresh` (Path `/api/v1/auth`) não é enviado a esta rota. Nenhuma ação extra do frontend.
- `400 validation_error` · `400 invalid_password` · `401`

### `DELETE /users/me`
```json
{ "password": "Senha123" }
```
Anonimiza e desativa a conta (ver D-014), revoga todos os tokens e remove o cookie.
- `204 No Content` · `400 invalid_password` · `401`

---

## 5. Enumerações compartilhadas (antecipadas para as próximas etapas)

Definidas agora para que ambos os lados usem os mesmos nomes. Endpoints correspondentes serão documentados em cada etapa.

```ts
type TransactionType = 'Expense' | 'Income';
type TransactionStatus = 'Planned' | 'Pending' | 'Paid' | 'Received' | 'Overdue' | 'Canceled';
type PaymentMethod = 'Cash' | 'DebitCard' | 'CreditCard' | 'Pix' | 'BankTransfer' | 'BankSlip' | 'Other';
type AccountType = 'Checking' | 'Savings' | 'Wallet' | 'Investment' | 'Other';
type CategoryType = 'Expense' | 'Income';
type RecurrenceFrequency = 'Daily' | 'Weekly' | 'Biweekly' | 'Monthly' | 'Bimonthly' | 'Quarterly' | 'Semiannual' | 'Annual' | 'Custom';
type GroupMemberRole = 'Owner' | 'Admin' | 'Member';
type GroupMemberStatus = 'Active' | 'Left' | 'Removed';
type GroupInvitationStatus = 'Pending' | 'Accepted' | 'Declined' | 'Revoked' | 'Expired';
```

---

## 6. Contas — `/api/v1/accounts` (Bearer)

Todas as rotas filtram pelo usuário autenticado. Recurso de outro usuário → `404 not_found`.

```ts
interface AccountDto {
  id: string;
  name: string;
  institution: string | null;
  type: AccountType;
  currency: 'BRL';
  isActive: boolean;
  latestSnapshot: { balance: number; referenceDate: string } | null; // snapshot de referência (D-019)
  projectedBalance: number | null;  // PROJETADO, não é saldo bancário; null se não há snapshot
  projectedAsOf: string;            // data-alvo usada (DateOnly)
  createdAt: string;
  updatedAt: string;
}
interface BalanceSnapshotDto { id: string; balance: number; referenceDate: string; createdAt: string; }
```

| Método e rota | Corpo / query | Resposta |
|---|---|---|
| `GET /accounts` | `includeInactive` (padrão false), `asOf` (DateOnly, padrão hoje) | `200 AccountDto[]` ordenado por `name` |
| `POST /accounts` | `{ name, institution?, type, initialBalance?, balanceReferenceDate? }` | `201 AccountDto` |
| `GET /accounts/{id}` | `asOf` | `200 AccountDto` |
| `PUT /accounts/{id}` | `{ name, institution?, type }` | `200 AccountDto` |
| `POST /accounts/{id}/deactivate` | — | `200 AccountDto` |
| `POST /accounts/{id}/activate` | — | `200 AccountDto` |
| `GET /accounts/{id}/balance-snapshots` | — | `200 BalanceSnapshotDto[]` (mais recente primeiro) |
| `POST /accounts/{id}/balance-snapshots` | `{ balance, referenceDate }` | `201 BalanceSnapshotDto` |

Validações: `name` 1–100 (único entre as contas do usuário, sem diferenciar maiúsculas → `409 conflict`); `institution` ≤ 100; `type` ∈ `AccountType`; `balance` com 2 casas, |valor| ≤ 9.999.999.999.999,99 (pode ser negativo); `referenceDate` ≤ hoje; `initialBalance` e `balanceReferenceDate` devem vir **juntos** (se ausentes, a conta nasce sem snapshot; `balanceReferenceDate` padrão = hoje quando só `initialBalance` é enviado).
Snapshots nunca são editados nem apagados. Um novo snapshot passa a ser a referência (D-019). Conta desativada: não aceita novas movimentações nem snapshots (`422`); as já existentes permanecem no histórico.
O histórico de movimentações da conta é `GET /transactions?accountId={id}`.

---

## 7. Categorias — `/api/v1/categories` (Bearer)

```ts
interface CategoryDto {
  id: string;
  name: string;
  type: CategoryType;
  parentCategoryId: string | null;
  icon: string | null;   // nome de ícone PrimeIcons, ex.: "pi-shopping-cart"
  color: string | null;  // "#RRGGBB"
  isDefault: boolean;    // padrão do sistema (somente leitura)
  isActive: boolean;
}
```

| Método e rota | Corpo / query | Resposta |
|---|---|---|
| `GET /categories` | `type?`, `includeInactive` (padrão false) | `200 CategoryDto[]` (lista **plana**: padrão + do usuário; o frontend monta a árvore) |
| `POST /categories` | `{ name, type, parentCategoryId?, icon?, color? }` | `201 CategoryDto` |
| `PUT /categories/{id}` | `{ name, icon?, color? }` | `200 CategoryDto` |
| `POST /categories/{id}/deactivate` | — | `200 CategoryDto` (desativa também as subcategorias) |
| `POST /categories/{id}/activate` | — | `200 CategoryDto` |

Regras (D-022): categorias padrão não podem ser alteradas nem desativadas (`422 business_rule_violation`, `detail` explica); subcategoria tem o mesmo `type` do pai e profundidade máxima 1; `name` 1–60, único entre irmãs do mesmo usuário e tipo (`409 conflict`); `color` no formato `#RRGGBB`. Não é possível usar categoria inativa em novas movimentações.
Categorias padrão — Despesa: Alimentação, Saúde, Educação, Lazer, Assinaturas, Moradia, Transporte, Compras, Outros. Receita: Salário, Freelance, Investimentos, Reembolsos, Outros.

---

## 8. Movimentações — `/api/v1/transactions` (Bearer)

Escopo da Etapa 2: movimentações **individuais**. Campos de grupo, cartão, parcelamento e recorrência serão adicionados de forma **aditiva** nas etapas 3 e 4 (o frontend deve ignorar propriedades desconhecidas).

```ts
interface TransactionDto {
  id: string;
  description: string;
  type: TransactionType;
  amount: number;
  paidAmount: number;          // soma dos pagamentos
  remainingAmount: number;     // amount - paidAmount
  status: TransactionStatus;   // apresentado: inclui 'Overdue' (STATE_MACHINE.md)
  categoryId: string;
  categoryName: string;
  parentCategoryName: string | null;
  categoryColor: string | null;
  categoryIcon: string | null;
  accountId: string | null;
  accountName: string | null;
  paymentMethod: PaymentMethod | null;
  transactionDate: string;     // DateOnly
  dueDate: string | null;      // DateOnly
  paidAt: string | null;       // DateOnly
  notes: string | null;
  createdAt: string;
  updatedAt: string;
}
interface TransactionPaymentDto {
  id: string; amount: number; paymentDate: string;
  accountId: string | null; accountName: string | null;
  paymentMethod: PaymentMethod | null; createdAt: string;
}
interface TransactionDetailDto extends TransactionDto { payments: TransactionPaymentDto[]; }
interface TransactionHistoryEntryDto {
  id: string;
  action: 'Created' | 'Updated' | 'PaymentAdded' | 'PaymentRemoved' | 'Canceled' | 'Reopened';
  field: string | null;      // ex.: "amount", "dueDate" (uma linha por campo alterado)
  oldValue: string | null;
  newValue: string | null;
  createdAt: string;
}
```

| Método e rota | Corpo / query | Resposta |
|---|---|---|
| `GET /transactions` | `page`, `pageSize`, `sort` (`transactionDate` \| `dueDate` \| `amount` \| `description` \| `createdAt`; padrão `-transactionDate`), `from`, `to` (por `transactionDate`), `dueFrom`, `dueTo`, `type`, `categoryId` (inclui subcategorias), `accountId`, `status` (apresentado, aceita `Overdue`), `search` (descrição, sem diferenciar maiúsculas) | `200 PagedResult<TransactionDto>` |
| `POST /transactions` | `CreateTransactionRequest` | `201 TransactionDetailDto` |
| `GET /transactions/{id}` | — | `200 TransactionDetailDto` |
| `PUT /transactions/{id}` | `UpdateTransactionRequest` | `200 TransactionDetailDto` |
| `DELETE /transactions/{id}` | — | `204` (só sem pagamentos; senão `422`) |
| `POST /transactions/{id}/cancel` | — | `200 TransactionDetailDto` |
| `POST /transactions/{id}/reopen` | — | `200 TransactionDetailDto` |
| `GET /transactions/{id}/history` | — | `200 TransactionHistoryEntryDto[]` (mais recente primeiro) |
| `POST /transactions/{id}/payments` | `{ amount, paymentDate, accountId?, paymentMethod? }` | `201 TransactionDetailDto` |
| `DELETE /transactions/{id}/payments/{paymentId}` | — | `200 TransactionDetailDto` |

```ts
interface CreateTransactionRequest {
  description: string; type: TransactionType; amount: number; categoryId: string;
  accountId?: string | null; paymentMethod?: PaymentMethod | null;
  status?: 'Planned' | 'Pending';   // padrão 'Pending'
  transactionDate: string; dueDate?: string | null; notes?: string | null;
}
// Update: mesmos campos, exceto `type` (imutável); sem `status` (derivado).
// Para voltar uma movimentação sem pagamentos de Pending para Planned (e vice-versa), enviar `status` ('Planned' | 'Pending') em UpdateTransactionRequest — opcional.
```

Validações: `description` 1–200; `amount` > 0 com 2 casas, ≤ 9.999.999.999.999,99; `categoryId` deve existir, estar ativa, ser do usuário ou padrão e ter o mesmo `type` da movimentação; `accountId` (se enviado) deve ser do usuário e estar ativa; `dueDate` ≥ `transactionDate`; `notes` ≤ 1000; pagamento: `amount` > 0, `amount` ≤ `remainingAmount`, `paymentDate` ≤ hoje, `accountId` do usuário.
Regras de negócio violadas (`422 business_rule_violation`, com `detail` em pt-BR): pagamento acima do restante, movimentação cancelada, excluir com pagamentos, reduzir `amount` abaixo do total pago, conta/categoria inativa.

---

## 9. Dashboard individual — `GET /api/v1/dashboard` (Bearer)

Query: `from`, `to` (DateOnly, obrigatórios, `from ≤ to`, intervalo ≤ 366 dias).

```ts
interface DashboardDto {
  period: { from: string; to: string };
  balance: {
    reportedTotal: number | null;   // soma dos saldos informados (snapshot de referência) das contas ATIVAS com snapshot
    projectedTotal: number | null;  // PROJETADO em `projectedAsOf` (D-019), mesmas contas
    projectedAsOf: string;          // = period.to
    accountsWithBalance: number;
    accountsWithoutBalance: number; // contas ativas sem snapshot (fora dos totais)
  };
  totals: {                          // movimentações NÃO canceladas com transactionDate no período
    income: number; expense: number; result: number;       // result = income - expense
    receivedIncome: number; pendingIncome: number;         // por pagamentos acumulados
    paidExpense: number; pendingExpense: number;
  };
  expensesByCategory: { categoryId: string; categoryName: string; color: string | null; total: number; percentage: number }[];
  // despesas não canceladas do período, agrupadas pela categoria RAIZ (subcategorias somadas ao pai), ordenado por total desc; percentage 0–100 com 2 casas
  upcomingDues: TransactionDto[];    // até 10 itens com restante > 0, não canceladas, dueDate definido e <= hoje+30 dias (inclui atrasadas), ordenadas por dueDate asc
  monthlyHistory: { month: string; income: number; expense: number; result: number }[];
  // 6 meses terminando no mês de `period.to`, em ordem cronológica; month = "YYYY-MM"; meses sem dados vêm zerados
  today: string;                      // "hoje" no fuso da aplicação (D-021)
}
```

Pagamentos parciais: `receivedIncome`/`paidExpense` somam `paidAmount`; `pendingIncome`/`pendingExpense` somam `remainingAmount`. O resumo de cartões entra na Etapa 3.

---

## Changelog

| Data | Versão | Alteração | Autor |
|---|---|---|---|
| 2026-11-18 | 1.0 | Convenções gerais, erros, health, auth e usuário (Etapa 1) | Backend |
| 2026-11-18 | 1.0.1 | Esclarecimento (sem impacto no frontend): em `PUT /users/me/password` a sessão preservada é identificada pelo claim `sid` do access token, não pelo cookie | Backend |
| 2026-11-18 | 1.0.2 | `detail` documentado como opcional no exemplo de erro; regra para novos `code` | Coordenação |
| 2026-11-18 | 1.1 | Etapa 2: contas, snapshots, categorias, movimentações, pagamentos, histórico e dashboard individual (seções 6–9) | Coordenação (backend implementa) |
