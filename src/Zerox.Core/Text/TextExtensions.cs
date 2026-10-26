using System.Text;

namespace Zerox.Core.Text;

public static class TextExtensions
{
    private const string Ellipsis = "...";

    // The text cut to maxLength, ending with "..." when it was cut; never longer than maxLength.
    public static string Truncate(this string text, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(maxLength);
        if (text.Length <= maxLength)
            return text;
        return maxLength <= Ellipsis.Length
            ? text[..maxLength]
            : string.Concat(text.AsSpan(0, maxLength - Ellipsis.Length), Ellipsis);
    }

    // The text cut to maxLength without ellipsis; maxLength ≤ 0 means no limit.
    public static string? Clip(this string? text, int maxLength)
        => text is not null && maxLength > 0 && text.Length > maxLength ? text[..maxLength] : text;

    public static string ToBase64Utf8(this string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
    }

    // Empty text gives the fallback; invalid Base64 throws FormatException.
    public static string FromBase64Utf8(this string base64, string fallback = "")
        => string.IsNullOrEmpty(base64) ? fallback : Encoding.UTF8.GetString(Convert.FromBase64String(base64));
}
