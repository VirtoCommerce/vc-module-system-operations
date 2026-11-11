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
    private const int RegexTimeoutMs = 1000;
    private const int MinCardDigits = 13;
    private const int MaxCardDigits = 19;
    private const int LuhnModulus = 10;

    // A doubled digit of the Luhn check, with the digits of a two-digit product added: 6 * 2 = 12 -> 1 + 2 = 3.
    private static readonly int[] _luhnDoubled = [0, 2, 4, 6, 8, 1, 3, 5, 7, 9];

    public virtual string Redact(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        value = SecretPairRegex().Replace(value, MaskSecretPair);
        value = BearerRegex().Replace(value, $"Bearer {Mask}");
        value = JwtRegex().Replace(value, Mask);
        value = UrlCredentialsRegex().Replace(value, $"://{Mask}@");
        value = EmailRegex().Replace(value, $"{Mask}@{Mask}");
        value = CardNumberRegex().Replace(value, m => IsLuhnValid(m.Value) ? "[card]" : m.Value);

        return value;
    }

    // A quoted value keeps its quotes, so JSON and logged objects stay readable.
    private static string MaskSecretPair(Match match)
    {
        var quote = match.Groups["quote"].Value;

        return $"{match.Groups["key"].Value}{match.Groups["keyQuote"].Value}{match.Groups["sep"].Value}{quote}{Mask}{quote}";
    }

    private static bool IsLuhnValid(string candidate)
    {
        var digits = candidate.Where(char.IsDigit).Select(x => x - '0').ToArray();
        if (digits.Length < MinCardDigits || digits.Length > MaxCardDigits)
        {
            return false;
        }

        // Every second digit from the right is doubled.
        var sum = 0;
        var doubled = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            sum += doubled ? _luhnDoubled[digits[i]] : digits[i];
            doubled = !doubled;
        }

        return sum % LuhnModulus == 0;
    }

    // key=value, key: value, "key": "value", Key: "value" (an object logged by Serilog), Key="value", key='value'.
    [GeneratedRegex(@"(?<key>password|pwd|accountkey|sharedaccesskey|secret|client_secret|apikey|api_key|api-key|access_token|refresh_token|token|key)(?<keyQuote>[""']?)(?<sep>\s*[=:]\s*)(?:(?<quote>"")(?:\\.|[^""\\\r\n])+""|(?<quote>')(?:\\.|[^'\\\r\n])+'|(?!\*\*\*)[^;,\s""'&]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeoutMs)]
    private static partial Regex SecretPairRegex();

    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9\-._~+/]+=*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeoutMs)]
    private static partial Regex BearerRegex();

    [GeneratedRegex(@"eyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+", RegexOptions.CultureInvariant, RegexTimeoutMs)]
    private static partial Regex JwtRegex();

    [GeneratedRegex(@"://[^/\s:@]+:[^/\s@]+@", RegexOptions.CultureInvariant, RegexTimeoutMs)]
    private static partial Regex UrlCredentialsRegex();

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9\-]+(\.[A-Za-z0-9\-]+)+", RegexOptions.CultureInvariant, RegexTimeoutMs)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"\b\d(?:[ -]?\d){12,18}\b", RegexOptions.CultureInvariant, RegexTimeoutMs)]
    private static partial Regex CardNumberRegex();
}

internal static class TextExtensions
{
    public static string Truncate(this string value, int maxLength)
    {
        return value?.Length > maxLength ? string.Concat(value.AsSpan(0, maxLength), "…") : value;
    }
}
