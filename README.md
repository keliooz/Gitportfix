# GitPortFix

Aplicativo Windows que pega a sua branch de correção, criada a partir de uma branch base,
e replanta os commits dela sobre outra branch base — sem copiar e colar código.

## Estrutura do repositório

```
gitportfix/
├─ src/GitPortFix/        código do aplicativo (.NET 8, WinForms)
├─ installer/             script do Inno Setup
├─ .github/workflows/     publicação de release por tag
├─ build.ps1              publica o exe e gera o instalador
└─ PUBLICAR.md            passo a passo de subir ao GitHub e testar
```

## Testar sem arriscar código de verdade

Existe um repositório de teste separado, chamado [`loja-teste`](https://github.com/keliooz/loja-teste), com quatro branches já
prontas (`main`, `release-candidate`, `hotfix` e `correcao/BUG-1234`) simulando um bug
real e o cenário exato que o GitPortFix resolve. Veja o passo a passo completo em
[PUBLICAR.md](PUBLICAR.md#testando).

## O comando por trás

Todo o app é um invólucro em cima de dois comandos do git.

**Levar todos os commits** (opção padrão):

```bash
FORK=$(git merge-base release-candidate minha-correcao)
git checkout -B minha-correcao-para-hotfix minha-correcao
git rebase --onto origin/hotfix $FORK minha-correcao-para-hotfix
```

O `merge-base` acha o commit onde a sua branch se separou da base. O `rebase --onto`
descarta a base antiga e reaplica só os seus commits em cima da nova base.

**Levar commits específicos:**

```bash
git checkout -B minha-correcao-para-hotfix origin/hotfix
git cherry-pick -x a1b2c3d e4f5g6h
```

Nos dois casos a branch original fica intacta. O app cria sempre uma branch nova.

## Como ele acha o projeto

Quatro caminhos, do mais rápido ao mais manual:

1. **Menu de contexto do Explorer.** Botão direito na pasta do projeto → "Abrir com
   GitPortFix". O instalador registra isso e o caminho chega por linha de comando.
   Também funciona clicando no fundo de uma pasta já aberta.
2. **Repositórios recentes.** Ficam guardados em `%AppData%\GitPortFix\config.json` e
   aparecem na lista suspensa. O último usado abre sozinho.
3. **Varredura automática.** Ao abrir, o app procura em segundo plano por pastas com
   `.git` em `%USERPROFILE%\source\repos`, `C:\dev`, `D:\Projects` e outras dez raízes
   comuns, até três níveis de profundidade.
4. **Arrastar e soltar.** Jogue a pasta em cima da janela. Se você soltar uma subpasta,
   ele sobe sozinho até achar a raiz do repositório.

## Como ele lista as branches

A leitura vem de um único comando, com metadados junto:

```bash
git for-each-ref --sort=-committerdate \
  --format="%(refname:short)%1f%(committerdate:iso8601)%1f%(authorname)%1f%(contents:subject)" \
  refs/heads refs/remotes
```

Com isso cada branch da lista mostra, em cinza à direita, há quanto tempo teve o último
commit e quem fez. Ordenadas por recência, a que você está usando fica no topo.

### Base ou branch de trabalho

O app separa as duas coisas por padrão de nome, definido em `PadroesDeBase` no
`config.json`. Já vem com `main`, `master`, `develop`, `release/*`, `hotfix/*`, `rc*`,
`homolog`, `producao` e outros. **Edite essa lista para os nomes que o seu time usa** —
é o único ajuste que costuma ser necessário.

A separação serve para: os campos de base mostrarem só bases, o campo de correção
mostrar só branches locais de trabalho, e a detecção automática abaixo funcionar.

### Detecção da base de origem

Ao escolher a sua branch de correção, o app tenta descobrir sozinho de qual base ela
nasceu. Para cada base candidata ele roda:

```bash
git rev-list --count <base>..<minha-correcao>
```

A base com a menor contagem é a mãe. A lógica: se a sua correção saiu da
`release-candidate` com 3 commits, contra a `release-candidate` dá 3, mas contra a
`main` daria 3 mais todos os commits de integração que a `release-candidate` tem e a
`main` não. Bases que não têm ancestral comum, ou que já contêm a correção inteira,
são descartadas.

Não é infalível — se duas bases estiverem no mesmo commit, o desempate é arbitrário.
Por isso o campo continua editável e o app mostra em verde o que deduziu.

## Quando a base de destino tem outros desenvolvedores

Se a `hotfix` recebe commits de outras pessoas, ela precisa estar em dia antes de você
aplicar sua correção nela. Mas você **não precisa fazer checkout nem pull**:

```bash
git fetch origin --prune
git rebase --onto origin/hotfix $FORK minha-correcao-para-hotfix
```

`origin/hotfix` é uma referência local que o `fetch` atualiza e que aponta para o topo
real do remoto. Rebasear em cima dela é mais seguro que usar a `hotfix` local, porque a
local pode estar velha, ou pior, ter commits seus que nunca foram enviados.

O app faz esse `fetch` duas vezes: quando você gera a prévia, e de novo no instante em
que você clica em executar. Isso fecha a janela em que um colega poderia empurrar um
commit entre as duas ações.

Se preferir manter a `hotfix` local em dia também, desmarque *"Usar sempre a versão
remota"*. Aí o app adianta a branch local com `git fetch origin hotfix:hotfix`, que
atualiza sem trocar de branch e que o git recusa se não for fast-forward — ou seja, ele
nunca descarta um commit local seu sem avisar.

## Compilar e empacotar

Precisa do .NET 8 SDK. Para o instalador, também do
[Inno Setup 6](https://jrsoftware.org/isdl.php).

```powershell
.\build.ps1                 # gera bin\publish\GitPortFix.exe
.\build.ps1 -Instalador     # gera dist\GitPortFix-Setup-1.0.0.exe
```

Para publicar uma versão para o time, empurre uma tag. O GitHub Actions compila, gera o
instalador e anexa tudo a um release:

```bash
git tag v1.0.0
git push origin v1.0.0
```

O executável é publicado como arquivo único e self-contained: roda em máquina sem .NET
instalado. Fica em torno de 60 MB por causa disso. Se todo mundo no time já tem o
runtime .NET 8 Desktop, troque para `--self-contained false` no `build.ps1` e o arquivo
cai para poucos megabytes.

O instalador não pede senha de administrador (`PrivilegesRequired=lowest`), instala em
`%LocalAppData%\Programs`, e avisa na hora se o git não estiver no PATH.

## Usar

1. Abra o projeto (ou clique com o botão direito na pasta dele no Explorer).
2. Confirme os três campos. O primeiro já vem com a branch em que você está e o segundo
   é preenchido pela detecção automática.
3. **Ver o que será levado** — a lista mostra exatamente quais commits serão aplicados.
4. **Levar para a base de destino**.

Se der conflito, o app avisa quais arquivos travaram e para. Resolva na sua IDE e rode
`git rebase --continue` (ou `git cherry-pick --continue`), ou use o botão de abortar
para voltar tudo ao estado anterior.

## Limitações conhecidas

- Não resolve conflitos sozinho, e nem deveria: isso é decisão sua.
- Exige a árvore de trabalho limpa antes de executar.
- Merges são ignorados na listagem de commits (`--no-merges`). Se a sua branch de
  correção tem merges internos, prefira o modo cherry-pick.
- A detecção da base de origem faz uma chamada ao git por base candidata, limitada às 20
  mais recentes. Em repositório com centenas de branches base, pode levar um segundo.
