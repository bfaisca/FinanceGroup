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

## 10. Adições às movimentações (Etapa 3) — **aditivas** ao §8

```ts
interface TransactionDto {
  // ... campos do §8 ...
  creditCardId: string | null;
  creditCardName: string | null;
  installment: { planId: string; number: number; count: number } | null;
  recurrenceRuleId: string | null;      // não nulo = ocorrência de recorrência
}
// CreateTransactionRequest ganha: creditCardId?: string | null
```

- **Compra no cartão** (D-025): `creditCardId` só em `type = Expense`; com cartão, `accountId` e `dueDate` **não podem ser enviados** (`400 validation_error` em `errors.accountId` / `errors.dueDate`); `paymentMethod` deve ser nulo ou `CreditCard`. A resposta traz `accountId`/`accountName` = **nulos** e `dueDate` = **vencimento da fatura calculado pelo backend**. `creditCardId` é **imutável** (enviar outro valor no `PUT` → `422`). O frontend **não** calcula nem prevê o vencimento.
- Cartão inexistente/de outro usuário no corpo → `400 validation_error` (`errors.creditCardId`); cartão inativo → `422`.
- **Parcelas** (`installment != null`) e **ocorrências** (`recurrenceRuleId != null`): `DELETE` → `422 business_rule_violation`. Parcelas: alterar `amount`, `transactionDate` (e `dueDate` quando há cartão) no `PUT` → `422`. Editar uma ocorrência é permitido (marca-a como editada à mão, D-027).
- Novos filtros em `GET /transactions`: `creditCardId`, `installmentPlanId`, `recurrenceRuleId`.
- `GET /transactions?accountId=` inclui também compras de cartões associados à conta (conta efetiva, D-025).

---

## 11. Cartões de crédito — `/api/v1/credit-cards` (Bearer)

```ts
interface CreditCardDto {
  id: string;
  name: string;
  institution: string | null;
  creditLimit: number;
  closingDay: number;   // 1–31
  dueDay: number;       // 1–31
  accountId: string | null;
  accountName: string | null;
  isActive: boolean;
  usedLimit: number;       // D-025: soma do restante de tudo que está em aberto
  availableLimit: number;  // creditLimit - usedLimit (negativo = limite excedido)
  openInvoice: { closingDate: string; dueDate: string; amount: number }; // fatura que recebe compras feitas hoje (D-025)
  createdAt: string;
  updatedAt: string;
}
```

| Método e rota | Corpo / query | Resposta |
|---|---|---|
| `GET /credit-cards` | `includeInactive` (padrão false) | `200 CreditCardDto[]` por `name` |
| `POST /credit-cards` | `{ name, institution?, creditLimit, closingDay, dueDay, accountId? }` | `201 CreditCardDto` |
| `GET /credit-cards/{id}` | — | `200 CreditCardDto` |
| `PUT /credit-cards/{id}` | mesmo corpo do POST | `200 CreditCardDto` |
| `POST /credit-cards/{id}/deactivate` | — | `200 CreditCardDto` |
| `POST /credit-cards/{id}/activate` | — | `200 CreditCardDto` |

Validações: `name` 1–100, único por usuário sem diferenciar maiúsculas (`409 conflict`); `institution` ≤ 100; `creditLimit` ≥ 0, 2 casas, ≤ 9.999.999.999.999,99; `closingDay`/`dueDay` inteiros 1–31; `accountId` (se enviado) do usuário e ativa (inexistente/de outro → `400 errors.accountId`). `openInvoice.closingDate` é a data de fechamento (dia ajustado ao mês). Compras do cartão: `GET /transactions?creditCardId={id}`.

---

## 12. Parcelamentos — `/api/v1/installment-plans` (Bearer)

```ts
type InstallmentPlanStatus = 'Open' | 'Completed' | 'Canceled';
interface InstallmentPlanDto {
  id: string; description: string; totalAmount: number; installmentCount: number;
  firstDate: string;                       // DateOnly (D-026)
  categoryId: string; categoryName: string;
  accountId: string | null; accountName: string | null;
  creditCardId: string | null; creditCardName: string | null;
  paymentMethod: PaymentMethod | null;
  status: InstallmentPlanStatus;
  paidCount: number; openCount: number; canceledCount: number;
  paidAmount: number; remainingAmount: number;   // somas de parcelas não canceladas
  nextDueDate: string | null;              // menor data efetiva (dueDate ?? transactionDate) de parcela em aberto
  createdAt: string;
}
interface InstallmentPlanDetailDto extends InstallmentPlanDto { installments: TransactionDto[]; } // ordem por número
interface CreateInstallmentPlanRequest {
  description: string; totalAmount: number; installmentCount: number; firstDate: string;
  categoryId: string; accountId?: string | null; creditCardId?: string | null;
  paymentMethod?: PaymentMethod | null; notes?: string | null;
}
```

| Método e rota | Corpo / query | Resposta |
|---|---|---|
| `GET /installment-plans` | `page`, `pageSize`, `status?`, `sort` (`firstDate`\|`description`\|`totalAmount`\|`createdAt`; padrão `-createdAt`) | `200 PagedResult<InstallmentPlanDto>` |
| `POST /installment-plans` | `CreateInstallmentPlanRequest` | `201 InstallmentPlanDetailDto` (parcelas geradas conforme D-026) |
| `GET /installment-plans/{id}` | — | `200 InstallmentPlanDetailDto` |
| `POST /installment-plans/{id}/cancel` | — | `200 InstallmentPlanDetailDto` |
| `DELETE /installment-plans/{id}` | — | `204` (só se nenhuma parcela tem pagamento; senão `422`) |

Validações: `description` 1–200; `totalAmount` > 0, 2 casas; `installmentCount` 2–120; `firstDate` válida; categoria ativa do tipo `Expense`; regras de cartão/conta como no §10 (com cartão: sem `accountId`, `paymentMethod` nulo/`CreditCard`); `notes` ≤ 1000. Parcelas são sempre despesas. Plano de outro usuário → `404`.

---

## 13. Recorrências — `/api/v1/recurrences` (Bearer)

```ts
interface RecurrenceDto {
  id: string;
  description: string; type: TransactionType; amount: number;
  categoryId: string; categoryName: string;
  accountId: string | null; accountName: string | null;
  creditCardId: string | null; creditCardName: string | null;
  paymentMethod: PaymentMethod | null; notes: string | null;
  frequency: RecurrenceFrequency; interval: number;   // D-027
  startDate: string; endDate: string | null;
  isActive: boolean;
  nextOccurrenceDate: string | null;   // próxima ocorrência futura (>= hoje) ainda não gerada ou gerada em aberto; null se encerrada/inativa
  generatedCount: number;              // ocorrências já geradas (inclui canceladas)
  createdAt: string; updatedAt: string;
}
interface CreateRecurrenceRequest {
  description: string; type: TransactionType; amount: number; categoryId: string;
  accountId?: string | null; creditCardId?: string | null; paymentMethod?: PaymentMethod | null; notes?: string | null;
  frequency: RecurrenceFrequency; interval?: number;   // padrão 1
  startDate: string; endDate?: string | null;
}
// UpdateRecurrenceRequest: mesmos campos, exceto `type` e `startDate` (imutáveis).
```

| Método e rota | Corpo / query | Resposta |
|---|---|---|
| `GET /recurrences` | `includeInactive` (padrão false) | `200 RecurrenceDto[]` por `description` |
| `POST /recurrences` | `CreateRecurrenceRequest` | `201 RecurrenceDto` (gera ocorrências até o horizonte) |
| `GET /recurrences/{id}` | — | `200 RecurrenceDto` |
| `PUT /recurrences/{id}` | `UpdateRecurrenceRequest` | `200 RecurrenceDto` (regenera conforme D-027) |
| `POST /recurrences/{id}/deactivate` | — | `200 RecurrenceDto` |
| `POST /recurrences/{id}/activate` | — | `200 RecurrenceDto` |

Validações: `description` 1–200; `amount` > 0; `interval` 1–365 (para `Custom` = dias); `startDate` ≥ hoje − 366 dias; `endDate` ≥ `startDate`; categoria ativa do mesmo `type`; cartão/conta como no §10 (cartão só em `Expense`). Ocorrências: `GET /transactions?recurrenceRuleId={id}`.

---

## 14. Relatórios — `/api/v1/reports` (Bearer)

Base: D-028. `from`/`to` (DateOnly, obrigatórios, `from ≤ to`, intervalo ≤ 366 dias) nos relatórios por período.

```ts
interface ReportTotals { income: number; expense: number; result: number; receivedIncome: number; pendingIncome: number; paidExpense: number; pendingExpense: number; }
interface ShareItem { total: number; percentage: number; }
```

| Rota | Query | Resposta |
|---|---|---|
| `GET /reports/summary` | `from`, `to`, `groupBy` = `day`\|`month` (padrão `month`) | `{ period, totals: ReportTotals, series: { key: string; income: number; expense: number; result: number }[] }` — `key` = `YYYY-MM-DD` ou `YYYY-MM`; períodos sem dados vêm zerados, em ordem cronológica |
| `GET /reports/by-category` | `from`, `to`, `type` = `Expense`\|`Income` (padrão `Expense`) | `{ period, type, total, items: { categoryId, categoryName, color, total, percentage, subcategories: { categoryId, categoryName, total, percentage }[] }[] }` por total desc; percentuais das subcategorias são relativos ao total da **categoria raiz** |
| `GET /reports/by-account` | `from`, `to`, `type` (padrão `Expense`) | `{ period, type, total, items: { accountId: string \| null; accountName: string; total; percentage }[] }` — conta efetiva (D-025); sem conta → `accountId: null`, `accountName: "Sem conta"` |
| `GET /reports/by-payment-method` | `from`, `to`, `type` (padrão `Expense`) | `{ period, type, total, items: { paymentMethod: PaymentMethod \| null; total; percentage }[] }` — `null` = "Não informado" |
| `GET /reports/monthly-evolution` | `months` 1–36 (padrão 12), `endMonth` `YYYY-MM` (padrão mês atual) | `{ items: { month: string; income: number; expense: number; result: number; cumulativeResult: number }[] }` cronológico; `cumulativeResult` = soma acumulada do `result` dentro da janela |
| `GET /reports/installments` | `months` 1–36 (padrão 12) | `{ openPlans: number; totalRemaining: number; commitmentByMonth: { month: string; amount: number; installmentsCount: number }[]; plans: InstallmentPlanDto[] }` — `commitmentByMonth` cobre `months` meses a partir do **mês atual** (inclui atrasadas no mês corrente); `plans` = planos `Open` por `nextDueDate` |
| `GET /reports/recurrences` | — | `{ activeRules: number; upcomingByMonth: { month: string; income: number; expense: number }[]; rules: { id; description; type; amount; frequency; interval; nextOccurrenceDate: string \| null; upcomingTotal12m: number }[] }` — usa só ocorrências **já geradas**, não canceladas, com data > hoje e ≤ hoje + 12 meses |

Todos valores monetários com 2 casas; listas vazias quando não há dados; usuário só vê os próprios dados.

---

## 15. Dashboard — adição da Etapa 3 (aditiva ao §9)

```ts
interface DashboardDto {
  // ... campos do §9 ...
  creditCards: { id: string; name: string; creditLimit: number; usedLimit: number; availableLimit: number;
                 openInvoice: { closingDate: string; dueDate: string; amount: number } }[]; // cartões ATIVOS, por nome
}
```

---

## 16. Grupos — `/api/v1/groups` (Bearer) — Etapa 4

Regras: D-030 a D-036. Não membro ativo → `404 not_found`; papel insuficiente → `403 forbidden`; regra violada → `422 business_rule_violation` (mensagem pt-BR em `detail`). **Nenhum DTO de grupo contém conta, cartão, saldo ou e-mail de membro.**

```ts
type GroupMemberRole = 'Owner' | 'Admin' | 'Member';
type GroupMemberStatus = 'Active' | 'Left' | 'Removed';

interface GroupSummaryDto {
  id: string; name: string; description: string | null;
  myRole: GroupMemberRole; memberCount: number;   // membros ativos
  isActive: boolean;                              // false = arquivado
  createdAt: string;
}
interface GroupMemberDto {
  id: string;                  // id do GroupMember
  userId: string; name: string; // "Usuário removido" se a conta foi excluída
  role: GroupMemberRole; status: GroupMemberStatus;
  joinedAt: string; leftAt: string | null;
  isMe: boolean;
}
interface GroupDetailDto extends GroupSummaryDto {
  createdByName: string; updatedAt: string;
  permissions: { canManageMembers: boolean; canInvite: boolean; canEditGroup: boolean; canArchive: boolean; canLeave: boolean; canCreateTransactions: boolean };
  members: GroupMemberDto[];   // apenas ativos
}
```

| Método e rota | Corpo / query | Resposta |
|---|---|---|
| `GET /groups` | `includeArchived` (padrão `false`) | `200 GroupSummaryDto[]` (ordem alfabética) |
| `POST /groups` | `{ name (1–100), description? (≤ 500) }` | `201 GroupDetailDto` (criador = Owner) |
| `GET /groups/{id}` | — | `200 GroupDetailDto` |
| `PUT /groups/{id}` | `{ name, description? }` (Admin/Owner) | `200 GroupDetailDto` |
| `POST /groups/{id}/archive` · `/unarchive` | — (Admin/Owner) | `200 GroupDetailDto` |
| `POST /groups/{id}/leave` | — (Owner → `422`) | `204` |
| `GET /groups/{id}/members` | `includeFormer` (padrão `false`) | `200 GroupMemberDto[]` |
| `PUT /groups/{id}/members/{memberId}/role` | `{ role: 'Admin' \| 'Member' }` | `200 GroupMemberDto` |
| `DELETE /groups/{id}/members/{memberId}` | — | `204` (status `Removed`) |

Papéis em `role`/remoção: ver D-030 (`403` se o papel não permite; `422` ao tentar alterar/remover o Owner ou a si mesmo na remoção).

### 16.1 Convites

```ts
type GroupInvitationStatus = 'Pending' | 'Accepted' | 'Declined' | 'Revoked' | 'Expired';
interface GroupInvitationDto {
  id: string; email: string; status: GroupInvitationStatus;   // Expired é derivado
  invitedByName: string; expiresAt: string; createdAt: string;
  emailSent?: boolean;                                         // apenas na resposta de POST/resend
}
interface InvitationPreviewDto {                               // GET /invitations/{token}
  groupName: string; invitedByName: string; email: string;
  status: GroupInvitationStatus; expiresAt: string;
  canAccept: boolean;      // Pending + não expirado + e-mail do usuário logado = e-mail do convite
  alreadyMember: boolean;
}
```

| Método e rota | Corpo | Resposta |
|---|---|---|
| `GET /groups/{id}/invitations` (Admin/Owner) | `status?` | `200 GroupInvitationDto[]` (mais recente primeiro) |
| `POST /groups/{id}/invitations` (Admin/Owner) | `{ email }` | `201 GroupInvitationDto` (`409 conflict` se já há `Pending` para o e-mail; `422` se o e-mail já é de membro ativo) |
| `POST /groups/{id}/invitations/{invitationId}/resend` | — | `200 GroupInvitationDto` (novo token/validade) |
| `DELETE /groups/{id}/invitations/{invitationId}` | — | `204` (`Revoked`; só `Pending`) |
| `GET /invitations/{token}` | — | `200 InvitationPreviewDto` (`404` se o token não existe) |
| `POST /invitations/{token}/accept` | — | `200 GroupSummaryDto` (`403` e-mail diferente; `422` expirado/revogado/já usado/já membro) |
| `POST /invitations/{token}/decline` | — | `204` (mesmas regras de e-mail) |

O link enviado por e-mail aponta para a **rota do frontend** `/invitations/:token`.

### 16.2 Movimentações do grupo — `/groups/{id}/transactions`

```ts
interface GroupUserRefDto { userId: string; name: string; }   // "Usuário removido" quando anonimizado

interface GroupTransactionDto {
  id: string; groupId: string;
  description: string; type: TransactionType; amount: number;
  paidAmount: number; remainingAmount: number;
  status: TransactionStatus;                                    // apresentado (inclui 'Overdue')
  categoryId: string; categoryName: string; parentCategoryName: string | null;
  categoryColor: string | null; categoryIcon: string | null;
  paymentMethod: PaymentMethod | null;
  transactionDate: string; dueDate: string | null; paidAt: string | null;
  notes: string | null;
  createdBy: GroupUserRefDto;
  createdAt: string; updatedAt: string;
  permissions: { canEdit: boolean; canCancel: boolean; canDelete: boolean; canPay: boolean };
}
interface GroupTransactionPaymentDto {
  id: string; amount: number; paymentDate: string;
  paymentMethod: PaymentMethod | null;
  paidBy: GroupUserRefDto; createdAt: string;
  canRemove: boolean;                  // só o próprio pagador, e com o grupo ativo
}
interface GroupTransactionDetailDto extends GroupTransactionDto { payments: GroupTransactionPaymentDto[]; }
interface GroupTransactionHistoryEntryDto {
  id: string; action: 'Created' | 'Updated' | 'PaymentAdded' | 'PaymentRemoved' | 'Canceled' | 'Reopened';
  field: string | null; oldValue: string | null; newValue: string | null;
  actorName: string; createdAt: string;
}
```

| Método e rota | Corpo / query | Resposta |
|---|---|---|
| `GET /groups/{id}/transactions` | `page`, `pageSize`, `sort` (`transactionDate`\|`dueDate`\|`amount`\|`description`\|`createdAt`; padrão `-transactionDate`), `from`, `to`, `type`, `categoryId` (inclui subcategorias), `status`, `search`, `createdByUserId`, `paidByUserId` (tem ao menos um pagamento dele) | `200 PagedResult<GroupTransactionDto>` |
| `POST /groups/{id}/transactions` | `CreateGroupTransactionRequest` | `201 GroupTransactionDetailDto` |
| `GET /groups/{id}/transactions/{txId}` | — | `200 GroupTransactionDetailDto` |
| `PUT /groups/{id}/transactions/{txId}` | `UpdateGroupTransactionRequest` (só criador) | `200 GroupTransactionDetailDto` |
| `DELETE /groups/{id}/transactions/{txId}` | — (só criador, sem pagamentos) | `204` |
| `POST .../{txId}/cancel` · `/reopen` | — (só criador) | `200 GroupTransactionDetailDto` |
| `GET .../{txId}/history` | — | `200 GroupTransactionHistoryEntryDto[]` |
| `POST .../{txId}/payments` | `{ amount, paymentDate, accountId?, paymentMethod? }` | `201 GroupTransactionDetailDto` (`accountId` = conta do próprio pagador; **nunca devolvido**) |
| `DELETE .../{txId}/payments/{paymentId}` | — (só o pagador) | `200 GroupTransactionDetailDto` |

```ts
interface CreateGroupTransactionRequest {
  description: string; type: TransactionType; amount: number; categoryId: string;
  paymentMethod?: PaymentMethod | null; status?: 'Planned' | 'Pending';
  transactionDate: string; dueDate?: string | null; notes?: string | null;
}
// Update: mesmos campos, exceto `type` (imutável). Validações iguais às do §8 (exceto conta); categoria: padrão ou do criador, ativa, mesmo `type`.
```
Violações do §8 (pagar acima do restante, cancelada, excluir com pagamentos etc.) → `422`; grupo arquivado → `422`; tentar editar/cancelar/excluir movimentação alheia → `403`; `GET`/mutação de movimentação de outro grupo → `404`.

### 16.3 Dashboard do grupo — `GET /groups/{id}/dashboard`

Query: `from`, `to` (obrigatórios, `from ≤ to`, ≤ 366 dias), `months` (1–36, padrão 6), `endMonth` (`YYYY-MM`, padrão mês de `to`). Regras: D-034.

```ts
interface GroupParticipationItemDto {
  userId: string; name: string; status: GroupMemberStatus;
  expenseAmount: number; expensePercent: number;   // 0–100, 2 casas
  incomeAmount: number; incomePercent: number;
}
interface GroupDashboardDto {
  period: { from: string; to: string };
  totals: { expenses: number; expensesPaid: number; expensesPending: number;
            income: number; incomeReceived: number; incomePending: number; balance: number /* income - expenses */ };
  createdBy: GroupParticipationItemDto[];   // por quem CADASTROU (soma dos valores)
  paidBy: GroupParticipationItemDto[];      // por quem PAGOU/RECEBEU (soma dos pagamentos)
  expensesByCategory: { categoryId: string; name: string; color: string | null; amount: number; percent: number;
                        children: { categoryId: string; name: string; amount: number; percent: number }[] }[];
  monthly: { month: string /* YYYY-MM */; income: number; expenses: number }[];
  previousPeriod: { from: string; to: string; expenses: number; income: number;
                    expensesChangePercent: number | null; incomeChangePercent: number | null };  // null se base 0
}
```
`createdBy`/`paidBy` ordenados por maior valor total (despesa + receita) e depois por nome; incluem apenas quem tem valor no período.

## 17. Notificações — `/api/v1/notifications` (Bearer) — Etapa 4b

Regras: D-041 a D-044. Só o dono acessa as próprias notificações (de outro usuário → `404`).

```ts
type NotificationType = 'GroupTransactionCreated' | 'GroupPaymentRegistered' | 'GroupInvitationReceived';

interface NotificationDto {
  id: string; type: NotificationType;
  createdAt: string; readAt: string | null; isRead: boolean;
  group: { id: string; name: string };
  actor: { userId: string; name: string } | null;       // "Usuário removido" se a conta foi excluída
  transaction: {                                         // null em convites
    id: string; description: string;
    type: 'Income' | 'Expense';                          // tipo da movimentação
    amount: number;                                      // GroupTransactionCreated: valor da movimentação; GroupPaymentRegistered: valor do pagamento
  } | null;
  invitation: {                                          // somente em GroupInvitationReceived
    id: string;
    status: GroupInvitationStatus;                       // derivado agora (Pending/Accepted/Declined/Revoked/Expired)
    expiresAt: string;
    canRespond: boolean;                                 // Pending + não expirado + e-mail da conta = e-mail do convite + não é membro
  } | null;
}
```

| Método e rota | Corpo / query | Resposta |
|---|---|---|
| `GET /notifications` | `page`, `pageSize` (padrão 20, máx 50), `unreadOnly` (padrão `false`) | `200 PagedResult<NotificationDto>` (mais recentes primeiro) |
| `GET /notifications/unread-count` | — | `200 { count: number }` |
| `POST /notifications/{id}/read` | — | `204` (idempotente) |
| `POST /notifications/read-all` | — | `204` |
| `POST /notifications/{id}/accept-invitation` | — | `200 GroupSummaryDto` (§16; marca como lida) — `422` expirado/revogado/já usado/já membro; `403` e-mail da conta diferente; `404` se a notificação não é de convite ou não é sua |
| `POST /notifications/{id}/decline-invitation` | — | `204` (marca como lida; mesmas regras) |

Navegação sugerida (frontend): movimentação → `/groups/{groupId}?tab=transactions` com o detalhe da movimentação; convite → ações Aceitar/Recusar na própria notificação.

## Changelog

| Data | Versão | Alteração | Autor |
|---|---|---|---|
| 2026-11-18 | 1.0 | Convenções gerais, erros, health, auth e usuário (Etapa 1) | Backend |
| 2026-11-18 | 1.0.1 | Esclarecimento (sem impacto no frontend): em `PUT /users/me/password` a sessão preservada é identificada pelo claim `sid` do access token, não pelo cookie | Backend |
| 2026-11-18 | 1.0.2 | `detail` documentado como opcional no exemplo de erro; regra para novos `code` | Coordenação |
| 2026-11-18 | 1.1 | Etapa 2: contas, snapshots, categorias, movimentações, pagamentos, histórico e dashboard individual (seções 6–9) | Coordenação (backend implementa) |
| 2026-11-18 | 1.1.1 | Esclarecimentos da implementação da Etapa 2 (**sem alterar rotas, DTOs ou códigos**): `pageSize` fora de 1–100 e `sort` desconhecido → `400 validation_error`; `categoryId`/`accountId`/`parentCategoryId` inexistentes ou de outro usuário no **corpo** → `400 validation_error` com `errors.<campo>` (mesma mensagem nos dois casos); categoria/conta inativa → `422`; subcategoria com tipo diferente do pai ou profundidade > 1 → `422`; `POST .../payments` responde `201` com o `TransactionDetailDto` atualizado; `payments` do detalhe em ordem cronológica; histórico: `field` ∈ `description, amount, category, account, paymentMethod, transactionDate, dueDate, notes, status, payment` (`category`/`account` trazem os **nomes**); `status` em `PUT` só vale para movimentações sem pagamentos (`Planned` com pagamentos → `422`); período do dashboard conta dias inclusivos (máx. 366); pagamentos simultâneos que perdem a disputa recebem `409 conflict` ou `422` | Backend |
| 2026-11-20 | 1.2 | Etapa 3: cartões, compras no cartão, parcelamentos, recorrências, relatórios, resumo de cartões no dashboard (seções 10–15; aditivo) | Coordenação (backend implementa) |
| 2026-11-20 | 1.2.1 | Esclarecimentos da implementação da Etapa 3 (**sem alterar rotas, DTOs ou códigos**; leitura mais literal do contrato): (1) nome de cartão duplicado (case-insensitive, por usuário) → `409 conflict`; (2) `PUT /transactions/{id}` de compra no cartão: `creditCardId` diferente do gravado → `422`, `null`/omitido é tolerado; `accountId` informado → `400` com `errors.accountId`; `dueDate` diferente do gravado → `400` com `errors.dueDate` (em parcela: `422`), repetir o valor gravado é tolerado; mudar `transactionDate` recalcula o vencimento pela fatura (D-025); (3) parcela: alterar `amount`/`transactionDate` → `422`; `DELETE` de parcela ou de ocorrência de recorrência → `422` (use cancelar / o plano / desativar a regra); `PUT` em ocorrência a marca como editada manualmente (preservada na regeneração, D-027); (4) parcelas: datas = `firstDate + i meses` com ajuste ao fim do mês sem deriva (31/jan → 28/fev → 31/mar); `Planned` se data > hoje, senão `Pending`; sem cartão `dueDate = transactionDate`; (5) `GET /installment-plans`: `status` é derivado das parcelas (Open = há parcela não cancelada em aberto; Canceled = todas canceladas; senão Completed), `sort` ∈ `firstDate, description, totalAmount, createdAt` (prefixo `-` desc.); (6) recorrências: `GET /recurrences` devolve array sem paginação; `startDate` até 366 dias no passado; `interval` 1–365 (em `Custom` = dias); `nextOccurrenceDate` = primeira data ≥ hoje do cronograma sem ocorrência paga/cancelada (`null` se inativa ou encerrada); `generatedCount` = ocorrências existentes; cartão/categoria/conta inativos fazem o gerador ignorar a regra (log) sem erro; (7) relatórios: base = movimentações **não canceladas** do usuário por `transactionDate`; `by-account` usa a conta da movimentação ou a do cartão, sem nenhuma → `accountId: null`, `accountName: "Sem conta"`; `by-payment-method` inclui `paymentMethod: null`; empates ordenados por nome; percentuais 0–100 com 2 casas (arredondamento comercial); `installments`: compromisso por `dueDate ?? transactionDate` do restante (`amount − paidAmount`) das parcelas não canceladas e em aberto, parcelas atrasadas somam no mês corrente, `plans` = planos `Open` por próximo vencimento; `recurrences`: somente ocorrências já geradas, não canceladas, com data > hoje e ≤ hoje + 12 meses, apenas regras ativas; `from`/`to` ausentes, `from > to` ou período > 366 dias → `400 validation_error` (`errors.from`/`errors.to`); `months` fora de 1–36 e `endMonth` fora de `YYYY-MM` → `400`; (8) exclusão da conta (`DELETE /users/me`) também apaga cartões, planos, regras e suas movimentações na mesma transação; (9) novas variáveis de ambiente `Recurrence__HorizonMonths` (12), `Recurrence__GenerationIntervalMinutes` (360) e `Recurrence__BackgroundEnabled` (true) | Backend |
| 2026-11-20 | 1.3 | Etapa 4: grupos, convites, membros, movimentações de grupo, pagamentos por membro e dashboard do grupo (seção 16; aditivo). Decisões D-030 a D-036 | Coordenação (backend implementa) |
| 2026-11-20 | 1.3.1 | Esclarecimentos da implementação da Etapa 4 (**sem alterar rotas, DTOs ou códigos**): (1) `permissions.canCancel` da movimentação de grupo também habilita `reopen`; `payments[].canRemove` só é `true` para o pagador, com grupo ativo e movimentação não cancelada; (2) grupo arquivado é somente leitura exceto `unarchive` e `leave` (demais mutações e aceitar convite → `422 business_rule_violation`, D-037); (3) `GroupInvitationDto.emailSent` só vem em `POST` de convite e `resend` (omitido nas listagens); (4) `GET /invitations/{token}`, `accept` e `decline` exigem autenticação e usam a política de rate limit `Auth`; aceitar com e-mail de outra conta → `403`; token inexistente/substituído → `404`; (5) em `expensesByCategory[].children[].percent` o percentual é sobre o total da categoria-raiz (como em `GET /reports/by-category`), não sobre o total de despesas do grupo; (6) alterar o papel para o mesmo papel atual é idempotente (`200`); (7) `memberId` que não é de membro ativo do grupo → `404`; (8) `DELETE /users/me`: categorias personalizadas usadas por movimentações de grupo são desativadas em vez de apagadas (D-038); (9) `accountId` enviado em movimentação de grupo é ignorado e nunca persistido; conta de outro usuário em pagamento de grupo → `400`/`404` | Backend |
| 2026-11-21 | 1.4 | Etapa 4b: notificações dentro do app (seção 17, aditiva); decisões D-041 a D-044 | Coordenação (backend implementa) |
