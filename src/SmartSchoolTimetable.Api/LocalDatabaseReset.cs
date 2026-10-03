namespace SmartSchoolTimetable.Api;

public static class LocalDatabaseReset
{
    /// <summary>The literal the operator must type. It stays ASCII so it can be typed on any keyboard layout.</summary>
    public const string ConfirmationWord = "RESET";

    public const string WarningMessage = "سيؤدي هذا إلى حذف قاعدة البيانات المحلية وملفاتها المرافقة نهائياً:";
    public const string ConfirmationPrompt = "اكتب " + ConfirmationWord + " للتأكيد: ";
    public const string CancelledMessage = "أُلغيت إعادة التعيين. لم يُحذف أي ملف.";
    public const string CompletedMessage = "اكتملت إعادة تعيين قاعدة البيانات المحلية.";

    public static bool DeleteAfterConfirmation(string databasePath, TextReader input, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        var fullPath = Path.GetFullPath(databasePath);
        output.WriteLine($"{WarningMessage} {fullPath}");
        output.Write(ConfirmationPrompt);
        if (!string.Equals(input.ReadLine()?.Trim(), ConfirmationWord, StringComparison.Ordinal))
        {
            output.WriteLine(CancelledMessage);
            return false;
        }

        foreach (var path in new[] { fullPath, $"{fullPath}-wal", $"{fullPath}-shm", $"{fullPath}-journal" })
            File.Delete(path);

        output.WriteLine(CompletedMessage);
        return true;
    }
}
