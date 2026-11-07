using System;
using System.Linq;
using System.Text.RegularExpressions;
using VirtoCommerce.SystemOperations.Core.Diagnostics;

namespace VirtoCommerce.SystemOperations.Data.Diagnostics;

// Applied to every message, stack trace and configuration value a tool returns, because the agent's chat and a support
// case leave the platform. It removes values, never the names around them, so the text still explains the problem.
public partial class SensitiveDataRedactor : ISensitiveDataRedactor
{
    private const string Mask = "***";

    public virtual string Redact(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        value = SecretPairRegex().Replace(value, m => $"{m.Groups["key"].Value}{m.Groups["sep"].Value}{Mask}");
        value = BearerRegex().Replace(value, $"Bearer {Mask}");
        value = JwtRegex().Replace(value, Mask);
        value = UrlCredentialsRegex().Replace(value, $"://{Mask}@");
        value = EmailRegex().Replace(value, $"{Mask}@{Mask}");
        value = CardNumberRegex().Replace(value, m => IsLuhnValid(m.Value) ? "[card]" : m.Value);

        return value;
    }

    private static bool IsLuhnValid(string candidate)
    {
        var digits = candidate.Where(char.IsDigit).Select(x => x - '0').ToArray();
        if (digits.Length < 13 || digits.Length > 19)
        {
            return false;
        }

        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            var digit = digits[digits.Length - 1 - i];
            if (i % 2 == 1)
            {
                digit *= 2;
                if (digit > 9)
                {
                    digit -= 9;
                }
            }

            sum += digit;
        }

        return sum % 10 == 0;
    }

    [GeneratedRegex(@"(?<key>password|pwd|accountkey|sharedaccesskey|secret|client_secret|apikey|api_key|api-key|access_token|refresh_token|token|key)(?<sep>\s*[=:]\s*)(?!\*\*\*)[^;,\s""'&]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 1000)]
    private static partial Regex SecretPairRegex();

    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9\-._~+/]+=*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 1000)]
    private static partial Regex BearerRegex();

    [GeneratedRegex(@"eyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex JwtRegex();

    [GeneratedRegex(@"://[^/\s:@]+:[^/\s@]+@", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex UrlCredentialsRegex();

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9\-]+(\.[A-Za-z0-9\-]+)+", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"\b\d(?:[ -]?\d){12,18}\b", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex CardNumberRegex();
}

internal static class TextExtensions
{
    public static string Truncate(this string value, int maxLength)
    {
        return value?.Length > maxLength ? string.Concat(value.AsSpan(0, maxLength), "…") : value;
    }
}
