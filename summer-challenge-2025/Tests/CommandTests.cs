using FluentAssertions;
using Xunit;
using SummerChallenge2025.Bot;

namespace SummerChallenge2025.Tests;

public class CommandTests
{
    [Theory]
    [InlineData(0b0UL, new int[] { })]
    [InlineData(0b1011UL, new[] { 0, 1, 3 })]
    [InlineData(0b1000000000UL, new[] { 9 })]   // 10-ty agent
    public void EnumerateActive_ReturnsExpectedOrder(ulong mask, int[] expected)
    {
        {
            var tc = new TurnCommand(GameState.MaxAgents) { ActiveMask = mask };
            tc.EnumerateActive().Should().Equal(expected);
        }
    }
}