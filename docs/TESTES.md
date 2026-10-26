# Verificação do Financeiro

## Testes automatizados

Execute no Windows a partir da raiz:

```powershell
dotnet test .\Financeiro.sln --configuration Release
```

`FinanceiroApp.Tests` aparece também no Gerenciador de Testes do Visual Studio 2026. Cada cenário de integração usa um banco próprio em `%TEMP%\FinanceiroApp.Tests`, sem acessar o banco pessoal.

| Arquivo | Cobertura |
| --- | --- |
| `FinanceServiceTests.cs` | Saldos, status, recorrências, parcelas, limites, faturas, vínculos, orçamento e metas |
| `BackupTests.cs` | Restauração válida, arquivo inválido, colunas ausentes, snapshot WAL e proteção do banco em uso |
| `ReportTests.cs` | Período exato, conta, cartão no relatório por categoria e período inválido |
| `FormattingTests.cs` | Valores brasileiros e decimais simples, independentemente da cultura do Windows |
| `LayoutTests.cs` | Largura utilizável do painel de cadastro na janela mínima |

O teste de layout usa controles em thread STA, mas não inspeciona visualmente todas as telas.

## Preparação manual

Se já houver dados reais, faça um backup em **Configuracoes > Backup**. Os passos seguintes acrescentam registros no banco da interface. Para manter os dados reais separados, utilize outro usuário do Windows.

**Limpar dados** apaga todos os dados financeiros, e não apenas exemplos. Não use essa ação para limpar somente testes de um banco misto.

## Cadastros e saldos

1. Em **Contas > Novo registro**, confira os campos do pop-up e salve `Conta manual` com saldo inicial `1.000,00`.
2. Confira a lista e o dashboard. Abra outro cadastro e cancele; nenhum registro deve ser criado.
3. Tente salvar sem nome; a validação deve manter o pop-up aberto.
4. Registre receita recebida de `200,00` nessa conta. O saldo deve ser `1.200,00`.
5. Registre despesa paga de `50,25`. O saldo deve ser `1.149,75`.
6. Registre despesa pendente de `30,00`. O saldo deve permanecer `1.149,75`.
7. Marque essa despesa como paga. O saldo deve ser `1.119,75`.
8. Exclua essa despesa. O saldo deve voltar para `1.149,75`.
9. Crie uma despesa pendente com vencimento de ontem; confira `Vencido`. Mova o vencimento para amanhã; confira `Pendente`.
10. Confira filtros de período, categoria, conta, status e pesquisa.

Os valores esperados pressupõem somente as movimentações desse roteiro na conta de teste. Abra e cancele também os pop-ups de categorias, recorrências, parcelas, cartões, compras, orçamentos e metas.

## Cartões e parcelas

1. Cadastre cartão com limite `1.000,00`, fechamento 25 e vencimento 8.
2. Registre compra de `100,00` em três parcelas em uma competência aberta.
3. Confira três parcelas positivas somando `100,00`; o limite disponível deve ser `900,00`.
4. Em **Parcelamentos**, selecione o grupo e confira os registros vinculados.
5. Feche a primeira fatura e pague pela conta de teste. Confira o débito e a liberação do mesmo valor no limite.
6. Tente pagar novamente; a operação deve ser bloqueada.
7. Tente editar/acrescentar compra nessa fatura fechada ou paga; confira a restrição.

## Recorrência, orçamento e meta

1. Crie recorrência com início neste mês e dia 31. Gere um mês futuro; o mês atual e o seguinte devem ser incluídos, com ajuste para o último dia válido.
2. Gere novamente o mesmo período; não devem surgir duplicações.
3. Defina orçamento `100,00` para categoria sem outros gastos e registre despesa `90,00`. Confira percentual e alerta de 90%.
4. Crie meta `100,00` e adicione aporte `100,00`. Confira histórico e conclusão.

## Relatórios e CSV

1. Gere receitas por período e selecione uma categoria de receita.
2. Gere saldo mensal filtrando uma conta e período menor que o mês; confira somente os registros selecionados.
3. Gere despesas por categoria e confira também compras no cartão.
4. Exporte CSV e importe no Excel com separador `;` e UTF-8. Confira cabeçalhos, valores e campos com aspas/acentos.

## Backup e restauração

1. Gere backup depois dos registros de teste.
2. Acrescente um registro temporário e restaure o backup com confirmação.
3. Confira que o temporário desapareceu e os anteriores permanecem.
4. Confira `financeiro.db.before-restore` na pasta do banco.
5. Tente restaurar texto ou um SQLite de outro sistema. A operação deve ser recusada e os dados atuais devem permanecer.
6. Feche e reabra o app para conferir persistência.

## Interface e distribuição

- Confira tamanho mínimo e janela maximizada, mantendo campos e botões acessíveis.
- Confira pop-ups em escalas de Windows de 100%, 125% e 150%, inclusive os campos finais de recorrências.
- Execute a pasta publicada em ambiente de teste e confira a biblioteca SQLite.
- Distribua a pasta inteira. Publicar com sucesso não equivale a testar um instalador ou todos os computadores de destino.

## Novos testes da versão 1.1

Além dos testes originais, a solução cobre migração de bancos REAL para centavos, precisão em valores grandes, transferências, conciliação sem ajuste automático, importação atômica/duplicidades/OFX/CSV, previsão, rateios, comprovantes, PIN e backup criptografado adulterado/senha errada.

`Financeiro.Api.Tests` usa um servidor em memória e bancos SQLite temporários para testar isolamento de usuários, IDs locais distintos, sincronização, exclusões, anexos, contribuições concorrentes e escolhas de conflitos. Não usa dados pessoais nem depende de PostgreSQL instalado. Validação PostgreSQL/Docker e biometria física continuam pendentes.

`scripts/Test-AndroidSmoke.ps1` só aceita emulador e limpa os dados dessa instalação de teste. Nunca o use em aparelho com seus dados. O CI anexa screenshots/hierarquia; teste também criação/edição, importação, bloqueio, restauração e conflitos em aparelhos reais antes de distribuir amplamente.
