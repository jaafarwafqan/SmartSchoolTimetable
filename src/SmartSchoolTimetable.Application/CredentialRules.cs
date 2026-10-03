namespace SmartSchoolTimetable.Application;

public static class CredentialRules
{
    public const int UsernameMinLength = 3;
    public const int UsernameMaxLength = 64;
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 1024;

    public static bool IsPasswordLengthValid(string password) =>
        password.Length is >= PasswordMinLength and <= PasswordMaxLength;

    public static bool IsUsernameValid(string normalizedUsername) =>
        normalizedUsername.Length is >= UsernameMinLength and <= UsernameMaxLength &&
        !normalizedUsername.Any(char.IsControl);
}
