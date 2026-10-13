# 💰 Meu Controle Financeiro

Aplicativo de finanças pessoais para **iOS** (.NET MAUI) com backend **ASP.NET Core + PostgreSQL**,
em **Clean Architecture**. Acompanhe receitas, despesas, cartões, contas, fluxo de caixa e hábitos
de consumo — em português (pt-BR), com valores em Real (R$) e datas no formato brasileiro.

> **Status:** Fase 1 concluída — arquitetura, banco + migração, API, dashboard funcional no app,
> testes, Docker e esteiras de CI. Veja o roadmap em [ARQUITETURA.md](ARQUITETURA.md).

## Estrutura

```
backend/   API + Domínio + Aplicação + Infraestrutura + Testes (solução .NET 8)
mobile/    App .NET MAUI (iOS/Android), MVVM (.NET 9)
docs/      Integrações (Open Finance) e checklist de segurança
.github/   Esteiras: build-ios.yml (gera .ipa) e backend-ci.yml (build + testes)
docker-compose.yml  PostgreSQL + API para desenvolvimento local
```

Detalhes de camadas e decisões: [ARQUITETURA.md](ARQUITETURA.md).

## Pré-requisitos

- [.NET SDK 8](https://dotnet.microsoft.com/download) (backend) e, para o app iOS, **.NET 9** com
  workloads `maui`/`ios` (a esteira já faz isso no macOS).
- [Docker](https://www.docker.com/) (opcional, para subir API + PostgreSQL rapidamente).
- Para compilar o app iOS: **macOS + Xcode** (a esteira usa Xcode 26.5).

## 1) Rodar o backend com Docker (recomendado)

```bash
docker compose up --build
```

- API em **http://localhost:5219** (Swagger em `http://localhost:5219/swagger`).
- PostgreSQL em `localhost:5432` (db/usuário/senha: `financas`).
- Sobe o banco, aplica as **migrações** automaticamente e **semeia dados de demonstração**.

**Credenciais de demonstração:**

```
E-mail: demo@financas.app
Senha:  Demo@12345
```

## 2) Rodar o backend sem Docker

Suba um PostgreSQL (ou use o do compose) e então:

```bash
# na raiz do repositório
dotnet run --project backend/src/FinanceApp.Api
```

Por padrão usa a conexão `Host=localhost;Port=5432;Database=financas;Username=financas;Password=financas`
(ajuste em `backend/src/FinanceApp.Api/appsettings.json` ou via variável
`ConnectionStrings__Default`). Em **Development** as migrações são aplicadas e os dados de
demonstração são semeados automaticamente.

### Migrações do Entity Framework Core

```bash
# criar nova migração
dotnet ef migrations add NomeDaMigracao \
  -p backend/src/FinanceApp.Infrastructure \
  -s backend/src/FinanceApp.Api \
  -o Persistence/Migrations

# aplicar manualmente
dotnet ef database update \
  -p backend/src/FinanceApp.Infrastructure \
  -s backend/src/FinanceApp.Api
```

## 3) Testes

```bash
dotnet test MeuControleFinanceiro.sln
```

Cobrem as regras do dashboard (cartão não afeta caixa, transferência não é despesa, projeção,
comparação mensal), o CRUD de lançamentos, o hasher de senha e o pipeline HTTP com autenticação
(via `WebApplicationFactory`, sem precisar de PostgreSQL).

## 4) Rodar o app (.NET MAUI)

O app funciona **100% local**: todos os dados ficam em um banco SQLite na área privada do aplicativo
(`financas.db3`), sem servidor e sem internet. Ele executa as **mesmas regras de negócio** do backend
(camadas `Domain` + `Application` + `Infrastructure` referenciadas pelo projeto MAUI), então orçamento,
faturas, importação CSV/OFX, relatórios e viagens se comportam igual à API.

- Na primeira abertura o app cria o perfil local com categorias e preferências padrão.
- **Backup:** *Config. › Backup dos dados* exporta uma cópia do banco (compartilhar/salvar em nuvem).
  Os dados só existem no aparelho — sem backup, trocar de celular ou desinstalar o app os perde.
- A API (`backend/`) continua no repositório para uso futuro (sincronização entre aparelhos / Open Finance).
- Limitação conhecida: o banco local é criado com `EnsureCreated`; mudanças futuras de esquema exigirão
  uma estratégia de migração local antes de publicar atualizações.

### Compilar e abrir no Xcode / dispositivo

No macOS, com .NET 9 + workloads instaladas:

```bash
cd mobile/FinanceApp.Mobile
dotnet build -f net9.0-ios -c Release -r ios-arm64 -p:EnableCodeSigning=false
```

Ou use a esteira **`.github/workflows/build-ios.yml`** (push na `main` ou *Run workflow*), que gera
um `Financas.ipa` **sem assinatura** como artefato. Para instalar no iPhone, assine com
**Sideloadly/AltStore** (teste) ou publique via **App Store Connect**.

### Assinatura, permissões e compilação para iPhone

1. Tenha uma conta no **Apple Developer Program** e um perfil de provisionamento para o
   `ApplicationId` (`com.meucontrole.financas`).
2. Permissões relevantes já estão no `Platforms/iOS/Info.plist` (ex.: `NSFaceIDUsageDescription`).
3. Para build assinado local, configure o time de assinatura no Xcode/MSBuild
   (`-p:EnableCodeSigning=true` com os parâmetros de assinatura). A esteira gera **sem assinatura**
   para você assinar depois.
4. Checklist completo de distribuição: [docs/checklist-seguranca.md](docs/checklist-seguranca.md).

## Endpoints principais

| Método | Rota | Descrição |
|---|---|---|
| POST | `/api/auth/register` | Cria usuário (semeia categorias) e devolve token |
| POST | `/api/auth/login` | Autentica e devolve token JWT |
| GET | `/api/dashboard` | Indicadores da tela inicial (filtros por mês/ano/conta/cartão/categoria) |
| GET/POST/PUT/DELETE | `/api/transactions` | Lançamentos (CRUD, filtros, paginação, duplicar) |
| GET/POST/PUT/DELETE | `/api/accounts` | Contas bancárias |
| GET/POST/PUT/DELETE | `/api/categories` | Categorias |
| GET | `/api/banking/provider` · `/api/banking/institutions` | Integração bancária (modo demo) |
| GET | `/api/health` | Health check |

Documentação interativa em `/swagger`.

## Segurança e privacidade

Nenhuma senha bancária é solicitada ou armazenada. Tokens vão para o Keychain do iOS. Em
desenvolvimento não se usa dado financeiro real. Detalhes: [docs/checklist-seguranca.md](docs/checklist-seguranca.md)
e integração: [docs/open-finance-provedores.md](docs/open-finance-provedores.md).
