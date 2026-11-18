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

## Changelog

| Data | Versão | Alteração | Autor |
|---|---|---|---|
| 2026-11-18 | 1.0 | Convenções gerais, erros, health, auth e usuário (Etapa 1) | Backend |
| 2026-11-18 | 1.0.1 | Esclarecimento (sem impacto no frontend): em `PUT /users/me/password` a sessão preservada é identificada pelo claim `sid` do access token, não pelo cookie | Backend |
| 2026-11-18 | 1.0.2 | `detail` documentado como opcional no exemplo de erro; regra para novos `code` | Coordenação |
