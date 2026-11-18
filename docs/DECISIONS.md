# Registro de decisões (ADR resumido)

Cada decisão possui identificador, status e justificativa. Alterações exigem nova entrada (não reescrever o histórico).

Status: **Aceita** (confirmada pelo responsável do produto), **Técnica** (decisão de engenharia dentro das diretrizes do CLAUDE.md), **Proposta** (aguardando confirmação antes de implementar a parte afetada).

---

## D-001 — Biblioteca de componentes: PrimeVue 4 — Aceita
Componentes ricos (DataTable, formulários, diálogos, toasts), boa acessibilidade (WAI-ARIA), tema customizável via design tokens (preset Aura) e suporte a modo escuro.

## D-002 — Biblioteca de gráficos: Apache ECharts via vue-echarts — Aceita
Gráficos responsivos, suporte a pizza/rosca, barras, linhas e séries temporais; importação modular (tree-shaking) para reduzir o bundle.

## D-003 — Compras no cartão de crédito e saldo projetado — Aceita
Uma compra no cartão é registrada como **uma única despesa** (`creditCardId` preenchido).
- Ela **impacta o saldo projetado da conta associada ao cartão na data de vencimento da fatura** em que a compra cai (calculada a partir de `closingDay`/`dueDay`).
- O pagamento da fatura **não** é registrado como nova despesa; portanto não há dupla contagem nos relatórios.
- Cartões sem conta associada não impactam o saldo projetado de nenhuma conta.
- Regra de ciclo: compra com dia **até** o dia de fechamento (inclusive) entra na fatura que fecha naquele mês; compra após o fechamento entra na fatura do mês seguinte. O vencimento é o `dueDay` posterior ao fechamento (se `dueDay <= closingDay`, vence no mês seguinte ao fechamento). Dias inexistentes no mês (ex.: 31) são ajustados para o último dia do mês.

## D-004 — Envio de e-mails: SMTP configurável + Mailpit em desenvolvimento — Aceita
A API usa SMTP configurado por variáveis de ambiente (`Smtp__*`). No `docker-compose.yml` o serviço `mailpit` captura os e-mails localmente (UI em http://localhost:8025). Nenhum e-mail real é enviado em desenvolvimento.

## D-005 — Transporte do refresh token: cookie httpOnly — Técnica
- Access token (JWT, 15 min) retornado no corpo e mantido **apenas em memória** no frontend (não em localStorage).
- Refresh token (opaco, 64 bytes aleatórios, 7 dias) enviado em cookie `gf_refresh` com `HttpOnly`, `Secure` (fora de Development), `SameSite=Strict`, `Path=/api/v1/auth`.
- No banco é armazenado apenas o **hash SHA-256** do refresh token.
- **Rotação** a cada uso; reutilização de um token já rotacionado revoga toda a família (detecção de roubo).
- Logout revoga o token atual. **Redefinição** de senha revoga todos os tokens do usuário. **Troca** de senha (usuário logado) revoga todos exceto a sessão atual, identificada pelo claim `sid` (família do refresh token) presente no access token — o cookie não chega a `/users/*` por causa do `Path=/api/v1/auth`.
- O access token continua válido até expirar (≤ 15 min) após troca de senha; um "security stamp" pode ser avaliado no futuro.

## D-006 — Mesma origem para frontend e API — Técnica
Em Docker, o Nginx do frontend faz proxy de `/api` para a API. Em desenvolvimento local, o Vite faz o mesmo proxy. Isso permite cookie `SameSite=Strict` sem CORS. CORS fica configurado apenas para origens explícitas listadas em `Cors__AllowedOrigins`.

## D-007 — Hash de senhas: PasswordHasher do ASP.NET Core Identity (PBKDF2-HMAC-SHA512, 100k iterações) — Técnica
Sem adotar o Identity completo (evita tabelas e abstrações desnecessárias). Política: mínimo 8 caracteres, ao menos uma letra e um número, máximo 128.

## D-008 — Formato de erros: ProblemDetails (RFC 7807) — Técnica
Toda resposta de erro usa `application/problem+json` com extensão `code` (código estável para o frontend) e `errors` (dicionário campo → mensagens) em erros de validação. Detalhes internos nunca são retornados fora de Development.

## D-009 — Status da movimentação — Técnica
- Persistidos: `Planned`, `Pending`, `Paid`, `Received`, `Canceled` (derivados dos pagamentos e de ações explícitas).
- `Overdue` é **calculado na leitura** (vencimento < hoje e não liquidada e não cancelada) — evita jobs de atualização e estados inconsistentes.
- Detalhes da máquina de estados serão documentados em `docs/STATE_MACHINE.md` na Etapa 2.

## D-010 — Recorrências: horizonte de geração de 12 meses — Técnica
Ocorrências são geradas até 12 meses à frente (configurável). Constraint única (`RecurrenceRuleId`, `OccurrenceDate`) garante idempotência.

## D-011 — Datas — Técnica
Datas financeiras (`transactionDate`, `dueDate`, `referenceDate`) usam `DateOnly` (`"2026-11-18"`). Carimbos de auditoria usam `DateTimeOffset` em UTC (ISO 8601). Fuso de apresentação: o do navegador.

## D-012 — Migrations aplicadas na inicialização apenas quando habilitado — Técnica
`Database__ApplyMigrationsOnStartup=true` no compose de desenvolvimento. Em produção recomenda-se aplicar via `dotnet ef database update` ou bundle de migrations no pipeline.

## D-013 — Atualização de perfil — Técnica
No MVP o perfil permite alterar **nome**. Alteração de e-mail exige fluxo de verificação e fica registrada como pendência (não implementada sem autorização).

## D-014 — Exclusão de conta — Proposta
Para cumprir "histórico de grupos preservado" e privacidade:
- Usuário é **anonimizado** (nome "Usuário removido", e-mail substituído por valor único não identificável, senha invalidada, `IsActive=false`), tokens revogados.
- Dados **individuais** (contas, cartões, movimentações sem grupo, snapshots) serão excluídos quando esses módulos existirem (Etapa 2/3).
- Movimentações **de grupo** permanecem, exibindo o autor anonimizado.
- Exige confirmação por senha.
*Aguardando confirmação da regra de exclusão dos dados individuais antes da Etapa 2.*

## D-015 — Testes de integração com PostgreSQL real — Técnica
Os testes de integração usam `WebApplicationFactory` contra um PostgreSQL real (conexão em `TEST_DATABASE_CONNECTION`, padrão `localhost:5432`). Cada execução cria um banco com nome único e o remove ao final. Para rodar localmente: `docker compose up -d db`.

## D-016 — Rate limiting nos endpoints de autenticação — Técnica
Limitador nativo do ASP.NET Core (janela fixa por IP) em login, cadastro, refresh, esqueci-senha e redefinição.

## D-017 — Navegação mobile: topbar + drawer — Técnica
Abaixo de 768px a navegação usa topbar com menu (drawer) contendo **todos** os itens do menu principal; entre 768 e 1023px sidebar recolhível (ícones); ≥ 1024px sidebar fixa. Barra inferior não adotada no MVP (10 itens não cabem sem ocultar funcionalidades).

## D-018 — Testes e asserts — Técnica
Backend usa asserts nativos do xUnit (FluentAssertions v8 tem licença comercial). Frontend usa Vitest + Vue Test Utils + axios-mock-adapter.
