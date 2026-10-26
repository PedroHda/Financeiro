# Pacotes e releases

## Windows

```powershell
.\scripts\Build-Desktop.ps1
```

Gera `artifacts/windows/Financeiro-Windows-x64-1.1.0.zip` e SHA-256. O ZIP inclui runtime .NET/SQLite: extraia e execute `FinanceiroApp.exe`.

Com Inno Setup 6, também gera `Financeiro-Setup-x64-1.1.0.exe`. O instalador é por usuário, sem administrador, cria atalhos e não remove o banco em `%LOCALAPPDATA%/FinanceiroApp` na desinstalação. Não há assinatura Authenticode nem atualização automática. Faça backup antes de atualizar. Sem Inno Setup local, apenas o ZIP é gerado; o workflow instala essa ferramenta.

Parâmetros: `-Version`, `-OutputDirectory`, `-InnoSetupCompiler`.

## Android

```powershell
.\scripts\Build-Android.ps1
```

Gera `artifacts/apk/Financeiro-Android.apk` e SHA-256, versão 1.1.0/código 10100. Preserve a chave e use códigos crescentes para atualizar mantendo os dados. Há cópia auxiliar ASCII para diretórios com acentos.

A chave e a senha DPAPI ficam em `%LOCALAPPDATA%/FinanceiroApp/Signing`, fora do Git. Preserve a recuperação com segurança: o XML DPAPI depende do perfil Windows, não é uma senha portátil. Veja [ANDROID.md](ANDROID.md).

## GitHub Actions

`Build and Tests` testa Windows/API, testa API no Linux, gera pacotes Windows e compila APK de teste. Um emulador Android 35 verifica inicialização, navegação, operações, formulário e sincronização; capturas/hierarquia UI ficam nos artifacts. O APK de teste usa assinatura de depuração e não substitui o APK de distribuição. O smoke não substitui testes físicos/biométricos.

`Release Packages` aceita execução manual ou tag `vMAJOR.MINOR.PATCH`. Tags criam uma GitHub Release somente quando Windows e Android terminam com sucesso. Configure estes Actions Secrets antes:

- `ANDROID_KEYSTORE_BASE64`: chave original codificada em Base64.
- `ANDROID_STORE_PASSWORD`: senha da chave original, mesma senha de store/key neste workflow.
- `ANDROID_KEY_ALIAS`: `financeiro` para a chave criada pelo script local.

**Nenhum secret foi enviado automaticamente.** Sem eles, a release Android falha de propósito em vez de trocar a chave. O workflow deriva o código Android de `major*10000 + minor*100 + patch`; mantenha minor/patch abaixo de 100 e preserve a sequência dos códigos.

Após revisar a versão e configurar os secrets:

```powershell
git tag v1.1.0
git push origin v1.1.0
```

Push para `master` executa CI, mas não cria tag/Release. Binários e dados pessoais ficam fora do histórico Git.
