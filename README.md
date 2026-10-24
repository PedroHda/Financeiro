# Financeiro Pessoal

Aplicativo desktop de controle financeiro pessoal em **C# com Windows Forms e .NET 10**, com banco **SQLite local**. Organiza contas, receitas, despesas, cartões, orçamentos e metas, com dashboard, relatórios, exportação CSV e backup.

O projeto possui uma solução **`.sln` para o Visual Studio 2026**. O botão **Novo registro** abre um pop-up com os campos específicos do cadastro.

## Índice

- [Tecnologias e requisitos](#tecnologias-e-requisitos)
- [Executar no Visual Studio 2026](#executar-no-visual-studio-2026)
- [Executar pelo terminal](#executar-pelo-terminal)
- [Primeiros passos](#primeiros-passos)
- [Funcionalidades](#funcionalidades)
- [Regras dos cálculos](#regras-dos-cálculos)
- [Cartões e faturas](#cartões-e-faturas)
- [Recorrências e parcelamentos](#recorrências-e-parcelamentos)
- [Relatórios e exportação](#relatórios-e-exportação)
- [Banco, backup e restauração](#banco-backup-e-restauração)
- [Estrutura e arquitetura](#estrutura-e-arquitetura)
- [Testes](#testes)
- [Publicar o aplicativo](#publicar-o-aplicativo)
- [Solução de problemas](#solução-de-problemas)
- [Limitações e próximas melhorias](#limitações-e-próximas-melhorias)

## Tecnologias e requisitos

| Tecnologia | Utilização |
| --- | --- |
| C# / .NET 10 | Linguagem e plataforma de execução |
| Windows Forms | Interface desktop do Windows |
| `net10.0-windows` | Framework de destino |
| Microsoft.Data.Sqlite `10.0.13` | Acesso ao banco de dados |
| SQLitePCLRaw.bundle_e_sqlite3 `3.0.5` | Distribuição da biblioteca nativa SQLite |
| xUnit / Microsoft.NET.Test.Sdk | Testes automatizados e integração com o Visual Studio |
| Coverlet | Coleta opcional de cobertura de testes |

Para desenvolver e compilar:

- Windows compatível com .NET 10.
- .NET SDK **10.0**, ou Visual Studio 2026 com a carga de trabalho **Desenvolvimento para desktop com .NET** e suporte ao .NET 10.
- Acesso ao NuGet na primeira restauração de pacotes.

O `global.json` mantém a solução na família de SDKs 10.0, permite atualizações de feature band e não seleciona versões de prévia. Confira o ambiente com `dotnet --info`.

O aplicativo funciona localmente após a instalação. Não exige servidor, conta de usuário ou instalação separada do SQLite. Esta aplicação Windows Forms não executa nativamente em Linux ou macOS.

## Executar no Visual Studio 2026

1. Abra **[Financeiro.sln](Financeiro.sln)** na raiz do repositório.
2. Aguarde a restauração dos pacotes NuGet.
3. No Gerenciador de Soluções, defina **FinanceiroApp** como projeto de inicialização.
4. Selecione `Debug` para desenvolver ou `Release` para validar a distribuição.
5. Pressione **F5** para depurar ou **Ctrl+F5** para executar sem depuração.

`Financeiro.slnx` é uma alternativa no formato XML de solução. Os dois arquivos incluem o aplicativo e os testes; utilize `Financeiro.sln` no fluxo principal.

Os formulários são montados em código nos arquivos de `Views`. Não há arquivos `.Designer.cs` para edição visual dos layouts.

## Executar pelo terminal

Abra o PowerShell na pasta que contém `Financeiro.sln`:

```powershell
dotnet restore .\Financeiro.sln
dotnet build .\Financeiro.sln --configuration Debug
dotnet run --project .\FinanceiroApp\FinanceiroApp.csproj
```

Na primeira inicialização, o app cria o banco, as tabelas, as configurações padrão e categorias iniciais. Contas e lançamentos começam vazios.

## Primeiros passos

1. Entre em **Contas > Novo registro**, preencha nome, tipo, saldo inicial e status e clique em **Salvar** no pop-up.
2. Consulte **Categorias**. Há categorias iniciais de receita e despesa; use **Novo registro** para acrescentar outras.
3. Em **Receitas > Novo registro**, selecione uma categoria de receita e a conta de destino e informe valor, data e status.
4. Em **Despesas > Novo registro**, informe categoria, conta, valor e vencimento. Use `Pago` quando o valor já tiver saído da conta.
5. Consulte **Dashboard** para conferir o resumo. Ajuste os filtros para localizar lançamentos fora do período inicial das listagens.

**Novo registro** abre um formulário vazio para inclusão. O formulário lateral mostra a linha selecionada e permite edição em **Adicionar / Salvar**. **Cancelar** ou fechar o pop-up descarta o cadastro ainda não salvo. Uma validação inválida mantém a janela aberta para correção.

Valores aceitam formato brasileiro, como `123,45` e `1.234,56`, com até duas casas decimais. Também é aceito ponto decimal simples, como `123.45`. Datas são exibidas em `DD/MM/AAAA`; competências mensais utilizam `AAAA-MM`.

Para experimentar o aplicativo, acesse **Configuracoes > Dados exemplo**. Essa ação adiciona contas, receitas, despesas, recorrência, cartão, orçamentos e uma meta. Chamadas repetidas adicionam novos registros e podem atualizar orçamentos das categorias usadas nos exemplos; prefira experimentar antes de lançar dados reais.

**Atenção:** **Limpar dados** remove todos os dados financeiros, inclusive os reais, e não apenas os exemplos. Categorias e configurações são preservadas. A operação solicita confirmação; gere um backup antes de usá-la.

## Funcionalidades

| Módulo | Recursos disponíveis |
| --- | --- |
| Dashboard | Saldo atual, receitas/despesas do mês, resultado mensal, total comprometido em cartões, próximas contas em sete dias, vencidas, barras por categoria e evolução mensal em tabela |
| Contas | Conta corrente, dinheiro, poupança, carteira digital e outros; saldo inicial, saldo calculado, status e observações |
| Categorias | Inclusão, edição, exclusão, separação por receita/despesa, cor e identificador de ícone armazenados |
| Receitas | Descrição, valor, recebimento, categoria, conta, forma, observações e status `Recebido` ou `Pendente` |
| Despesas | Descrição, valor, data, vencimento, categoria, conta, forma, observações e status `Pago`, `Pendente` ou `Vencido` |
| Recorrentes | Recorrência mensal, dia de vencimento, início, fim opcional, ativação e geração por comando |
| Parcelamentos | Geração em conta ou cartão, distribuição de centavos e consulta das parcelas vinculadas |
| Cartões | Cadastro, limite total/disponível, fechamento, vencimento, compras, parcelas, fechamento e pagamento de fatura |
| Orçamentos | Limite por categoria/mês, gasto, percentual e alertas de 80%, 90% e 100% |
| Metas | Objetivo, valor atual, prazo, descrição, status, percentual e histórico de aportes |
| Relatórios | Receitas, despesas, categorias, saldo mensal, contas pagas/pendentes/vencidas, cartões e comparação receitas/despesas |
| Exportação | CSV de receitas, despesas, compras no cartão e resultados dos relatórios |
| Manutenção | Backup, restauração validada, dados de exemplo e limpeza confirmada |

Receitas e despesas têm pesquisa, período, categoria, conta, status e ordenação por data, valor ou descrição. Os demais módulos oferecem os filtros implementados em suas telas; não há um conjunto idêntico em todas as listagens.

## Regras dos cálculos

```text
Saldo atual da conta = saldo inicial
                     + receitas recebidas
                     - despesas pagas
                     - faturas pagas por essa conta
```

- Receitas pendentes e despesas não pagas não movimentam o saldo da conta.
- Editar, excluir ou alterar o status recalcula o saldo sem acumular a mesma movimentação duas vezes.
- O saldo atual no dashboard soma as contas ativas.
- As receitas do mês no dashboard consideram receitas recebidas pela data de recebimento.
- As despesas do mês consideram despesas pelo vencimento, independentemente do pagamento, e compras de cartão pela competência da fatura.
- O saldo mensal é `receitas do mês - despesas do mês`. É o resultado do período, não o saldo bancário acumulado.
- Despesas não pagas com vencimento anterior a hoje ficam vencidas. Ao mover o vencimento para hoje ou uma data futura, voltam a pendentes.
- O limite disponível é o limite total menos compras ainda não pagas, incluindo parcelas futuras. O pagamento da fatura libera o limite correspondente.
- Orçamentos consideram despesas e compras no cartão. O pagamento da fatura não é somado novamente como consumo de categoria.
- Aportes aumentam o progresso da meta e registram um histórico, mas não debitam nem transferem dinheiro entre contas. Atingir o objetivo por aportes conclui a meta.

## Cartões e faturas

1. Na aba **Cartoes**, cadastre limite, dia de fechamento e dia de vencimento.
2. Na aba **Compras > Novo registro**, informe a compra e a quantidade de parcelas. O valor informado é o total da compra.
3. Compras após o dia de fechamento vão para a competência seguinte; no próprio dia entram na competência atual.
4. Na aba **Faturas**, escolha o cartão e o mês `AAAA-MM` e use **Fechar fatura**.
5. Selecione a fatura fechada, escolha a conta e use **Pagar fatura**. O valor sai da conta e o limite é liberado.

Se o dia de vencimento for anterior ao fechamento, a fatura vence no mês seguinte. Dias inexistentes são ajustados para o último dia válido do mês.

Faturas fechadas ou pagas não aceitam novas compras ou alterações pelos cadastros. Compras fechadas/pagas não podem ser editadas ou excluídas. O pagamento repetido é bloqueado. Não há reabertura de fatura ou estorno de pagamento nesta versão.

Registre compras na aba **Compras** ou em **Parcelamentos** com cartão. Pagar a fatura já realiza a saída da conta; lançar essa mesma saída como despesa adicional duplicaria o débito.

## Recorrências e parcelamentos

### Despesas recorrentes

Em **Recorrentes > Novo registro**, informe valor, conta, categoria, dia do mês e início. A data final é opcional.

**Gerar meses** aceita de `0` a `24` meses futuros. **Gerar** cria vencimentos entre o início da recorrência e o último dia do mês escolhido, respeitando a data final. `0` inclui o mês atual. O dia 31 é ajustado para o último dia em meses mais curtos.

A geração evita repetir um lançamento da mesma recorrência e competência. Desativar a regra impede novas gerações; excluí-la preserva os lançamentos gerados. Alterar a regra não modifica retroativamente esses lançamentos.

A geração depende do comando do usuário. Não há tarefa em segundo plano nem agendamento do Windows.

### Despesas parceladas

Em **Parcelamentos > Novo parcelamento**, informe total, quantidade, primeiro vencimento, categoria e destino. Escolha uma conta para gerar despesas pendentes ou um cartão para gerar compras parceladas.

As parcelas recebem identificação `1/6`, `2/6` etc. A distribuição de centavos mantém a soma igual ao total e impede parcelas nulas ou negativas. A lista inferior mostra os registros vinculados ao grupo selecionado.

No cartão, a primeira data determina a competência inicial conforme o fechamento; as demais parcelas usam competências mensais consecutivas.

## Relatórios e exportação

Em **Relatorios**, escolha o tipo, período e filtros e clique em **Gerar**. Os filtros devem corresponder ao tipo consultado. Um filtro de conta, por exemplo, não seleciona compras de cartão sem conta vinculada.

| Registro | Data usada no filtro |
| --- | --- |
| Receita | Recebimento |
| Despesa | Vencimento |
| Compra no cartão | Data da compra/parcela registrada |

Nos relatórios mensais, as compras de cartão são agrupadas pela competência da fatura. O relatório por categoria inclui despesas e compras no cartão. O saldo mensal sem filtro de status considera somente receitas recebidas; um status selecionado restringe os registros àquele status.

**Contas pagas**, **Contas pendentes** e **Contas vencidas** listam despesas. O detalhamento de faturas fica no módulo de cartões.

**Exportar CSV** grava a consulta atual, com cabeçalho, separador `;`, UTF-8 com BOM e escape de campos com aspas ou quebras de linha. O arquivo pode ser importado no Excel, mas não é um `.xlsx`.

## Banco, backup e restauração

O banco padrão fica fora do código, por usuário do Windows:

```text
%LOCALAPPDATA%\FinanceiroApp\financeiro.db
```

O diretório e as tabelas são criados automaticamente. Não é necessário executar scripts SQL ou instalar um servidor. Recompilar o projeto ou executar `dotnet clean` não apaga os dados.

### Fazer backup

Em **Configuracoes > Backup**, escolha um arquivo `.db` diferente do banco em uso e guarde-o em um local apropriado para recuperação.

O backup usa a API de snapshot do SQLite, incluindo dados confirmados que estejam no WAL. A gravação passa por arquivo temporário para preservar um backup existente caso a criação falhe.

### Restaurar

Em **Configuracoes > Restaurar**, selecione um backup do Financeiro e confirme a substituição dos dados atuais.

Antes da substituição, são verificadas integridade SQLite, tabelas, colunas necessárias e referências entre registros. Arquivos inválidos e bancos sem o schema do aplicativo são rejeitados. O estado anterior fica em:

```text
%LOCALAPPDATA%\FinanceiroApp\financeiro.db.before-restore
```

Esse arquivo também pode ser selecionado em **Restaurar**, usando a opção de todos os arquivos do seletor. Uma nova restauração substitui essa cópia de recuperação.

O banco e os backups não são criptografados. Não há login ou sincronização na nuvem. Arquivos de dados e backups estão excluídos do Git pelo `.gitignore`.

## Estrutura e arquitetura

```text
Financeiro/
|-- Financeiro.sln                 # Solucao principal para Visual Studio
|-- Financeiro.slnx                # Alternativa em XML
|-- global.json                   # Selecao do SDK .NET 10
|-- .editorconfig                 # Convencoes de edicao
|-- README.md
|-- docs/TESTES.md                 # Roteiro manual e cobertura automatizada
|-- FinanceiroApp/
|   |-- FinanceiroApp.csproj
|   |-- Program.cs                # Cultura pt-BR e inicializacao
|   |-- Models/AppModels.cs       # Entradas, filtros e resumos
|   |-- Data/AppDatabase.cs       # Schema SQLite e Repository
|   |-- Services/
|   |   |-- FinanceService.cs     # Regras e consultas financeiras
|   |   `-- DatabaseBackupService.cs
|   |-- Helpers/
|   |   |-- Ui.cs                 # Controles, estilos e tabelas
|   |   `-- FinancialFormatting.cs
|   `-- Views/                    # Janela principal, modais e telas
`-- FinanceiroApp.Tests/           # Testes com bancos temporarios isolados
```

As telas coletam valores e chamam os serviços. `FinanceService` valida, consulta e recalcula saldos/limites. `Repository` usa comandos parametrizados. Operações compostas, como criação de parcelas e pagamento de faturas, usam transações.

O schema possui 13 tabelas: `Accounts`, `Categories`, `Incomes`, `Expenses`, `RecurringExpenses`, `InstallmentGroups`, `CreditCards`, `CardPurchases`, `Invoices`, `Budgets`, `Goals`, `GoalContributions` e `Settings`. Chaves estrangeiras preservam vínculos, e há índices para datas, status e competências.

Datas são persistidas como texto ISO (`AAAA-MM-DD`) e competências como `AAAA-MM`. Valores monetários estão em colunas `REAL`; os serviços usam `decimal` e validam centavos. Migrar a persistência para centavos inteiros é uma melhoria planejada para eliminar a representação binária de dinheiro no banco.

## Testes

```powershell
dotnet test .\Financeiro.sln --configuration Release
```

No Visual Studio 2026, use **Teste > Gerenciador de Testes > Executar Todos**.

Os testes usam arquivos SQLite próprios em `%TEMP%\FinanceiroApp.Tests\<identificador>` e removem somente sua pasta temporária. Não acessam nem limpam o banco pessoal em `%LOCALAPPDATA%`.

A cobertura inclui saldos, status, parcelas/centavos, recorrências, limites, pagamento de fatura, filtros, metas, orçamentos, leitura monetária, largura do painel de cadastro e backup/restauração, inclusive arquivo inválido e snapshot WAL.

Para coletar cobertura:

```powershell
dotnet test .\Financeiro.sln --configuration Release --collect:"XPlat Code Coverage"
```

O roteiro manual está em **[docs/TESTES.md](docs/TESTES.md)**. A automação não substitui a conferência visual dos pop-ups, navegação e escalas de DPI.

## Publicar o aplicativo

Execute na raiz do repositório. Publique o projeto do aplicativo, e não a solução inteira.

### Windows x64 com runtime incluído

```powershell
dotnet publish .\FinanceiroApp\FinanceiroApp.csproj --configuration Release --runtime win-x64 --self-contained true --output .\artifacts\publish\win-x64
```

Execute `FinanceiroApp.exe` na pasta publicada. Distribua **a pasta inteira**, incluindo DLLs e SQLite nativo. Essa variante inclui o runtime e dispensa instalar o .NET Desktop Runtime separadamente no destino.

### Windows x64 com runtime instalado

```powershell
dotnet publish .\FinanceiroApp\FinanceiroApp.csproj --configuration Release --runtime win-x64 --self-contained false --output .\artifacts\publish\win-x64-framework
```

Essa variante exige **.NET Desktop Runtime 10** no destino. Para outra arquitetura, utilize um identificador de runtime correspondente e valide no computador de destino.

A publicação não inclui seus dados pessoais. O aplicativo continua usando o banco do usuário em `%LOCALAPPDATA%`. Não há instalador, atualização automática ou assinatura do executável nesta versão.

## Solução de problemas

| Sintoma | Verificação |
| --- | --- |
| SDK não encontrado | Execute `dotnet --info`, instale o SDK 10.0 e confira a carga de trabalho desktop no Visual Studio |
| Testes iniciam ao pressionar F5 | Defina `FinanceiroApp` como projeto de inicialização |
| Falha no NuGet | Confira conexão e fonte `nuget.org`; execute `dotnet restore .\Financeiro.sln` |
| Conta não aparece | Confira se existe e está ativa; abra novamente a tela ou o pop-up |
| Categoria não aparece | Confira status ativo e tipo de receita/despesa correspondente |
| Registro salvo não aparece | Ajuste período, pesquisa, categoria, conta e status da listagem |
| Cartão sem limite | Confira compras e parcelas ainda não pagas; pagar a fatura libera seu valor |
| Compra bloqueada para edição | Confira se a fatura já foi fechada ou paga |
| Exclusão bloqueada | Há registros vinculados; prefira inativar o cadastro quando necessário |
| Banco bloqueado ou sem acesso | Confira permissões em `%LOCALAPPDATA%\FinanceiroApp`, espaço em disco e outras instâncias do app |
| Backup recusado | Selecione um backup válido deste aplicativo; a extensão `.db` não garante compatibilidade |
| Interface antiga | Encerre a execução, recompile e execute o projeto correto |

## Limitações e próximas melhorias

Recursos ainda não implementados ou parciais:

- Tema escuro: a preferência é armazenada, mas a interface utiliza o tema claro.
- Moeda e data personalizadas: o app usa BRL e `DD/MM/AAAA`; alterar a preferência de data não modifica todos os controles.
- Exportação nativa Excel (`.xlsx`), PDF e impressão.
- Gráficos de linha e comparativos: evolução mensal e comparativos estão em tabelas; o dashboard tem barras por categoria.
- Identificação e remoção exclusiva dos dados de demonstração; a limpeza atual é geral.
- Migrações versionadas do schema e dinheiro persistido em centavos inteiros.
- Transferências entre contas, conciliação, importação bancária e sincronização.
- Estornos, reabertura de fatura, edição do parcelamento completo e exclusão de uma série inteira.
- Vinculação de aportes de metas a movimentações reais de contas.
- Aplicação visual de ícones/cores de categorias e uniformização dos filtros entre módulos.
- Testes visuais abrangentes em diferentes resoluções e escalas de DPI.

Para contribuir, mantenha regras financeiras nos serviços, use parâmetros SQL, acrescente testes para mudanças de comportamento e execute `dotnet test` antes do commit. As convenções básicas ficam em `.editorconfig`.
