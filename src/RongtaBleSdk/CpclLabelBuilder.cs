using System.Text;

namespace RongtaBleSdk;

/// <summary>
/// Monta comandos CPCL (Comtec Printer Command Language) para etiquetas, no dialeto aceito pela RPP30.
/// Coordenadas em dots (203dpi -> ~8 dots/mm).
/// </summary>
public sealed class CpclLabelBuilder
{
    const int DotsPerMm = 8; // 203 dpi

    readonly StringBuilder _body = new();
    readonly int _widthDots;
    readonly int _heightDots;
    readonly int _qty;

    CpclLabelBuilder(int widthDots, int heightDots, int qty)
    {
        _widthDots = widthDots;
        _heightDots = heightDots;
        _qty = qty;
    }

    /// <summary>Cria um builder de etiqueta a partir do tamanho em milímetros (ex.: 120x80mm).</summary>
    public static CpclLabelBuilder CreateMm(double widthMm, double heightMm, int quantity = 1)
        => new((int)(widthMm * DotsPerMm), (int)(heightMm * DotsPerMm), quantity);

    /// <summary>Cria um builder de etiqueta a partir do tamanho já em dots.</summary>
    public static CpclLabelBuilder CreateDots(int widthDots, int heightDots, int quantity = 1)
        => new(widthDots, heightDots, quantity);

    /// <summary>Adiciona uma linha de texto. Fonte 0-7 (ver tabela de fontes internas da impressora).</summary>
    public CpclLabelBuilder AddText(int x, int y, string text, int font = 4, int size = 0)
    {
        _body.Append($"TEXT {font} {size} {x} {y} {Escape(text)}\r\n");
        return this;
    }

    /// <summary>Adiciona um código de barras 1D (CODE128, EAN13, etc).</summary>
    public CpclLabelBuilder AddBarcode(int x, int y, string data, string type = "128", int height = 60,
        int narrowBar = 2, int wideBar = 2, bool printHumanReadable = true)
    {
        _body.Append($"BARCODE {type} {narrowBar} {wideBar} {height} {x} {y} {Escape(data)}\r\n");
        if (printHumanReadable)
            _body.Append($"BARCODE-TEXT 4 0 2 {x} {y + height + 4}\r\n");
        return this;
    }

    /// <summary>Adiciona um QR Code.</summary>
    public CpclLabelBuilder AddQrCode(int x, int y, string data, int cellSize = 6)
    {
        _body.Append($"B QR {x} {y} M 2 U {cellSize}\r\n");
        _body.Append($"MA,{Escape(data)}\r\n");
        _body.Append("ENDQR\r\n");
        return this;
    }

    /// <summary>Adiciona uma linha horizontal (ex.: separador).</summary>
    public CpclLabelBuilder AddLine(int x, int y, int lengthDots, int thicknessDots = 2)
    {
        _body.Append($"LINE {x} {y} {x + lengthDots} {y + thicknessDots} {thicknessDots}\r\n");
        return this;
    }

    /// <summary>Gera o comando CPCL final pronto para envio via BLE.</summary>
    public byte[] Build()
    {
        var sb = new StringBuilder();
        sb.Append($"! 0 200 200 {_heightDots} {_qty}\r\n");
        sb.Append($"PAGE-WIDTH {_widthDots}\r\n");
        sb.Append(_body);
        sb.Append("FORM\r\n");
        sb.Append("PRINT\r\n");
        return Encoding.GetEncoding("ISO-8859-1").GetBytes(sb.ToString());
    }

    static string Escape(string text) => text.Replace("\r", "").Replace("\n", " ");
}
