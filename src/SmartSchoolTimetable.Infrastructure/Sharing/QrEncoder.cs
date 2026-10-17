using QRCoder;
using SmartSchoolTimetable.Application.Sharing;

namespace SmartSchoolTimetable.Infrastructure.Sharing;

/// <summary>QR codes for the sharing page and the teacher cards (QRCoder, managed code; ADR 0056). Level M, byte mode, quiet zone of 4.</summary>
public sealed class QrEncoder : IQrEncoder
{
    public QrCodeDto? Encode(string text, int maxVersion)
    {
        ArgumentNullException.ThrowIfNull(text);
        QRCodeData data;
        try
        {
            data = QRCodeGenerator.GenerateQrCode(text, QRCodeGenerator.ECCLevel.M);
        }
        catch (QRCoder.Exceptions.DataTooLongException)
        {
            return null;
        }
        using (data)
        {
            if (data.Version > maxVersion)
                return null;
            var rows = data.ModuleMatrix.Select(row => string.Concat(Enumerable.Range(0, row.Length).Select(index => row[index] ? '1' : '0'))).ToArray();
            return new QrCodeDto(data.Version, rows.Length, rows);
        }
    }
}
