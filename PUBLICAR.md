# Subindo para o GitHub

Passo a passo, do zero ao primeiro release, e como testar depois.

## 1. Criar o repositório local

```bash
cd gitportfix
git init -b main
git add .
git commit -m "GitPortFix: leva correções entre branches base"
```

## 2. Criar o repositório remoto

Com o [GitHub CLI](https://cli.github.com):

```bash
gh repo create gitportfix --private --source=. --remote=origin --push
```

Sem o CLI: crie o repositório vazio pela interface do GitHub — **sem** README, .gitignore
ou licença, porque esses arquivos já existem aqui — e depois:

```bash
git remote add origin https://github.com/SEU-USUARIO/gitportfix.git
git push -u origin main
```

## 3. Ajustar o README

Troque `SEU-USUARIO` na URL do badge, no topo do `README.md`.

## 4. Primeiro release

```bash
git tag v1.0.0
git push origin v1.0.0
```

O workflow `release.yml` compila, instala o Inno Setup no runner, gera o instalador e
anexa a um release. Uns 3 a 5 minutos. O time baixa o `.exe` direto da aba Releases.

---

## Testando

Para testar o GitPortFix você precisa de um projeto com branches de verdade. Preparei
um chamado `loja-teste`, já com o histórico pronto: um bug real em `Pedido.cs` e as
quatro branches que compõem o cenário (`main`, `release-candidate`, `hotfix` e
`correcao/BUG-1234`).

### Subir o projeto de teste ao GitHub

Ele veio como um `.tar.gz` porque já é um repositório git de verdade — extrair preserva
todos os commits e branches, então você não precisa recriá-los na mão.

```bash
tar -xzf loja-teste.tar.gz
cd loja-teste
git log --oneline --all --graph     # confira que o histórico veio certo
```

Crie o repositório no GitHub (pela interface, vazio, sem README) e depois:

```bash
git remote add origin https://github.com/SEU-USUARIO/loja-teste.git
git push -u origin --all
```

O `--all` é o que importa aqui: manda as quatro branches de uma vez, não só a `main`.

### Testar o GitPortFix nele

1. Clone o `loja-teste` numa pasta separada, do jeito que qualquer desenvolvedor faria:

   ```bash
   git clone https://github.com/SEU-USUARIO/loja-teste.git
   ```

2. Abra o GitPortFix e aponte para essa pasta clonada.
3. **Minha correção:** `correcao/BUG-1234`
4. O campo **Nasceu de** deve se preencher sozinho com `release-candidate` — é a
   detecção automática funcionando, já que essa branch tem só 3 commits de diferença
   contra ela, e mais contra qualquer outra base.
5. **Levar para:** `hotfix`
6. Clique em **Ver o que será levado** — deve listar os 3 commits com "BUG-1234" na
   mensagem.
7. Clique em **Levar para a base de destino**.

### Como saber se deu certo

Depois de executar, dê uma olhada na branch nova que o app criou:

```bash
git log --oneline <nome-da-branch-nova>
```

Ela deve ter:

- o commit `Adiciona log de erro para o incidente em producao` (exclusivo da `hotfix`);
- os 3 commits de `BUG-1234`;
- **mas não** o `Adiciona suporte a cupom de desconto` (exclusivo da `release-candidate`).

Esse último ponto é o que prova que o `rebase --onto` fez o trabalho certo: ele pegou
só a sua correção, sem arrastar os commits de integração que a `release-candidate`
tinha e a `hotfix` não.

Se você quiser testar o cenário de conflito, edite `src/Pedido.cs` na `hotfix` mexendo
nas mesmas linhas que a correção altera, empurre esse commit, e tente o port de novo —
o app deve parar e listar o arquivo em conflito, sem travar, e o botão de abortar deve
devolver o repositório ao estado anterior.

---

## Público ou privado?

**Privado** faz sentido se o `PadroesDeBase` no `AppConfig.cs` acabar refletindo a
convenção de nomes interna da empresa, ou se o README ganhar exemplos com nomes de
projetos reais. Nada disso é segredo grave, mas também não precisa estar aberto.

**Público** faz sentido porque o app não contém nada proprietário: é um invólucro em
cima de `git rebase --onto`. E o problema que ele resolve é comum o bastante para que
outras pessoas achem útil.

Se for público, revise antes:

- `installer/GitPortFix.iss` — o campo `AppPublisher` está genérico, ajuste ou deixe;
- o `loja-teste` usa e-mails `@exemplo.local`, sem dado real, então pode ficar público
  também sem problema.

## Sobre o aviso do SmartScreen

O executável não é assinado, então o Windows vai avisar que o publicador é desconhecido
na primeira execução de cada máquina. Opções:

1. **Conviver.** Documente no README que é para clicar em "Mais informações" → "Executar
   assim mesmo". É o que a maioria dos projetos pequenos faz.
2. **Assinar.** Um certificado de code signing custa a partir de uns 200 dólares por ano.
   Só vale se o app for distribuído fora do seu time.
3. **Distribuir o `.exe` avulso por uma pasta de rede** em vez do instalador. O aviso
   continua, mas some a etapa de instalação.

Se sua empresa já tem um certificado de assinatura, dá para adicionar um passo de
`signtool` no `release.yml`, guardando o certificado como secret do repositório.
