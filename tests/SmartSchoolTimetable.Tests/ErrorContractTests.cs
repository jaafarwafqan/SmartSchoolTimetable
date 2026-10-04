using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using SmartSchoolTimetable.Api;
using SmartSchoolTimetable.Application;

namespace SmartSchoolTimetable.Tests;

public sealed partial class ErrorContractTests
{
    private static readonly string[] ConstantCodes = typeof(ErrorCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsLiteral && field.FieldType == typeof(string))
        .Select(field => (string)field.GetRawConstantValue()!)
        .ToArray();

    [Fact]
    public void EveryErrorCodeConstantIsRegisteredMappedToAnErrorStatusAndTranslated()
    {
        Assert.NotEmpty(ConstantCodes);
        Assert.Equal(
            ConstantCodes.Order(StringComparer.Ordinal),
            ApiErrorCodes.All.Order(StringComparer.Ordinal));

        var dictionary = File.ReadAllText(TestPaths.FrontendFile("src", "i18n", "ar", "errors.ts"));
        foreach (var code in ConstantCodes)
        {
            var status = ApiErrorCodes.StatusFor(code);
            Assert.True(status is >= 400 and <= 599, $"{code} must map to an HTTP error status, got {status}.");

            var match = Regex.Match(
                dictionary,
                $@"^\s{{2}}{Regex.Escape(code)}:\s*""([^""]+)""",
                RegexOptions.Multiline);
            Assert.True(match.Success, $"Arabic error dictionary is missing {code}.");
            Assert.Matches(@"\p{IsArabic}", match.Groups[1].Value);
        }
    }

    [Fact]
    public void BackendSourceEmitsErrorCodesOnlyThroughRegisteredConstants()
    {
        var registered = ApiErrorCodes.All.ToHashSet(StringComparer.Ordinal);
        var violations = new List<string>();
        var scannedFiles = 0;
        foreach (var file in TestPaths.BackendSourceFiles())
        {
            if (Path.GetFileName(file) == "ErrorCodes.cs")
                continue;
            scannedFiles++;
            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                var location = $"{Path.GetFileName(file)}:{index + 1}";
                if (EmissionWithLiteral().IsMatch(line))
                    violations.Add($"{location} emits an error code as a string literal: {line.Trim()}");

                foreach (Match literal in StringLiteral().Matches(line))
                {
                    var value = literal.Groups[1].Value;
                    if (registered.Contains(value.ToUpperInvariant()))
                        violations.Add($"{location} uses literal \"{value}\"; use the ErrorCodes constant.");
                    else if (UpperSnakeCase().IsMatch(value))
                        violations.Add($"{location} uses unregistered code-like literal \"{value}\".");
                }
            }
        }

        Assert.True(scannedFiles >= 10, $"Expected to scan the backend sources, scanned {scannedFiles} files.");
        Assert.Empty(violations);
    }

    [Theory]
    [InlineData("NOT_A_REGISTERED_CODE", 200, 500, "INTERNAL_ERROR")]
    [InlineData("NOT_A_REGISTERED_CODE", 204, 500, "INTERNAL_ERROR")]
    [InlineData("NOT_A_REGISTERED_CODE", 302, 500, "INTERNAL_ERROR")]
    [InlineData("NOT_A_REGISTERED_CODE", 422, 422, "NOT_A_REGISTERED_CODE")]
    [InlineData("unauthenticated", 422, 422, "unauthenticated")]
    [InlineData("UNAUTHENTICATED", 422, 401, "UNAUTHENTICATED")]
    [InlineData("UNAUTHENTICATED", 200, 401, "UNAUTHENTICATED")]
    [InlineData(null, 418, 500, "INTERNAL_ERROR")]
    [InlineData(null, 404, 404, "NOT_FOUND")]
    public async Task MiddlewareNeverSendsAnErrorWithASuccessStatus(
        string? code,
        int statusSetByEndpoint,
        int expectedStatus,
        string expectedCode)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/test";
        context.Response.Body = new MemoryStream();
        var middleware = new UnifiedApiErrorMiddleware(
            httpContext =>
            {
                if (code is not null)
                    httpContext.Items[UnifiedApiErrorMiddleware.ErrorCodeItem] = code;
                httpContext.Response.StatusCode = statusSetByEndpoint;
                return Task.CompletedTask;
            },
            NullLogger<UnifiedApiErrorMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.True(context.Response.StatusCode >= 400);
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(expectedCode, json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public void ResolveErrorNeverReturnsANonErrorStatusForAnyInput()
    {
        string?[] codes = [null, "NOT_A_REGISTERED_CODE", "unauthenticated", .. ConstantCodes];
        foreach (var code in codes)
        {
            for (var status = 100; status <= 599; status++)
            {
                var (resolvedStatus, resolvedCode) = UnifiedApiErrorMiddleware.ResolveError(code, status);
                Assert.True(resolvedStatus >= 400, $"code={code ?? "null"} status={status} resolved {resolvedStatus}.");
                Assert.False(string.IsNullOrEmpty(resolvedCode));
            }
        }
    }

    [GeneratedRegex(@"(AuthOperationResult\(\s*false\s*,\s*""|ErrorCodeItem\]\s*=\s*""|WithErrorCode\(\s*""|Failure\([^;]*?"")")]
    private static partial Regex EmissionWithLiteral();

    [GeneratedRegex(@"""([A-Za-z][A-Za-z0-9_]{2,})""")]
    private static partial Regex StringLiteral();

    [GeneratedRegex("^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)+$")]
    private static partial Regex UpperSnakeCase();
}
