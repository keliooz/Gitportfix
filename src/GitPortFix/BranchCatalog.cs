using System.Globalization;
using System.Text.RegularExpressions;

namespace GitPortFix;

public sealed record BranchInfo(
    string Name,
    bool IsRemote,
    DateTimeOffset? UltimoCommit,
    string Autor,
    string Assunto)
{
    /// <summary>Nome sem o prefixo do remoto: "origin/hotfix" vira "hotfix".</summary>
    public string NomeCurto => IsRemote && Name.Contains('/') ? Name[(Name.IndexOf('/') + 1)..] : Name;

    public bool EhBase { get; init; }

    /// <summary>Texto cinza mostrado à direita na lista suspensa.</summary>
    public string Resumo
    {
        get
        {
            var partes = new List<string>();
            if (UltimoCommit is { } d) partes.Add(TempoRelativo(d));
            if (!string.IsNullOrWhiteSpace(Autor)) partes.Add(PrimeiroNome(Autor));
            return string.Join("  ·  ", partes);
        }
    }

    private static string PrimeiroNome(string autor) => autor.Split(' ')[0];

    private static string TempoRelativo(DateTimeOffset quando)
    {
        var dias = (DateTimeOffset.Now - quando).TotalDays;
        return dias switch
        {
            < 1 => "hoje",
            < 2 => "ontem",
            < 31 => $"há {(int)dias} dias",
            < 365 => $"há {(int)(dias / 30)} meses",
            _ => quando.ToString("MM/yyyy", CultureInfo.InvariantCulture)
        };
    }
}

/// <summary>
/// Lê as branches do repositório com metadados e ajuda a separar o joio do trigo:
/// quais são bases de integração e qual delas originou a branch de correção.
/// </summary>
public sealed class BranchCatalog
{
    private readonly GitRunner _git;
    private readonly AppConfig _config;

    public BranchCatalog(GitRunner git, AppConfig config)
    {
        _git = git;
        _config = config;
    }

    public IReadOnlyList<BranchInfo> Todas { get; private set; } = Array.Empty<BranchInfo>();

    /// <summary>Branches base, remotas na frente das locais, sem repetir o mesmo nome curto.</summary>
    public IEnumerable<BranchInfo> Bases =>
        Todas.Where(b => b.EhBase)
             .GroupBy(b => b.NomeCurto, StringComparer.OrdinalIgnoreCase)
             .Select(g => g.OrderByDescending(b => b.IsRemote).First())
             .OrderByDescending(b => b.UltimoCommit);

    /// <summary>Branches de trabalho locais: as candidatas naturais a "minha correção".</summary>
    public IEnumerable<BranchInfo> DeTrabalho =>
        Todas.Where(b => !b.IsRemote && !b.EhBase)
             .OrderByDescending(b => b.UltimoCommit);

    public async Task RecarregarAsync()
    {
        // %1f é um separador de byte, seguro porque nunca aparece em nome ou assunto.
        const string formato = "%(refname:short)%1f%(committerdate:iso8601)%1f%(authorname)%1f%(contents:subject)";

        var r = await _git.RunAsync(
            $"for-each-ref --sort=-committerdate --format=\"{formato}\" refs/heads refs/remotes");

        var lista = new List<BranchInfo>();
        if (!r.Ok) { Todas = lista; return; }

        foreach (var linha in r.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = linha.Split('\u001f');
            if (p.Length < 4) continue;

            var nome = p[0].Trim();
            if (nome.Length == 0 || nome.EndsWith("/HEAD", StringComparison.Ordinal)) continue;

            var remota = !nome.StartsWith("refs/", StringComparison.Ordinal)
                         && linha.Contains('/')
                         && EhReferenciaRemota(nome);

            DateTimeOffset? data = DateTimeOffset.TryParse(
                p[1].Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

            var info = new BranchInfo(nome, remota, data, p[2].Trim(), p[3].Trim());
            lista.Add(info with { EhBase = EhNomeDeBase(info.NomeCurto) });
        }

        Todas = lista;
    }

    // As remotas vêm de refs/remotes, então o primeiro segmento é o nome do remoto.
    private HashSet<string> _remotos = new(StringComparer.OrdinalIgnoreCase);

    public async Task CarregarRemotosAsync()
    {
        var r = await _git.RunAsync("remote");
        _remotos = r.Ok ? new HashSet<string>(r.Lines, StringComparer.OrdinalIgnoreCase)
                        : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private bool EhReferenciaRemota(string nome)
    {
        var primeiro = nome.Split('/')[0];
        return _remotos.Contains(primeiro);
    }

    /// <summary>Compara o nome com os padrões configurados, aceitando curinga.</summary>
    public bool EhNomeDeBase(string nomeCurto)
    {
        foreach (var padrao in _config.PadroesDeBase)
        {
            var regex = "^" + Regex.Escape(padrao).Replace("\\*", ".*") + "$";
            if (Regex.IsMatch(nomeCurto, regex, RegexOptions.IgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Descobre de qual base a branch de correção nasceu.
    /// A lógica: para cada base candidata, conta quantos commits a correção tem a mais.
    /// A base com a menor contagem é a mãe — qualquer outra base incluiria também os
    /// commits de integração que a mãe já tem.
    /// </summary>
    public async Task<(string Base, int Commits)?> DetectarBaseDeOrigemAsync(
        string branchCorrecao, CancellationToken ct = default)
    {
        var candidatas = Bases
            .Where(b => !string.Equals(b.Name, branchCorrecao, StringComparison.OrdinalIgnoreCase))
            .Take(20)
            .ToList();

        (string Base, int Commits)? melhor = null;

        foreach (var candidata in candidatas)
        {
            if (ct.IsCancellationRequested) break;

            // Sem ancestral comum, não é a base desta branch.
            var ancestral = await _git.GetMergeBaseAsync(candidata.Name, branchCorrecao);
            if (ancestral is null) continue;

            var contagem = await _git.RunAsync(
                $"rev-list --count {GitRunner.Quote(candidata.Name)}..{GitRunner.Quote(branchCorrecao)}");
            if (!contagem.Ok || !int.TryParse(contagem.StdOut.Trim(), out var n)) continue;

            // Zero commits significa que a correção já está contida na base: não é a origem.
            if (n == 0) continue;

            if (melhor is null || n < melhor.Value.Commits)
                melhor = (candidata.Name, n);
        }

        return melhor;
    }
}
