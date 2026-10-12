# Open Finance — Provedores a avaliar

Este documento orienta a contratação de um **agregador de Open Finance** para conectar bancos
brasileiros ao aplicativo. Enquanto não houver provedor contratado e credenciais configuradas, o
sistema opera em **modo demonstração** (`DemoBankIntegrationProvider`, dados fictícios identificados).

> Importante: os valores de preço, limites e requisitos mudam com frequência e **devem ser
> confirmados diretamente com cada fornecedor**. Aqui listamos o que precisa ser avaliado, não
> números fixos. Não há endpoints inventados neste repositório.

## Por que um agregador?

Conectar diretamente a cada banco participante do Open Finance exige habilitação regulatória,
certificados, diretório de participantes e manutenção contínua. Agregadores encapsulam isso e
expõem uma API única. O app nunca pede a senha do banco: o acesso é por **consentimento**.

## Candidatos (mercado brasileiro)

| Provedor | Observações | O que confirmar |
|---|---|---|
| **Pluggy** | Agregador brasileiro focado em Open Finance/dados financeiros. | Cobertura de instituições, modelo de preço, ambiente sandbox, formato das transações, suporte a cartões, webhooks de atualização. |
| **Belvo** | Agregador latino-americano com cobertura no Brasil. | Mesmos itens acima + residência de dados e conformidade LGPD. |
| **Klavi / Quanto / outros** | Demais participantes/iniciadores. | Avaliar caso a caso conforme a necessidade. |

## Requisitos de acesso a confirmar com o fornecedor escolhido

1. **Cadastro e credenciais**: client id/secret, chaves de ambiente (sandbox e produção).
2. **Fluxo de consentimento**: como iniciar a autorização, URLs/SDK de conexão, prazo de validade
   do consentimento e renovação.
3. **Dados disponíveis**: contas, saldos, cartões, faturas e transações; granularidade e histórico.
4. **Identificadores estáveis** por transação (para idempotência/anti-duplicidade).
5. **Atualização**: webhooks ou polling; frequência permitida; custo por sincronização.
6. **Revogação**: como revogar consentimento e o que acontece com os dados.
7. **Limites e custo**: chamadas/mês, número de conexões, preço por item conectado.
8. **Conformidade**: LGPD, criptografia em trânsito/repouso, região de armazenamento, DPA.

## Como integrar no código (sem reescrever regras)

1. Implemente `FinanceApp.Application.Banking.IBankIntegrationProvider` em
   `FinanceApp.Infrastructure/Banking/` (ex.: `PluggyBankIntegrationProvider`).
2. Mapeie `ExternalAccount`/`ExternalTransaction` para as entidades do domínio, preenchendo
   `Transaction.ExternalId` com o id do provedor (chave de idempotência).
3. Troque o registro em `AddInfrastructure`:
   ```csharp
   // services.AddScoped<IBankIntegrationProvider, DemoBankIntegrationProvider>();
   services.AddScoped<IBankIntegrationProvider, PluggyBankIntegrationProvider>();
   ```
4. Guarde segredos fora do código (variáveis de ambiente / cofre). Nunca commite credenciais.
5. As regras financeiras (saldo, conciliação, categorização) permanecem inalteradas.

## Anti-duplicidade

Ao importar, procure primeiro por `Transaction.ExternalId` (id do provedor). Na ausência dele,
use a combinação valor + data + estabelecimento + conta como heurística antes de inserir.
