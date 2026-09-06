namespace GitPortFix;

/// <summary>
/// ComboBox de branches: aceita digitação com autocompletar e desenha, à direita de
/// cada item, quando foi o último commit e quem fez. Isso evita ter que abrir o
/// terminal só para lembrar qual das quinze branches é a atual.
/// </summary>
public sealed class BranchComboBox : ComboBox
{
    private readonly Dictionary<string, BranchInfo> _meta = new(StringComparer.Ordinal);

    public BranchComboBox()
    {
        DropDownStyle = ComboBoxStyle.DropDown;
        AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        AutoCompleteSource = AutoCompleteSource.ListItems;
        DrawMode = DrawMode.OwnerDrawFixed;
        ItemHeight = 20;
        DropDownHeight = 340;
        FlatStyle = FlatStyle.Standard;
    }

    /// <summary>Substitui a lista, preservando o que estava digitado.</summary>
    public void Carregar(IEnumerable<BranchInfo> branches)
    {
        var textoAnterior = Text;

        BeginUpdate();
        Items.Clear();
        _meta.Clear();

        foreach (var b in branches)
        {
            if (_meta.ContainsKey(b.Name)) continue;
            _meta[b.Name] = b;
            Items.Add(b.Name);
        }

        EndUpdate();
        Text = textoAnterior;
    }

    public BranchInfo? InfoSelecionada =>
        _meta.TryGetValue(Text.Trim(), out var info) ? info : null;

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0) { base.OnDrawItem(e); return; }

        e.DrawBackground();

        var nome = Items[e.Index]?.ToString() ?? string.Empty;
        var selecionado = e.State.HasFlag(DrawItemState.Selected);
        var corNome = selecionado ? SystemColors.HighlightText : SystemColors.ControlText;
        var corMeta = selecionado ? SystemColors.HighlightText : Color.Gray;

        _meta.TryGetValue(nome, out var info);
        var resumo = info?.Resumo ?? string.Empty;

        var larguraResumo = resumo.Length == 0
            ? 0
            : TextRenderer.MeasureText(e.Graphics, resumo, Font).Width + 10;

        TextRenderer.DrawText(
            e.Graphics, nome, Font,
            new Rectangle(e.Bounds.Left + 3, e.Bounds.Top, e.Bounds.Width - larguraResumo - 6, e.Bounds.Height),
            corNome,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        if (larguraResumo > 0)
            TextRenderer.DrawText(
                e.Graphics, resumo, Font,
                new Rectangle(e.Bounds.Right - larguraResumo, e.Bounds.Top, larguraResumo, e.Bounds.Height),
                corMeta,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

        e.DrawFocusRectangle();
    }
}
