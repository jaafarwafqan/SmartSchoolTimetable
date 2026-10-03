using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.Dashboard;
using SmartSchoolTimetable.Application.SchoolSetup;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase2;

public sealed class SchoolProfileApiTests
{
    private const string ProfilePath = "/api/v1/school-profile";

    internal static object ValidProfile(int version, string name = "إعدادية النور للبنات") => new
    {
        name,
        schoolType = "preparatory",
        studyType = "morning",
        principalName = "أ. زينب",
        scheduleOfficerName = "أ. علي",
        timeZone = "Asia/Baghdad",
        numeralSystem = "arabicIndic",
        calendarDisplay = "gregorian",
        version,
    };

    internal static async Task<HttpResponseMessage> UploadAsync(
        TestHost host, string kind, byte[] bytes, string contentType, int version, string token, bool withOrigin = true)
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(file, "file", "image.bin");
        content.Add(new StringContent(version.ToString(CultureInfo.InvariantCulture)), "version");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{ProfilePath}/{kind}") { Content = content };
        if (withOrigin)
            request.Headers.TryAddWithoutValidation("Origin", "http://127.0.0.1:5080");
        request.Headers.TryAddWithoutValidation("X-Local-Launch-Token", token);
        return await host.Client.SendAsync(request);
    }

    [Fact]
    public async Task EndpointsRequireTheOwnerSession()
    {
        await using var host = new TestHost();
        var bootstrap = await host.GetBootstrapAsync();
        foreach (var path in new[] { ProfilePath, "/api/v1/school-context", "/api/v1/dashboard-summary", $"{ProfilePath}/logo", "/api/v1/academic-years" })
        {
            var response = await host.Client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            await AssertApiErrorAsync(response, "UNAUTHENTICATED");
        }
        var put = await host.PutAsync(ProfilePath, ValidProfile(1), bootstrap.LaunchToken);
        Assert.Equal(HttpStatusCode.Unauthorized, put.StatusCode);
    }

    [Fact]
    public async Task ProfileUpdatesValidateFieldsAndRejectStaleVersions()
    {
        await using var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);

        var initial = await ReadAsync<SchoolProfileDto>(await host.Client.GetAsync(ProfilePath));
        Assert.Equal(string.Empty, initial.Name);
        Assert.Equal("Asia/Baghdad", initial.TimeZone);
        Assert.Contains("hijri", initial.Options.CalendarDisplays);

        var invalid = await host.PutAsync(ProfilePath, new { name = "", schoolType = "castle", studyType = "morning", timeZone = "Mars/Base", numeralSystem = "arabicIndic", calendarDisplay = "gregorian", version = initial.Version }, token);
        Assert.Equal((HttpStatusCode)422, invalid.StatusCode);
        var invalidError = await AssertApiErrorAsync(invalid, "VALIDATION_FAILED");
        Assert.Contains(invalidError.Errors, issue => issue is { Field: "SchoolType", Code: "INVALID_OPTION" });

        var domainInvalid = await host.PutAsync(ProfilePath, new { name = "", schoolType = "primary", studyType = "morning", timeZone = "Mars/Base", numeralSystem = "arabicIndic", calendarDisplay = "gregorian", version = initial.Version }, token);
        var domainError = await AssertApiErrorAsync(domainInvalid, "VALIDATION_FAILED");
        Assert.Contains(domainError.Errors, issue => issue is { Field: "Name", Code: "REQUIRED" });
        Assert.Contains(domainError.Errors, issue => issue is { Field: "TimeZone", Code: "INVALID_OPTION" });

        var saved = await host.PutAsync(ProfilePath, ValidProfile(initial.Version), token);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var updated = await ReadAsync<SchoolProfileDto>(saved);
        Assert.Equal(initial.Version + 1, updated.Version);
        Assert.Equal("preparatory", updated.SchoolType);

        // A second tab still holding the old version is rejected instead of silently overwriting.
        var stale = await host.PutAsync(ProfilePath, ValidProfile(initial.Version, "اسم من تبويب قديم"), token);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        await AssertApiErrorAsync(stale, "CONFLICT");

        var context = await ReadAsync<SchoolContextDto>(await host.Client.GetAsync("/api/v1/school-context"));
        Assert.Equal("إعدادية النور للبنات", context.SchoolName);
        Assert.Null(context.CurrentYear);
    }

    [Fact]
    public async Task UploadsAreValidatedByMagicBytesAndServedInertly()
    {
        await using var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);
        var version = (await ReadAsync<SchoolProfileDto>(await host.Client.GetAsync(ProfilePath))).Version;

        async Task AssertRejected(HttpResponseMessage response, string code)
        {
            Assert.Equal((HttpStatusCode)422, response.StatusCode);
            var error = await AssertApiErrorAsync(response, "VALIDATION_FAILED");
            Assert.Contains(error.Errors, issue => issue.Field == "File" && issue.Code == code);
        }

        await AssertRejected(await UploadAsync(host, "logo", TestImages.Svg, "image/svg+xml", version, token), "ASSET_TYPE_NOT_ALLOWED");
        await AssertRejected(await UploadAsync(host, "logo", TestImages.Gif, "image/gif", version, token), "ASSET_TYPE_NOT_ALLOWED");
        await AssertRejected(await UploadAsync(host, "logo", "plain text"u8.ToArray(), "image/png", version, token), "ASSET_TYPE_NOT_ALLOWED");
        await AssertRejected(await UploadAsync(host, "logo", TestImages.Jpeg, "image/png", version, token), "ASSET_TYPE_MISMATCH");
        var oversize = new byte[(int)ImageSignature.MaxBytes + 1];
        TestImages.Png.CopyTo(oversize, 0);
        await AssertRejected(await UploadAsync(host, "logo", oversize, "image/png", version, token), "ASSET_TOO_LARGE");

        var tooLargeRequest = await UploadAsync(host, "logo", new byte[4 * 1024 * 1024], "image/png", version, token);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooLargeRequest.StatusCode);
        await AssertApiErrorAsync(tooLargeRequest, "PAYLOAD_TOO_LARGE");

        Assert.Equal(HttpStatusCode.Forbidden, (await UploadAsync(host, "logo", TestImages.Png, "image/png", version, token, withOrigin: false)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await UploadAsync(host, "logo", TestImages.Png, "image/png", version, "wrong-token")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await UploadAsync(host, "banner", TestImages.Png, "image/png", version, token)).StatusCode);

        var uploaded = await UploadAsync(host, "logo", TestImages.Png, "image/png", version, token);
        Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        var profile = await ReadAsync<SchoolProfileDto>(uploaded);
        Assert.True(profile.HasLogo);
        Assert.False(profile.HasStamp);

        var download = await host.Client.GetAsync($"{ProfilePath}/logo");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("image/png", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal("nosniff", download.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("sandbox", download.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
        Assert.True(download.Headers.CacheControl?.NoStore);
        Assert.Equal(TestImages.Png, await download.Content.ReadAsByteArrayAsync());

        var stampUpload = await UploadAsync(host, "stamp", TestImages.WebP, "image/webp", profile.Version, token);
        Assert.Equal(HttpStatusCode.OK, stampUpload.StatusCode);
        var withStamp = await ReadAsync<SchoolProfileDto>(stampUpload);

        using var anonymous = host.CreateClientWithoutCookies();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"{ProfilePath}/logo")).StatusCode);

        var removed = await host.SendJsonAsync(HttpMethod.Delete, $"{ProfilePath}/logo?version={withStamp.Version}", new { }, token);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.False((await ReadAsync<SchoolProfileDto>(removed)).HasLogo);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync($"{ProfilePath}/logo")).StatusCode);
        var assetsFolder = Path.Combine(Path.GetDirectoryName(host.DatabasePath)!, "assets");
        Assert.Single(Directory.GetFiles(assetsFolder));
    }

    [Fact]
    public async Task DashboardChecklistReflectsRealData()
    {
        await using var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);
        var empty = await ReadAsync<DashboardSummaryDto>(await host.Client.GetAsync("/api/v1/dashboard-summary"));
        Assert.All(empty.Checklist, item => Assert.False(item.Done));

        var version = (await ReadAsync<SchoolProfileDto>(await host.Client.GetAsync(ProfilePath))).Version;
        await host.PutAsync(ProfilePath, ValidProfile(version), token);
        var year = await ReadAsync<AcademicYearDto>(await host.PostAsync("/api/v1/academic-years",
            new { label = "2026-2027", startDate = "2026-09-01", endDate = "2027-06-30", version = 0 }, token));
        var withTerm = await ReadAsync<AcademicYearDto>(await host.PostAsync($"/api/v1/academic-years/{year.Id}/terms",
            new { name = "الفصل الأول", startDate = "2026-09-01", endDate = "2027-01-15", version = year.Version }, token));
        await host.PostAsync($"/api/v1/academic-years/{year.Id}/terms/{withTerm.Terms[0].Id}/make-current", new { version = withTerm.Version }, token);

        var summary = await ReadAsync<DashboardSummaryDto>(await host.Client.GetAsync("/api/v1/dashboard-summary"));
        Assert.True(summary.Checklist.Single(item => item.Key == "schoolProfile").Done);
        Assert.True(summary.Checklist.Single(item => item.Key == "academicYear").Done);
        Assert.Equal(1, summary.Counts.Single(count => count.Key == "academicYears").Value);
    }
}
