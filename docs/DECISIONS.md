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

## D-014 — Exclusão de conta — Aceita
- O usuário é **anonimizado** (nome "Usuário removido", e-mail substituído por valor único não identificável, senha invalidada, `IsActive=false`) e todos os tokens são revogados.
- Os dados **individuais** são **apagados definitivamente**: contas, snapshots de saldo, cartões, categorias personalizadas, parcelamentos, recorrências e movimentações **sem grupo** (com seus pagamentos e histórico). Tudo na mesma transação de banco.
- Movimentações **de grupo** (Etapa 4) permanecem no histórico do grupo, exibindo o autor como "Usuário removido"; os dados privados (conta/cartão) dessas movimentações são desvinculados.
- Exige confirmação por senha. A tela de exclusão deve informar isso com clareza.
- Cada etapa que cria dados individuais deve estender essa rotina e seu teste.

## D-015 — Testes de integração com PostgreSQL real — Técnica
Os testes de integração usam `WebApplicationFactory` contra um PostgreSQL real (conexão em `TEST_DATABASE_CONNECTION`, padrão `localhost:5432`). Cada execução cria um banco com nome único e o remove ao final. Para rodar localmente: `docker compose up -d db`.

## D-016 — Rate limiting nos endpoints de autenticação — Técnica
Limitador nativo do ASP.NET Core (janela fixa por IP) em login, cadastro, refresh, esqueci-senha e redefinição.

## D-017 — Navegação mobile: topbar + drawer — Técnica
Abaixo de 768px a navegação usa topbar com menu (drawer) contendo **todos** os itens do menu principal; entre 768 e 1023px sidebar recolhível (ícones); ≥ 1024px sidebar fixa. Barra inferior não adotada no MVP (10 itens não cabem sem ocultar funcionalidades).

## D-018 — Testes e asserts — Técnica
Backend usa asserts nativos do xUnit (FluentAssertions v8 tem licença comercial). Frontend usa Vitest + Vue Test Utils + axios-mock-adapter.

## D-019 — Saldo projetado — Aceita
Para uma conta e uma data-alvo `asOf` (padrão: hoje no fuso da aplicação):

`projetado = saldo do snapshot de referência + Σ receitas − Σ despesas`

- **Snapshot de referência**: o de maior `referenceDate`; em empate, o de maior `createdAt`. Sem snapshot → saldo projetado indisponível (`null`).
- **Movimentações consideradas**: da própria conta (`accountId`), **não canceladas**, com **data efetiva** `dueDate ?? transactionDate` estritamente **posterior** a `referenceDate` e **menor ou igual** a `asOf`. Entram previstas, pendentes e pagas, pelo **valor total** da movimentação (pagamentos parciais não alteram o valor projetado).
- Se `asOf <= referenceDate`, o projetado é igual ao saldo informado.
- Pagamento de fatura de cartão não é despesa (D-003; Etapa 3).
- A interface sempre rotula o valor como **projetado**.

## D-020 — Exclusão de movimentação — Aceita
Movimentação **sem pagamentos** pode ser excluída. Com pagamentos, só pode ser **cancelada** (histórico preservado). Pagamentos individuais podem ser removidos (o status é recalculado).

## D-021 — Fuso horário da aplicação — Técnica
"Hoje" (status Overdue, padrões de período, `asOf`) usa o fuso configurado em `App__TimeZone` (padrão `America/Sao_Paulo`), nunca o UTC puro. O backend é a única fonte desse cálculo; o frontend usa os valores devolvidos pela API. A imagem Docker precisa conter tzdata.

## D-022 — Categorias padrão — Técnica
Categorias padrão (`userId = null`, `isDefault = true`) são criadas por migration com **IDs fixos** (seed idempotente, sem duplicar a cada inicialização). São somente leitura para os usuários. Usuários criam subcategorias (profundidade máxima 1) sob categorias padrão ou próprias e categorias próprias. Subcategorias têm o mesmo tipo do pai.

## D-023 — Tipo e valor da movimentação — Técnica
`type` não pode ser alterado depois de criada. O `amount` não pode ficar abaixo do total já pago. Pagamentos não podem ultrapassar o valor da movimentação nem ter data futura.

## D-024 — `paid_amount` denormalizado e histórico com nomes — Técnica
- `transactions.paid_amount` é mantido exclusivamente pelo domínio (soma dos pagamentos) e protegido por constraints (`0 <= paid_amount <= amount`, coerência com o status). Facilita as agregações do dashboard sem junções.
- O histórico (`transaction_audit_entries`) grava, para `category` e `account`, o **nome** vigente na época (não o id), e pagamentos no formato `"200.00 (2026-11-18)"` (campo `payment`). O frontend interpreta esse formato.
- Ids de categoria, conta ou categoria-pai inexistentes/de outro usuário **no corpo** → `400 validation_error` (não vaza existência); `404` fica para ids na rota.
- Projeção (D-019): movimentações na **mesma data** do snapshot não entram (o saldo informado vale para o fim do dia).

## D-025 — Compras no cartão (detalha D-003) — Aceita
- `Transaction` ganha `creditCardId` (opcional, **imutável** após criar). Só **despesas** podem ter cartão.
- Compra no cartão: `accountId` **não é aceito** (a conta é a associada ao cartão, `credit_cards.account_id`); `dueDate` **não é aceito** (é calculado pelo backend); `paymentMethod` é `CreditCard` (enviar outro valor → 400).
- **Vencimento da fatura** (calculado no backend a partir de `transactionDate`, `closingDay`, `dueDay` do cartão): se o dia da compra ≤ `closingDay` do mês (ajustado ao último dia do mês quando o dia não existe), a fatura fecha nesse mês; senão, no mês seguinte. O vencimento é o `dueDay` do mês de fechamento, ou do mês seguinte se `dueDay <= closingDay`; dia inexistente → último dia do mês. O `dueDate` é **gravado** na compra; mudar fechamento/vencimento do cartão depois **não** recalcula compras já feitas.
- **Saldo projetado (estende D-019):** entram, para a conta X, as movimentações com `accountId = X` **e** as compras no cartão cujo cartão tenha `accountId = X`. Data efetiva = `dueDate` (vencimento da fatura). Cartão sem conta associada não impacta nenhuma conta.
- Relatórios "por conta": conta efetiva = `accountId` da movimentação, ou a conta do cartão; sem nenhuma → "Sem conta".
- Pagar a fatura **não** gera despesa. O pagamento de uma compra é registrado como pagamento da própria compra (sem gestão de faturas no MVP).
- **Limite utilizado** = soma de `amount − paidAmount` de **todas** as compras/parcelas **não canceladas** do cartão (inclui parcelas futuras). `limite disponível = limite − utilizado` (pode ser negativo → apresentado como excedido).
- **Fatura aberta** (resumo): a fatura que recebe compras feitas **hoje**: `dueDate` calculado para "hoje"; valor = soma do restante das compras não canceladas com esse `dueDate`.
- Cartão inativo não aceita novas compras/parcelamentos/recorrências (422); histórico permanece.

## D-026 — Parcelamentos — Aceita
- `InstallmentPlan` (`firstDate` generaliza o `FirstDueDate` do CLAUDE.md: para cartão é a data da compra; sem cartão também é o vencimento da 1ª parcela).
- Parcela *i* (1..N): `transactionDate = firstDate + (i−1) meses` (dia ajustado ao último dia do mês quando não existe; sempre calculado a partir do dia de `firstDate`). Sem cartão: `dueDate = transactionDate`. Com cartão: `dueDate` pelo cálculo da fatura (D-025) a partir da `transactionDate` de cada parcela.
- **Centavos (decisão do responsável):** `base = floor(total/N, 2 casas)`; a **primeira parcela absorve toda a diferença** (`total − base×N`, no máximo N−1 centavos). Ex.: 100,00 em 3x = 33,34 + 33,33 + 33,33. A soma das parcelas é **sempre** igual ao total.
- Situação inicial de cada parcela: `Planned` se `transactionDate > hoje`, senão `Pending`. Descrição de cada parcela = descrição do plano; o número aparece em `installment { number, count }`.
- 2 ≤ N ≤ 120. Parcelas individuais: **valor e datas imutáveis** (422); podem ser pagas, canceladas, e ter categoria/conta/observações editadas (e vencimento apenas sem cartão). **Não podem ser excluídas individualmente** (422).
- Cancelar plano: cancela as parcelas **sem pagamento** que não estejam canceladas; parcelas com pagamento permanecem. Excluir plano: só se **nenhuma** parcela tiver pagamento (remove plano e parcelas); senão 422.
- Estado do plano (derivado): `Canceled` se todas as parcelas canceladas; `Completed` se não há parcela em aberto (`restante > 0` e não cancelada); senão `Open`.

## D-027 — Recorrências — Aceita
- Passo: `Daily`=1 dia, `Weekly`=7 dias, `Biweekly`=14 dias, `Monthly`=1 mês, `Bimonthly`=2, `Quarterly`=3, `Semiannual`=6, `Annual`=12 meses, **multiplicado por `interval`** (padrão 1). `Custom` = a cada `interval` **dias**. Ocorrência *k* = `startDate + k×passo`; para meses, calculada **sempre a partir de `startDate`** (31/jan → 28/fev → 31/mar), nunca da ocorrência anterior.
- Cada ocorrência é uma `Transaction` independente com `recurrenceRuleId` e `occurrenceDate`; **índice único (`recurrence_rule_id`, `occurrence_date`)** garante idempotência. `transactionDate = occurrenceDate`; sem cartão `dueDate = occurrenceDate`; com cartão, `dueDate` pela fatura. Situação inicial: `Planned` se a data > hoje, senão `Pending`.
- **Horizonte:** gera até hoje + 12 meses (`Recurrence__HorizonMonths`, D-010), respeitando `endDate`. Gerado na criação/ativação/edição da regra e por um **serviço em segundo plano** (executa na inicialização e a cada `Recurrence__GenerationIntervalMinutes`, padrão 360). `Recurrence__BackgroundEnabled=false` desliga o serviço (testes). O gerador ignora datas que já tenham ocorrência (inclusive canceladas).
- `startDate` ≥ hoje − 366 dias (ocorrências passadas são geradas e aparecem como atrasadas se em aberto). `type` e `startDate` são imutáveis.
- **Editar/desativar (decisão do responsável):** ocorrências **futuras** (`transactionDate > hoje`) em `Planned`/`Pending`, **sem pagamentos** e **nunca editadas à mão** (`is_manually_edited = false`) são **removidas** e regeneradas com a nova regra (desativar apenas remove). Pagas, vencidas, canceladas e editadas à mão permanecem. Reativar regenera.
- Ocorrências **não podem ser excluídas individualmente** (422: cancele-a ou desative a regra). Editar uma ocorrência (campos permitidos como em movimentações comuns) marca `is_manually_edited`. Regras não são excluídas, apenas desativadas.
- Cartão: ocorrências de regra com cartão seguem D-025 (cartão inativo → a geração é ignorada e registrada em log).

## D-028 — Relatórios — Técnica
Base: movimentações **não canceladas** com `transactionDate` no período (mesma base dos totais do dashboard). Percentuais 0–100 com 2 casas; categorias agrupadas pela raiz (com detalhamento das subcategorias). "Comprometimento futuro" de parcelamentos usa a data efetiva `dueDate ?? transactionDate` e o **restante** das parcelas em aberto. O relatório de recorrências usa apenas ocorrências **já geradas** (sem estimativas). Relatórios de grupos: Etapa 4.

## D-029 — Exclusão de conta estendida — Aceita (D-014)
`DeleteAccount` passa a apagar também cartões, parcelamentos e recorrências do usuário (movimentações já eram apagadas). Teste obrigatório.
