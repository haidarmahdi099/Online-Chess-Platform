using Game.Application.Validation;

namespace Game.UnitTests.Validation;

public sealed class TimeControlValidatorTests
{
    [Theory]
    [InlineData(60, 0, "bullet", true)]
    [InlineData(180, 0, "blitz", true)]
    [InlineData(600, 0, "rapid", true)]
    [InlineData(1800, 0, "classical", true)]
    [InlineData(60, 0, "BLITZ", false)]
    [InlineData(1, 0, "bullet", true)]
    [InlineData(1, 1, "bullet", true)]
    [InlineData(60, 3, "bullet", false)]
    [InlineData(60, -1, "bullet", false)]
    [InlineData(0, 0, "blitz", false)]
    [InlineData(180, 0, "bullet", false)]
    [InlineData(600, 0, "blitz", false)]
    [InlineData(1800, 0, "rapid", false)]
    [InlineData(300, 0, "unknown", false)]
    [InlineData(300, 0, " ", false)]
    public void IsValid_classifies_time_controls(int initial, int increment, string category, bool expected)
    {
        Assert.Equal(expected, TimeControlValidator.IsValid(initial, increment, category));
    }
}
