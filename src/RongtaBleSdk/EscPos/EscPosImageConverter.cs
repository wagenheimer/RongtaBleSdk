using System;
using System.IO;
using System.Text;

using SkiaSharp;

namespace RongtaBleSdk.EscPos;

/// <summary>
/// Converts images (PNG/JPEG/BMP) into ESC/POS bit-image commands for thermal receipt printers.
/// Uses the modern GS v 0 raster command and high-quality Floyd-Steinberg dithering for logos and
/// complex images. Includes optional center alignment and CP850 text encoding helpers.
/// This class is domain-independent — mirroring the ESC/POS capabilities that complement the CPCL
/// builder already shipped in this SDK.
/// </summary>
public static class EscPosImageConverter
{
    /// <summary>Dithering algorithm used to binarize the image.</summary>
    public enum DitheringAlgorithm
    {
        /// <summary>Simple luminosity threshold (fast, loses fine detail).</summary>
        Threshold,

        /// <summary>Error-diffusion dithering that approximates grayscale (better quality for logos).</summary>
        FloydSteinberg,
    }

    /// <summary>
    /// Converts image bytes into ESC/POS raster bit-image commands.
    /// </summary>
    /// <param name="imageBytes">PNG/JPEG/BMP bytes.</param>
    /// <param name="printerMaxWidth">Max print width in dots (384 for 58mm, 576 for 80mm).</param>
    /// <param name="alignCenter">Center the image horizontally.</param>
    /// <param name="ditheringAlgorithm">Binarization algorithm.</param>
    /// <returns>ESC/POS commands ready to send. Empty array on failure.</returns>
    public static byte[] ImageToEscPos(
        byte[] imageBytes,
        int printerMaxWidth = 384,
        bool alignCenter = true,
        DitheringAlgorithm ditheringAlgorithm = DitheringAlgorithm.FloydSteinberg)
    {
        if (imageBytes is null || imageBytes.Length == 0)
        {
            throw new ArgumentNullException(nameof(imageBytes), "Image bytes must not be null or empty.");
        }

        try
        {
            using var inputStream = new SKMemoryStream(imageBytes);
            using var originalBitmap = SKBitmap.Decode(inputStream);
            if (originalBitmap is null)
            {
                throw new InvalidDataException("Unable to decode image bytes. Supported formats: PNG, JPEG, BMP.");
            }

            using var resizedBitmap = ResizeImage(originalBitmap, printerMaxWidth);
            var blackAndWhitePixels = ConvertTo1bpp(resizedBitmap, ditheringAlgorithm);
            return GenerateRasterCommands(blackAndWhitePixels, alignCenter);
        }
        catch
        {
            // Return empty on any failure so callers can degrade gracefully.
            return Array.Empty<byte>();
        }
    }

    /// <summary>
    /// Converts a UTF-8 string to Code Page 850 bytes (used by ESC/POS thermal printers so accented
    /// characters print correctly). Falls back to ASCII when CP850 is unavailable.
    /// </summary>
    public static byte[] ConvertToCP850(string text)
    {
        if (string.IsNullOrEmpty(text)) return Array.Empty<byte>();

        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var cp850 = Encoding.GetEncoding(850);
            return cp850.GetBytes(text);
        }
        catch
        {
            return Encoding.ASCII.GetBytes(text);
        }
    }

    private static SKBitmap ResizeImage(SKBitmap source, int maxWidth)
    {
        var width = Math.Min(maxWidth, source.Width);
        var height = (int)(source.Height * (width / (float)source.Width));

        var resizedInfo = new SKImageInfo(width, height, SKColorType.Gray8, SKAlphaType.Opaque);
        var resized = new SKBitmap(resizedInfo);

        using var canvas = new SKCanvas(resized);
        using var paint = new SKPaint();
        var image = SKImage.FromBitmap(source);
        canvas.DrawImage(image, resized.Info.Rect, new SKSamplingOptions(SKCubicResampler.Mitchell), paint);

        return resized;
    }

    private static bool[,] ConvertTo1bpp(SKBitmap source, DitheringAlgorithm algorithm)
    {
        var width = source.Width;
        var height = source.Height;
        var pixels = new bool[width, height];
        var luminance = new float[width, height];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                luminance[x, y] = source.GetPixel(x, y).Red; // In Gray8, Red=Green=Blue
            }
        }

        if (algorithm == DitheringAlgorithm.FloydSteinberg)
        {
            ApplyFloydSteinbergDithering(luminance, width, height, pixels);
        }
        else
        {
            const byte threshold = 127;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    pixels[x, y] = luminance[x, y] < threshold;
                }
            }
        }

        return pixels;
    }

    private static void ApplyFloydSteinbergDithering(float[,] luminance, int width, int height, bool[,] outputPixels)
    {
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var oldPixel = luminance[x, y];
                var newPixel = oldPixel < 128 ? 0 : 255;
                outputPixels[x, y] = newPixel == 0;
                var quantError = oldPixel - newPixel;

                if (x + 1 < width) luminance[x + 1, y] += quantError * 7 / 16;
                if (x - 1 >= 0 && y + 1 < height) luminance[x - 1, y + 1] += quantError * 3 / 16;
                if (y + 1 < height) luminance[x, y + 1] += quantError * 5 / 16;
                if (x + 1 < width && y + 1 < height) luminance[x + 1, y + 1] += quantError * 1 / 16;
            }
        }
    }

    private static byte[] GenerateRasterCommands(bool[,] pixels, bool alignCenter)
    {
        var width = pixels.GetLength(0);
        var height = pixels.GetLength(1);

        using var stream = new MemoryStream();

        // Minimal line spacing to avoid white stripes between image slices.
        stream.Write(new byte[] { 0x1B, 0x33, 0x00 }, 0, 3);

        if (alignCenter)
        {
            stream.Write(new byte[] { 0x1B, 0x61, 0x01 }, 0, 3); // ESC a 1 (center)
        }

        // Process the image in vertical slices of up to 24 pixels using GS v 0 raster mode.
        for (var y = 0; y < height; y += 24)
        {
            var sliceHeight = Math.Min(24, height - y);
            var bytesPerSliceLine = (width + 7) / 8;

            stream.Write(new byte[] { 0x1D, 0x76, 0x30, 0x00 }, 0, 4);
            stream.WriteByte((byte)(bytesPerSliceLine % 256));
            stream.WriteByte((byte)(bytesPerSliceLine / 256));
            stream.WriteByte((byte)(sliceHeight % 256));
            stream.WriteByte((byte)(sliceHeight / 256));

            for (var x = 0; x < bytesPerSliceLine * sliceHeight; x++)
            {
                byte sliceByte = 0;
                var currentX = (x % bytesPerSliceLine) * 8;
                var currentY = y + (x / bytesPerSliceLine);

                for (var bit = 0; bit < 8; bit++)
                {
                    if (currentX + bit < width && pixels[currentX + bit, currentY])
                    {
                        sliceByte |= (byte)(0x80 >> bit);
                    }
                }
                stream.WriteByte(sliceByte);
            }
        }

        if (alignCenter)
        {
            stream.Write(new byte[] { 0x1B, 0x61, 0x00 }, 0, 3); // ESC a 0 (align left)
        }
        stream.Write(new byte[] { 0x1B, 0x32 }, 0, 2); // ESC 2 (restore default line spacing)

        return stream.ToArray();
    }
}
