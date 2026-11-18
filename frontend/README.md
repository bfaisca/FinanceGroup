# GroupFinance — Frontend

SPA em Vue 3 + TypeScript (Composition API, `<script setup>`), Vite, Vue Router, Pinia, Axios, PrimeVue 4 (preset Aura customizado, modo escuro por classe `app-dark`) e ECharts via vue-echarts.

Contratos consumidos: [`docs/API_CONTRACTS.md`](../docs/API_CONTRACTS.md). Decisões: [`docs/DECISIONS.md`](../docs/DECISIONS.md).

## Requisitos

Node.js ≥ 22.12 e npm.

## Scripts

| Comando | Descrição |
|---|---|
| `npm install` | Instala as dependências |
| `npm run dev` | Servidor de desenvolvimento em http://localhost:5173 |
| `npm run build` | Type-check + build de produção em `dist/` |
| `npm run preview` | Serve o build localmente (com o mesmo proxy) |
| `npm run type-check` | `vue-tsc --noEmit` (app, testes e configs) |
| `npm run lint` / `lint:fix` | ESLint (flat config, Vue + TypeScript) |
| `npm run format` / `format:check` | Prettier |
| `npm run test` | Vitest (watch); `npm run test -- --run` executa uma vez |

## Proxy e mesma origem (D-006)

O frontend chama a API sempre em `/api/v1` (mesma origem), o que permite o refresh token em cookie `httpOnly` + `SameSite=Strict` sem CORS.

- **Desenvolvimento**: o Vite encaminha `/api` e `/health` para `VITE_API_PROXY_TARGET` (padrão `http://localhost:5000`, a porta publicada da API no `docker-compose.yml`). Para mudar, copie `.env.example` para `.env.local`.
- **Docker**: o `nginx.conf` serve a SPA (`try_files … /index.html`) e faz `proxy_pass` de `/api/` e `/health` para `http://api:8080`, preservando `Host`, `X-Forwarded-For` e `X-Forwarded-Proto`. Assets com hash têm cache de 1 ano; `index.html` usa `no-cache`. Cabeçalhos de segurança em `nginx/security-headers.conf`.

```bash
# na raiz do repositório
docker compose up --build   # web em http://localhost:8080
```

## Sessão (D-005)

- O access token fica **somente em memória** (store `auth`); o refresh token é o cookie `gf_refresh`, inacessível ao JavaScript.
- Ao iniciar, o guard do router aguarda `auth.bootstrap()`, que tenta `POST /auth/refresh` para restaurar a sessão.
- `services/http.ts` injeta o `Bearer` e, em `401` (exceto `/auth/*`), faz **um** refresh compartilhado entre requisições concorrentes e repete a requisição. Se o refresh falhar, a sessão é limpa e o usuário vai para `/login?redirect=…`.
- Erros da API (ProblemDetails, D-008) são normalizados por `utils/problem.ts` em `{ message, code, status, fieldErrors }`.

## Estrutura

```text
src/
├── assets/styles/        # CSS global (tokens --gf-*, foco visível, skip-link)
├── components/           # Reutilizáveis: EmptyState, LoadingState, PageHeader, FormField,
│   │                     # FinancialCard, AppConfirmDialog, ModulePlaceholder, NotFoundPage
│   ├── charts/           # BaseChart (ECharts modular)
│   └── layout/           # AppNav, UserMenu, ThemeToggle, BrandLogo
├── composables/          # useTheme, useFormErrors, useMediaQuery
├── layouts/              # AuthLayout, AppLayout, navigation.ts (10 itens do menu)
├── modules/              # auth, dashboard, accounts, credit-cards, categories, transactions,
│                         # installments, recurrence, groups, reports, settings
├── plugins/primevue.ts   # PrimeVue, preset de tema, locale pt-BR
├── router/               # rotas (lazy), guards, tipos de meta
├── services/             # http.ts (Axios único), setupSession.ts (ponte http ↔ store)
├── stores/               # auth (Pinia)
├── types/api.ts          # tipos espelhando o contrato
└── utils/                # format, problem, validation, redirect
```

Testes ficam em `__tests__/` ao lado do código testado.

## Layout responsivo

- **Desktop (≥ 1024px)**: sidebar fixa expandida.
- **Tablet (768–1023px)**: sidebar recolhida (ícones, com rótulos acessíveis e tooltip), expansível.
- **Mobile (< 768px)**: topbar + drawer com **todos** os itens do menu.
