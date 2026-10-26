# Segurança e recuperação

## PIN e biometria

No Windows: **Seguranca**. No Android: **Mais > Preferências e backup > Segurança**.

O PIN possui de 6 a 12 dígitos e é armazenado como hash PBKDF2-HMAC-SHA256, com salt aleatório e 600.000 iterações. Alterar/remover exige o PIN atual. Após cinco falhas, há espera crescente até 30 minutos, persistida entre execuções. O Windows bloqueia após dois minutos sem interação; o Android exige desbloqueio após dois minutos em segundo plano. O Android oferece biometria nativa a partir da API 28, com PIN alternativo.

A configuração do PIN fica em `financeiro.db.lock.json`, ao lado do banco, fora dele. Não é incluída em backups nem sincronizada. Cada aparelho configura seu próprio bloqueio. O PIN é proteção de interface, não criptografia do banco ou proteção contra alguém com acesso aos arquivos do mesmo usuário Windows/aparelho comprometido. O Android usa armazenamento privado; o Windows depende das permissões do perfil do usuário. Biometria só verifica o proprietário do aparelho; não cifra o SQLite.

Se perder o PIN, não há recuperação por email no aplicativo. Guarde um backup protegido e a senha em local seguro. Evite compartilhar o perfil do sistema operacional com terceiros.

## Backups protegidos

**Backup protegido** gera `.finbackup` com senha de no mínimo dez caracteres. Usa AES-256-GCM, salt de 16 bytes, nonce de 12 bytes, tag de 16 bytes e chave derivada por PBKDF2-HMAC-SHA256/600.000 iterações. Cabeçalho, salt e nonce são autenticados. A senha não é armazenada e não há recuperação se ela for perdida. O limite do banco é 256 MB para essa modalidade, pois a operação criptográfica usa memória.

**Restaurar protegido** valida a autenticação antes de substituir o banco. Senha errada ou alteração do arquivo é recusada, preservando o banco atual. Depois, valem as validações normais de schema/integridade/vínculos e a cópia `financeiro.db.before-restore`.

São usados snapshots e arquivos temporários no diretório local/cache; eles são removidos ao concluir a operação, mas não há promessa de apagamento forense de setores de disco. Cancelar seletores Android ou encerrar o processo abruptamente pode deixar arquivos temporários no cache privado. O snapshot `.before-restore` é SQLite sem criptografia: proteja o diretório do aplicativo.

Backups `.db`, CSV, o banco em uso e cópias `.before-migration-v1`/`.before-sync` não são criptografados. O recurso protegido não aplica criptografia retroativa aos arquivos existentes. Use proteção de disco e controle de acesso no sistema operacional quando necessário.

## Sincronização

A sincronização é opcional e exige um servidor HTTPS escolhido pelo usuário. Senhas e tokens não são gravados no SQLite: tokens ficam somente na memória da sessão. Reiniciar o app exige login novamente. O email e a URL podem ficar nas preferências. O PIN e preferências visuais não são enviados; dados financeiros e comprovantes são.

No servidor, ASP.NET Core Identity trata hashes, tokens e bloqueio por tentativas; há limitação de requisições e isolamento por usuário autenticado. Não há senha universal embutida. PostgreSQL e chaves de Data Protection precisam de volumes protegidos e backups próprios. O conteúdo financeiro do servidor não tem criptografia ponta a ponta: o administrador do servidor pode acessá-lo. Não use um servidor no qual não confia.

Não são solicitados números completos de cartão, senhas bancárias ou códigos de autenticação dos bancos. O aplicativo não executa pagamentos nem Open Finance.

## Validação pendente

Testes automatizados verificam PIN, senha errada/arquivo adulterado, migrações e isolamento da API. Não houve auditoria de segurança independente. Biometria e comportamento de bloqueio em diferentes aparelhos precisam de teste físico. Não trate este projeto pessoal como um aplicativo bancário auditado.
