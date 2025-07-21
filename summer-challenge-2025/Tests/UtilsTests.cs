using FluentAssertions;
using Xunit;

namespace SummerChallenge2025.Tests;

public class BitBoardTests
{
    [Fact(DisplayName = "Set/Test pojedynczych bitów")]
    public void SetAndTestBits()
    {
        var bb = new Bot.BitBoard();
        bb.Set(0);
        bb.Set(37);
        bb.Set(199);

        bb.Test(0).Should().BeTrue();
        bb.Test(37).Should().BeTrue();
        bb.Test(199).Should().BeTrue();
        bb.Test(123).Should().BeFalse();
    }

    [Fact(DisplayName = "Clear zeruje poprawnie")]
    public void ClearBit()
    {
        var bb = new Bot.BitBoard();
        bb.Set(37);
        bb.Test(37).Should().BeTrue();

        bb.Clear(37);
        bb.Test(37).Should().BeFalse();
    }

    [Fact(DisplayName = "Losowy fuzz – write/read consistency")]
    public void FuzzRandomBits()
    {
        var rng = new Random(12345);
        var bb  = new Bot.BitBoard();
        bool[] refBits = new bool[200];
        for (int i = 0; i < 1000; ++i)
        {
            int idx = rng.Next(0, 200);
            if (rng.NextDouble() < 0.5)
            {
                bb.Set(idx);
                refBits[idx] = true;
            }
            else
            {
                bb.Clear(idx);
                refBits[idx] = false;
            }
        }

        for (int i = 0; i < 200; ++i)
            bb.Test(i).Should().Be(refBits[i], $"bit {i}");
    }
}
