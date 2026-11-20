# Plano de implementação

Entregas incrementais, cada etapa testada e integrada antes da próxima. Status: ⬜ não iniciada · 🟡 em andamento · ✅ concluída.

## Etapa 1 — Fundação ✅
> Validada: build, 92 testes backend (62 unit + 30 integração PostgreSQL), 59 testes frontend, E2E via proxy (cadastro, login, refresh por cookie, logout, esqueci/redefinir com SMTP real). **Pendente:** executar `docker compose up --build` em máquina com Docker.
| Item | Backend | Frontend |
|---|---|---|
| Estrutura | Solução Clean Architecture, DI, Serilog, ProblemDetails, Swagger, health | Vite + Vue 3 + TS, PrimeVue, Router, Pinia, Axios, layouts |
| Banco | EF Core + Npgsql, migration inicial (`users`, `refresh_tokens`, `password_reset_tokens`) | — |
| Autenticação | Cadastro, login, refresh com rotação, logout, esqueci/redefinir senha (SMTP), rate limit | Telas de login, cadastro, esqueci/redefinir senha; guards de rota; refresh automático |
| Perfil | `GET/PUT /users/me`, troca de senha, exclusão de conta | Tela de Configurações (perfil, senha, excluir conta) |
| Infra | Dockerfile API | Dockerfile + Nginx (proxy `/api`) |
| Testes | Unitários (validação, tokens), integração (fluxos de auth com PostgreSQL) | Stores, serviço HTTP, formulários de auth |

## Etapa 2 — Controle financeiro individual ✅
> Validada: backend 407 testes (291 unit + 116 integração PostgreSQL), frontend 262 testes, E2E real (API + navegador) cobrindo contas, categorias, movimentações, pagamentos parcial/total, histórico, dashboard e isolamento entre usuários.
Contratos: `API_CONTRACTS.md` §6–9 · Regras: `STATE_MACHINE.md`, D-014, D-019–D-023. Inclui também a rotina de exclusão de dados individuais (D-014).
Contas + snapshots + saldo projetado; categorias (seed padrão, personalizadas, subcategorias); movimentações (CRUD, filtros, auditoria); pagamentos parciais/integrais e máquina de estados; dashboard individual.

## Etapa 3 — Recursos financeiros ✅
Contratos: `API_CONTRACTS.md` §10–15 · Regras: D-025 a D-029, `STATE_MACHINE.md`.
Cartões (regra D-003); parcelamentos (diferença de centavos na primeira parcela (D-026)); recorrências (horizonte D-010, idempotência); relatórios.

## Etapa 4 — Grupos 🟡
Grupos, convites por token (hash), participantes (saída preserva histórico), movimentações compartilhadas (DTO sem dados privados), dashboard consolidado (participação por autor **e** por pagador, separadas).

## Etapa 5 — Qualidade ⬜
Ampliação de testes, revisão de segurança, responsividade, acessibilidade, documentação final.

## Pendências de decisão
- Alteração de e-mail do perfil (D-013) — fora do escopo até autorização.
