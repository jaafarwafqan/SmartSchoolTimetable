using System.Text;
using SmartSchoolTimetable.Infrastructure.Persistence;

namespace SmartSchoolTimetable.Api;

/// <summary>
/// What the owner is told (in Arabic) when the program cannot start because the data could not be upgraded. The app cannot serve its screens in that
/// case (the data and the program disagree), so the text goes to the console and to <c>startup-error.txt</c> in the data folder, where the launcher shows it.
/// </summary>
public static class StartupFailureMessages
{
    public const string FileName = "startup-error.txt";

    public static string For(DatabaseUpgradeException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception.Failure switch
        {
            DatabaseUpgradeFailure.BackupFailed =>
                "تعذّر حفظ نسخة احتياطية قبل ترقية البيانات، لذلك لم يُغيَّر أي شيء من بياناتك. تأكد من وجود مساحة كافية على القرص ثم شغّل البرنامج من جديد.",
            DatabaseUpgradeFailure.MigrationFailedRestored =>
                "فشلت ترقية البيانات إلى هذا الإصدار، وأُعيدت بياناتك تلقائياً كما كانت قبل الترقية فلم يضِع شيء منها. شغّل الإصدار السابق من البرنامج، أو راجع دليل الترقية (UPGRADE_AR) وأرسل سجل الدعم إلى المطوّر.",
            DatabaseUpgradeFailure.MigrationFailedNotRestored =>
                $"فشلت ترقية البيانات وتعذّرت إعادتها تلقائياً. نسخة الأمان محفوظة في: {exception.BackupFile}. لا تحذفها، وتواصل مع المطوّر.",
            _ =>
                "بياناتك أُنشئت بإصدار أحدث من هذا البرنامج، لذلك لم يُغيَّر شيء. ثبّت الإصدار الأحدث من البرنامج.",
        };
    }

    /// <summary>Writes the message next to the database (an application-made status file, replaced on each failed start).</summary>
    public static void Write(string databasePath, string message)
    {
        try
        {
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(databasePath))!, FileName), message, new UTF8Encoding(true));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The console message above still reached the owner.
        }
    }
}
