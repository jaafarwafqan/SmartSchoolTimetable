using System.Net;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.SchoolSetup;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase2;

public sealed class AcademicYearApiTests
{
    private const string YearsPath = "/api/v1/academic-years";

    internal static async Task<AcademicYearDto> CreateYearAsync(TestHost host, string token, string label, string start, string end)
    {
        var response = await host.PostAsync(YearsPath, new { label, startDate = start, endDate = end, version = 0 }, token);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<AcademicYearDto>(response);
    }

    [Fact]
    public async Task FirstYearBecomesCurrentAndOnlyOneYearIsCurrent()
    {
        await using var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);
        var first = await CreateYearAsync(host, token, "2025-2026", "2025-09-01", "2026-06-30");
        var second = await CreateYearAsync(host, token, "2026-2027", "2026-09-01", "2027-06-30");
        Assert.True(first.IsCurrent);
        Assert.False(second.IsCurrent);

        var madeCurrent = await host.PostAsync($"{YearsPath}/{second.Id}/make-current", new { version = second.Version }, token);
        Assert.Equal(HttpStatusCode.OK, madeCurrent.StatusCode);
        var list = await ReadAsync<PagedResult<AcademicYearDto>>(await host.Client.GetAsync(YearsPath));
        Assert.Equal(2, list.Total);
        Assert.Single(list.Items, year => year.IsCurrent);
        Assert.Equal("2026-2027", list.Items[0].Label); // default sort: newest start first

        var context = await ReadAsync<SchoolContextDto>(await host.Client.GetAsync("/api/v1/school-context"));
        Assert.Equal(second.Id, context.CurrentYear?.Id);
    }

    [Fact]
    public async Task ValidationDuplicatesSearchAndPaging()
    {
        await using var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);

        var badDates = await host.PostAsync(YearsPath, new { label = "x", startDate = "2026-13-01", endDate = "", version = 0 }, token);
        var badDatesError = await AssertApiErrorAsync(badDates, "VALIDATION_FAILED");
        Assert.Contains(badDatesError.Errors, issue => issue is { Field: "StartDate", Code: "INVALID_DATE" });
        Assert.Contains(badDatesError.Errors, issue => issue is { Field: "EndDate", Code: "REQUIRED" });

        var reversed = await host.PostAsync(YearsPath, new { label = "x", startDate = "2026-09-01", endDate = "2026-01-01", version = 0 }, token);
        Assert.Contains((await AssertApiErrorAsync(reversed, "VALIDATION_FAILED")).Errors, issue => issue.Code == "INVALID_DATE_RANGE");

        await CreateYearAsync(host, token, "العام ٢٠٢٤-٢٠٢٥", "2024-09-01", "2025-06-30");
        var duplicate = await host.PostAsync(YearsPath, new { label = "العام 2024-2025", startDate = "2024-09-01", endDate = "2025-06-30", version = 0 }, token);
        Assert.Contains((await AssertApiErrorAsync(duplicate, "VALIDATION_FAILED")).Errors, issue => issue is { Field: "Label", Code: "DUPLICATE_NAME" });

        await CreateYearAsync(host, token, "2025-2026", "2025-09-01", "2026-06-30");
        await CreateYearAsync(host, token, "2026-2027", "2026-09-01", "2027-06-30");
        var searched = await ReadAsync<PagedResult<AcademicYearDto>>(await host.Client.GetAsync($"{YearsPath}?search=٢٠٢٤"));
        Assert.Equal(1, searched.Total);
        var paged = await ReadAsync<PagedResult<AcademicYearDto>>(await host.Client.GetAsync($"{YearsPath}?page=2&pageSize=2&sort=startDate"));
        Assert.Equal(3, paged.Total);
        Assert.Single(paged.Items);
        Assert.Equal("2026-2027", paged.Items[0].Label);
        var byLabel = await ReadAsync<PagedResult<AcademicYearDto>>(await host.Client.GetAsync($"{YearsPath}?sort=-label"));
        Assert.Equal("العام ٢٠٢٤-٢٠٢٥", byLabel.Items[0].Label);

        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync($"{YearsPath}/9999")).StatusCode);
    }

    [Fact]
    public async Task TermsRulesCurrentTermAndStaleEditsFromAnotherTab()
    {
        await using var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);
        var year = await CreateYearAsync(host, token, "2026-2027", "2026-09-01", "2027-06-30");

        var outside = await host.PostAsync($"{YearsPath}/{year.Id}/terms", new { name = "خارج", startDate = "2026-08-01", endDate = "2026-12-01", version = year.Version }, token);
        Assert.Contains((await AssertApiErrorAsync(outside, "VALIDATION_FAILED")).Errors, issue => issue is { Field: "StartDate", Code: "TERM_OUTSIDE_YEAR" });

        var first = await ReadAsync<AcademicYearDto>(await host.PostAsync($"{YearsPath}/{year.Id}/terms",
            new { name = "الفصل الأول", startDate = "2026-09-01", endDate = "2027-01-15", version = year.Version }, token));
        var overlap = await host.PostAsync($"{YearsPath}/{year.Id}/terms", new { name = "الثاني", startDate = "2027-01-01", endDate = "2027-05-01", version = first.Version }, token);
        Assert.Contains((await AssertApiErrorAsync(overlap, "VALIDATION_FAILED")).Errors, issue => issue.Code == "TERMS_OVERLAP");

        var two = await ReadAsync<AcademicYearDto>(await host.PostAsync($"{YearsPath}/{year.Id}/terms",
            new { name = "الفصل الثاني", startDate = "2027-02-01", endDate = "2027-06-01", version = first.Version }, token));
        var secondTerm = two.Terms.Single(term => term.Name == "الفصل الثاني");

        // Tab A and tab B both loaded version two.Version; A saves first, B is rejected with CONFLICT.
        var tabA = await host.PutAsync($"{YearsPath}/{year.Id}/terms/{secondTerm.Id}", new { name = "الفصل الثاني (معدل)", startDate = "2027-02-01", endDate = "2027-06-01", version = two.Version }, token);
        Assert.Equal(HttpStatusCode.OK, tabA.StatusCode);
        var tabB = await host.PutAsync($"{YearsPath}/{year.Id}", new { label = "2026/2027", startDate = "2026-09-01", endDate = "2027-06-30", version = two.Version }, token);
        Assert.Equal(HttpStatusCode.Conflict, tabB.StatusCode);
        await AssertApiErrorAsync(tabB, "CONFLICT");

        var afterA = await ReadAsync<AcademicYearDto>(tabA);
        var current = await ReadAsync<AcademicYearDto>(await host.PostAsync($"{YearsPath}/{year.Id}/terms/{secondTerm.Id}/make-current", new { version = afterA.Version }, token));
        Assert.True(current.Terms.Single(term => term.Id == secondTerm.Id).IsCurrent);
        var context = await ReadAsync<SchoolContextDto>(await host.Client.GetAsync("/api/v1/school-context"));
        Assert.Equal("الفصل الثاني (معدل)", context.CurrentTerm?.Name);

        var deletedTerm = await host.SendJsonAsync(HttpMethod.Delete, $"{YearsPath}/{year.Id}/terms/{secondTerm.Id}?version={current.Version}", new { }, token);
        Assert.Equal(HttpStatusCode.OK, deletedTerm.StatusCode);
        var afterDelete = await ReadAsync<AcademicYearDto>(deletedTerm);
        Assert.Single(afterDelete.Terms);
        Assert.Equal(HttpStatusCode.NotFound, (await host.PutAsync($"{YearsPath}/{year.Id}/terms/{secondTerm.Id}", new { name = "x", startDate = "2027-02-01", endDate = "2027-06-01", version = afterDelete.Version }, token)).StatusCode);

        var staleDelete = await host.SendJsonAsync(HttpMethod.Delete, $"{YearsPath}/{year.Id}?version={year.Version}", new { }, token);
        Assert.Equal(HttpStatusCode.Conflict, staleDelete.StatusCode);
        var deleted = await host.SendJsonAsync(HttpMethod.Delete, $"{YearsPath}/{year.Id}?version={afterDelete.Version}", new { }, token);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(0, (await ReadAsync<PagedResult<AcademicYearDto>>(await host.Client.GetAsync(YearsPath))).Total);
    }
}
