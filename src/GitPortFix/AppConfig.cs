using System.Text.Json;
using System.Text.Json.Serialization;

namespace GitPortFix;

/// <summary>
/// Preferências guardadas em %AppData%\GitPortFix\config.json.
/// Os padrões de branch base são editáveis porque cada empresa nomeia do seu jeito.
/// </summary>
public sealed class AppConfig
{
    public List<string> ReposRecentes { get; set; } = new();

    /// <summary>Nomes que identificam uma branch base. Aceita curinga com *.</summary>
    public List<string> PadroesDeBase { get; set; } = new()
    {
        "main", "master", "develop", "dev", "trunk",
        "release", "release/*", "releases/*", "release-*",
        "rc", "rc/*", "release-candidate*",
        "hotfix", "hotfix/*", "hotfixes/*",
        "homolog", "homologacao", "staging", "stage",
        "producao", "production", "prod",
        "support/*"
    };

    public string? UltimaBaseDestino { get; set; }
    public string? UltimaBaseOrigem { get; set; }
    public bool PreferirRemoto { get; set; } = true;
    public bool AtualizarDestino { get; set; } = true;
    public bool FetchAoAbrir { get; set; } = true;
    public bool PushAoFinal { get; set; }
    public bool VarrerPastas { get; set; } = true;

    [JsonIgnore]
    public static string Caminho { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GitPortFix", "config.json");

    private static readonly JsonSerializerOptions Opcoes = new() { WriteIndented = true };

    public static AppConfig Carregar()
    {
        try
        {
            if (File.Exists(Caminho))
                return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(Caminho)) ?? new AppConfig();
        }
        catch
        {
            // Config corrompido não pode impedir o app de abrir.
        }
        return new AppConfig();
    }

    public void Salvar()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Caminho)!);
            File.WriteAllText(Caminho, JsonSerializer.Serialize(this, Opcoes));
        }
        catch
        {
            // Idem: falha ao salvar preferência não derruba nada.
        }
    }

    public void RegistrarRepo(string caminho)
    {
        if (string.IsNullOrWhiteSpace(caminho)) return;

        ReposRecentes.RemoveAll(r => string.Equals(r, caminho, StringComparison.OrdinalIgnoreCase));
        ReposRecentes.Insert(0, caminho);

        if (ReposRecentes.Count > 12)
            ReposRecentes.RemoveRange(12, ReposRecentes.Count - 12);
    }
}
