namespace RongtaBleSdk.EscPos;

/// <summary>
/// Constantes de comandos ESC/POS (comandos de controle para impressoras térmicas de cupom/recibo
/// compatíveis com ESC/POS, tanto genéricas chinesas quanto modelos com suporte ao padrão).
/// Todas são independentes de qualquer domínio de aplicação — são apenas bytes de protocolo.
/// </summary>
public static class EscPosCommands
{
    /// <summary>ESC @ — Inicializa a impressora (reset).</summary>
    public const string Reset = "\x1B\x40";

    /// <summary>ESC t 02 — Seleciona a Code Page 850 (Latin-1) para acentuação correta.</summary>
    public const string CodePage850 = "\x1B\x74\x02";

    /// <summary>ESC a 1 — Alinhamento centralizado.</summary>
    public const string AlignCenter = "\x1B\x61\x01";

    /// <summary>ESC a 0 — Alinhamento à esquerda.</summary>
    public const string AlignLeft = "\x1B\x61\x00";

    /// <summary>ESC a 2 — Alinhamento à direita.</summary>
    public const string AlignRight = "\x1B\x61\x02";

    /// <summary>ESC ! 0x08 — Negrito ON (seleção de modo de impressão).</summary>
    public const string BoldOn = "\x1B\x21\x08";

    /// <summary>ESC ! 0x00 — Negrito OFF (seleção de modo de impressão padrão).</summary>
    public const string BoldOff = "\x1B\x21\x00";

    /// <summary>ESC ! 0x10 — Altura dupla (double height).</summary>
    public const string DoubleHeightOn = "\x1B\x21\x10";

    /// <summary>ESC ! 0x20 — Largura dupla (double width).</summary>
    public const string DoubleWidthOn = "\x1B\x21\x20";

    /// <summary>ESC ! 0x30 — Tamanho duplo (double height + double width).</summary>
    public const string DoubleSizeOn = "\x1B\x21\x30";

    /// <summary>ESC ! 0x20 — Fonte grande (legado).</summary>
    public const string LargeText = "\x1B\x21\x20";

    /// <summary>ESC d 03 — Alimenta 3 linhas (feed).</summary>
    public const string Feed3 = "\x1B\x64\x03";

    /// <summary>ESC m — Corta o papel (partial cut).</summary>
    public const string Cut = "\x1B\x6D";

    /// <summary>Largura de linha padrão usada por muitos layouts ESC/POS (caracteres mono de 58mm).</summary>
    public const int LineWidth = 32;

    /// <summary>Largura de linha padrão para impressoras térmicas de 80mm.</summary>
    public const int LineWidth80mm = 48;
}
