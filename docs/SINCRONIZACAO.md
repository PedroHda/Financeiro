# Sincronização opcional

## Recursos e limites

A API usa ASP.NET Core 10, Identity, EF Core e PostgreSQL. Os clientes Windows/Android continuam funcionando offline. A sincronização usa identidades globais, remapeamento de IDs locais, versões por registro, paginação e tombstones de exclusão. Não é uma troca integral de `.db`.

Conflitos exigem escolher a versão local ou a do servidor; cancelar preserva os registros. Rateios acompanham a despesa; aportes são combinados sem duplicar o saldo base das metas. Identificadores de extrato evitam duplicar importações entre aparelhos. Saldos/limites são recalculados, e combinações com vínculos, limites ou faturas inconsistentes são recusadas. Uma cópia `financeiro.db.before-sync` precede alterações recebidas.

Os testes simulam bancos independentes, conflitos, exclusões, anexos e isolamento entre usuários. PostgreSQL/Docker não foi executado neste ambiente: valide a hospedagem antes de usar dados reais. Não há servidor público incluído.

## Conectar

1. Hospede a API com certificado HTTPS válido.
2. Abra **Sincronizacao** no Windows ou **Mais > Sincronização** no Android.
3. Informe a URL raiz HTTPS, email e senha da conta do Financeiro, nunca credenciais bancárias.
4. Crie a conta se o administrador permitiu cadastros. A senha exige 12 caracteres, maiúscula, minúscula, número e símbolo.
5. Conecte o segundo aparelho à mesma conta e use **Sincronizar agora**.
6. Opcionalmente ative **Sincronizar durante o uso**, com tentativas a cada dois minutos. Após reiniciar o app, faça login novamente: tokens ficam apenas em memória.

Não há sincronização com o Android fechado. Ela não é um backup histórico: exclusões também se propagam. Preferências locais, PIN e senhas não são enviados; dados financeiros e comprovantes são. Restaurar um backup antigo pode gerar conflitos.

**O banco pertence à instalação, não troca automaticamente ao mudar o login. Trocar de conta pode enviar os dados locais à nova conta.** Faça backup e use um banco/instalação separado para separar pessoas. A primeira sincronização inclui os dados que já existem na instalação.

## Desenvolvimento

```powershell
dotnet run --project .\Financeiro.Api --environment Development --urls http://localhost:5080
```

Development usa SQLite próprio `financeiro-api-dev.db`. `GET /health` informa o protocolo. As rotas `/api/*` exigem autenticação. HTTP é exclusivo de desenvolvimento/testes: as interfaces exigem HTTPS com certificado confiável no aparelho. Não desabilite validação TLS.

| Rota | Função |
| --- | --- |
| `POST /auth/register` | Cadastro quando habilitado |
| `POST /auth/login?useCookies=false` | Token Identity |
| `POST /auth/refresh` | Renovação da sessão |
| `GET /api/me` | Identidade autenticada |
| `GET /api/sync?cursor=0` | Alterações paginadas do usuário |
| `POST /api/sync` | Lote com versão esperada |

O servidor inicial não configura envio de email ou recuperação de senha. Configure isso antes de abrir cadastros públicos. Bloqueios de conta e limitação de requisições não substituem monitoramento operacional.

## Docker Compose e HTTPS

Requer Docker Compose, domínio apontado para o servidor e portas 80/443 acessíveis. `deploy` contém PostgreSQL, API e Caddy.

1. Crie `deploy/.env` a partir de `deploy/.env.example`.
2. Defina domínio e senha PostgreSQL longa/aleatória. `.env` é ignorado pelo Git.
3. Use `ALLOW_REGISTRATION=true` apenas durante o cadastro das contas autorizadas.
4. Execute:

```powershell
docker compose --env-file deploy/.env -f deploy/compose.yml up --build -d
```

5. Confira `https://SEU-DOMINIO/health`, cadastre sua conta e volte `ALLOW_REGISTRATION=false`, recriando a API.
6. Faça backups dos volumes PostgreSQL e `api-private`, com chaves de Data Protection.

PostgreSQL não tem porta pública no Compose. O proxy confiável é `172.30.10.10`; ajuste API e subnet juntos se mudar a rede. Chaves no volume Linux não são cifradas automaticamente: proteja host, permissões e backups. Não há criptografia ponta a ponta; o administrador do servidor pode ler os dados financeiros.

## Migrações e testes

PostgreSQL aplica migrações EF Core na inicialização. SQLite é apenas para desenvolvimento/testes, com criação inicial de schema.

```powershell
dotnet tool restore
dotnet ef migrations add NomeDaMudanca --project Financeiro.Api
dotnet ef migrations script --idempotent --project Financeiro.Api
dotnet test Financeiro.Api.Tests -c Release
```

Revise SQL e faça backup antes de atualizar. Testes locais usam SQLite temporário, não PostgreSQL: valide também TLS, volumes, concorrência, restauração e migrações na hospedagem escolhida.
