using System.Text;
using FluentAssertions;
using RongtaBleSdk.EscPos;
using Xunit;

namespace RongtaBleSdk.Tests;

public class EscPosReceiptBuilderTests
{
    [Fact]
    public void Initialize_ShouldIncludeResetAndCodePage850()
    {
        // Arrange
        var builder = EscPosReceiptBuilder.Create58mm()
            .Initialize();

        // Act
        var bytes = builder.Build();

        // Assert
        // Reset: \x1B\x40, CP850: \x1B\x74\x02
        var expectedHeader = "\x1B\x40\x1B\x74\x02";
        var ascii = Encoding.ASCII.GetString(bytes);
        ascii.Should().StartWith(expectedHeader);
    }

    [Fact]
    public void AlignmentMethods_ShouldAppendExpectedEscCommands()
    {
        // Arrange & Act
        var bytes = EscPosReceiptBuilder.Create58mm()
            .AlignCenter()
            .AddLine("TITULO")
            .AlignLeft()
            .AddLine("TEXTO")
            .AlignRight()
            .AddLine("TOTAL")
            .Build();

        var text = Encoding.ASCII.GetString(bytes);

        // Assert
        text.Should().Contain(EscPosCommands.AlignCenter + "TITULO\n");
        text.Should().Contain(EscPosCommands.AlignLeft + "TEXTO\n");
        text.Should().Contain(EscPosCommands.AlignRight + "TOTAL\n");
    }

    [Fact]
    public void AddDivider_ShouldCreateExactLengthDashedLine()
    {
        // Arrange
        var builder = EscPosReceiptBuilder.Create58mm()
            .AddDivider('-', 32);

        // Act
        var bytes = builder.Build();
        var text = Encoding.ASCII.GetString(bytes);

        // Assert
        text.Should().Be(new string('-', 32) + "\n");
    }

    [Fact]
    public void AddKeyValue_ShouldPadWithSpacesCorrectly()
    {
        // Arrange
        var builder = EscPosReceiptBuilder.Create58mm()
            .AddKeyValue("DATA:", "19/09/2026", 32);

        // Act
        var bytes = builder.Build();
        var text = Encoding.ASCII.GetString(bytes);

        // Assert
        // "DATA:" (5 chars) + spaces (17) + "19/09/2026" (10 chars) = 32 chars + \n
        text.TrimEnd('\n').Length.Should().Be(32);
        text.Should().StartWith("DATA:");
        text.Should().EndWith("19/09/2026\n");
    }

    [Fact]
    public void SetBold_ShouldToggleBoldCommands()
    {
        // Arrange
        var builder = EscPosReceiptBuilder.Create58mm()
            .SetBold(true)
            .AddText("DESTAQUE")
            .SetBold(false);

        // Act
        var bytes = builder.Build();
        var text = Encoding.ASCII.GetString(bytes);

        // Assert
        text.Should().Contain(EscPosCommands.BoldOn);
        text.Should().Contain(EscPosCommands.BoldOff);
    }

    [Fact]
    public void FeedAndCut_ShouldAppendCorrectCommands()
    {
        // Arrange
        var builder = EscPosReceiptBuilder.Create58mm()
            .Feed(3)
            .Cut();

        // Act
        var bytes = builder.Build();
        var text = Encoding.ASCII.GetString(bytes);

        // Assert
        text.Should().Contain("\n\n\n");
        text.Should().EndWith(EscPosCommands.Cut);
    }
}
