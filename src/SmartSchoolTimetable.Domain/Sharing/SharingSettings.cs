using SmartSchoolTimetable.Domain.Common;

namespace SmartSchoolTimetable.Domain.Sharing;

/// <summary>
/// Settings of «مشاركة الجدول مع الهاتف» (exactly one row, Id = 1): the «حمّل التطبيق» link printed as a small QR on every teacher card
/// (a Telegram channel, a store page …). Only https links are accepted; the app itself never opens it.
/// </summary>
public sealed class SharingSettings : VersionedEntity
{
    public const long SingletonId = 1;
    public const int AppLinkMaxLength = 300;

    private SharingSettings()
    {
    }

    public string? AppLink { get; private set; }

    public static SharingSettings CreateDefault() => new() { Id = SingletonId };

    /// <summary>Returns false (no new version) when nothing changed. Field name: "AppLink".</summary>
    public bool Update(string? appLink)
    {
        var trimmed = string.IsNullOrWhiteSpace(appLink) ? null : appLink.Trim();
        new DomainErrors()
            .When(trimmed is { Length: > AppLinkMaxLength }, nameof(AppLink), DomainErrorCode.TooLong)
            .When(trimmed is not null && !IsHttpsLink(trimmed), nameof(AppLink), DomainErrorCode.InvalidOption)
            .ThrowIfAny();
        if (AppLink == trimmed)
            return false;
        AppLink = trimmed;
        Touch();
        return true;
    }

    public static bool IsHttpsLink(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && !string.IsNullOrEmpty(uri.Host)
        && string.IsNullOrEmpty(uri.UserInfo) && !value.Any(char.IsWhiteSpace);
}
