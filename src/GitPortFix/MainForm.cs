namespace GitPortFix;

public sealed class MainForm : Form
{
    private readonly AppConfig _config = AppConfig.Carregar();
    private readonly GitRunner _git = new();
    private readonly BranchCatalog _catalogo;

    private List<CommitInfo> _commits = new();
    private string? _pontoDeFork;
    private string? _destinoResolvido;
    private CancellationTokenSource? _varredura;

    // Passo 1 — projeto
    private readonly ComboBox _cmbRepo = new() { DropDownStyle = ComboBoxStyle.DropDown };
    private readonly Button _btnProcurar = new() { Text = "Procurar..." };
    private readonly Button _btnAbrir = new() { Text = "Abrir" };
    private readonly Label _lblRepo = new() { Text = "Arraste a pasta do projeto aqui, escolha um recente ou clique em Procurar.", ForeColor = Color.DimGray, AutoSize = true };

    // Passo 2 — branches
    private readonly BranchComboBox _cmbCorrecao = new();
    private readonly BranchComboBox _cmbBaseOrigem = new();
    private readonly BranchComboBox _cmbBaseDestino = new();
    private readonly TextBox _txtNovaBranch = new();
    private readonly Label _lblDeteccao = new() { Text = " ", ForeColor = Color.DimGray, AutoSize = true };
    private readonly CheckBox _chkSoBases = new() { Text = "Mostrar só branches base nos campos de base", Checked = true };

    // Passo 3 — opções
    private readonly CheckBox _chkAtualizarDestino = new() { Text = "Atualizar a base de destino antes de aplicar" };
    private readonly CheckBox _chkPreferirRemoto = new() { Text = "Usar sempre a versão remota da base (origin/...)" };
    private readonly CheckBox _chkFetch = new() { Text = "Buscar do remoto ao abrir o projeto" };
    private readonly CheckBox _chkPush = new() { Text = "Enviar a nova branch para o remoto ao final" };
    private readonly RadioButton _rbTodos = new() { Text = "Levar todos os commits (rebase --onto)", Checked = true };
    private readonly RadioButton _rbSelecionados = new() { Text = "Levar só os commits marcados (cherry-pick)" };
    private readonly Label _lblEstadoDestino = new() { Text = "Base de destino: ainda não verificada.", ForeColor = Color.DimGray, MaximumSize = new Size(400, 0), AutoSize = true };

    // Ações e saída
    private readonly Button _btnConferirDestino = new() { Text = "Conferir base de destino" };
    private readonly Button _btnPrevia = new() { Text = "Ver o que será levado" };
    private readonly Button _btnExecutar = new() { Text = "Levar para a base de destino", Enabled = false };
    private readonly Button _btnAbortar = new() { Text = "Abortar operação", Enabled = false };
    private readonly CheckedListBox _lstCommits = new() { CheckOnClick = true, Font = new Font("Consolas", 9F) };
    private readonly TextBox _txtLog = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 9F), BackColor = Color.FromArgb(28, 28, 30), ForeColor = Color.Gainsboro };
    private readonly Label _lblStatus = new() { Text = "Escolha um projeto para começar.", AutoSize = true };

    public MainForm(string? caminhoInicial = null)
    {
        _catalogo = new BranchCatalog(_git, _config);

        Text = "GitPortFix — levar sua correção para outra branch base";
        Width = 1040;
        Height = 800;
        MinimumSize = new Size(900, 660);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F);
        AllowDrop = true;

        _git.OnLog += Log;

        AplicarConfig();
        MontarLayout();
        LigarEventos();

        Shown += async (_, _) => await AoAbrirAsync(caminhoInicial);
    }

    // ------------------------------------------------------------------ layout

    private void MontarLayout()
    {
        var raiz = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(14)
        };
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        raiz.Controls.Add(MontarPasso1(), 0, 0);
        raiz.Controls.Add(MontarPasso2e3(), 0, 1);
        raiz.Controls.Add(MontarAreaCentral(), 0, 2);

        _lblStatus.Padding = new Padding(2, 8, 2, 0);
        raiz.Controls.Add(_lblStatus, 0, 3);

        Controls.Add(raiz);
    }

    private GroupBox MontarPasso1()
    {
        var grupo = new GroupBox { Text = "1. Projeto", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(12, 8, 12, 10) };

        var conteudo = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, AutoSize = true };
        conteudo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        conteudo.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        conteudo.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _cmbRepo.Dock = DockStyle.Fill;
        _btnProcurar.Width = 100;
        _btnProcurar.Height = 26;
        _btnAbrir.Width = 90;
        _btnAbrir.Height = 26;

        conteudo.Controls.Add(_cmbRepo, 0, 0);
        conteudo.Controls.Add(_btnProcurar, 1, 0);
        conteudo.Controls.Add(_btnAbrir, 2, 0);
        conteudo.SetColumnSpan(_lblRepo, 3);
        conteudo.Controls.Add(_lblRepo, 0, 1);

        grupo.Controls.Add(conteudo);
        return grupo;
    }

    private TableLayoutPanel MontarPasso2e3()
    {
        var linha = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, RowCount = 1, AutoSize = true };
        linha.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        linha.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));

        // Passo 2
        var grpBranches = new GroupBox { Text = "2. Branches", Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(12, 8, 12, 10) };
        var grade = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 6, AutoSize = true };
        grade.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        grade.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AdicionarCampo(grade, 0, "Minha correção", _cmbCorrecao);
        AdicionarCampo(grade, 1, "Nasceu de", _cmbBaseOrigem);

        _lblDeteccao.Margin = new Padding(0, 0, 0, 6);
        grade.Controls.Add(_lblDeteccao, 1, 2);

        AdicionarCampo(grade, 3, "Levar para", _cmbBaseDestino);
        AdicionarCampo(grade, 4, "Nova branch", _txtNovaBranch);

        _chkSoBases.AutoSize = true;
        _chkSoBases.Margin = new Padding(0, 6, 0, 0);
        grade.Controls.Add(_chkSoBases, 1, 5);

        grpBranches.Controls.Add(grade);
        linha.Controls.Add(grpBranches, 0, 0);

        // Passo 3
        var grpOpcoes = new GroupBox { Text = "3. Como levar", Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(12, 8, 12, 10) };
        var pilha = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true };

        foreach (Control c in new Control[]
                 { _rbTodos, _rbSelecionados, _chkAtualizarDestino, _chkPreferirRemoto, _chkFetch, _chkPush })
        {
            c.AutoSize = true;
            c.Margin = new Padding(0, 4, 0, 4);
            pilha.Controls.Add(c);
        }

        _lblEstadoDestino.Margin = new Padding(0, 10, 0, 0);
        pilha.Controls.Add(_lblEstadoDestino);

        grpOpcoes.Controls.Add(pilha);
        linha.Controls.Add(grpOpcoes, 1, 0);

        return linha;
    }

    private TableLayoutPanel MontarAreaCentral()
    {
        var central = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        central.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        central.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var barra = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(0, 8, 0, 8) };
        _btnConferirDestino.Width = 190;
        _btnPrevia.Width = 180;
        _btnExecutar.Width = 230;
        _btnAbortar.Width = 150;
        _btnExecutar.BackColor = Color.FromArgb(0, 120, 60);
        _btnExecutar.ForeColor = Color.White;
        _btnExecutar.FlatStyle = FlatStyle.Flat;

        foreach (Control c in new Control[] { _btnConferirDestino, _btnPrevia, _btnExecutar, _btnAbortar })
        {
            c.Height = 32;
            c.Margin = new Padding(0, 0, 8, 0);
            barra.Controls.Add(c);
        }
        central.Controls.Add(barra, 0, 0);

        var divisor = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        divisor.HandleCreated += (_, _) => { if (divisor.Height > 200) divisor.SplitterDistance = divisor.Height / 2; };

        var grpCommits = new GroupBox { Text = "Commits da sua correção", Dock = DockStyle.Fill, Padding = new Padding(8) };
        _lstCommits.Dock = DockStyle.Fill;
        grpCommits.Controls.Add(_lstCommits);
        divisor.Panel1.Controls.Add(grpCommits);

        var grpLog = new GroupBox { Text = "Log do git", Dock = DockStyle.Fill, Padding = new Padding(8) };
        _txtLog.Dock = DockStyle.Fill;
        grpLog.Controls.Add(_txtLog);
        divisor.Panel2.Controls.Add(grpLog);

        central.Controls.Add(divisor, 0, 1);
        return central;
    }

    private static void AdicionarCampo(TableLayoutPanel grade, int linha, string rotulo, Control campo)
    {
        var lbl = new Label { Text = rotulo, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 9, 8, 0) };
        campo.Dock = DockStyle.Fill;
        campo.Margin = new Padding(0, 5, 0, 5);
        grade.Controls.Add(lbl, 0, linha);
        grade.Controls.Add(campo, 1, linha);
    }

    // ------------------------------------------------------------------ eventos

    private void LigarEventos()
    {
        DragEnter += (_, e) =>
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
                e.Effect = DragDropEffects.Copy;
        };

        DragDrop += async (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is not string[] itens || itens.Length == 0) return;

            var raiz = RepoLocator.SubirAteRaiz(itens[0]);
            if (raiz is null) { Alerta("Não encontrei um repositório git nesse caminho."); return; }

            _cmbRepo.Text = raiz;
            await AbrirRepositorioAsync();
        };

        _btnProcurar.Click += async (_, _) =>
        {
            using var dlg = new FolderBrowserDialog { Description = "Selecione a pasta do projeto" };
            if (Directory.Exists(_cmbRepo.Text)) dlg.SelectedPath = _cmbRepo.Text;
            if (dlg.ShowDialog() != DialogResult.OK) return;

            _cmbRepo.Text = RepoLocator.SubirAteRaiz(dlg.SelectedPath) ?? dlg.SelectedPath;
            await AbrirRepositorioAsync();
        };

        _btnAbrir.Click += async (_, _) => await AbrirRepositorioAsync();
        _cmbRepo.SelectionChangeCommitted += async (_, _) => await AbrirRepositorioAsync();

        _btnConferirDestino.Click += async (_, _) =>
        {
            UsarEspera(true, "Conferindo a base de destino...");
            try { await PrepararDestinoAsync(); }
            finally { UsarEspera(false); }
        };

        _btnPrevia.Click += async (_, _) => await CarregarPreviaAsync();
        _btnExecutar.Click += async (_, _) => await ExecutarAsync();
        _btnAbortar.Click += async (_, _) => await AbortarAsync();

        // Ao escolher a correção, tenta descobrir sozinho de qual base ela saiu.
        _cmbCorrecao.SelectionChangeCommitted += async (_, _) => await DetectarOrigemAsync();
        _cmbCorrecao.Leave += async (_, _) => await DetectarOrigemAsync();

        _chkSoBases.CheckedChanged += (_, _) => PreencherCombos();

        _rbSelecionados.CheckedChanged += (_, _) =>
        {
            if (!_rbSelecionados.Checked) return;
            for (int i = 0; i < _lstCommits.Items.Count; i++) _lstCommits.SetItemChecked(i, true);
        };

        _cmbCorrecao.TextChanged += (_, _) => SugerirNomeDaNovaBranch();
        _cmbBaseDestino.TextChanged += (_, _) => SugerirNomeDaNovaBranch();

        FormClosing += async (_, e) => await AoFecharAsync(e);
    }

    // Evita reentrância: sem isso, o Close() chamado lá dentro dispara o
    // FormClosing de novo e o app fica perguntando a mesma coisa em loop.
    private bool _fechamentoLiberado;

    private async Task AoFecharAsync(FormClosingEventArgs e)
    {
        if (_fechamentoLiberado) return;

        e.Cancel = true;
        _varredura?.Cancel();

        var repoValido = !string.IsNullOrWhiteSpace(_git.RepoPath) && await _git.IsRepositoryAsync();

        if (repoValido && await _git.IsOperationInProgressAsync())
        {
            var resposta = MessageBox.Show(
                "Existe um rebase ou cherry-pick em andamento neste repositório.\n\n" +
                "Fechar o GitPortFix agora vai abortar essa operação — qualquer conflito " +
                "que você já tenha resolvido na mão será descartado, e o repositório " +
                "volta ao estado de antes de você clicar em \"Levar para a base de destino\".\n\n" +
                "Abortar e fechar mesmo assim?",
                "Operação em andamento", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (resposta != DialogResult.Yes)
            {
                // Mantém a janela aberta. O usuário pode preferir resolver e
                // continuar depois, ou terminar pelo terminal antes de sair.
                return;
            }

            UsarEspera(true, "Abortando a operação pendente...");
            try
            {
                var r = await _git.RunAsync("rebase --abort");
                if (!r.Ok) await _git.RunAsync("cherry-pick --abort");
            }
            finally
            {
                UsarEspera(false);
            }
        }

        GuardarConfig();
        _fechamentoLiberado = true;
        Close();
    }

    // ------------------------------------------------------------------ projeto

    private async Task AoAbrirAsync(string? caminhoInicial)
    {
        _cmbRepo.Items.Clear();
        foreach (var r in _config.ReposRecentes.Where(Directory.Exists))
            _cmbRepo.Items.Add(r);

        // Caminho vindo da linha de comando: menu de contexto do Explorer.
        if (!string.IsNullOrWhiteSpace(caminhoInicial))
        {
            var raiz = RepoLocator.SubirAteRaiz(caminhoInicial);
            if (raiz is not null)
            {
                _cmbRepo.Text = raiz;
                await AbrirRepositorioAsync();
                return;
            }
        }

        if (_cmbRepo.Items.Count > 0)
        {
            _cmbRepo.SelectedIndex = 0;
            await AbrirRepositorioAsync();
        }

        if (_config.VarrerPastas) _ = VarrerPastasAsync();
    }

    private async Task VarrerPastasAsync()
    {
        _varredura?.Cancel();
        _varredura = new CancellationTokenSource();

        try
        {
            var achados = await RepoLocator.ProcurarAsync(3, _varredura.Token);
            if (achados.Count == 0 || IsDisposed) return;

            BeginInvoke(() =>
            {
                var existentes = _cmbRepo.Items.Cast<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var a in achados.Where(a => !existentes.Contains(a)))
                    _cmbRepo.Items.Add(a);

                Log($"— Varredura encontrou {achados.Count} repositório(s) nas pastas de desenvolvimento.");
            });
        }
        catch (OperationCanceledException) { }
    }

    private async Task AbrirRepositorioAsync()
    {
        var caminho = _cmbRepo.Text.Trim();
        if (caminho.Length == 0) return;

        _git.RepoPath = caminho;

        if (!await _git.IsRepositoryAsync())
        {
            _lblRepo.Text = "Essa pasta não é um repositório git.";
            _lblRepo.ForeColor = Color.Firebrick;
            Alerta("Essa pasta não é um repositório git. Aponte para a pasta raiz do projeto, a que contém a .git.");
            return;
        }

        UsarEspera(true, "Lendo branches...");
        try
        {
            if (_chkFetch.Checked)
                await _git.RunAsync("fetch --all --prune");

            await _catalogo.CarregarRemotosAsync();
            await _catalogo.RecarregarAsync();
            PreencherCombos();

            var atual = await _git.GetCurrentBranchAsync();
            if (!string.IsNullOrEmpty(atual) && atual != "HEAD")
                _cmbCorrecao.Text = atual;

            _cmbBaseDestino.Text = _config.UltimaBaseDestino ?? "";

            var totalBases = _catalogo.Bases.Count();
            var totalTrabalho = _catalogo.DeTrabalho.Count();

            _lblRepo.Text = $"{Path.GetFileName(caminho)}  —  branch atual: {atual}  ·  " +
                            $"{totalBases} base(s), {totalTrabalho} de trabalho";
            _lblRepo.ForeColor = Color.DimGray;

            _config.RegistrarRepo(caminho);
            if (!_cmbRepo.Items.Contains(caminho)) _cmbRepo.Items.Insert(0, caminho);

            if (await _git.IsOperationInProgressAsync())
            {
                _btnAbortar.Enabled = true;
                Alerta("Existe um rebase ou cherry-pick inacabado aqui. Resolva ou aborte antes de continuar.");
            }

            Status("Projeto aberto.");
            await DetectarOrigemAsync();
        }
        finally
        {
            UsarEspera(false);
        }
    }

    private void PreencherCombos()
    {
        // Na correção mostramos todas as locais, com as de trabalho no topo.
        var paraCorrecao = _catalogo.DeTrabalho
            .Concat(_catalogo.Todas.Where(b => !b.IsRemote && b.EhBase))
            .ToList();

        var paraBase = _chkSoBases.Checked
            ? _catalogo.Bases.ToList()
            : _catalogo.Bases.Concat(_catalogo.Todas.Where(b => !b.EhBase)).ToList();

        _cmbCorrecao.Carregar(paraCorrecao);
        _cmbBaseOrigem.Carregar(paraBase);
        _cmbBaseDestino.Carregar(paraBase);
    }

    // ------------------------------------------------------- detecção da origem

    private async Task DetectarOrigemAsync()
    {
        var correcao = _cmbCorrecao.Text.Trim();
        if (correcao.Length == 0 || string.IsNullOrWhiteSpace(_git.RepoPath)) return;

        _lblDeteccao.Text = "Procurando de qual base essa branch saiu...";
        _lblDeteccao.ForeColor = Color.DimGray;

        var achado = await _catalogo.DetectarBaseDeOrigemAsync(correcao);

        if (achado is null)
        {
            _lblDeteccao.Text = "Não consegui deduzir a base de origem. Escolha na lista acima.";
            _lblDeteccao.ForeColor = Color.DarkGoldenrod;
            if (_config.UltimaBaseOrigem is { } ultima) _cmbBaseOrigem.Text = ultima;
            return;
        }

        _cmbBaseOrigem.Text = achado.Value.Base;
        _lblDeteccao.Text = $"Detectado: {achado.Value.Base}, com {achado.Value.Commits} commit(s) seu(s) em cima.";
        _lblDeteccao.ForeColor = Color.DarkGreen;
        SugerirNomeDaNovaBranch();
    }

    // ------------------------------------------------------------ base destino

    /// <summary>
    /// Deixa a base de destino no estado mais recente do remoto e devolve a referência
    /// que o rebase deve usar. Pode devolver "origin/hotfix" no lugar de "hotfix".
    /// </summary>
    private async Task<string?> PrepararDestinoAsync()
    {
        var destino = _cmbBaseDestino.Text.Trim();
        if (destino.Length == 0) { Alerta("Escolha a base de destino primeiro."); return null; }

        var remoto = await _git.GetDefaultRemoteAsync();

        if (_chkAtualizarDestino.Checked && remoto is not null)
        {
            var fetch = await _git.RunAsync($"fetch {GitRunner.Quote(remoto)} --prune");
            if (!fetch.Ok)
            {
                var seguir = MessageBox.Show(
                    $"Não consegui buscar do remoto \"{remoto}\":\n\n{fetch.Combined}\n\n" +
                    "Continuar com o que já está na máquina? A base pode estar desatualizada.",
                    "Falha no fetch", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (seguir != DialogResult.Yes) return null;
            }
        }

        if (await _git.IsRemoteTrackingRefAsync(destino))
        {
            EstadoDestino($"Destino: {destino} — referência remota, recém-atualizada.", Color.DarkGreen);
            _destinoResolvido = destino;
            return destino;
        }

        var upstream = await _git.GetUpstreamAsync(destino);
        if (upstream is null)
        {
            EstadoDestino($"Destino: {destino} — branch local sem remoto associado.", Color.DarkGoldenrod);
            _destinoResolvido = destino;
            return destino;
        }

        var comparacao = await _git.GetAheadBehindAsync(destino, upstream);
        if (comparacao is null) { _destinoResolvido = destino; return destino; }

        var (atras, frente) = comparacao.Value;

        if (atras == 0 && frente == 0)
        {
            EstadoDestino($"Destino: {destino} — igual a {upstream}.", Color.DarkGreen);
            _destinoResolvido = destino;
            return destino;
        }

        if (_chkPreferirRemoto.Checked)
        {
            var detalhe = atras > 0 ? $"{atras} commit(s) atrás" : "divergente";
            EstadoDestino($"A local \"{destino}\" está {detalhe}. Usando {upstream}.", Color.DarkGreen);
            _destinoResolvido = upstream;
            return upstream;
        }

        if (frente > 0)
        {
            Alerta(
                $"A branch local \"{destino}\" tem {frente} commit(s) que não estão em {upstream}" +
                (atras > 0 ? $", e está {atras} commit(s) atrás dele" : "") + ".\n\n" +
                "Não vou mexer nela. Resolva isso antes, ou marque a opção de usar sempre a versão remota.");
            EstadoDestino($"Destino: {destino} — divergiu de {upstream}.", Color.Firebrick);
            _destinoResolvido = destino;
            return destino;
        }

        var branchAtual = await _git.GetCurrentBranchAsync();
        var nomeRemoto = upstream.Split('/')[0];

        var ff = branchAtual == destino
            ? await _git.RunAsync("pull --ff-only")
            : await _git.FastForwardSemCheckoutAsync(nomeRemoto, destino);

        if (!ff.Ok)
        {
            EstadoDestino($"Não deu para adiantar \"{destino}\". Usando {upstream}.", Color.DarkGoldenrod);
            _destinoResolvido = upstream;
            return upstream;
        }

        EstadoDestino($"Destino: {destino} — atualizada com {atras} commit(s) de {upstream}.", Color.DarkGreen);
        _destinoResolvido = destino;
        return destino;
    }

    // ---------------------------------------------------------------- operações

    private async Task CarregarPreviaAsync()
    {
        if (!ValidarCampos(exigirNomeDaBranch: false)) return;

        UsarEspera(true, "Calculando commits...");
        try
        {
            var destino = await PrepararDestinoAsync();
            if (destino is null) return;

            _pontoDeFork = await _git.GetMergeBaseAsync(_cmbBaseOrigem.Text.Trim(), _cmbCorrecao.Text.Trim());
            if (_pontoDeFork is null)
            {
                Alerta("Não achei um ancestral comum entre a sua correção e a base de origem. Confira os nomes.");
                return;
            }

            _commits = await _git.GetCommitsAsync(_pontoDeFork, _cmbCorrecao.Text.Trim());
            _lstCommits.Items.Clear();

            if (_commits.Count == 0)
            {
                Status("Nenhum commit exclusivo. A sua correção parece idêntica à base de origem.");
                _btnExecutar.Enabled = false;
                return;
            }

            foreach (var c in _commits) _lstCommits.Items.Add(c, isChecked: true);

            _btnExecutar.Enabled = true;
            Status($"{_commits.Count} commit(s) irão de {_cmbBaseOrigem.Text} para {destino}. Fork em {_pontoDeFork[..7]}.");
        }
        finally
        {
            UsarEspera(false);
        }
    }

    private async Task ExecutarAsync()
    {
        if (!ValidarCampos(exigirNomeDaBranch: true)) return;

        if (_commits.Count == 0 || _pontoDeFork is null)
        {
            Alerta("Gere a prévia antes de executar.");
            return;
        }

        if (await _git.HasLocalChangesAsync())
        {
            Alerta("Há alterações não commitadas. Faça commit ou stash antes de continuar.");
            return;
        }

        var correcao = _cmbCorrecao.Text.Trim();
        var nova = _txtNovaBranch.Text.Trim();

        // Busca de novo agora: entre a prévia e o clique alguém pode ter empurrado commits.
        UsarEspera(true, "Verificando se a base de destino mudou...");
        string? destino;
        try { destino = await PrepararDestinoAsync(); }
        finally { UsarEspera(false); }
        if (destino is null) return;

        var modo = _rbTodos.Checked ? "rebase --onto" : "cherry-pick";
        var confirma = MessageBox.Show(
            $"Criar a branch \"{nova}\" a partir de \"{destino}\" e aplicar nela a sua correção usando {modo}?\n\n" +
            $"A branch original \"{correcao}\" não será alterada.",
            "Confirmar", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
        if (confirma != DialogResult.OK) return;

        UsarEspera(true, "Aplicando...");
        try
        {
            GitResult resultado;

            if (_rbTodos.Checked)
            {
                var criar = await _git.RunAsync($"checkout -B {GitRunner.Quote(nova)} {GitRunner.Quote(correcao)}");
                if (!criar.Ok) { Falhou(criar); return; }

                resultado = await _git.RunAsync(
                    $"rebase --onto {GitRunner.Quote(destino)} {_pontoDeFork} {GitRunner.Quote(nova)}");
            }
            else
            {
                var marcados = _lstCommits.CheckedItems.Cast<CommitInfo>().ToList();
                if (marcados.Count == 0) { Alerta("Marque pelo menos um commit."); return; }

                var criar = await _git.RunAsync($"checkout -B {GitRunner.Quote(nova)} {GitRunner.Quote(destino)}");
                if (!criar.Ok) { Falhou(criar); return; }

                var shas = string.Join(' ', marcados.Select(c => c.Sha));
                resultado = await _git.RunAsync($"cherry-pick -x {shas}");
            }

            if (!resultado.Ok)
            {
                var conflitos = await _git.GetConflictedFilesAsync();
                _btnAbortar.Enabled = true;

                if (conflitos.Length > 0)
                {
                    Status("Conflito. Resolva os arquivos e continue pela IDE ou pelo terminal.");
                    Alerta(
                        "Deu conflito nestes arquivos:\n\n" +
                        string.Join("\n", conflitos.Take(15)) +
                        (conflitos.Length > 15 ? $"\n... e mais {conflitos.Length - 15}" : "") +
                        "\n\nResolva na sua IDE e rode:\n" +
                        (_rbTodos.Checked ? "  git rebase --continue" : "  git cherry-pick --continue") +
                        "\n\nOu clique em \"Abortar operação\" para desfazer tudo.");
                }
                else
                {
                    Falhou(resultado);
                }
                return;
            }

            _btnAbortar.Enabled = false;

            if (_chkPush.Checked && !await EnviarAsync(nova)) return;

            _config.UltimaBaseDestino = _cmbBaseDestino.Text.Trim();
            _config.UltimaBaseOrigem = _cmbBaseOrigem.Text.Trim();
            GuardarConfig();

            Status($"Pronto. A branch \"{nova}\" está com a sua correção sobre \"{destino}\".");
            MessageBox.Show(
                $"A branch \"{nova}\" foi criada a partir de \"{destino}\" com a sua correção aplicada." +
                (_chkPush.Checked ? "\n\nEnviada para o remoto." : ""),
                "Concluído", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        finally
        {
            UsarEspera(false);
        }
    }

    private async Task<bool> EnviarAsync(string nova)
    {
        var remoto = await _git.GetDefaultRemoteAsync() ?? "origin";
        var push = await _git.RunAsync($"push -u {GitRunner.Quote(remoto)} {GitRunner.Quote(nova)}");
        if (push.Ok) return true;

        // Reexecutar o app reescreve a branch, então o push deixa de ser fast-forward.
        var rejeitado = push.Combined.Contains("non-fast-forward", StringComparison.OrdinalIgnoreCase)
                     || push.Combined.Contains("rejected", StringComparison.OrdinalIgnoreCase);

        if (rejeitado && MessageBox.Show(
                $"A branch \"{nova}\" já existe em {remoto} com um histórico diferente, " +
                "provavelmente de uma execução anterior deste app.\n\n" +
                "Sobrescrever a versão remota? Só faça isso se ninguém mais estiver usando essa branch.",
                "Push rejeitado", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
        {
            push = await _git.RunAsync($"push --force-with-lease -u {GitRunner.Quote(remoto)} {GitRunner.Quote(nova)}");
        }

        if (push.Ok) return true;

        Status($"Correção aplicada em \"{nova}\", mas o push falhou. Veja o log.");
        return false;
    }

    private async Task AbortarAsync()
    {
        UsarEspera(true, "Abortando...");
        try
        {
            var r = await _git.RunAsync("rebase --abort");
            if (!r.Ok) await _git.RunAsync("cherry-pick --abort");

            _btnAbortar.Enabled = await _git.IsOperationInProgressAsync();
            Status("Operação abortada. O repositório voltou ao estado anterior.");
        }
        finally
        {
            UsarEspera(false);
        }
    }

    // -------------------------------------------------------------------- apoio

    private bool ValidarCampos(bool exigirNomeDaBranch)
    {
        if (string.IsNullOrWhiteSpace(_git.RepoPath)) { Alerta("Abra um projeto primeiro."); return false; }
        if (string.IsNullOrWhiteSpace(_cmbCorrecao.Text)) { Alerta("Informe a branch da sua correção."); return false; }
        if (string.IsNullOrWhiteSpace(_cmbBaseOrigem.Text)) { Alerta("Informe de qual base a correção nasceu."); return false; }
        if (string.IsNullOrWhiteSpace(_cmbBaseDestino.Text)) { Alerta("Informe a base de destino."); return false; }
        if (exigirNomeDaBranch && string.IsNullOrWhiteSpace(_txtNovaBranch.Text)) { Alerta("Dê um nome para a nova branch."); return false; }
        return true;
    }

    private void SugerirNomeDaNovaBranch()
    {
        if (_txtNovaBranch.Modified) return;

        var correcao = _cmbCorrecao.Text.Trim();
        var destino = _cmbBaseDestino.Text.Trim();
        if (correcao.Length == 0 || destino.Length == 0) return;

        _txtNovaBranch.Text = $"{correcao.Split('/').Last()}-para-{destino.Split('/').Last()}";
    }

    private void Log(string texto)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(() => Log(texto)); return; }
        _txtLog.AppendText(texto + Environment.NewLine);
    }

    private void Status(string texto) { _lblStatus.Text = texto; Log("— " + texto); }

    private void EstadoDestino(string texto, Color cor)
    {
        _lblEstadoDestino.Text = texto;
        _lblEstadoDestino.ForeColor = cor;
        Log("— " + texto);
    }

    private void Falhou(GitResult r)
    {
        Status("O git recusou o comando. Detalhes no log.");
        Alerta($"O git retornou erro (código {r.ExitCode}):\n\n{r.Combined}");
    }

    private static void Alerta(string mensagem) =>
        MessageBox.Show(mensagem, "GitPortFix", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    private void UsarEspera(bool ocupado, string? mensagem = null)
    {
        Cursor = ocupado ? Cursors.WaitCursor : Cursors.Default;

        _btnAbrir.Enabled = !ocupado;
        _btnProcurar.Enabled = !ocupado;
        _btnConferirDestino.Enabled = !ocupado;
        _btnPrevia.Enabled = !ocupado;
        _btnExecutar.Enabled = !ocupado && _commits.Count > 0;

        if (mensagem is not null) _lblStatus.Text = mensagem;
        Application.DoEvents();
    }

    // ------------------------------------------------------------------ config

    private void AplicarConfig()
    {
        _chkPreferirRemoto.Checked = _config.PreferirRemoto;
        _chkAtualizarDestino.Checked = _config.AtualizarDestino;
        _chkFetch.Checked = _config.FetchAoAbrir;
        _chkPush.Checked = _config.PushAoFinal;
    }

    private void GuardarConfig()
    {
        _config.PreferirRemoto = _chkPreferirRemoto.Checked;
        _config.AtualizarDestino = _chkAtualizarDestino.Checked;
        _config.FetchAoAbrir = _chkFetch.Checked;
        _config.PushAoFinal = _chkPush.Checked;
        _config.Salvar();
    }
}