# Financeiro para Android

Aplicativo nativo Android em C#, com .NET 10 e controles próprios para toque. As regras de saldo, cartões, parcelas, orçamentos, metas e backups são as mesmas do Windows, por meio da biblioteca `Financeiro.Core`.

## Instalar o APK

O arquivo gerado é:

```text
artifacts/apk/Financeiro-Android.apk
```

1. Transfira o APK para o celular.
2. Abra o arquivo pelo gerenciador de arquivos do Android.
3. Quando solicitado, autorize a instalação de aplicativos por essa origem.
4. Toque em **Instalar** e abra **Financeiro**.

O APK inclui o runtime necessário. Não é preciso instalar .NET ou SQLite no celular. Esta versão atende **Android 7.0/API 24 ou superior**, em aparelhos **ARM64**; também inclui **x86_64** para emuladores. Não inclui Android de 32 bits nem iOS.

O identificador do aplicativo é `com.pedrohda.financeiro`. Para atualizar uma instalação preservando os dados, utilize a mesma chave de assinatura e um `ApplicationVersion` maior. Faça backup antes de trocar de aparelho ou desinstalar.

## Telas e fluxos

| Navegação | Recursos |
| --- | --- |
| Início | Saldo, resultado mensal, receitas/despesas, atalhos de lançamento, categorias, próximas contas e últimos movimentos |
| Movimentos | Extrato unificado, busca, seletor de mês, tipo e status, inclusão e edição |
| Planejar | Orçamentos com alertas, metas com aportes e histórico, recorrências e parcelamentos |
| Carteira | Contas, saldos, cartões, limites, compras e fechamento/pagamento de faturas |
| Mais | Categorias, relatórios, operações, importação OFX/CSV, conciliação, sincronização, PIN/biometria e backups |

Os botões de inclusão abrem formulários com campos, seletores e calendário nativos. A validação mantém o formulário aberto; exclusões exigem confirmação. Não há cadastro obrigatório, anúncios ou solicitação de credenciais bancárias. A conexão com um servidor próprio só é usada quando o usuário configura a sincronização.

### Aparência e privacidade

- Tema claro ou escuro em **Mais > Preferências e backup**.
- Ícone de olho no cabeçalho para ocultar/exibir valores.
- Preferências persistidas entre execuções.
- Gráficos de categorias e barras de progresso de orçamentos/metas.
- Datas e moeda em português do Brasil.

Ocultar valores é uma função visual, não autenticação. O app não realiza Pix, transferências bancárias, login no banco ou Open Finance. O PIN é um bloqueio de interface; o SQLite local continua sem criptografia. A sincronização opcional pode ocorrer a cada dois minutos durante o uso, após login e ativação, mas não com o aplicativo fechado.

### Novas operações

- **Mais > Operações**: transferências entre contas, conciliação, OFX/CSV, regras de categoria e previsão de saldo.
- A prévia de importação tem seleção por registro, categorias, alerta de possíveis duplicados e páginas de 50 itens.
- Em **Movimentos**, pressione uma receita/despesa por mais tempo para duplicar, alterar status, adicionar/exportar comprovantes ou dividir uma despesa entre categorias.
- **Mais > Preferências e backup > Segurança**: PIN de 6 a 12 dígitos e biometria em aparelhos Android 9/API 28 ou superiores com suporte. Em versões anteriores, use o PIN. Após dois minutos em segundo plano, o desbloqueio é exigido novamente.
- **Backup protegido** exporta um `.finbackup` com senha; **Restaurar backup protegido** o importa no Android ou Windows.

Veja os limites e as regras completas em [OPERACOES.md](OPERACOES.md) e [SEGURANCA.md](SEGURANCA.md).

## Usar os dados do Windows no celular

1. No Windows, entre em **Configuracoes > Backup** e salve um `.db`.
2. Transfira esse arquivo ao Android.
3. No Android, entre em **Mais > Preferências e backup > Restaurar backup**.
4. Escolha o arquivo e confirme a substituição.

O caminho inverso é **Exportar backup** no Android e **Restaurar** no Windows. A restauração substitui todo o banco de destino; não mescla movimentações. Para uso simultâneo, configure os dois na mesma conta HTTPS em **Sincronização**; veja [SINCRONIZACAO.md](SINCRONIZACAO.md). Não misture restauração de snapshots com edições independentes sem conferir os conflitos.

O seletor de arquivos do Android dá acesso apenas ao documento escolhido, sem permissão ampla ao armazenamento. O banco fica no diretório privado do aplicativo. Backups `.db` e CSV exportados não são criptografados; `.finbackup` usa criptografia autenticada. A permissão de internet atende a sincronização opcional, e as permissões biométricas atendem ao desbloqueio. Não há acesso às credenciais do banco.

## Compilar no Windows

Requisitos:

- .NET SDK 10.0 e workload **android**.
- Android SDK com plataforma **36** e ferramentas de compilação.
- JDK **21**.

Na raiz do repositório:

```powershell
dotnet workload install android
.\scripts\Build-Android.ps1
```

Para informar caminhos próprios:

```powershell
.\scripts\Build-Android.ps1 `
  -AndroidSdkDirectory 'C:\Android\android-sdk' `
  -JavaSdkDirectory 'C:\Java\jdk-21'
```

O script utiliza uma cópia auxiliar em `%LOCALAPPDATA%\FinanceiroApp\AndroidBuild-...` se houver acentos no caminho do repositório. Isso contorna a limitação da ferramenta AAPT2 no Windows, preservando a pasta original. A Microsoft documenta essa limitação em [APT2265](https://learn.microsoft.com/en-us/dotnet/android/messages/apt2265).

No Visual Studio 2026, abra `Financeiro.Mobile.sln` e configure o ambiente de desenvolvimento Android. Para compilar diretamente no IDE, prefira um checkout em caminho sem acentos, como `C:\Projetos\Financeiro`.

### Assinatura e atualizações

Na primeira execução, o script cria uma chave de assinatura local em:

```text
%LOCALAPPDATA%\FinanceiroApp\Signing\financeiro-mobile.keystore
%LOCALAPPDATA%\FinanceiroApp\Signing\signing-password.xml
```

A senha é aleatória e protegida pelo usuário Windows via DPAPI; o arquivo de senha não é portátil entre usuários/máquinas. Preserve a chave e o acesso à senha para futuras atualizações. Esses arquivos não são enviados ao Git. O script passa a senha ao `apksigner` por variável de ambiente do processo, removida ao finalizar, e verifica a assinatura antes de entregar o APK.

Exemplo de nova versão:

```powershell
.\scripts\Build-Android.ps1 -VersionCode 2 -DisplayVersion '1.0.1'
```

O script gera um APK assinado para instalação direta e `Financeiro-Android.apk.sha256`. Não publica na Play Store. A publicação em loja requer procedimentos próprios, conta de desenvolvedor e políticas da plataforma.

## Organização do código

```text
Financeiro.Core/                 Regras, modelos, SQLite e backups
Financeiro.Mobile/
  MainActivity.cs               Estado, navegação e composição nativa
  MainActivity.Home.cs          Dashboard
  MainActivity.Ledger.cs        Extrato e pesquisa
  MainActivity.Planning.cs      Planejamento
  MainActivity.Wallet.cs        Contas, cartões e faturas
  MainActivity.More.cs          Relatórios, aparência e arquivos
  MainActivity.Editors.cs       Formulários ligados aos serviços
  Forms/EntryForm.cs            Janela de cadastro reutilizável
  UI/MobileUi.cs                Componentes, cores e insets
  Assets/                       Fonte Material Icons e licença
  AndroidManifest.xml           Aplicativo sem permissão de internet
scripts/Build-Android.ps1        APK assinado e checksum
```

A biblioteca compartilhada preserva os namespaces `FinanceiroApp.Models`, `FinanceiroApp.Services`, `FinanceiroApp.Data` e `FinanceiroApp.Helpers` para compatibilidade com o desktop.

## Referências de produto e design

As referências serviram para escolher padrões de organização, sem copiar marcas, telas ou dados:

- [Organizze](https://www.organizze.com.br/app-de-financas/): contas, cartões, orçamento por categoria e acompanhamento dos gastos.
- [Mobills](https://www.mobills.com.br/blog/mobills/como-utilizar-o-mobills/): planejamento, metas e visão de receitas/despesas.
- [Nubank](https://nu.com/pt/sala-de-imprensa/consumidores/nubank-integra-limites-disponiveis-em-uma-unica-tela-para-facilitar-jornada-financeira-de-clientes): visualização clara de limites e atalhos financeiros.
- [Material Icons](https://github.com/google/material-design-icons): fonte de ícones utilizada sob Apache 2.0, com licença incluída em `Assets`.

O resultado tem identidade própria em verde, navegação inferior e conteúdo organizado para leitura rápida. O aplicativo é um gerenciador local de finanças pessoais, não um aplicativo bancário.

## Validação desta entrega

- Solução Windows e biblioteca compartilhada compiladas, com **39 testes automatizados aprovados**.
- APK Release gerado para ARM64 e x86_64, mínimo API 24 e destino API 36.
- Alinhamento do APK e assinaturas v2/v3 verificados com Android SDK (`zipalign` e `apksigner`).
- Manifesto e arquiteturas conferidos com `aapt2`; o app não solicita permissão de internet.
- A execução visual e os fluxos de cadastro no Android **ainda precisam de validação em um aparelho**. O emulador local não inicializou: o computador está sem driver de aceleração, e a tentativa por software ficou indisponível.

O roteiro recomendado é instalar o APK, criar uma conta e uma receita/despesa, fechar/reabrir o app para conferir persistência, testar tema/ocultação de valores e exportar/restaurar um backup. Os testes da biblioteca financeira não substituem esses testes de interface no celular.
