using System.Globalization;
using System.Text;

namespace ByteBazaar.Application.Services;

/// <summary>
/// Minimal CSV writer for report downloads. Fields containing commas, quotes or newlines
/// are quoted, with embedded quotes doubled (RFC 4180).
/// </summary>
public static class CsvWriter
{
    public static string Write(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows)
    {
        var sb = new StringBuilder();
        AppendRow(sb, headers.Select(h => (object?)h).ToList());
        foreach (var row in rows)
            AppendRow(sb, row);
        return sb.ToString();
    }

    private static void AppendRow(StringBuilder sb, IReadOnlyList<object?> fields)
    {
        for (var i = 0; i < fields.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(Escape(fields[i]));
        }
        sb.Append("\r\n");
    }

    public static string Escape(object? value)
    {
        var text = value switch
        {
            null => string.Empty,
            decimal d => d.ToString(CultureInfo.InvariantCulture),
            double d => d.ToString(CultureInfo.InvariantCulture),
            float f => f.ToString(CultureInfo.InvariantCulture),
            DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };

        return text.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0
            ? $"\"{text.Replace("\"", "\"\"")}\""
            : text;
    }
}
