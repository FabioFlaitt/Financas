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

O app consome a API em `ApiConfig.BaseUrl` (padrão `http://localhost:5219`). Ao abrir, ele tenta
autenticar com o usuário de demonstração; se a API não estiver acessível, exibe **dados de
demonstração** claramente identificados (para a interface nunca ficar vazia).

- **Simulador iOS:** `http://localhost:5219` funciona direto.
- **iPhone físico:** troque `BaseUrl` para o IP da sua máquina na rede (ex.:
  `http://192.168.0.10:5219`) em `mobile/FinanceApp.Mobile/Services/ApiConfig.cs`, ou use um
  domínio HTTPS. Em produção, **sempre HTTPS**.

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
