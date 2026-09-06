namespace GitPortFix;

internal static class Program
{
    /// <summary>
    /// Aceita um caminho por linha de comando. É assim que o menu de contexto do
    /// Explorer entrega a pasta do projeto: GitPortFix.exe "C:\dev\meu-projeto".
    /// </summary>
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var caminhoInicial = args.FirstOrDefault(a => !a.StartsWith('-'));
        Application.Run(new MainForm(caminhoInicial));
    }
}
