namespace VirtoCommerce.SystemOperations.Core.Diagnostics;

// Removes what must not leave the platform: passwords and keys in connection strings, tokens, emails, card numbers.
public interface ISensitiveDataRedactor
{
    string Redact(string value);
}
