using SmartSchoolTimetable.Api;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.SchoolSetup;
using SmartSchoolTimetable.Domain.Common;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Text;

namespace SmartSchoolTimetable.Tests.Phase2;

public sealed class SchoolSetupDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly YearStart = new(2026, 9, 1);
    private static readonly DateOnly YearEnd = new(2027, 6, 30);

    [Theory]
    [InlineData("  الأول   المتوسط ", "الاول المتوسط")]
    [InlineData("إعدادية", "اعدادية")]
    [InlineData("آمنة", "امنة")]
    [InlineData("مُصْطَفَى", "مصطفي")]
    [InlineData("الريـــاضيات", "الرياضيات")]
    [InlineData("٢٠٢٦-٢٠٢٧", "2026-2027")]
    [InlineData("Physics A", "physics a")]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    public void ArabicNormalizationFoldsSpellingVariantsForComparison(string? input, string expected) =>
        Assert.Equal(expected, ArabicText.Normalize(input));

    [Fact]
    public void ArabicCleanKeepsSpellingButCollapsesSpaces() =>
        Assert.Equal("الأول المتوسط", ArabicText.Clean("  الأول \t المتوسط "));

    [Fact]
    public void SchoolProfileValidatesEveryFieldAndTouchesVersion()
    {
        var profile = SchoolProfile.CreateDefault(Now);
        Assert.False(profile.IsFilled);
        var exception = Assert.Throws<DomainValidationException>(() => profile.Update(
            " ", (SchoolType)99, StudyType.Morning, new string('x', 151), null, "Mars/Base",
            NumeralSystem.Western, CalendarDisplay.Hijri, Now));
        Assert.Contains(exception.Errors, error => error is { Field: "Name", Code: DomainErrorCode.Required });
        Assert.Contains(exception.Errors, error => error is { Field: "SchoolType", Code: DomainErrorCode.InvalidOption });
        Assert.Contains(exception.Errors, error => error is { Field: "PrincipalName", Code: DomainErrorCode.TooLong });
        Assert.Contains(exception.Errors, error => error is { Field: "TimeZone", Code: DomainErrorCode.InvalidOption });
        Assert.Equal(1, profile.Version);

        profile.Update(" إعدادية  النور ", SchoolType.Preparatory, StudyType.Dual, " ", "علي", "Asia/Baghdad",
            NumeralSystem.Western, CalendarDisplay.Hijri, Now);
        Assert.Equal("إعدادية النور", profile.Name);
        Assert.Null(profile.PrincipalName);
        Assert.True(profile.IsFilled);
        Assert.Equal(2, profile.Version);

        var previous = profile.SetAsset(SchoolAssetKind.Logo, new SchoolAsset("logo-a.png", "image/png", 10, Now), Now);
        Assert.Null(previous);
        Assert.Equal("logo-a.png", profile.SetAsset(SchoolAssetKind.Logo, null, Now));
        Assert.Null(profile.GetAsset(SchoolAssetKind.Stamp));
    }

    [Fact]
    public void AcademicYearRejectsInvalidRangesAndLabels()
    {
        var invalid = Assert.Throws<DomainValidationException>(() => AcademicYear.Create("", YearEnd, YearStart));
        Assert.Contains(invalid.Errors, error => error is { Field: "Label", Code: DomainErrorCode.Required });
        Assert.Contains(invalid.Errors, error => error is { Field: "EndDate", Code: DomainErrorCode.InvalidDateRange });

        var tooLong = Assert.Throws<DomainValidationException>(() =>
            AcademicYear.Create("2026", YearStart, YearStart.AddDays(AcademicYear.MaxLengthInDays + 1)));
        Assert.Contains(tooLong.Errors, error => error.Code == DomainErrorCode.OutOfRange);
    }

    [Fact]
    public void TermsMustLieInsideTheYearNotOverlapAndHaveUniqueNames()
    {
        var year = AcademicYear.Create("2026-2027", YearStart, YearEnd);
        year.AddTerm("الفصل الأول", new DateOnly(2026, 9, 1), new DateOnly(2027, 1, 15));

        Assert.Contains(Assert.Throws<DomainValidationException>(() =>
                year.AddTerm("الفصل الثاني", new DateOnly(2027, 1, 10), new DateOnly(2027, 6, 1))).Errors,
            error => error.Code == DomainErrorCode.TermsOverlap);
        Assert.Contains(Assert.Throws<DomainValidationException>(() =>
                year.AddTerm("الفصل الثاني", new DateOnly(2027, 2, 1), new DateOnly(2027, 7, 15))).Errors,
            error => error is { Field: "EndDate", Code: DomainErrorCode.TermOutsideYear });
        Assert.Contains(Assert.Throws<DomainValidationException>(() =>
                year.AddTerm("الفصل الاول", new DateOnly(2027, 2, 1), new DateOnly(2027, 6, 1))).Errors,
            error => error is { Field: "Name", Code: DomainErrorCode.Duplicate });
        Assert.Contains(Assert.Throws<DomainValidationException>(() =>
                year.AddTerm("الفصل الثالث", new DateOnly(2027, 3, 1), new DateOnly(2027, 2, 1))).Errors,
            error => error.Code == DomainErrorCode.InvalidDateRange);

        year.AddTerm("الفصل الثاني", new DateOnly(2027, 2, 1), new DateOnly(2027, 6, 1));
        Assert.Equal(2, year.Terms.Count);

        // Shrinking the year must not leave a term outside it.
        Assert.Contains(Assert.Throws<DomainValidationException>(() =>
                year.Update("2026-2027", YearStart, new DateOnly(2027, 5, 1))).Errors,
            error => error is { Field: "EndDate", Code: DomainErrorCode.TermOutsideYear });
    }

    [Fact]
    public void CurrentTermIsClearedWhenItsTermIsRemoved()
    {
        var year = AcademicYear.Create("2026-2027", YearStart, YearEnd);
        var term = year.AddTerm("الأول", YearStart, new DateOnly(2027, 1, 1));
        var version = year.Version;
        year.SetCurrentTerm(term.Id);
        Assert.Same(term, year.CurrentTerm);
        Assert.True(year.Version > version);

        year.UpdateTerm(term.Id, "الأول المعدل", YearStart, new DateOnly(2027, 1, 2));
        Assert.Equal("الأول المعدل", year.Terms[0].Name);
        year.RemoveTerm(term.Id);
        Assert.Null(year.CurrentTermId);
        Assert.Throws<KeyNotFoundException>(() => year.SetCurrentTerm(term.Id));

        year.MarkCurrent(true);
        var afterCurrent = year.Version;
        year.MarkCurrent(true);
        Assert.Equal(afterCurrent, year.Version);
    }

    [Fact]
    public void ImageSignatureAcceptsOnlyPngJpegAndWebP()
    {
        Assert.Equal(ImageSignature.Png, ImageSignature.Detect(TestImages.Png));
        Assert.Equal(ImageSignature.Jpeg, ImageSignature.Detect(TestImages.Jpeg));
        Assert.Equal(ImageSignature.WebP, ImageSignature.Detect(TestImages.WebP));
        Assert.Null(ImageSignature.Detect(TestImages.Svg));
        Assert.Null(ImageSignature.Detect(TestImages.Gif));
        Assert.Null(ImageSignature.Detect("<html><script>alert(1)</script>"u8));
        Assert.Null(ImageSignature.Detect([]));

        Assert.True(ImageSignature.MatchesDeclared(ImageSignature.Png, "image/png"));
        Assert.True(ImageSignature.MatchesDeclared(ImageSignature.Jpeg, "image/jpg"));
        Assert.True(ImageSignature.MatchesDeclared(ImageSignature.WebP, null));
        Assert.False(ImageSignature.MatchesDeclared(ImageSignature.Jpeg, "image/png"));
    }

    [Fact]
    public void EveryDomainErrorMapsToARegisteredApiCode()
    {
        foreach (var code in Enum.GetValues<DomainErrorCode>())
        {
            var apiCode = DomainErrorMapping.ToErrorCode(code);
            Assert.Contains(apiCode, ApiErrorCodes.All);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => DomainErrorMapping.ToErrorCode((DomainErrorCode)999));
    }

    [Fact]
    public void ListQueryClampsPagingAndParsesSortDirection()
    {
        var query = new ListQuery(" الأول ", "-name", 0, 5000, null);
        Assert.Equal(1, query.SafePage);
        Assert.Equal(ListQuery.MaxPageSize, query.SafePageSize);
        Assert.Equal("الاول", query.NormalizedSearch);
        Assert.Equal(("name", true), query.SortKey("label"));
        Assert.Equal(("label", false), new ListQuery(null, null, null, null, true).SortKey("label"));
        Assert.Equal(ListQuery.DefaultPageSize, new ListQuery(null, null, 2, null, null).SafePageSize);
    }

    [Fact]
    public void ApiTextRoundTripsEnumValues()
    {
        Assert.Equal("preparatory", ApiText.ToValue(SchoolType.Preparatory));
        Assert.True(ApiText.TryParse<CalendarDisplay>("hijri", out var calendar));
        Assert.Equal(CalendarDisplay.Hijri, calendar);
        Assert.False(ApiText.TryParse<CalendarDisplay>("Hijri", out _));
        Assert.False(ApiText.TryParse<CalendarDisplay>(null, out _));
        Assert.Equal(["arabicIndic", "western"], ApiText.Values<NumeralSystem>());
    }
}

internal static class TestImages
{
    public static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52];
    public static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 16, 0x4A, 0x46, 0x49, 0x46, 0, 1];
    public static readonly byte[] WebP = [0x52, 0x49, 0x46, 0x46, 0x24, 0, 0, 0, 0x57, 0x45, 0x42, 0x50, 0x56, 0x50, 0x38, 0x20];
    public static readonly byte[] Gif = "GIF89a\u0001\u0000\u0001\u0000"u8.ToArray();
    public static readonly byte[] Svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"u8.ToArray();
}
