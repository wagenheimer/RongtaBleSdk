using System.Text;
using SkiaSharp;

namespace RongtaBleSdk;

/// <summary>
/// Converte imagens (PNG/JPG/etc.) para o formato monocromático usado pelo comando CPCL <c>EG</c>,
/// permitindo imprimir logos, gráficos ou qualquer bitmap na etiqueta.
/// </summary>
public static class CpclImageConverter
{
    /// <summary>
    /// Converte uma imagem para o comando CPCL EG, redimensionando-a para caber nos limites informados.
    /// </summary>
    /// <returns>O comando CPCL pronto (terminado em CRLF) e a altura final em dots ocupada pela imagem.</returns>
    public static (string Command, int HeightDots) ConvertToEgCommand(
        byte[] imageBytes, int x, int y, int maxWidthDots, int? maxHeightDots = null, int blackThreshold = 215)
    {
        using var bitmap = SKBitmap.Decode(imageBytes);
        if (bitmap is null)
            return (string.Empty, 0);

        var (targetWidth, targetHeight) = CalculateTargetSize(bitmap.Width, bitmap.Height, maxWidthDots, maxHeightDots);

        var info = new SKImageInfo(targetWidth, targetHeight);
        using var resized = new SKBitmap(info);
        bitmap.ScalePixels(resized, new SKSamplingOptions(SKCubicResampler.Mitchell));

        var bytesPerRow = (targetWidth + 7) / 8;
        var data = new byte[bytesPerRow * targetHeight];
        var pixels = resized.Pixels;

        for (var row = 0; row < targetHeight; row++)
        {
            var rowOffset = row * targetWidth;
            var dataRowOffset = row * bytesPerRow;

            for (var col = 0; col < targetWidth; col++)
            {
                var pixel = pixels[rowOffset + col];
                var gray = (pixel.Red * 77 + pixel.Green * 150 + pixel.Blue * 29) >> 8;

                if (pixel.Alpha > 128 && gray < blackThreshold)
                {
                    var byteIndex = dataRowOffset + (col >> 3);
                    var bitIndex = 7 - (col & 7);
                    data[byteIndex] |= (byte)(1 << bitIndex);
                }
            }
        }

        var sb = new StringBuilder();
        sb.Append($"EG {bytesPerRow} {targetHeight} {x} {y} ");
        foreach (var b in data)
            sb.Append(b.ToString("X2"));
        sb.Append("\r\n");

        return (sb.ToString(), targetHeight);
    }

    static (int Width, int Height) CalculateTargetSize(int sourceWidth, int sourceHeight, int maxWidth, int? maxHeight)
    {
        var scale = (float)maxWidth / sourceWidth;
        var targetWidth = maxWidth;
        var targetHeight = (int)(sourceHeight * scale);

        if (maxHeight.HasValue && targetHeight > maxHeight.Value && maxHeight.Value > 10)
        {
            scale = (float)maxHeight.Value / sourceHeight;
            targetHeight = maxHeight.Value;
            targetWidth = (int)(sourceWidth * scale);

            if (targetWidth > maxWidth)
            {
                targetWidth = maxWidth;
                scale = (float)maxWidth / sourceWidth;
                targetHeight = (int)(sourceHeight * scale);
            }
        }

        return (Math.Max(1, targetWidth), Math.Max(1, targetHeight));
    }
}
