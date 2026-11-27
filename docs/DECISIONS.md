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

---

# Etapa 4 — Grupos

## D-030 — Papéis e permissões de grupo — Aceita (papéis) / Proposta (detalhes marcados ⚠)
- Papéis: **Owner** (criador; tem tudo o que o Admin tem; não pode sair, ser removido nem rebaixado ⚠ — transferência de propriedade fica fora do MVP), **Admin**, **Member**.
- **Owner/Admin:** convidar (e reenviar/revogar convites), remover **Members** (o Owner também remove Admins), promover Member→Admin e rebaixar Admin→Member (⚠ só o Owner rebaixa Admin), editar nome/descrição, arquivar/reativar o grupo.
- **Member:** ver o grupo, cadastrar movimentações, registrar o próprio pagamento, sair.
- Admin/Owner **não** editam, cancelam, excluem nem pagam em nome de movimentações de terceiros (CLAUDE.md §6).
- Grupo **arquivado** é somente leitura: sem novas movimentações, edições, cancelamentos ou pagamentos; some da lista padrão (`includeArchived=true` mostra). Grupos nunca são excluídos (preservar histórico).
- Quem não é membro **ativo** (nunca foi, saiu ou foi removido) recebe `404` em qualquer rota do grupo (não vazar existência). Papel insuficiente → `403`.

## D-031 — Convites por e-mail — Aceita
- Admin/Owner informa o **e-mail**; o sistema gera token aleatório (32 bytes, base64url), guarda **apenas o SHA-256** e envia o link `{App:FrontendBaseUrl}/invitations/{token}` por SMTP (Mailpit em dev). O token nunca é retornado pela API nem logado.
- Validade de **7 dias**. Um convite `Pending` por (grupo, e-mail); novo convite para o mesmo e-mail com um pendente → `409 conflict`. **Reenviar** gera novo token e nova validade (o anterior deixa de valer).
- O convidado não precisa ter conta no momento do convite: cadastra-se e aceita logado. Só aceita quem está logado com o **mesmo e-mail** do convite (comparação sem diferenciar maiúsculas); outro e-mail → `403`.
- Estados: `Pending`, `Accepted`, `Declined`, `Revoked`, `Expired` (expiração derivada na leitura: `Pending` com `expiresAt` no passado). Aceitar convite de quem já é membro ativo → `422`.
- Falha no envio do e-mail **não** impede criar o convite (`emailSent=false`; Admin pode reenviar).
- Ex-membros (saíram/removidos) podem ser convidados de novo: o registro `GroupMember` é **reativado** (mesma linha, índice único GroupId+UserId), `status=Active`, `joinedAt` atualizado, `leftAt=null`; o histórico de movimentações e pagamentos permanece.

## D-032 — Movimentações de grupo — Aceita
- Mesma tabela/entidade `Transaction` com `GroupId` preenchido; **um único registro** por movimentação. **Sem** `AccountId`, `CreditCardId`, parcelamento ou recorrência (⚠ parcelamentos/recorrências em grupo ficam fora do MVP).
- Criar: membro **ativo** de grupo **ativo** (não arquivado). Autoria = `CreatedByUserId`. Editar/cancelar/reabrir/excluir: **somente o criador**, ainda membro ativo (se o criador saiu, ninguém edita; os pagamentos continuam possíveis). Excluir só sem pagamentos; senão cancelar (D-020).
- Categoria: padrão do sistema **ou** categoria do próprio criador (nome/cor/ícone ficam visíveis ao grupo; a categoria em si continua sendo do criador).
- **Pagamentos (qualquer membro ativo registra o próprio):** o pagador/recebedor é `CreatedByUserId` do pagamento (não existe pagador único na movimentação). Vários pagadores por movimentação (pagamentos parciais) são permitidos; só quem registrou remove o próprio pagamento. Mesma máquina de estados (STATE_MACHINE.md): total pago ≤ valor, `paymentDate` ≤ hoje, nada em movimentação cancelada.
- O pagamento aceita `accountId` opcional **do próprio pagador**: é privado — nunca aparece em nenhum DTO de grupo e só serve ao individual (D-033).
- Histórico de auditoria igual ao individual, com o **nome** de quem agiu.

## D-033 — Impacto no controle individual — Aceita
- Movimentações de grupo **não** entram em listagens, dashboard, relatórios, limites de cartão nem projeções **individuais** como movimentações.
- Única ligação: um **pagamento** de grupo com `accountId` informado (conta do próprio pagador) altera o **saldo projetado dessa conta** na `paymentDate` (despesa subtrai, receita soma), respeitando a regra do snapshot (estritamente após a data do snapshot, até `asOf`). Sem conta no pagamento → só o grupo.
- Toda consulta individual existente passa a filtrar `GroupId == null`.

## D-034 — Dashboard do grupo — Técnica
Período `[from, to]` por `transactionDate`, movimentações **não canceladas**.
- Totais: `expenses`/`income` (soma dos valores), `expensesPaid`/`incomeReceived` (soma dos pagamentos), `expensesPending`/`incomePending` (valor − pago). Pendente nunca conta como pago.
- Participação **por quem cadastrou** (`createdBy`): soma do valor das movimentações por `CreatedByUserId`; percentual sobre o total do período do mesmo tipo.
- Participação **por quem pagou** (`paidBy`): soma dos **pagamentos** por pagador (de movimentações do período); percentual sobre o total pago do mesmo tipo. Os dois critérios são sempre exibidos separados, com a regra de cálculo explícita na interface; são indicadores estatísticos (CLAUDE.md §6), não dívidas.
- Também: despesas por categoria (raiz, com subcategorias), evolução mensal (`months` 1–36 terminando em `endMonth`, padrão 6 e mês de `to`), comparação com o período anterior de mesma duração.
- Membros que saíram/foram removidos continuam aparecendo (com `status`); contas removidas aparecem como "Usuário removido". Percentuais 0–100 com 2 casas.

## D-035 — Privacidade nos DTOs de grupo — Técnica
Membros aparecem com `userId`, `name`, papel, status e datas — **sem e-mail**. O e-mail só aparece na lista de convites (Admin/Owner). Nenhum DTO de grupo contém conta, cartão, saldo ou qualquer movimentação individual. Testes de privacidade obrigatórios.

## D-036 — Exclusão de conta com grupos — Aceita (estende D-014/D-029)
Além de D-014: memberships ativas passam a `Left`; convites pendentes enviados pelo usuário são revogados. Se o usuário era **Owner** de um grupo com outros membros ativos, a propriedade passa ao Admin ativo mais antigo (senão ao Member ativo mais antigo); se não restar nenhum membro ativo, o grupo é arquivado. Movimentações e pagamentos de grupo do usuário permanecem, com autor "Usuário removido" e `accountId` de pagamentos desvinculado.

## D-037 — Grupo arquivado é somente leitura — Proposta
Em grupo arquivado só é possível ler, **reativar** (`unarchive`) e **sair** (`leave`). Convidar, editar o grupo, alterar papéis, remover membros, criar/editar/cancelar/excluir movimentações e registrar/remover pagamentos retornam `422 business_rule_violation`. Aceitar um convite para grupo arquivado também retorna 422. Motivo: comportamento mais conservador (preserva o histórico congelado); alternativa seria permitir pagamentos em aberto.

## D-038 — Categorias preservadas na exclusão de conta — Proposta
Categorias personalizadas do usuário que ainda são usadas por movimentações de grupo (e suas categorias-pai) não são apagadas em `DELETE /users/me`: são **desativadas**, para não quebrar a FK nem o histórico do grupo. As demais categorias individuais continuam sendo removidas (D-014).

## D-039 — Pagamentos de movimentação cancelada no saldo projetado — Proposta
Pagamentos de grupo vinculados a uma movimentação `Canceled` não entram no saldo projetado da conta do pagador (coerente com STATE_MACHINE: cancelada não afeta saldos). Reabrir a movimentação os traz de volta ao cálculo.

## D-040 — Sair de grupo arquivado e reativação — Proposta
O membro (inclusive Admin) pode sair de um grupo arquivado; o Owner não pode sair (precisa transferir/excluir conta). Quem retorna por novo convite reaproveita a mesma linha de `group_members` com papel `Member`, recuperando a edição das próprias movimentações antigas (autoria é por `created_by_user_id`).


---

# Etapa 4b — Notificações no app

## D-041 — Notificações básicas dentro do app — Aceita (autorizada pelo usuário em 2026-11-21)
CLAUDE.md §17 deixa "notificações avançadas" fora do MVP; o usuário autorizou expressamente **notificações básicas dentro da plataforma**, limitadas a: sino com contador, lista, marcar como lida e ação de aceitar/recusar convite. **Fora do escopo** (continuam proibidos sem nova autorização): push, e-mail de atividade/resumo, preferências por grupo/tipo, websockets/tempo real.

## D-042 — Eventos e destinatários — Aceita
Tipos: `GroupTransactionCreated` (nova movimentação de grupo), `GroupPaymentRegistered` (pagamento/recebimento registrado) e `GroupInvitationReceived` (convite). Edição, cancelamento, reabertura e entrada/saída de membros **não** notificam nesta versão.
- Destinatários de eventos de movimentação/pagamento: todos os **membros ativos** do grupo no momento do evento, **exceto quem executou a ação**.
- A notificação é criada na **mesma transação** do evento (se o evento falhar, nada é notificado).
- Guarda um **instantâneo** do necessário (descrição, valor do evento — da movimentação ou do pagamento —, tipo, ator, grupo); o texto é montado pelo frontend. O nome do ator é lido da conta na consulta (conta excluída → "Usuário removido").
- Privacidade: a notificação só carrega o que já é visível ao grupo (D-035); nunca conta, cartão ou dados individuais.

## D-043 — Convite dentro da plataforma — Aceita
- Ao criar ou reenviar um convite, se já existe conta ativa com o **mesmo e-mail** (sem diferenciar maiúsculas), cria-se uma notificação `GroupInvitationReceived` para essa conta; **o e-mail continua sendo enviado** (D-031).
- Quem cria a conta depois, com o e-mail convidado, recebe as notificações dos convites `Pending` não expirados ao se **cadastrar** (por e-mail igual, sem exigir verificação extra: o convite já exigia e-mail igual ao aceitar).
- Aceitar/recusar pela notificação não usa token: `POST /notifications/{id}/accept-invitation|decline-invitation`, valido só para o dono da notificação e se o e-mail da conta ainda é o do convite. Mesmas regras de D-031 (expirado/revogado/já usado/já membro → 422). O estado do convite na notificação é **derivado ao consultar** (aceito por e-mail, revogado e expirado aparecem corretamente, sem ações).
- Reenviar o convite invalida o token anterior mas não duplica a notificação pendente: reaproveita a existente (marca como não lida).

## D-044 — Visibilidade, leitura e atualização — Técnica
- Notificações de grupo só aparecem/contam enquanto o destinatário for **membro ativo** do grupo (saiu/removido → somem da lista e do contador, sem apagar o registro). Convites não dependem disso.
- Marcar como lida: individual e todas. Aceitar/recusar convite marca a notificação como lida.
- Sem tempo real: o frontend atualiza o contador ao carregar o app, ao abrir o sino, ao trocar de rota e ao voltar o foco à aba (sem polling por timer).
- Retenção: notificações lidas há mais de 90 dias são removidas pelo serviço de background já existente (junto à manutenção periódica) — se não houver rotina, deixar como pendência registrada.
- Exclusão de conta (D-014/D-036): as notificações do usuário são apagadas.

## D-045 — Detalhes de implementação das notificações — Proposta
Escolhas conservadoras para o que D-041 a D-044 não detalham (sem alterar rotas ou DTOs):
- **Mesma transação:** as notificações são adicionadas ao `DbContext` e persistidas no mesmo `SaveChanges` do evento (criar movimentação, registrar pagamento, criar/reenviar convite, cadastro). Não há fila nem tarefa assíncrona; se a gravação da notificação falhar, o evento também falha (é a garantia de D-042).
- **Reenvio do convite:** além de voltar a ser não lida, a notificação reaproveitada tem `createdAt` atualizado (sobe ao topo da lista) e o ator passa a ser quem reenviou.
- **Convite não gera notificação** se a conta com o e-mail já é membro ativo do grupo (reenvio) ou se a conta está inativa/anonimizada.
- **Visibilidade em ações:** marcar como lida uma notificação de grupo da qual o usuário já não é membro ativo responde `404` (mesma regra da listagem). `read-all` só altera as notificações visíveis. Notificações de convite sempre são visíveis ao destinatário.
- **`canRespond`** segue exatamente o §17 (Pending não expirado + e-mail da conta = e-mail do convite + não é membro ativo); não considera grupo arquivado: aceitar nesse caso responde `422` (D-037), recusar continua possível.
- **Aceitar/recusar por token** (`/invitations/{token}/...`) também marca como lida a notificação do convite do mesmo usuário, se existir.
- **Exclusão de movimentação de grupo** (só sem pagamentos) remove, por cascata, as notificações dela. Notificações em que o usuário excluído é o **ator** permanecem (histórico do grupo) e exibem "Usuário removido".
- **Retenção:** `NotificationRetention.PurgeReadAsync` roda a cada execução do serviço de background existente (`RecurrenceGenerationHostedService`, mesmo intervalo e mesmo `Recurrence__BackgroundEnabled`); falha nela é apenas registrada. Notificações não lidas nunca expiram por tempo.


---

# Etapa 4c — Ajustes de grupos pedidos pelo usuário (2026-11-21)

## D-046 — Grupos só têm despesas — Aceita
Receitas não existem em grupos: não se cria (`422`), não aparece em listagens, dashboard, notificações nem projeção; some o filtro de tipo e a variante "recebeu". A regra é garantida **no banco**: o check `ck_transactions_group_rules` exige `type = 'Expense'` quando `group_id IS NOT NULL` (junto de "sem conta/cartão/parcelamento/recorrência"), sem tratamento de linhas legadas (o produto ainda não foi lançado). Emenda D-032/D-034/D-042.

## D-047 — Lançar já paga e vencimento livre — Aceita
`CreateGroupTransactionRequest.payment` cria a conta e o primeiro pagamento do criador na mesma transação (data de pagamento ≤ hoje, qualquer data passada; conta privada opcional). O vencimento (`dueDate`) é livre, inclusive no passado e sem relação mínima com a data da movimentação, **apenas para movimentações de grupo**. Criar já paga gera as duas notificações (criação e pagamento).

## D-048 — Juros/multa no pagamento (valor original × efetivamente pago) — Aceita
- A conta mantém o **valor original** (`amount`). Cada pagamento tem `amount` (principal; Σ ≤ valor da conta, define a situação Pendente/Paga) e `interestAmount` (juros/multa ≥ 0, fora dessa regra). Efetivamente pago = principal + juros.
- O pagador pode **editar o próprio pagamento** (valor, juros, data, conta privada, método) enquanto a conta não estiver cancelada e o grupo ativo; histórico `PaymentUpdated`.
- Dashboard: `expensesPaid` = principal pago + juros; `expensesPending` = original − principal pago; juros destacados à parte. "Contas Pagas" soma principal + juros por pessoa.
- Saldo projetado individual (D-033): um pagamento de grupo com `accountId` subtrai principal + juros.
- Notificação de pagamento mostra o valor total pago.

**Ajuste (contas privadas na edição):** como a API nunca devolve `accountId`, o `PUT` do pagamento mantém a conta atual quando `accountId` é omitido; `clearAccount: true` desvincula; `accountId` troca. Evita que editar juros apague silenciosamente o vínculo com o saldo individual.

## D-049 — Recorrência no grupo (N ocorrências geradas na hora) — Aceita (emenda D-032 ⚠)
`recurrence {frequency, occurrences 2–60}`: cria todas as ocorrências de imediato, cada uma uma conta independente do grupo (mesmo valor, categoria, criador). Frequências: semanal, quinzenal, mensal, bimestral, trimestral, semestral, anual (sem diária nem personalizada). Datas: `transactionDate + (i−1)·passo`, com ajuste de fim de mês como D-026; o vencimento, se houver, mantém o mesmo deslocamento em dias da data da movimentação. Tabela `group_transaction_series`; cada ocorrência carrega `series {id, number, total}`. Depois de criada, cada ocorrência é editada/paga/cancelada/excluída isoladamente (para refletir juros em um mês). Ações em lote na série (cancelar restantes, editar todas) ficam **fora** desta versão. Uma única notificação de criação por série. Parcelamentos de grupo continuam fora do MVP.

## D-050 — Atualizar manualmente — Técnica
Botão "Atualizar" na tela do grupo recarrega dados da aba atual, o dashboard e o contador de notificações. Sem tempo real (D-044 mantida).

## D-051 — Migrations de grupos consolidadas (pré-lançamento) — Aceita
Como o produto ainda não foi lançado, as migrations `Etapa4_Groups`, `Etapa4b_Notifications` e os ajustes da 4c foram consolidados em UMA migration `Etapa4_Groups` com o esquema final (groups, group_members, group_invitations, group_transaction_series, notifications; colunas e checks de grupo em `transactions` e `transaction_payments`). Migrations das Etapas 1–3 não foram alteradas. **Quem já rodou as versões anteriores deve recriar o banco** (`docker compose down -v`). O snapshot do modelo foi regenerado a partir do estado da Etapa 3.

## D-052 — Detalhes da Etapa 4c — Proposta
Escolhas conservadoras para o que D-046 a D-049 não detalham (sem alterar rotas ou DTOs do contrato v1.5):
- **Status das ocorrências:** cada ocorrência nasce com o `status` pedido (`Planned`/`Pending`, padrão `Pending`), sem a regra "futura = Planned" das recorrências individuais; o vencimento define o `Overdue` apresentado.
- **Vencimento deslocado:** `dueDate_i = data_i + (dueDate − transactionDate)` em dias (pode ser negativo); datas fora do intervalo suportado → `422`.
- **`PUT /payments/{paymentId}` é substituição completa:** `accountId`/`paymentMethod` omitidos limpam a conta/forma gravadas; `amount` é obrigatório; a edição não gera notificação (D-042). `PaidAt`/`paidByUserId` voltam a ser calculados como em `POST` (último pagamento registrado que liquidou a conta).
- **Validação:** `payment` e `recurrence` inválidos → `400 validation_error` com `errors["payment.paymentDate"]`, `errors["recurrence.frequency"]` etc.; receita (`type: "Income"`), pagamento acima do valor, grupo arquivado → `422`.
- **Exclusão de ocorrência:** permitida ao criador sem pagamentos; `series.total` continua sendo o total original. A série é removida quando a última ocorrência é excluída. A notificação de criação da série referencia a 1ª ocorrência (excluí-la remove a notificação por cascata, D-045).
- **Notificações:** a criação com `payment` gera duas notificações por destinatário (criação, `amount` = valor da conta; pagamento, `amount` = principal + juros).
- **Sem coluna `transaction_type` em `notifications`:** o DTO continua com `type: 'Expense'` fixo.


---

# Etapa 4d — Bot do Telegram (2026-11-23)

## D-053 — Bot do Telegram: escopo, vínculo e segurança — Aceita (escolhas do usuário) / Proposta (detalhes)
Escolhas do usuário: **só despesas individuais**; entrada por **texto simples com regras fixas** (sem IA, que segue fora do MVP); vínculo por **código gerado no site**; o bot **só lança despesa** (sem saldo, consultas, marcar como paga/conta ou notificações no Telegram).
- Serviço externo novo (Bot API do Telegram), avaliado e autorizado pelo usuário. Sem biblioteca nova: `HttpClient` + webhook (compatível com a hospedagem gratuita que "dorme": o webhook acorda a API).
- **Vínculo:** `telegram_links` (um por usuário e um por chat). Código de 8 caracteres (sem 0/O/1/I/L), **hash SHA-256**, uso único, 10 minutos; novo código invalida o anterior. Chat já vinculado a outra conta não pode ser revinculado sem `/desvincular`. Excluir a conta remove vínculo, códigos e pendências.
- **Autenticidade do webhook:** segredo no cabeçalho `X-Telegram-Bot-Api-Secret-Token` (mínimo 16 caracteres, comparação em tempo constante); sem segredo válido → 404. Só chats privados são tratados.
- **Identidade:** a despesa é criada como o usuário do **vínculo verificado** (chat → conta) por `CurrentUserOverride`, definido no servidor apenas durante a confirmação; nunca por dados do cliente. Mesmas validações e regras do `TransactionService`.
- **Token do bot:** vem de `Telegram__BotToken`; os logs padrão do `HttpClient` são removidos (a URL da Bot API contém o token) e erros registram só o tipo da exceção. Conteúdo de mensagens não é registrado.

## D-054 — Bot do Telegram: regras do lançamento — Proposta
- **Interpretação:** exatamente um valor, no início ou no fim do texto (`45,90`, `45.90`, `R$ 1.234,56`, `1.500`); o resto é a descrição (até 120 caracteres, como em movimentações). Dois valores (primeiro e último) é ambíguo → o bot pede reenvio. Vírgula é o separador decimal; ponto com exatamente 3 dígitos depois é milhar.
- **Padrões financeiros (não inventam regra nova):** data = hoje no fuso da aplicação (D-021), `status = Pending` (padrão da API), sem conta/cartão/forma de pagamento → **não afeta o saldo projetado** até o usuário vincular uma conta no site.
- **Categoria:** escolhida em botões entre as categorias de despesa ativas do nível principal (padrão + personalizadas do usuário); subcategorias não aparecem.
- **Pendência:** `telegram_pending_expenses` (id numérico curto cabe no `callback_data`), validade de 15 minutos, restrita ao usuário do chat; limpeza oportunista das vencidas.
- **Falhas:** o webhook sempre responde 200 após autenticado; erros são registrados sem conteúdo e o usuário vê mensagem curta.

## D-055 — E-mail por API HTTPS (Brevo) quando o SMTP é bloqueado — Aceita
O Render gratuito bloqueia as portas SMTP 25, 465 e 587 (desde setembro/2025), então convites e redefinição de senha não saem por SMTP ali. Com `Brevo__ApiKey` definida, `IEmailSender` usa a API transacional da Brevo (HTTPS, plano gratuito de 300 e-mails/dia); sem ela, continua o SMTP (Mailpit em desenvolvimento). Remetente = `Smtp__From`/`Smtp__FromName`, que precisa estar validado na Brevo. A chave não é registrada em log, e o erro de envio não inclui o corpo da resposta. Sem biblioteca nova; sem mudança de contrato.

## D-056 — Bot do Telegram: despesas de grupo — Aceita (fluxo pedido pelo usuário) / Proposta (detalhes)
Fluxo pedido: enviar `grupo` → escolher o grupo em botões → enviar valor e descrição → escolher a categoria em botões, igual ao fluxo individual.
- **Seleção de uso único:** `telegram_group_selections` (uma por usuário, 15 minutos). Vale só para a próxima despesa; depois o bot volta ao lançamento individual, para ninguém publicar no grupo sem querer. Escolha expirada ou grupo indisponível → o bot avisa e não lança nada (nunca recai silenciosamente em despesa individual).
- **Quem aparece:** só grupos ativos (não arquivados) em que o usuário é membro ativo; nada de grupos alheios. A permissão é revalidada na escolha e de novo na confirmação (quem saiu do grupo no meio do caminho não lança).
- **Criação:** `GroupTransactionService.CreateAsync` com a identidade do vínculo (mesmo mecanismo de D-053): mesmas regras de D-046/D-047 (somente despesa; `Pending`; data de hoje; sem conta/pagamento/recorrência). As notificações do grupo (D-041) são geradas normalmente. `telegram_pending_expenses.group_id` (nulo = individual).
- Fora do escopo: marcar como paga, recorrência e vencimento pelo bot (o usuário completa no site).

## D-057 — Pagar fatura do cartão (baixa em lote) — Aceita (pedido do usuário) / Proposta (detalhes)
Pedido: dar baixa em todas as compras de uma fatura de uma vez, em vez de uma por uma.
- **Coerente com D-025:** não há entidade de fatura nem despesa nova; "pagar a fatura" registra o pagamento do **valor restante** de cada compra individual não cancelada do cartão com aquele vencimento, numa única transação (tudo ou nada; concorrência → 409).
- **Só pagamento integral da fatura.** Compras já parcialmente pagas são completadas; parciais por compra continuam pelo pagamento da movimentação.
- **Data** do pagamento: padrão hoje, nunca futura. **Sem conta** no pagamento (mesmo comportamento de compras de cartão sem conta, D-025), então não altera o saldo projetado além do que D-025 já define.
- **Escopo:** apenas compras individuais (`GroupId` nulo) do próprio usuário.
- **Em aberto para o usuário:** pagamento parcial da fatura (valor menor que o total) e escolha da conta que pagou ficam fora até decisão explícita.

## D-058 — Receitas: sem vencimento/situação no formulário; "Repetir todo mês" — Aceita (pedido do usuário)
- No formulário, **receitas não têm vencimento nem situação**: ficam sem vencimento e `Pending` (a receber) ao criar; em edição os valores existentes são preservados. Despesas continuam iguais.
- **"Repetir todo mês"** (só ao criar receita): em vez de um lançamento único, cria uma **recorrência mensal** (D-027) com `startDate` = data da movimentação e sem data final; sem mudança no backend.
- **Encerrar repetição:** na movimentação gerada por uma recorrência, a ação "Encerrar repetição" define `endDate` = dia anterior àquela movimentação (ou desativa a regra, se for a primeira ocorrência). Pela regra de D-027, ocorrências **futuras** sem pagamento e nunca editadas à mão são removidas; as já lançadas até hoje e as com pagamento permanecem (podem ser canceladas individualmente).

## D-059 — Lançamento retroativo já quitado — Aceita (pedido do usuário)
Receitas novas podem nascer **já recebidas** e despesas novas **já pagas** (exceto compras no cartão) ("Já recebida" + data do recebimento no formulário), para registrar o passado sem um segundo passo. No backend é o campo opcional `paymentDate` na criação (vale para qualquer tipo de movimentação não cartão e não prevista): cria a movimentação e o pagamento total na **mesma gravação** (tudo ou nada), com as regras normais de pagamento (data não futura, não anterior à data da movimentação). A interface oferece a opção só para receitas; "Já recebida" e "Repetir todo mês" são mutuamente exclusivos (as ocorrências da recorrência nascem pendentes).

## D-060 — Pagamentos de grupo na listagem individual — Aceita (pedido do usuário)
Os pagamentos de grupo feitos pelo próprio usuário (**com ou sem conta**; só os com conta afetam o saldo projetado, D-033) **aparecem na lista de movimentações** (e no extrato da conta), identificado pela coluna **Origem**: "Individual" por padrão, ou o nome do grupo. É só exibição: a linha é o pagamento (não cria movimentação individual; nos relatórios não entra, e no dashboard conta conforme D-061) e, ao clicar, leva à movimentação dentro do grupo. Pagamentos de outros membros ou de movimentações de grupo canceladas não aparecem. Privacidade preservada: a conta nunca aparece nos DTOs do grupo. A intercalação com as movimentações individuais respeita ordenação e paginação (ordenação em memória só sobre os pagamentos de grupo do próprio usuário, que são poucos).

## D-061 — Dashboard: o que o usuário pagou em grupos conta como despesa paga; listas de 5 — Aceita (pedido do usuário)
- **Totais, despesas por categoria e histórico de 6 meses** somam os pagamentos (principal + juros) que o usuário fez em movimentações de grupo não canceladas, **na data do pagamento**, como despesa **paga** (pela categoria raiz da movimentação do grupo). Pendente de grupo, ou pago por outros membros, não conta. Relatórios (§14) e saldo projetado seguem inalterados.
- **Próximos vencimentos** passa a no máximo **5** itens e o dashboard ganha **Últimas movimentações** (5 mais recentemente registradas, `sort=-createdAt`, inclui pagamentos de grupo).
