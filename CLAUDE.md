# CLAUDE.md — Sistema de Gestão Financeira

## 1. Visão geral

Projeto de uma aplicação web de gestão financeira pessoal e compartilhada.

O sistema tem como objetivo permitir que usuários controlem suas próprias finanças e, opcionalmente, participem de grupos financeiros para consolidar despesas e receitas.

A prioridade do produto é o controle financeiro individual. Os grupos são uma extensão que permite transparência e visualização consolidada das movimentações de seus participantes.

### Princípios fundamentais

1. O controle financeiro individual deve funcionar independentemente dos grupos.
2. Os dados financeiros individuais são privados.
3. Movimentações compartilhadas devem possuir apenas um registro.
4. Grupos não são sistemas de divisão de dívidas.
5. A participação financeira é informativa, não representa obrigação de pagamento.
6. O backend é a fonte definitiva das regras de negócio.
7. A aplicação deve ser responsiva desde a primeira versão.
8. A arquitetura deve permitir evolução sem complexidade desnecessária.

---

## 2. Stack tecnológica

### Backend

- .NET 8.
- C#.
- ASP.NET Core Web API.
- Entity Framework Core.
- PostgreSQL.
- REST API.
- Swagger / OpenAPI.
- JWT.
- Refresh Token.
- FluentValidation.

### Frontend

- Vue.js 3.
- TypeScript.
- Composition API.
- Vite.
- Vue Router.
- Pinia.
- Axios.
- Biblioteca de componentes compatível com Vue 3.
- Biblioteca de gráficos compatível com Vue 3.

### Infraestrutura

- Docker.
- Docker Compose.
- PostgreSQL em container.
- Volumes persistentes.
- Variáveis de ambiente.

### Diretriz

Não adicionar tecnologias, bibliotecas ou serviços externos sem avaliar sua necessidade e compatibilidade com a arquitetura.

Não criar aplicativo mobile separado nesta fase. O frontend web deve ser responsivo e preparado para futuras integrações mobile.

---

## 3. Arquitetura

Utilizar Clean Architecture no backend e organização modular no frontend.

### Estrutura sugerida do backend

```text
backend/
├── FinanceApp.sln
├── src/
│   ├── FinanceApp.API/
│   ├── FinanceApp.Application/
│   ├── FinanceApp.Domain/
│   └── FinanceApp.Infrastructure/
├── tests/
│   ├── FinanceApp.UnitTests/
│   └── FinanceApp.IntegrationTests/
└── Dockerfile
```

### Responsabilidades

**Domain**
- Entidades.
- Enums.
- Value Objects.
- Regras de domínio.
- Invariantes.
- Exceções de domínio.

Não deve depender de Entity Framework, ASP.NET ou infraestrutura.

**Application**
- Casos de uso.
- Serviços de aplicação.
- DTOs.
- Interfaces.
- Validações.
- Regras de orquestração.

**Infrastructure**
- Entity Framework Core.
- DbContext.
- Mapeamentos.
- Repositórios, quando necessários.
- Persistência.
- Implementações de serviços externos.

**API**
- Controllers ou endpoints.
- Autenticação.
- Autorização.
- Configuração de dependências.
- Middlewares.
- Swagger.
- Tratamento HTTP.

### Estrutura sugerida do frontend

```text
frontend/
├── src/
│   ├── assets/
│   ├── components/
│   ├── composables/
│   ├── layouts/
│   ├── modules/
│   │   ├── auth/
│   │   ├── dashboard/
│   │   ├── accounts/
│   │   ├── credit-cards/
│   │   ├── categories/
│   │   ├── transactions/
│   │   ├── installments/
│   │   ├── recurrence/
│   │   ├── groups/
│   │   └── reports/
│   ├── router/
│   ├── services/
│   ├── stores/
│   ├── types/
│   ├── utils/
│   └── App.vue
├── public/
└── Dockerfile
```

Cada módulo deve concentrar seus componentes, tipos, serviços e funcionalidades específicas, evitando componentes gigantes e dependências desnecessárias entre módulos.

---

## 4. Convenções de desenvolvimento

### Backend

- Utilizar PascalCase para classes e métodos públicos.
- Utilizar camelCase para variáveis e parâmetros.
- Utilizar async/await nas operações assíncronas.
- Propagar CancellationToken nas operações assíncronas quando aplicável.
- Utilizar injeção de dependência.
- Evitar lógica de negócio em controllers.
- Evitar DbContext diretamente em controllers.
- Utilizar DTOs para entrada e saída da API.
- Não expor entidades do domínio diretamente.
- Centralizar tratamento de erros.
- Utilizar migrations do Entity Framework Core.
- Manter as responsabilidades das camadas bem definidas.

### Frontend

- Utilizar Composition API e `<script setup lang="ts">`.
- Utilizar TypeScript de forma consistente.
- Separar componentes de apresentação e regras de interação.
- Utilizar Pinia para estado compartilhado.
- Utilizar Axios por meio de serviços centralizados.
- Centralizar tratamento de erros HTTP.
- Evitar chamadas HTTP diretamente dentro de componentes complexos.
- Evitar componentes monolíticos.
- Utilizar componentes reutilizáveis.
- Manter os tipos alinhados aos contratos da API.

### Geral

- Nomes claros e objetivos.
- Evitar abstrações prematuras.
- Evitar duplicação de regras.
- Não adicionar dependências sem justificativa.
- Não deixar código de teste ou exemplo em produção.
- Não utilizar dados simulados como substitutos de funcionalidades reais.
- Não implementar regras de negócio somente no frontend.

---

## 5. Modelo de domínio

O modelo inicial deve considerar as seguintes entidades.

### User

Representa o usuário da aplicação.

Campos principais:

- Id (UUID).
- Name.
- Email.
- PasswordHash.
- CreatedAt.
- UpdatedAt.
- IsActive.

### Account

Representa uma conta financeira individual.

- Id.
- UserId.
- Name.
- Institution.
- Type.
- Currency.
- IsActive.
- CreatedAt.
- UpdatedAt.

### AccountBalanceSnapshot

Representa o saldo informado pelo usuário em determinada data.

- Id.
- AccountId.
- Balance.
- ReferenceDate.
- CreatedAt.

Cada snapshot representa uma nova referência para cálculo do saldo projetado.

### CreditCard

Representa um cartão de crédito.

- Id.
- UserId.
- AccountId (opcional).
- Name.
- Institution.
- CreditLimit.
- ClosingDay.
- DueDay.
- IsActive.

Não implementar gestão completa de faturas no MVP.

### Category

Representa categorias e subcategorias.

- Id.
- UserId (nulo para categorias padrão do sistema).
- Name.
- Type.
- ParentCategoryId (opcional).
- Icon.
- Color.
- IsDefault.
- IsActive.

Categorias podem ser globais ou personalizadas pelo usuário.

### Transaction

Representa uma receita ou despesa.

Campos principais:

- Id.
- CreatedByUserId.
- PaidByUserId (opcional).
- AccountId (opcional).
- CreditCardId (opcional).
- CategoryId.
- GroupId (opcional).
- InstallmentPlanId (opcional).
- RecurrenceRuleId (opcional).
- Description.
- Amount.
- Type.
- PaymentMethod.
- Status.
- TransactionDate.
- DueDate (opcional).
- PaidAt (opcional).
- Notes.
- CreatedAt.
- UpdatedAt.

`CreatedByUserId` identifica quem cadastrou a movimentação.

`PaidByUserId` identifica quem efetivamente pagou uma despesa ou recebeu uma receita, quando aplicável.

Não confundir autoria com pagamento.

Valores monetários devem utilizar NUMERIC(18,2) no PostgreSQL e decimal no C#.

### TransactionPayment

Representa um pagamento ou recebimento associado a uma movimentação.

- Id.
- TransactionId.
- Amount.
- PaymentDate.
- CreatedByUserId.
- AccountId (opcional).
- PaymentMethod (opcional).
- CreatedAt.

Permitir pagamentos parciais e integrais.

O valor acumulado dos pagamentos não pode ultrapassar o valor da movimentação, salvo se houver uma regra de negócio explícita para isso.

O status deve ser derivado ou atualizado de maneira consistente com os pagamentos registrados.

### InstallmentPlan

Representa o planejamento de uma compra parcelada.

- Id.
- UserId.
- Description.
- TotalAmount.
- InstallmentCount.
- FirstDueDate.
- CreatedAt.

Cada parcela deve ser uma Transaction independente, associada ao plano e identificada por seu número de parcela.

Garantir consistência de valores, incluindo diferenças de centavos.

### RecurrenceRule

Representa a configuração de uma recorrência.

- Id.
- UserId.
- Frequency.
- Interval.
- StartDate.
- EndDate (opcional).
- NextOccurrence.
- IsActive.

Cada ocorrência gerada deve ser uma Transaction independente.

A geração precisa ser idempotente para evitar duplicidades.

### Group

Representa um grupo financeiro.

- Id.
- Name.
- Description.
- CreatedByUserId.
- CreatedAt.
- UpdatedAt.
- IsActive.

### GroupMember

Representa a participação de um usuário em um grupo.

- Id.
- GroupId.
- UserId.
- Role.
- Status.
- JoinedAt.

Criar índice único para GroupId + UserId, considerando a estratégia de reativação de membros.

O criador do grupo também deve ser membro.

Preservar o histórico de participação e não apagar registros antigos quando alguém sair.

### GroupInvitation

Representa convites para grupos.

- Id.
- GroupId.
- InvitedByUserId.
- Email (opcional).
- TokenHash.
- ExpiresAt.
- Status.
- CreatedAt.

Armazenar hash do token, nunca o token em texto puro.

---

## 6. Regras de negócio financeiras

### Movimentações individuais

Quando GroupId for nulo, a movimentação pertence ao contexto individual do usuário.

Somente o proprietário/autorizado pode consultar e modificar seus dados.

### Movimentações compartilhadas

Quando GroupId possuir valor, a movimentação pertence ao grupo.

Todos os participantes ativos do grupo podem visualizar as movimentações compartilhadas.

O usuário que cria a movimentação precisa ser membro autorizado do grupo.

Uma movimentação compartilhada deve possuir um único registro no banco.

Não criar uma cópia para cada participante.

### Privacidade

Os dados individuais não podem ser expostos aos membros do grupo.

Mesmo quando uma movimentação é compartilhada, não expor automaticamente:
- Informações bancárias privadas.
- Dados de contas pessoais.
- Informações privadas de cartões.
- Outras movimentações individuais.

A API deve retornar DTOs específicos para a visualização do grupo, omitindo dados privados de contas e cartões.

### Autoria e pagamento

O sistema deve diferenciar:

- Criador da movimentação.
- Pagador ou recebedor efetivo.

Uma pessoa pode cadastrar uma despesa e outra pessoa pode pagá-la.

Relatórios devem distinguir essas duas dimensões.

### Grupos não representam divisão de dívidas

Não implementar no MVP:

- TransactionShare.
- Settlement.
- Dívidas entre participantes.
- Saldos devedores.
- Cobranças.
- Rateio obrigatório.
- Reembolsos automáticos.
- Responsabilidade financeira proporcional.

Percentuais são apenas indicadores estatísticos.

Exemplo: se um participante paga 70% das despesas, isso não significa que os demais devam valores a ele.

### Dashboard do grupo

Apresentar:

- Total de despesas.
- Total de receitas.
- Total pago.
- Total pendente.
- Participação por participante.
- Despesas por categoria.
- Evolução mensal.
- Comparação entre períodos.

Preferir gráficos separados para:

1. Participação por quem cadastrou as movimentações.
2. Participação por quem efetivamente pagou.

Não misturar os dois critérios em um único indicador sem deixar explícita a regra de cálculo.

Para movimentações pendentes, não contabilizar pagamento inexistente.

### Participantes

- Um usuário pode participar de vários grupos.
- Um grupo pode ter vários participantes.
- Participantes ativos visualizam todas as movimentações compartilhadas.
- A saída ou remoção de um participante não apaga seu histórico.
- Um participante não deve editar movimentações de terceiros por padrão.
- O administrador não possui permissão automática para alterar movimentações de outros participantes.

Permissões administrativas adicionais devem ser implementadas somente após decisão explícita.

---

## 7. Saldos e contas

O saldo informado pelo usuário é a referência para os cálculos.

O saldo projetado deve considerar as movimentações posteriores à data do snapshot.

Conceitualmente:

Saldo projetado = saldo informado + receitas posteriores - despesas posteriores.

Movimentações devem ser consideradas conforme seu estado financeiro e suas datas.

Não somar uma despesa de cartão novamente quando ocorrer o pagamento da fatura.

Sempre deixar claro na interface quando o valor apresentado for projetado.

Um novo snapshot substitui o marco de referência para projeções futuras, sem apagar os snapshots anteriores.

---

## 8. Status das movimentações

Os status iniciais devem contemplar:

- Planned: previsto.
- Pending: pendente.
- Paid: pago.
- Received: recebido.
- Overdue: atrasado.
- Canceled: cancelado.

O status deve respeitar o tipo da movimentação, a data de vencimento e os pagamentos registrados.

Evitar estados inconsistentes.

Exemplos:

- Despesa integralmente paga: Paid.
- Despesa parcialmente paga: Pending, com valor restante.
- Despesa vencida sem pagamento integral: Overdue, conforme regra de apresentação.
- Receita integralmente recebida: Received.
- Movimentação cancelada: Canceled.

Documentar a máquina de estados e testar suas transições.

---

## 9. Parcelamentos e recorrências

### Parcelamentos

- Gerar parcelas independentes.
- Vincular todas ao mesmo InstallmentPlan.
- Identificar número e quantidade de parcelas.
- Garantir consistência do valor total.
- Permitir acompanhamento de parcelas pagas, pendentes e futuras.
- Evitar duplicidade na geração.

### Recorrências

Suportar inicialmente:

- Diária.
- Semanal.
- Quinzenal.
- Mensal.
- Bimestral.
- Trimestral.
- Semestral.
- Anual.
- Intervalo personalizado.

A geração deve respeitar a regra de recorrência e possuir mecanismo para impedir ocorrências duplicadas.

Não gerar ocorrências indefinidamente sem uma estratégia de horizonte de geração.

---

## 10. Categorias iniciais

### Despesas

- Alimentação.
- Saúde.
- Educação.
- Lazer.
- Assinaturas.
- Moradia.
- Transporte.
- Compras.
- Outros.

Subcategorias podem ser cadastradas pelo usuário.

### Receitas

- Salário.
- Freelance.
- Investimentos.
- Reembolsos.
- Outros.

Categorias padrão devem ser criadas por seed idempotente.

Não duplicar categorias padrão a cada inicialização.

---

## 11. API e contratos

A API deve utilizar convenções REST.

Criar DTOs específicos para:
- Criação.
- Atualização.
- Consulta.
- Respostas resumidas.
- Dashboards.
- Relatórios.

Não retornar entidades do domínio diretamente.

Padronizar:
- Paginação.
- Ordenação.
- Filtros.
- Validação.
- Respostas de erro.
- Códigos HTTP.

Utilizar Swagger/OpenAPI para documentar os endpoints.

Manter um documento de contratos entre frontend e backend.

Quando um contrato mudar, atualizar a documentação e comunicar o outro agente.

### Autorização

Toda consulta e alteração deve validar o contexto do usuário autenticado.

Não confiar em UserId recebido livremente do frontend.

Obter a identidade do usuário a partir do contexto autenticado.

Para operações de grupo, verificar participação e status do membro.

---

## 12. Segurança

Implementar:

- Hash seguro de senhas.
- JWT com expiração.
- Refresh Token com armazenamento seguro.
- Revogação de Refresh Tokens.
- Validação de entradas.
- Autorização em todas as operações.
- Proteção de dados privados.
- Tratamento centralizado de exceções.
- Logs sem informações sensíveis.
- Configuração segura de CORS.
- Segredos via variáveis de ambiente.

Não armazenar senhas em texto puro.

Não armazenar tokens de convite em texto puro.

Não retornar detalhes internos de exceções em produção.

Não confiar em permissões controladas apenas pelo frontend.

---

## 13. Docker e ambiente local

O projeto deve possuir Docker Compose para iniciar:

- PostgreSQL.
- API.
- Frontend.

Estrutura sugerida:

```text
project/
├── backend/
├── frontend/
├── docker-compose.yml
├── .env.example
├── .gitignore
├── CLAUDE.md
└── README.md
```

O PostgreSQL deve utilizar volume persistente.

Configurar healthcheck e dependência da API em relação à disponibilidade do banco.

As configurações devem ser feitas por variáveis de ambiente.

Não versionar `.env` com credenciais reais.

O ambiente deve poder ser iniciado por meio de:

```bash
docker compose up --build
```

Documentar portas, variáveis, migrations e comandos úteis.

---

## 14. Frontend e responsividade

A interface deve priorizar clareza e facilidade de uso.

Utilizar:
- Vue 3.
- TypeScript.
- Composition API.
- Pinia.
- Vue Router.
- Axios.

Criar layouts distintos para:
- Autenticação.
- Aplicação principal.

O menu deve permitir navegar entre:

- Dashboard.
- Contas.
- Cartões.
- Movimentações.
- Categorias.
- Parcelamentos.
- Recorrências.
- Grupos.
- Relatórios.
- Configurações.

A navegação deve adaptar-se a telas menores.

Não ocultar funcionalidades essenciais em dispositivos móveis.

Criar componentes reutilizáveis para:
- Cards financeiros.
- Tabelas.
- Gráficos.
- Filtros.
- Formulários.
- Modais.
- Feedbacks.
- Estados vazios.
- Indicadores de carregamento.

---

## 15. Testes

### Backend

Criar testes unitários para as regras de domínio e aplicação.

Priorizar testes para:

- Cálculo de saldo projetado.
- Pagamentos parciais.
- Pagamentos integrais.
- Status de movimentações.
- Parcelamentos.
- Recorrências.
- Permissões individuais.
- Permissões de grupos.
- Privacidade de movimentações.
- Preservação do histórico.

Criar testes de integração com PostgreSQL para os fluxos críticos.

### Frontend

Testar:
- Componentes importantes.
- Formulários.
- Validações.
- Stores.
- Serviços HTTP.
- Fluxos principais.

Garantir que os estados de carregamento, sucesso, erro e ausência de dados sejam tratados.

---

## 16. Divisão de responsabilidade dos agentes

### Agente Backend

Responsável por:
- Domínio.
- Banco de dados.
- Migrations.
- API.
- Autenticação.
- Autorização.
- Regras financeiras.
- Testes de backend.
- Documentação dos contratos.

### Agente Frontend

Responsável por:
- Layout.
- Componentes.
- Páginas.
- Navegação.
- Formulários.
- Gráficos.
- Responsividade.
- Stores.
- Serviços HTTP.
- Testes de frontend.

### Integração

Os agentes devem compartilhar contratos e decisões.

O backend define os contratos da API.

O frontend consome esses contratos.

Ambos devem respeitar os mesmos nomes, tipos, enumerações e regras.

Não implementar funcionalidades usando respostas fictícias quando a API real estiver disponível.

---

## 17. Funcionalidades fora do MVP

Não implementar sem autorização:

- Aplicativo mobile nativo.
- Integração com bancos.
- Open Finance.
- Sincronização automática de extratos.
- Gestão completa de faturas.
- Divisão automática de despesas.
- Dívidas entre participantes.
- Acertos financeiros.
- Cobranças.
- Orçamentos avançados por categoria.
- Metas financeiras avançadas.
- Investimentos completos.
- Inteligência artificial financeira.
- Notificações avançadas.

A arquitetura pode permitir essas funcionalidades no futuro, mas não deve ser complexificada antecipadamente.

---

## 18. Processo de desenvolvimento

O desenvolvimento deve ser incremental.

Antes de iniciar cada etapa:

1. Analisar o escopo.
2. Identificar dependências.
3. Definir contratos.
4. Dividir tarefas.
5. Implementar.
6. Testar.
7. Integrar.
8. Documentar.

Ao finalizar cada etapa, apresentar:

- Funcionalidades implementadas.
- Arquivos criados ou modificados.
- Migrations adicionadas.
- Endpoints disponíveis.
- Testes executados.
- Pendências.
- Próxima etapa recomendada.

Não marcar tarefas como concluídas sem verificar compilação, integração e testes relevantes.

---

## 19. Diretriz final

O objetivo não é apenas construir telas ou endpoints, mas desenvolver um produto financeiro confiável.

Priorizar:

1. Correção dos cálculos financeiros.
2. Segurança e privacidade.
3. Integridade dos dados.
4. Clareza das regras de negócio.
5. Boa experiência do usuário.
6. Manutenibilidade.
7. Evolução gradual.

Em caso de dúvida, não inventar regras financeiras. Registrar a dúvida, explicar as alternativas e solicitar decisão antes de implementar algo que possa comprometer os dados ou o comportamento do sistema.