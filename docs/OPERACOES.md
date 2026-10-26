# Operações financeiras da versão 1.1

As regras são compartilhadas por Windows e Android. O menu é **Operacoes** no Windows e **Mais > Operações** no Android.

## Transferências internas

Selecione contas de origem/destino diferentes, valor positivo, data realizada e observações opcionais. A origem diminui e o destino aumenta pelo mesmo valor. A transferência não é receita nem despesa e não muda o patrimônio consolidado quando ambas as contas estão ativas. Não há comunicação com o banco nem execução de Pix. Datas futuras são recusadas; use o planejamento para compromissos previstos.

Transferências podem ser editadas/excluídas, recalculando os saldos. Contas com vínculos não podem ser excluídas. Contas inativas não entram no saldo consolidado.

## Conciliação

Informe conta, data e saldo mostrado no banco. O aplicativo registra seu saldo calculado, o saldo informado, a diferença e as observações. Não cria ajustes nem altera o saldo automaticamente. Corrija ou cadastre os lançamentos responsáveis após conferir o extrato.

O saldo histórico considera a data dos registros: receitas recebidas, despesas pagas pelo vencimento, faturas pela data de pagamento e transferências pela data realizada. Uma despesa antiga marcada como paga hoje não possui uma data de pagamento separada; essa limitação afeta a conciliação histórica. Prefira conferir a data atual.

## Importar CSV

Formato recomendado em UTF-8; arquivos Latin-1 também são lidos. Limite: 10 MB e 10.000 movimentos. Delimitadores `;`, tabulação e `,` são detectados pelo cabeçalho; campos entre aspas são lidos por `TextFieldParser`.

```csv
Data;Descricao;Valor;Id
01/10/2026;Salario;3500,00;credito-001
02/10/2026;Mercado;-120,35;debito-001
```

- Cabeçalhos obrigatórios: `Data`, `Descricao` e `Valor`; também há aliases em inglês e grafias acentuadas.
- Datas `DD/MM/AAAA` ou `AAAA-MM-DD`.
- Positivos são receitas; negativos são despesas; valores zero são ignorados.
- Valores brasileiros (`1.234,56`) ou ponto decimal simples (`1234.56`), com até duas casas.
- `Id`/`FITID`/`Identificador` é opcional, mas recomendado e deve ser estável entre exportações.
- Datas futuras são recusadas na confirmação.

Escolha a conta, abra o arquivo, revise os itens e selecione categorias padrão para receitas/despesas. É possível alterar a categoria de cada item. Registros importados entram como recebidos/pagos; não há importação de compras de cartão diretamente para faturas. Para faturas, utilize o cadastro próprio de compras.

## Importar OFX

Suporta OFX 1/SGML por OFXSharp e OFX 2/XML por `XmlReader`/`XDocument`. Aceita um extrato de uma conta, em BRL, com identificadores FITID. Arquivos com entidades externas/DOCTYPE ou correções de transações são recusados. O parser OFX 1 requer um documento bancário completo compatível com a biblioteca; arquivos fora desse padrão podem ser convertidos pelo usuário para CSV.

A conta é escolhida pelo usuário: confira se corresponde ao extrato. Identificadores OFX são usados para impedir reimportação. Possíveis correspondências de data, descrição e valor são desmarcadas na prévia, mas podem ser selecionadas após revisão. Um identificador já importado não é inserido novamente, mesmo se selecionado. Sem identificador, usa-se conteúdo e ocorrência no arquivo; reordenação de lançamentos idênticos pode exigir conferência manual.

A confirmação é transacional: erro em um item selecionado desfaz o lote. Nenhuma alteração ocorre ao apenas abrir a prévia. Itens desmarcados não são gravados.

## Regras, rateios e comprovantes

Regras sugerem categorias pelo texto da descrição e tipo. A regra com texto mais específico tem precedência. A sugestão pode ser alterada na prévia e não executa importação sozinha.

No Windows, abra **Receitas/Despesas > Mais acoes**. É possível duplicar como pendente, alterar o status das linhas selecionadas e gerenciar comprovantes. O rateio de despesa fica no mesmo menu. No Android, pressione uma receita/despesa por mais tempo em **Movimentos**.

O rateio distribui o valor da despesa entre categorias. A soma deve coincidir exatamente com o total. O saldo da conta é debitado uma única vez; relatórios por categoria e orçamentos usam a distribuição. Remova o rateio antes de alterar o valor total. O Android oferece de 2 a 10 partes; o Windows usa uma grade de linhas.

Comprovantes ficam dentro do SQLite, com limite de 5 MB por arquivo e dez arquivos por lançamento. São exportados, não executados automaticamente. Excluir a receita/despesa também remove os comprovantes. Incluem-se em backups e sincronização; não anexe arquivos que não deseja enviar ao servidor configurado.

## Previsão de saldo

Começa pelo saldo atual das contas ativas e projeta até 24 meses no Windows ou seis meses no Android. Inclui receitas pendentes no período, despesas ainda não pagas, parcelas de cartão pela data prevista/real da fatura e recorrências ativas ainda não geradas naquele mês. Parcelas já criadas são somadas uma vez; o modelo recorrente não duplica sua despesa já gerada. Contas vencidas entram no primeiro mês.

A previsão não grava lançamentos e não garante receitas futuras. Não inclui inflação, rendimentos estimados ou novas despesas que ainda não tenham sido cadastradas. Metas/aportes continuam separados do saldo de contas, como na versão anterior.
