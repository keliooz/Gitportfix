namespace GitPortFix;

/// <summary>
/// Descobre repositórios git na máquina, varrendo as pastas onde projetos costumam ficar.
/// A varredura é rasa de propósito: repositório aninhado a cinco níveis é raro e a busca
/// profunda deixaria a abertura do app lenta.
/// </summary>
public static class RepoLocator
{
    private static readonly string[] PastasIgnoradas =
        { "node_modules", "bin", "obj", "packages", ".vs", ".git", "dist", "build", "target", "vendor" };

    /// <summary>Pastas onde vale a pena procurar, em ordem de probabilidade.</summary>
    public static IEnumerable<string> RaizesProvaveis()
    {
        var perfil = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var documentos = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        var candidatas = new[]
        {
            Path.Combine(perfil, "source", "repos"),   // padrão do Visual Studio
            Path.Combine(perfil, "Projects"),
            Path.Combine(perfil, "projetos"),
            Path.Combine(perfil, "dev"),
            Path.Combine(perfil, "git"),
            Path.Combine(perfil, "repos"),
            Path.Combine(documentos, "GitHub"),
            @"C:\dev", @"C:\src", @"C:\Projects", @"C:\projetos", @"C:\git", @"C:\repos",
            @"D:\dev", @"D:\src", @"D:\Projects", @"D:\projetos", @"D:\git", @"D:\repos"
        };

        return candidatas.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Procura repositórios nas raízes prováveis. Nunca lança exceção.</summary>
    public static Task<List<string>> ProcurarAsync(int profundidade = 3, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            var achados = new List<string>();

            foreach (var raiz in RaizesProvaveis())
            {
                if (ct.IsCancellationRequested) break;
                Varrer(raiz, profundidade, achados, ct);
            }

            return achados
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }, ct);

    private static void Varrer(string pasta, int restante, List<string> achados, CancellationToken ct)
    {
        if (restante < 0 || ct.IsCancellationRequested) return;

        try
        {
            // Achou .git: é repositório, e não faz sentido descer mais aqui dentro.
            if (Directory.Exists(Path.Combine(pasta, ".git")) || File.Exists(Path.Combine(pasta, ".git")))
            {
                achados.Add(pasta);
                return;
            }

            foreach (var sub in Directory.EnumerateDirectories(pasta))
            {
                var nome = Path.GetFileName(sub);
                if (nome.StartsWith('.') || PastasIgnoradas.Contains(nome, StringComparer.OrdinalIgnoreCase))
                    continue;

                Varrer(sub, restante - 1, achados, ct);
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
    }

    /// <summary>
    /// Sobe a partir de um caminho até achar a raiz do repositório.
    /// Assim funciona mesmo se o usuário apontar para uma subpasta do projeto.
    /// </summary>
    public static string? SubirAteRaiz(string caminho)
    {
        try
        {
            var dir = File.Exists(caminho) ? Path.GetDirectoryName(caminho) : caminho;

            while (!string.IsNullOrEmpty(dir))
            {
                if (Directory.Exists(Path.Combine(dir, ".git")) || File.Exists(Path.Combine(dir, ".git")))
                    return dir;

                dir = Path.GetDirectoryName(dir);
            }
        }
        catch { }

        return null;
    }
}
