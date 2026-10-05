namespace SmartSchoolTimetable.Domain.SchoolSetup;

/// <summary>How generated section labels look (spec 2.5 §3.4).</summary>
public enum SectionLabelStyle { Arabic, Numbers, Latin }

/// <summary>
/// Generates section labels in order: أ، ب، ج، د، هـ، و، ز، ح، ط، ي … then أ1، ب1 …; or 1، 2، 3 …; or A, B, C … then A1 ….
/// The n-th label (0-based) is stable, so adding sections continues the sequence after the existing ones.
/// </summary>
public static class SectionLabels
{
    private static readonly string[] Arabic =
        ["أ", "ب", "ج", "د", "هـ", "و", "ز", "ح", "ط", "ي", "ك", "ل", "م", "ن", "س", "ع", "ف", "ص", "ق", "ر", "ش", "ت", "ث", "خ", "ذ", "ض", "ظ", "غ"];

    public static string At(SectionLabelStyle style, int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        return style switch
        {
            SectionLabelStyle.Numbers => (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
            SectionLabelStyle.Latin => Cycle(index, 26, position => ((char)('A' + position)).ToString()),
            _ => Cycle(index, Arabic.Length, position => Arabic[position]),
        };
    }

    /// <summary>The first <paramref name="count"/> labels of the style.</summary>
    public static IReadOnlyList<string> First(SectionLabelStyle style, int count) =>
        Enumerable.Range(0, Math.Max(0, count)).Select(index => At(style, index)).ToArray();

    /// <summary>The next label not already used (compared after Arabic normalization by the caller's set).</summary>
    public static string Next(SectionLabelStyle style, Func<string, bool> isUsed)
    {
        ArgumentNullException.ThrowIfNull(isUsed);
        for (var index = 0; ; index++)
        {
            var label = At(style, index);
            if (!isUsed(label))
                return label;
        }
    }

    /// <summary>Position of a label in the style's sequence, or -1 when the label was typed by hand.</summary>
    public static int IndexOf(SectionLabelStyle style, string label, int searchLimit = 500)
    {
        for (var index = 0; index < searchLimit; index++)
        {
            if (string.Equals(At(style, index), label, StringComparison.Ordinal))
                return index;
        }
        return -1;
    }

    private static string Cycle(int index, int alphabet, Func<int, string> letter)
    {
        var round = index / alphabet;
        return round == 0 ? letter(index % alphabet) : $"{letter(index % alphabet)}{round}";
    }
}
