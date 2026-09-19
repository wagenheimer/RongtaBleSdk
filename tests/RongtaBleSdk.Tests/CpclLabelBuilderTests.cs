using System.Text;
using FluentAssertions;
using Xunit;

namespace RongtaBleSdk.Tests;

public class CpclLabelBuilderTests
{
    [Fact]
    public void CreateMm_ShouldProduceValidCpclHeaderAndFooter()
    {
        // Arrange
        var builder = CpclLabelBuilder.CreateMm(widthMm: 40, heightMm: 30, quantity: 1, gapMm: 2);

        // Act
        var bytes = builder.Build();
        var cpcl = Encoding.ASCII.GetString(bytes);

        // Assert
        // 40mm * 8 dots/mm = 320 dots width
        // 30mm * 8 = 240 dots height, 2mm * 8 = 16 dots gap => total height = 256
        cpcl.Should().StartWith("! 0 203 203 256 1\r\n");
        cpcl.Should().Contain("PAGE-WIDTH 320\r\n");
        cpcl.Should().EndWith("FORM\r\nPRINT\r\n");
    }

    [Fact]
    public void CreateAutoHeight_WithoutGap_ShouldIncludeJournalMode()
    {
        // Arrange
        var builder = CpclLabelBuilder.CreateAutoHeightMm(widthMm: 50, quantity: 1, gapMm: 0);
        builder.AddText(0, 10, "CELMI PESAGEM");

        // Act
        var bytes = builder.Build();
        var cpcl = Encoding.ASCII.GetString(bytes);

        // Assert
        cpcl.Should().Contain("JOURNAL\r\n");
    }

    [Fact]
    public void AddText_ShouldStripDiacriticsAndFormatCommand()
    {
        // Arrange
        var builder = CpclLabelBuilder.CreateDots(widthDots: 384, heightDots: 200);
        builder.AddText(10, 20, "Coração & Acentuação");

        // Act
        var bytes = builder.Build();
        var cpcl = Encoding.ASCII.GetString(bytes);

        // Assert
        // Diacritics stripped: "Coração & Acentuação" -> "Coracao & Acentuacao"
        cpcl.Should().Contain("TEXT 4 0 10 20 Coracao & Acentuacao\r\n");
        cpcl.Should().NotContain("ã");
        cpcl.Should().NotContain("ç");
    }

    [Fact]
    public void AddBarcode_ShouldEmitBarcodeAndHumanReadableText()
    {
        // Arrange
        var builder = CpclLabelBuilder.CreateDots(widthDots: 384, heightDots: 300);
        builder.AddBarcode(20, 30, "789123456", printHumanReadable: true);

        // Act
        var bytes = builder.Build();
        var cpcl = Encoding.ASCII.GetString(bytes);

        // Assert
        cpcl.Should().Contain("BARCODE 128 2 2 60 20 30 789123456\r\n");
        cpcl.Should().Contain("BARCODE-TEXT 4 0 2 20 94\r\n");
    }

    [Fact]
    public void AddQrCode_ShouldEmitCpclQrSequence()
    {
        // Arrange
        var builder = CpclLabelBuilder.CreateDots(widthDots: 384, heightDots: 300);
        builder.AddQrCode(50, 50, "https://celmi.com.br", cellSize: 5);

        // Act
        var bytes = builder.Build();
        var cpcl = Encoding.ASCII.GetString(bytes);

        // Assert
        cpcl.Should().Contain("B QR 50 50 M 2 U 5\r\n");
        cpcl.Should().Contain("MA,https://celmi.com.br\r\n");
        cpcl.Should().Contain("ENDQR\r\n");
    }

    [Fact]
    public void AddBox_ShouldEmitBoxCoordinates()
    {
        // Arrange
        var builder = CpclLabelBuilder.CreateDots(widthDots: 384, heightDots: 300);
        builder.AddBox(10, 15, widthDots: 100, heightDots: 50, thicknessDots: 3);

        // Act
        var bytes = builder.Build();
        var cpcl = Encoding.ASCII.GetString(bytes);

        // Assert
        // BOX x y (x + width) (y + height) thickness
        cpcl.Should().Contain("BOX 10 15 110 65 3\r\n");
    }

    [Fact]
    public void AddInverse_ShouldEmitInverseLineCoordinates()
    {
        // Arrange
        var builder = CpclLabelBuilder.CreateDots(widthDots: 384, heightDots: 300);
        builder.AddInverse(10, 20, widthDots: 120, heightDots: 30);

        // Act
        var bytes = builder.Build();
        var cpcl = Encoding.ASCII.GetString(bytes);

        // Assert
        // INVERSE-LINE x y (x + width) y height
        cpcl.Should().Contain("INVERSE-LINE 10 20 130 20 30\r\n");
    }

    [Fact]
    public void AddCenterText_ShouldComputeCenteredXCoordinate()
    {
        // Arrange (width 400 dots, font 4 has estimated width 28 dots per char)
        var builder = CpclLabelBuilder.CreateDots(widthDots: 400, heightDots: 200);
        // Text "CELMI" = 5 chars * 28 = 140 dots => X = (400 - 140) / 2 = 130
        builder.AddCenterText(50, "CELMI", font: 4);

        // Act
        var bytes = builder.Build();
        var cpcl = Encoding.ASCII.GetString(bytes);

        // Assert
        cpcl.Should().Contain("TEXT 4 0 130 50 CELMI\r\n");
    }
}
