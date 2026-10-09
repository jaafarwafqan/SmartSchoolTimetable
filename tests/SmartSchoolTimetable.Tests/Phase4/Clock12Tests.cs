using SmartSchoolTimetable.Infrastructure.Export;

namespace SmartSchoolTimetable.Tests.Phase4;

/// <summary>Generated documents use the Iraqi 12-hour clock (R1), the same cases as frontend lib/time.test.ts.</summary>
public sealed class Clock12Tests
{
    [Theory]
    [InlineData(0, "١٢:٠٠ ص", "12:00 ص")]
    [InlineData(5, "١٢:٠٥ ص", "12:05 ص")]
    [InlineData(11 * 60 + 59, "١١:٥٩ ص", "11:59 ص")]
    [InlineData(12 * 60, "١٢:٠٠ م", "12:00 م")]
    [InlineData(12 * 60 + 30, "١٢:٣٠ م", "12:30 م")]
    [InlineData(13 * 60, "١:٠٠ م", "1:00 م")]
    [InlineData(23 * 60 + 59, "١١:٥٩ م", "11:59 م")]
    public void FormatsTheBoundaries(int minutes, string arabic, string western)
    {
        Assert.Equal(arabic, Clock12.Format(minutes, arabicIndic: true));
        Assert.Equal(western, Clock12.Format(minutes, arabicIndic: false));
    }

    [Fact]
    public void NeverShowsA24HourClock()
    {
        for (var minutes = 0; minutes < 1440; minutes++)
            Assert.DoesNotMatch(@"\b(1[3-9]|2[0-3]):\d\d\b", Clock12.Format(minutes, arabicIndic: false));
    }
}
