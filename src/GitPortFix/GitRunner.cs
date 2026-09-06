using System.Diagnostics;
using System.Text;

namespace GitPortFix;

/// <summary>
/// Resultado de um comando git.
/// </summary>
public sealed record GitResult(string Command, int ExitCode, string StdOut, string StdErr)
{
    public bool Ok => ExitCode == 0;

    public string Combined =>
        string.Join(Environment.NewLine,
            new[] { StdOut, StdErr }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();

    public string[] Lines =>
        StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries)
              .Select(l => l.Trim())
              .Where(l => l.Length > 0)
              .ToArray();
}

/// <summary>
/// Executa o git.exe dentro de um repositório e devolve saída padrão e de erro.
/// Não usa LibGit2Sharp de propósito: "rebase --onto" e "cherry-pick" com
/// resolução de conflito funcionam muito melhor chamando o git de verdade.
/// </summary>
public sealed class GitRunner
{
    public string RepoPath { get; set; } = string.Empty;

    /// <summary>Disparado a cada comando executado, para alimentar o log da tela.</summary>
    public event Action<string>? OnLog;

    public async Task<GitResult> RunAsync(string arguments, CancellationToken ct = default)
    {
        OnLog?.Invoke($"> git {arguments}");

        var psi = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = RepoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        // Evita que o git abra caixinha de senha e trave o app.
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        // Mensagens do git em formato estável, sem tradução.
        psi.Environment["LC_ALL"] = "C";

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            var msg = $"Não foi possível executar o git: {ex.Message}. " +
                      "Verifique se o Git está instalado e no PATH do Windows.";
            OnLog?.Invoke(msg);
            return new GitResult(arguments, -1, string.Empty, msg);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        var result = new GitResult(
            arguments,
            process.ExitCode,
            stdout.ToString().TrimEnd(),
            stderr.ToString().TrimEnd());

        if (!string.IsNullOrWhiteSpace(result.Combined))
            OnLog?.Invoke(result.Combined);

        return result;
    }

    // ----- Consultas -----

    public async Task<bool> IsRepositoryAsync()
    {
        if (string.IsNullOrWhiteSpace(RepoPath) || !Directory.Exists(RepoPath))
            return false;

        var r = await RunAsync("rev-parse --is-inside-work-tree");
        return r.Ok && r.StdOut.Trim() == "true";
    }

    /// <summary>Lista branches locais e remotas, sem duplicar e sem ponteiros HEAD.</summary>
    public async Task<List<string>> GetBranchesAsync()
    {
        var locais = await RunAsync("for-each-ref --format=%(refname:short) refs/heads");
        var remotas = await RunAsync("for-each-ref --format=%(refname:short) refs/remotes");

        var lista = new List<string>();
        lista.AddRange(locais.Lines);
        lista.AddRange(remotas.Lines.Where(b => !b.EndsWith("/HEAD")));

        return lista.Distinct(StringComparer.Ordinal)
                    .OrderBy(b => b, StringComparer.OrdinalIgnoreCase)
                    .ToList();
    }

    public async Task<string> GetCurrentBranchAsync()
    {
        var r = await RunAsync("rev-parse --abbrev-ref HEAD");
        return r.Ok ? r.StdOut.Trim() : string.Empty;
    }

    /// <summary>true se há alterações não commitadas (staged ou não).</summary>
    public async Task<bool> HasLocalChangesAsync()
    {
        var r = await RunAsync("status --porcelain");
        return r.Ok && !string.IsNullOrWhiteSpace(r.StdOut);
    }

    /// <summary>Nome do remoto padrão: origin, se existir; senão o primeiro da lista.</summary>
    public async Task<string?> GetDefaultRemoteAsync()
    {
        var r = await RunAsync("remote");
        if (!r.Ok || r.Lines.Length == 0) return null;
        return r.Lines.Contains("origin") ? "origin" : r.Lines[0];
    }

    /// <summary>true se a referência é remote-tracking, tipo "origin/hotfix".</summary>
    public async Task<bool> IsRemoteTrackingRefAsync(string reference)
    {
        var r = await RunAsync($"rev-parse --verify --quiet refs/remotes/{reference}");
        return r.Ok && !string.IsNullOrWhiteSpace(r.StdOut);
    }

    /// <summary>Upstream configurado da branch local, por exemplo "origin/hotfix".</summary>
    public async Task<string?> GetUpstreamAsync(string branch)
    {
        var r = await RunAsync(
            $"rev-parse --abbrev-ref --symbolic-full-name {Quote(branch + "@{upstream}")}");
        return r.Ok && !string.IsNullOrWhiteSpace(r.StdOut) ? r.StdOut.Trim() : null;
    }

    /// <summary>
    /// Compara duas referências. Atras = commits que o remoto tem e o local não.
    /// Frente = commits que o local tem e o remoto não.
    /// </summary>
    public async Task<(int Atras, int Frente)?> GetAheadBehindAsync(string local, string remoto)
    {
        var r = await RunAsync($"rev-list --left-right --count {Quote(remoto)}...{Quote(local)}");
        if (!r.Ok) return null;

        var p = r.StdOut.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        return p.Length == 2 && int.TryParse(p[0], out var atras) && int.TryParse(p[1], out var frente)
            ? (atras, frente)
            : null;
    }

    /// <summary>
    /// Adianta uma branch local até o remoto sem precisar de checkout.
    /// O git só aceita se for fast-forward, o que é exatamente a proteção que queremos.
    /// </summary>
    public Task<GitResult> FastForwardSemCheckoutAsync(string remote, string branch) =>
        RunAsync($"fetch {Quote(remote)} {Quote(branch)}:{Quote(branch)}");

    /// <summary>Commit onde a branch se separou da base — o ponto de fork.</summary>
    public async Task<string?> GetMergeBaseAsync(string baseRef, string branchRef)
    {
        var r = await RunAsync($"merge-base {Quote(baseRef)} {Quote(branchRef)}");
        return r.Ok ? r.StdOut.Trim() : null;
    }

    /// <summary>Commits exclusivos da branch em relação ao ponto de fork, do mais antigo ao mais novo.</summary>
    public async Task<List<CommitInfo>> GetCommitsAsync(string fromRef, string toRef)
    {
        var r = await RunAsync(
            $"log --no-merges --reverse --pretty=format:%H%x1f%h%x1f%an%x1f%ad%x1f%s --date=short {Quote(fromRef)}..{Quote(toRef)}");

        var commits = new List<CommitInfo>();
        if (!r.Ok) return commits;

        foreach (var linha in r.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = linha.Split('\u001f');
            if (p.Length == 5)
                commits.Add(new CommitInfo(p[0].Trim(), p[1].Trim(), p[2].Trim(), p[3].Trim(), p[4].Trim()));
        }
        return commits;
    }

    /// <summary>true se um rebase ou cherry-pick está no meio do caminho.</summary>
    public async Task<bool> IsOperationInProgressAsync()
    {
        var gitDir = await RunAsync("rev-parse --absolute-git-dir");
        if (!gitDir.Ok) return false;

        var raiz = gitDir.StdOut.Trim();
        return Directory.Exists(Path.Combine(raiz, "rebase-merge"))
            || Directory.Exists(Path.Combine(raiz, "rebase-apply"))
            || File.Exists(Path.Combine(raiz, "CHERRY_PICK_HEAD"));
    }

    /// <summary>Arquivos em conflito no momento.</summary>
    public async Task<string[]> GetConflictedFilesAsync()
    {
        var r = await RunAsync("diff --name-only --diff-filter=U");
        return r.Ok ? r.Lines : Array.Empty<string>();
    }

    public static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";
}

public sealed record CommitInfo(string Sha, string ShortSha, string Author, string Date, string Subject)
{
    public override string ToString() => $"{ShortSha}  {Date}  {Subject}   ({Author})";
}
