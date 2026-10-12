# Arquitetura e Decisões Técnicas — Meu Controle Financeiro

> Documento vivo. Reflete o estado ao final da **Fase 5** (importação de extratos CSV/OFX com conciliação
> idempotente), sobre a base das Fases 1–4 (arquitetura, CRUD, Viagens, orçamento, relatórios, gráficos e cartões).

## 1. Objetivo

Aplicativo de finanças pessoais para iOS (.NET MAUI) com backend ASP.NET Core, pensado para uso
pessoal e com arquitetura preparada para virar produto. Permite acompanhar receitas, despesas,
cartões, contas, fluxo de caixa e hábitos de consumo — com dados **reais do próprio usuário** e um
**modo de demonstração** claramente identificado enquanto não há integrações externas configuradas.

## 2. Visão geral da solução

```
Financas/
├─ backend/
│  ├─ src/
│  │  ├─ FinanceApp.Domain          # Entidades e regras de negócio (sem dependências externas)
│  │  ├─ FinanceApp.Application      # Casos de uso, DTOs, interfaces, validações
│  │  ├─ FinanceApp.Infrastructure   # EF Core + PostgreSQL, segurança, integrações, seed
│  │  └─ FinanceApp.Api              # ASP.NET Core Web API (controllers, JWT, Swagger)
│  ├─ tests/FinanceApp.Tests         # xUnit: unidade + integração (InMemory/WebApplicationFactory)
│  └─ Dockerfile                     # Imagem da API
├─ mobile/FinanceApp.Mobile          # App .NET MAUI (iOS/Android), MVVM
├─ docs/                             # Documentação de integrações e segurança
├─ .github/workflows/                # Esteiras: build-ios.yml e backend-ci.yml
├─ docker-compose.yml                # PostgreSQL + API para desenvolvimento local
└─ MeuControleFinanceiro.sln         # Solução do backend (o app MAUI é compilado à parte)
```

### Camadas (Clean Architecture)

- **Domain**: entidades (`User`, `Account`, `Transaction`, …), enums e regras intrínsecas. Não
  depende de nada. Dinheiro é sempre `decimal`; datas financeiras são `DateOnly`; carimbos de
  auditoria são `DateTime` em UTC.
- **Application**: orquestra casos de uso (`DashboardService`, `TransactionService`, `AuthService`…),
  define interfaces (`IApplicationDbContext`, `ICurrentUser`, `ITokenService`, `IBankIntegrationProvider`)
  e DTOs. Depende apenas de Domain.
- **Infrastructure**: implementa persistência (`AppDbContext` + EF Core + Npgsql), segurança
  (`PasswordHasher` PBKDF2, `JwtTokenService`), o provedor de banco demonstrativo e o seed.
- **Api**: expõe HTTP, autentica via JWT, documenta com Swagger, resolve o usuário atual a partir
  das claims e traduz exceções em respostas padronizadas.

A regra de dependência aponta sempre para dentro: Api → Application → Domain; Infrastructure → Application → Domain.

## 3. Decisões técnicas

| Decisão | Escolha | Porquê |
|---|---|---|
| Versão do .NET (backend) | **.NET 8 (LTS)** | É o SDK LTS disponível no ambiente; compila e testa localmente. Migrar para .NET 10 LTS depois é trocar o TFM e os pacotes `8.0.*` por `10.0.*`. |
| Versão do .NET (app MAUI) | **.NET 9** | Reaproveita a **mesma esteira iOS comprovada** (`macos`, Xcode 26.5, workloads `maui ios`). O app é independente do backend (tem seus próprios modelos), então as versões podem divergir sem problema. |
| Banco de dados | **PostgreSQL 16** + **EF Core 8** (Npgsql) | Pedido do projeto; ótimo suporte a `numeric`, `uuid`, `date` e índices. |
| Identificadores | `Guid` | Facilita futura sincronização offline e evita colisões multiusuário. |
| Valores monetários | `decimal(18,2)` | Evita erros de ponto flutuante. Precisão aplicada globalmente no `OnModelCreating`. |
| Autenticação | **JWT (HS256)** | Simples e adequado ao app. Preparado para evoluir a OAuth/OIDC. Senha com **PBKDF2** (210k iterações, salt por senha). |
| Apresentação (app) | **MVVM** (CommunityToolkit.Mvvm) | Mantém a lógica fora do code-behind. |
| Gráficos (app) | **Microsoft.Maui.Graphics** (`IDrawable`/`GraphicsView`) | Biblioteca nativa do MAUI, sem dependências externas nem licenças — a esteira compila sem surpresas. Rosca de categorias e linha de evolução do saldo já implementadas. |
| Integração bancária | Abstração `IBankIntegrationProvider` + provedor **Demo** | Permite trocar de agregador (Pluggy/Belvo) sem reescrever regras. Nada de endpoints fictícios. |

## 4. Modelo de dados

Entidades (todas com `Id: Guid`, `CreatedAtUtc`, `UpdatedAtUtc`):

- **User** — guarda apenas hash da senha; nunca credenciais bancárias.
- **Account** — conta/carteira; `InitialBalance` + lançamentos confirmados = saldo atual.
- **Category** — com cor, ícone, natureza (fixa/variável) e subcategoria (auto-relacionamento).
- **Transaction** — receita/despesa/transferência/estorno/pagamento de fatura. Campos para
  conta de origem/destino, cartão, fatura, estabelecimento, forma de pagamento, status,
  recorrência, origem (manual/Open Finance/notificação/arquivo) e **`ExternalId` idempotente**.
- **CreditCard** / **CreditCardInvoice** / **Installment** — cartões, faturas mensais e compras parceladas.
- **Budget** — orçamento total (categoria nula) ou por categoria, por mês/ano.
- **FinancialGoal** — metas/reservas com progresso.
- **RecurringTransaction** — modelos de lançamentos recorrentes.
- **BankConnection** / **BankSyncHistory** — conexões de Open Finance e histórico de sincronização.
- **NotificationPreference** — preferências por tipo de notificação.

Índices e restrições relevantes: `User.Email` único; `Transaction(UserId, TransactionDate)` e
`Transaction(UserId, Type, Status)`; **`Transaction(UserId, ExternalId)` único** (idempotência);
`CreditCardInvoice(UserId, CreditCardId, ReferenceMonth)` único; `Budget(UserId, Year, Month, CategoryId)` único;
`NotificationPreference(UserId, Type)` único. Migração inicial: `backend/src/FinanceApp.Infrastructure/Persistence/Migrations`.

## 5. Regras de negócio já implementadas (Fase 1)

Centralizadas no cálculo do dashboard e nos serviços — e cobertas por testes:

1. **Compras no cartão não reduzem o saldo de caixa** até o pagamento da fatura; elas compõem a
   fatura e aparecem como despesa nos relatórios/categorias.
2. **Transferências entre contas próprias** não contam como receita nem despesa (`ExcludeFromReports`).
3. **Pagamento de fatura** reduz o caixa da conta pagadora, mas não recontabiliza as compras.
4. **Saldo atual** = saldo inicial + efeitos de caixa de todos os lançamentos confirmados até hoje.
5. **Despesas realizadas × previstas**: realizadas = confirmadas; previstas = previsto/pendente.
6. **Saldo projetado** = saldo atual + fluxos previstos/pendentes do restante do mês.
7. **Comparação com o mês anterior** só aparece quando há base para comparar.
8. **Isolamento por usuário**: todos os serviços filtram por `UserId` do token — um usuário nunca vê dados de outro.

## 6. Segurança (resumo; detalhes em `docs/checklist-seguranca.md`)

- Senha com PBKDF2 + salt; **nunca** em texto puro, **nunca** em logs.
- JWT assinado; chave vem de segredo/variável de ambiente (em produção é obrigatória).
- Endpoints protegidos por `[Authorize]` e por filtro de `UserId`.
- Token no app guardado no **Keychain** via `SecureStorage`.
- Nenhuma senha bancária é solicitada ou armazenada; Open Finance usa consentimento.
- Em desenvolvimento **não se usa dado financeiro real** — há um usuário demo fictício.

## 7. Integração bancária

A interface `IBankIntegrationProvider` abstrai conexão, consentimento, sincronização e desconexão.
A implementação atual (`DemoBankIntegrationProvider`) gera dados fictícios identificados. Trocar por
um provedor real (ex.: Pluggy/Belvo) é registrar outra implementação em `AddInfrastructure`, sem tocar
nas regras financeiras. Fornecedores a avaliar, requisitos, custos e limitações estão em
`docs/open-finance-provedores.md`.

## 8. Roadmap por fases

- **Fase 1 (entregue)**: arquitetura, banco + migração, API (auth, dashboard, lançamentos, contas,
  categorias, banking demo, health), dashboard funcional no app, testes, Docker, esteiras.
- **Fase 2 (entregue)**: API + telas completas de lançamentos, contas, categorias e recorrências
  (CRUD no app, acessíveis pela aba **Config.**) + geração de recorrências. Módulo extra de **Viagens**
  (despesas de viagem com rota por GPS e mapa). 33 testes no backend.
- **Fase 3 (entregue)**: **orçamento mensal** (API `BudgetService`/`/api/budgets` + telas de lista e
  edição, com acompanhamento orçado×realizado por categoria ou total do mês), **relatórios** (API
  `ReportService`/`/api/reports/summary`: série mensal de receitas×despesas, despesas por categoria e
  fixas×variáveis) e a **aba Gráficos** no app. Filtros de lançamento (período, categoria, conta, tipo,
  busca) já existiam na API e por mês/busca no app. 43 testes no backend.
- **Fase 4 (entregue)**: **cartões** (API `CardService`/`/api/cards` com fatura atual e limite disponível),
  **faturas** (`InvoiceService`/`/api/invoices`: alocação de compras ao ciclo pela regra de fechamento,
  fechamento, pagamento que reduz o caixa sem recontabilizar as compras) e **compras parceladas**
  (`/api/cards/{id}/installments`, uma parcela por fatura). No app: aba **Cartões** (lista, edição,
  faturas por cartão, detalhe da fatura com pagar/fechar, formulário de parcelamento). 50 testes no backend.
- **Fase 5 (entregue)**: **importação de extratos CSV/OFX** (`StatementImportService`/`/api/imports`:
  `preview` analisa o arquivo e marca duplicados; `commit` importa os itens selecionados). Conciliação
  **idempotente** por `ExternalId` (FITID do OFX ou hash dos dados no CSV, prefixado pela conta) — reimportar
  não duplica. No app: tela **Importar extrato** (escolher arquivo, revisar com seleção, importar). 55 testes.
- **Fase 6**: Open Finance real e sincronização bancária.
- **Fase 7**: notificações, reforço de segurança, mais testes e preparação para distribuição iOS.
