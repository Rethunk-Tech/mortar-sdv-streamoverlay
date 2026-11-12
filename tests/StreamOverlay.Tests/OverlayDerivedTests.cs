using StreamOverlay;
using Xunit;

namespace StreamOverlay.Tests;

public class OverlayDerivedTests
{
    [Theory]
    [InlineData(1, "Monday")]
    [InlineData(7, "Sunday")]
    [InlineData(8, "Monday")]
    [InlineData(28, "Sunday")]
    public void DayOfWeekName_follows_game_week(int dayOfMonth, string expected)
        => Assert.Equal(expected, OverlayDerived.DayOfWeekName(dayOfMonth));

    [Theory]
    [InlineData(1, 27)]
    [InlineData(14, 14)]
    [InlineData(28, 0)]
    public void DaysLeftInSeason_is_remainder_of_28(int dayOfMonth, int expected)
        => Assert.Equal(expected, OverlayDerived.DaysLeftInSeason(dayOfMonth));

    [Fact]
    public void Friend_picks_spouse_or_highest_hearted_npc()
    {
        (string? Spouse, (string Name, int Hearts)[] Npcs, (string Name, int Hearts)? Expected)[] cases =
        [
            (null, [], null),
            ("", [("A", 0)], null),
            (null, [("A", 0), ("B", 0)], null),
            (null, [("B", 5), ("A", 8)], ("A", 8)),
            (null, [("B", 8), ("A", 8)], ("A", 8)),
            ("B", [("A", 10), ("B", 2)], ("B", 2)),
            ("Z", [("A", 3)], ("A", 3)),
            ("A", [("A", 0)], ("A", 0)),
        ];

        foreach (var (spouse, npcs, expected) in cases)
            Assert.Equal(expected, OverlayDerived.Friend(spouse, npcs));
    }
}
