# Distribuição iOS — checklist

> App `.NET MAUI` (`net9.0-ios`). No Windows o alvo iOS **não** compila (exige macOS + Xcode); a build
> oficial roda na esteira `.github/workflows/build-ios.yml` (macOS, Xcode 26.5, workloads `maui ios`).

## 1. Antes de versionar

- [ ] `mobile/FinanceApp.Mobile/FinanceApp.Mobile.csproj`: subir `ApplicationDisplayVersion` (ex.: `1.1`)
      e **incrementar** `ApplicationVersion` (build number) a cada envio.
- [ ] `Platforms/iOS/Info.plist` com as descrições de uso (já presentes): `NSFaceIDUsageDescription`,
      `NSLocationWhenInUseUsageDescription`, `ITSAppUsesNonExemptEncryption=false`, localização `pt-BR`.
- [ ] `Services/ApiConfig.cs`: apontar `BaseUrl` para a API de produção (HTTPS) e revisar `FallbackToDemoData`.
- [ ] Ícone e splash definidos (`Resources/AppIcon`, `Resources/Splash`).

## 2. Build sem assinatura (Sideloadly / AltStore — uso pessoal)

É o fluxo da esteira atual: gera um `.ipa` **sem assinatura** para instalar em um dispositivo próprio.

```bash
dotnet workload install maui ios
dotnet publish mobile/FinanceApp.Mobile/FinanceApp.Mobile.csproj \
  -f net9.0-ios -c Release \
  -r ios-arm64 -p:EnableCodeSigning=false
```

O `.ipa` sai em `bin/Release/net9.0-ios/ios-arm64/publish/`. Instalar via Sideloadly/AltStore
(reinstalar a cada 7 dias com conta Apple gratuita).

## 3. Build assinado (TestFlight / App Store)

Requer **conta paga** no Apple Developer Program e, na esteira, os segredos de assinatura:

- [ ] App ID `com.meucontrole.financas` registrado no portal Apple.
- [ ] Certificado de distribuição + perfil de provisionamento (ou `App Store Connect` com assinatura automática).
- [ ] Publicar com assinatura habilitada (`-p:EnableCodeSigning=true` + `-p:CodesignKey=...` / `-p:CodesignProvision=...`)
      ou via `xcodebuild`/Fastlane no runner macOS, gerando o `.ipa` assinado.
- [ ] Enviar ao App Store Connect (`altool`/`xcrun notarytool` ou Transporter) e distribuir no TestFlight.

## 4. Revisão de segurança antes do envio

- [ ] API de produção com `Jwt:SigningKey` por variável de ambiente/segredo (obrigatório: a API falha ao
      subir em produção sem ela) e `Swagger:Enabled=false`.
- [ ] CORS restrito a origens conhecidas (`Cors:AllowedOrigins`).
- [ ] Rate limiting ativo nos endpoints de autenticação (já configurado) e HTTPS ponta a ponta.
- [ ] Token guardado no **Keychain** via `SecureStorage` (já implementado); nenhuma credencial bancária é
      solicitada ou armazenada.
- [ ] `docs/checklist-seguranca.md` revisado.

## 5. Dados e privacidade (App Store)

- [ ] Preencher o *Privacy Nutrition Label*: dados financeiros do próprio usuário, localização (apenas durante
      o uso, para o módulo de Viagens), sem rastreamento de terceiros.
- [ ] `PrivacyInfo.xcprivacy` (em `Platforms/iOS/Resources`) coerente com os usos declarados.
