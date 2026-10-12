# Checklist de Segurança e Preparação para Distribuição

Marque conforme cada item for implementado/validado. Itens com ✅ já estão na Fase 1.

## Autenticação e autorização
- ✅ Senha com PBKDF2 (210k iterações, salt por senha) — nunca em texto puro.
- ✅ JWT assinado (HS256); chave de assinatura via segredo/variável de ambiente.
- ✅ Chave de assinatura **obrigatória em produção** (a API recusa subir sem ela).
- ✅ Endpoints protegidos por `[Authorize]`.
- ✅ Isolamento por usuário: todos os serviços filtram por `UserId` do token.
- ⬜ Refresh tokens / expiração curta + rotação (evoluir de JWT simples).
- ⬜ Bloqueio por Face ID / Touch ID no app (Fase 7).

## Transporte e armazenamento
- ✅ Token guardado no Keychain do iOS (via `SecureStorage`).
- ✅ Valores monetários em `decimal` (sem erro de ponto flutuante).
- ⬜ HTTPS/TLS obrigatório em produção (ATS no iOS; certificado válido no backend).
- ⬜ Criptografia em repouso de dados sensíveis conforme a arquitetura final.
- ⬜ Gestão de segredos no backend (cofre: Azure Key Vault/AWS Secrets Manager/etc.).

## Dados e privacidade (LGPD)
- ✅ Nenhuma senha bancária é solicitada ou armazenada.
- ✅ Modo demonstração com dados fictícios — sem dados reais em desenvolvimento.
- ⬜ Consentimento explícito para integração bancária (tela + registro do consentimento).
- ⬜ Exportação dos dados pessoais do usuário.
- ⬜ Exclusão da conta e dos dados a pedido do usuário.
- ⬜ Política de privacidade e termos.

## Logging e auditoria
- ✅ Erros tratados centralmente sem vazar detalhes internos ao cliente.
- ✅ Não registrar senhas, tokens nem dados bancários em logs.
- ⬜ Trilha de auditoria de ações sensíveis (login, conexão/desconexão bancária, exclusão).

## Validação
- ✅ Validação de entradas no servidor (valores, transferências, referências por usuário).
- ⬜ Validação no cliente (formulários) — Fase 2.

## Preparação para distribuição iOS
- ⬜ Apple Developer Program ativo e certificados/perfis de provisionamento.
- ⬜ Bundle ID definitivo e ícones/splash finais.
- ⬜ `NSFaceIDUsageDescription` e demais permissões descritas no `Info.plist` (Face ID já incluído).
- ⬜ Revisão de ATS (sem exceções de HTTP em produção).
- ⬜ Testes em dispositivo físico (ver esteira `build-ios.yml` para gerar o `.ipa`).
- ⬜ Assinatura do `.ipa` (Sideloadly/AltStore para teste; App Store Connect para publicação).
