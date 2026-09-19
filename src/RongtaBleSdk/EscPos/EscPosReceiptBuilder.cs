using System.Text;

namespace RongtaBleSdk.EscPos;

/// <summary>
/// Text alignment options for ESC/POS receipt printers.
/// </summary>
public enum ReceiptAlignment
{
    Left,
    Center,
    Right
}

/// <summary>
/// Fluent builder for assembling ESC/POS receipts (vouchers, tickets, reports) for 58mm and 80mm thermal printers.
/// Automatically handles CP850 code page text encoding for accented characters, alignments, formatting,
/// key-value aligned columns, dividers, image dithering, feed and cutting commands.
/// </summary>
public sealed class EscPosReceiptBuilder
{
    private readonly MemoryStream _buffer = new();
    private readonly int _lineWidth;

    public EscPosReceiptBuilder(int lineWidth = EscPosCommands.LineWidth)
    {
        _lineWidth = lineWidth > 0 ? lineWidth : EscPosCommands.LineWidth;
    }

    /// <summary>
    /// Creates a receipt builder configured for 58mm thermal printers (default 32 monospace columns).
    /// </summary>
    public static EscPosReceiptBuilder Create58mm() => new(EscPosCommands.LineWidth);

    /// <summary>
    /// Creates a receipt builder configured for 80mm thermal printers (default 48 monospace columns).
    /// </summary>
    public static EscPosReceiptBuilder Create80mm() => new(EscPosCommands.LineWidth80mm);

    /// <summary>
    /// Appends ESC @ (initialize/reset) and selects Code Page 850 (Latin-1) for correct accentuation.
    /// Recommended as the very first call.
    /// </summary>
    public EscPosReceiptBuilder Initialize()
    {
        AppendAscii(EscPosCommands.Reset);
        AppendAscii(EscPosCommands.CodePage850);
        return this;
    }

    /// <summary>
    /// Changes text alignment for subsequent lines (Left, Center, Right).
    /// </summary>
    public EscPosReceiptBuilder SetAlignment(ReceiptAlignment alignment)
    {
        switch (alignment)
        {
            case ReceiptAlignment.Left:
                AppendAscii(EscPosCommands.AlignLeft);
                break;
            case ReceiptAlignment.Center:
                AppendAscii(EscPosCommands.AlignCenter);
                break;
            case ReceiptAlignment.Right:
                AppendAscii(EscPosCommands.AlignRight);
                break;
        }
        return this;
    }

    public EscPosReceiptBuilder AlignLeft() => SetAlignment(ReceiptAlignment.Left);
    public EscPosReceiptBuilder AlignCenter() => SetAlignment(ReceiptAlignment.Center);
    public EscPosReceiptBuilder AlignRight() => SetAlignment(ReceiptAlignment.Right);

    /// <summary>
    /// Toggles bold mode.
    /// </summary>
    public EscPosReceiptBuilder SetBold(bool enable = true)
    {
        AppendAscii(enable ? EscPosCommands.BoldOn : EscPosCommands.BoldOff);
        return this;
    }

    /// <summary>
    /// Toggles double-height font.
    /// </summary>
    public EscPosReceiptBuilder SetDoubleHeight(bool enable = true)
    {
        AppendAscii(enable ? EscPosCommands.DoubleHeightOn : EscPosCommands.BoldOff);
        return this;
    }

    /// <summary>
    /// Toggles double-width font.
    /// </summary>
    public EscPosReceiptBuilder SetDoubleWidth(bool enable = true)
    {
        AppendAscii(enable ? EscPosCommands.DoubleWidthOn : EscPosCommands.BoldOff);
        return this;
    }

    /// <summary>
    /// Toggles double-size font (double height + double width).
    /// </summary>
    public EscPosReceiptBuilder SetDoubleSize(bool enable = true)
    {
        AppendAscii(enable ? EscPosCommands.DoubleSizeOn : EscPosCommands.BoldOff);
        return this;
    }

    /// <summary>
    /// Appends text encoded as Code Page 850 without adding a trailing newline.
    /// </summary>
    public EscPosReceiptBuilder AddText(string text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            var bytes = EscPosImageConverter.ConvertToCP850(text);
            _buffer.Write(bytes, 0, bytes.Length);
        }
        return this;
    }

    /// <summary>
    /// Appends a line of text (or empty line) followed by LF (\n).
    /// </summary>
    public EscPosReceiptBuilder AddLine(string text = "")
    {
        AddText(text);
        _buffer.WriteByte(0x0A); // LF
        return this;
    }

    /// <summary>
    /// Appends a horizontal divider line across the page width (e.g. "--------------------------------").
    /// </summary>
    public EscPosReceiptBuilder AddDivider(char c = '-', int? length = null)
    {
        int width = length ?? _lineWidth;
        return AddLine(new string(c, width));
    }

    /// <summary>
    /// Formats a key-value pair aligned on opposite sides of the receipt line.
    /// E.g. "DATA:" on the left, and "19/09/2026" on the right.
    /// </summary>
    public EscPosReceiptBuilder AddKeyValue(string key, string value, int? totalWidth = null)
    {
        int width = totalWidth ?? _lineWidth;
        int spacesNeeded = width - key.Length - value.Length;

        if (spacesNeeded > 0)
        {
            AddLine(key + new string(' ', spacesNeeded) + value);
        }
        else
        {
            // Overflow: print key and value on separate lines or with single space
            AddLine($"{key} {value}");
        }

        return this;
    }

    /// <summary>
    /// Adds a monochrome bit-image (logo or graphic) converted via Floyd-Steinberg dithering or threshold.
    /// </summary>
    public EscPosReceiptBuilder AddImage(
        byte[] imageBytes,
        int maxWidth = 384,
        bool alignCenter = true,
        EscPosImageConverter.DitheringAlgorithm ditheringAlgorithm = EscPosImageConverter.DitheringAlgorithm.FloydSteinberg)
    {
        var imageCmds = EscPosImageConverter.ImageToEscPos(imageBytes, maxWidth, alignCenter, ditheringAlgorithm);
        if (imageCmds.Length > 0)
        {
            _buffer.Write(imageCmds, 0, imageCmds.Length);
        }
        return this;
    }

    /// <summary>
    /// Appends paper feed lines.
    /// </summary>
    public EscPosReceiptBuilder Feed(int lines = 3)
    {
        for (int i = 0; i < lines; i++)
        {
            _buffer.WriteByte(0x0A); // LF
        }
        return this;
    }

    /// <summary>
    /// Appends paper cut command.
    /// </summary>
    public EscPosReceiptBuilder Cut()
    {
        AppendAscii(EscPosCommands.Cut);
        return this;
    }

    /// <summary>
    /// Appends raw bytes directly to the command buffer.
    /// </summary>
    public EscPosReceiptBuilder AddRawBytes(byte[] bytes)
    {
        if (bytes is { Length: > 0 })
        {
            _buffer.Write(bytes, 0, bytes.Length);
        }
        return this;
    }

    /// <summary>
    /// Assembles and returns the final ESC/POS byte array ready to send to the printer.
    /// </summary>
    public byte[] Build() => _buffer.ToArray();

    private void AppendAscii(string command)
    {
        var bytes = Encoding.ASCII.GetBytes(command);
        _buffer.Write(bytes, 0, bytes.Length);
    }
}
