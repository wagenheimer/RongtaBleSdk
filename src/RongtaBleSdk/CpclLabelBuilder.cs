using System.Globalization;
using System.Text;

namespace RongtaBleSdk;

/// <summary>
/// Monta comandos CPCL (Comtec Printer Command Language) para etiquetas, no dialeto aceito pela RPP30.
/// Coordenadas em dots (203dpi -> ~8 dots/mm). Calcula a altura da etiqueta automaticamente a partir
/// do conteúdo quando não informada explicitamente, remove acentos (CPCL costuma ser ASCII-only) e
/// normaliza comandos abreviados.
/// </summary>
public sealed class CpclLabelBuilder
{
    const int DotsPerMm = 8; // 203 dpi
    const int MinAutoHeightDots = 100;
    const int AutoHeightMargin = 40;

    static readonly Dictionary<int, int> FontHeights = new()
    {
        [0] = 24,
        [1] = 16,
        [2] = 32,
        [3] = 48,
        [4] = 60,
        [5] = 24,
        [7] = 24,
    };

    readonly StringBuilder _body = new();
    readonly int _widthDots;
    readonly int? _heightDots;
    readonly int _gapDots;
    readonly int _qty;
    int _maxYReached;

    CpclLabelBuilder(int widthDots, int? heightDots, int gapDots, int qty)
    {
        _widthDots = widthDots;
        _heightDots = heightDots;
        _gapDots = gapDots;
        _qty = qty;
    }

    /// <summary>Cria um builder de etiqueta com altura fixa, a partir do tamanho em milímetros.</summary>
    public static CpclLabelBuilder CreateMm(double widthMm, double heightMm, int quantity = 1, double gapMm = 0)
        => new((int)(widthMm * DotsPerMm), (int)(heightMm * DotsPerMm), (int)(gapMm * DotsPerMm), quantity);

    /// <summary>
    /// Cria um builder de etiqueta com altura calculada automaticamente a partir do conteúdo adicionado
    /// (útil quando o comprimento da etiqueta varia conforme os dados, ex.: papel contínuo/plástico).
    /// </summary>
    public static CpclLabelBuilder CreateAutoHeightMm(double widthMm, int quantity = 1, double gapMm = 0)
        => new((int)(widthMm * DotsPerMm), null, (int)(gapMm * DotsPerMm), quantity);

    /// <summary>Cria um builder de etiqueta a partir do tamanho já em dots.</summary>
    public static CpclLabelBuilder CreateDots(int widthDots, int heightDots, int quantity = 1, int gapDots = 0)
        => new(widthDots, heightDots, gapDots, quantity);

    /// <summary>Como <see cref="CreateAutoHeightMm"/>, mas com a largura já em dots.</summary>
    public static CpclLabelBuilder CreateAutoHeightDots(int widthDots, int quantity = 1, int gapDots = 0)
        => new(widthDots, null, gapDots, quantity);

    /// <summary>Adiciona uma linha de texto. Fonte 0-7 (ver tabela de fontes internas da impressora).</summary>
    public CpclLabelBuilder AddText(int x, int y, string text, int font = 4, int size = 0)
    {
        _body.Append($"TEXT {font} {size} {x} {y} {Escape(text)}\r\n");
        TrackHeight(y, GetFontHeight(font));
        return this;
    }

    /// <summary>Adiciona um código de barras 1D (CODE128, EAN13, etc).</summary>
    public CpclLabelBuilder AddBarcode(int x, int y, string data, string type = "128", int height = 60,
        int narrowBar = 2, int wideBar = 2, bool printHumanReadable = true)
    {
        _body.Append($"BARCODE {type} {narrowBar} {wideBar} {height} {x} {y} {Escape(data)}\r\n");
        var bottom = y + height;
        if (printHumanReadable)
        {
            _body.Append($"BARCODE-TEXT 4 0 2 {x} {bottom + 4}\r\n");
            bottom += 4 + GetFontHeight(4);
        }
        TrackHeight(bottom, 0);
        return this;
    }

    /// <summary>Adiciona um QR Code.</summary>
    public CpclLabelBuilder AddQrCode(int x, int y, string data, int cellSize = 6, int estimatedModules = 25)
    {
        _body.Append($"B QR {x} {y} M 2 U {cellSize}\r\n");
        _body.Append($"MA,{Escape(data)}\r\n");
        _body.Append("ENDQR\r\n");
        TrackHeight(y, cellSize * estimatedModules);
        return this;
    }

    /// <summary>Adiciona uma linha horizontal (ex.: separador).</summary>
    public CpclLabelBuilder AddLine(int x, int y, int lengthDots, int thicknessDots = 2)
    {
        _body.Append($"LINE {x} {y} {x + lengthDots} {y + thicknessDots} {thicknessDots}\r\n");
        TrackHeight(y, thicknessDots);
        return this;
    }

    /// <summary>
    /// Adiciona uma imagem (logo, gráfico) convertida para o formato monocromático CPCL (comando EG).
    /// </summary>
    /// <param name="x">Posição X em dots.</param>
    /// <param name="y">Posição Y em dots.</param>
    /// <param name="imageBytes">Bytes da imagem (PNG/JPG/etc — qualquer formato suportado pelo SkiaSharp).</param>
    /// <param name="maxWidthDots">Largura máxima em dots (a imagem é redimensionada mantendo proporção). Padrão: largura da etiqueta.</param>
    /// <param name="maxHeightDots">Altura máxima em dots, se quiser limitar (ex.: logos no topo da etiqueta).</param>
    public CpclLabelBuilder AddImage(int x, int y, byte[] imageBytes, int? maxWidthDots = null, int? maxHeightDots = null)
    {
        var (command, height) = CpclImageConverter.ConvertToEgCommand(imageBytes, x, y, maxWidthDots ?? _widthDots, maxHeightDots);
        if (!string.IsNullOrEmpty(command))
        {
            _body.Append(command);
            TrackHeight(y, height);
        }
        return this;
    }

    /// <summary>
    /// Anexa um bloco de comandos CPCL cru (uma ou mais linhas), para casos não cobertos pela API
    /// fluente — ex.: conteúdo já montado por um formatador próprio. Faz um parsing best-effort das
    /// linhas TEXT/T e LINE/L para manter o cálculo automático de altura funcionando mesmo aqui.
    /// </summary>
    public CpclLabelBuilder AddRawCommand(string cpclBlock)
    {
        foreach (var rawLine in cpclBlock.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            var line = rawLine.TrimEnd();
            if (line.Length == 0)
                continue;

            _body.Append(line).Append("\r\n");
            TrackHeightFromRawLine(line);
        }
        return this;
    }

    void TrackHeightFromRawLine(string line)
    {
        var parts = line.TrimStart().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return;

        var cmd = parts[0].ToUpperInvariant();

        // T/TEXT font size x y ...  -> Y no índice 4
        if ((cmd == "T" || cmd == "TEXT") && parts.Length >= 5 && int.TryParse(parts[4], out var textY))
        {
            var fontHeight = int.TryParse(parts[1], out var font) ? GetFontHeight(font) : 24;
            TrackHeight(textY, fontHeight);
        }
        // L/LINE x1 y1 x2 y2 ... -> Y no índice 2
        else if ((cmd == "L" || cmd == "LINE") && parts.Length >= 3 && int.TryParse(parts[2], out var lineY))
        {
            TrackHeight(lineY, 0);
        }
        // EG bytesPerRow height x y ... -> altura no índice 2, Y no índice 4
        else if (cmd == "EG" && parts.Length >= 5 &&
                 int.TryParse(parts[2], out var imgHeight) && int.TryParse(parts[4], out var imgY))
        {
            TrackHeight(imgY, imgHeight);
        }
    }

    /// <summary>Gera o comando CPCL final pronto para envio via BLE.</summary>
    public byte[] Build()
    {
        var height = _heightDots ?? Math.Max(MinAutoHeightDots, _maxYReached + AutoHeightMargin);
        var totalHeight = height + _gapDots;

        var sb = new StringBuilder();
        sb.Append($"! 0 203 203 {totalHeight} {_qty}\r\n");
        sb.Append($"PAGE-WIDTH {_widthDots}\r\n");

        // Sem gap definido = modo contínuo/plástico: desabilita o sensor de gap (evita form-feed excessivo).
        if (_gapDots <= 0)
            sb.Append("JOURNAL\r\n");

        sb.Append("TONE 0\r\n");
        sb.Append("SETMAG 0 0\r\n");

        sb.Append(NormalizeAndStripDiacritics(_body.ToString()));
        sb.Append("FORM\r\n");
        sb.Append("PRINT\r\n");

        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    void TrackHeight(int y, int elementHeight) => _maxYReached = Math.Max(_maxYReached, y + elementHeight);

    static int GetFontHeight(int font) => FontHeights.GetValueOrDefault(font, 24);

    static string Escape(string text) => text.Replace("\r", "").Replace("\n", " ");

    /// <summary>
    /// Normaliza EOL para CRLF e remove diacríticos (á, ç, ã, etc.) — CPCL costuma ser ASCII-only
    /// e etiquetas com acentuação viram lixo em boa parte das impressoras chinesas.
    /// </summary>
    static string NormalizeAndStripDiacritics(string body)
    {
        var normalizedEol = body.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
        if (!normalizedEol.EndsWith("\r\n"))
            normalizedEol += "\r\n";

        return RemoveDiacritics(normalizedEol);
    }

    static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
