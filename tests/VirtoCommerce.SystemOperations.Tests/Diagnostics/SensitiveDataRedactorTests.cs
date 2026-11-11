using VirtoCommerce.SystemOperations.Data.Diagnostics;
using Xunit;

namespace VirtoCommerce.SystemOperations.Tests.Diagnostics;

[Trait("Category", "Unit")]
public class SensitiveDataRedactorTests
{
    private readonly SensitiveDataRedactor _redactor = new();

    [Theory]
    [InlineData("Server=db;User ID=virto;Password=S3cret!;", "Server=db;User ID=virto;Password=***;")]
    [InlineData("AccountKey=abc+def/123==;EndpointSuffix=core.windows.net", "AccountKey=***;EndpointSuffix=core.windows.net")]
    [InlineData("Authorization: Bearer abc.def-ghi_jkl", "Authorization: Bearer ***")]
    [InlineData("token eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.c2lnbmF0dXJl here", "token *** here")]
    [InlineData("https://user:p4ss@search.example.com/index", "https://***@search.example.com/index")]
    [InlineData("Order of jane.doe@example.com failed", "Order of ***@*** failed")]
    [InlineData("Card 4111 1111 1111 1111 declined", "Card [card] declined")]
    // Quoted values: JSON, an object logged by Serilog, attributes.
    [InlineData("{\"password\":\"hunter2\",\"userName\":\"admin\"}", "{\"password\":\"***\",\"userName\":\"admin\"}")]
    [InlineData("{ \"ApiKey\": \"abc 123\" }", "{ \"ApiKey\": \"***\" }")]
    [InlineData("SignIn { UserName: \"admin\", Password: \"hunter2\" }", "SignIn { UserName: \"admin\", Password: \"***\" }")]
    [InlineData("<add Password=\"S3 cret\" />", "<add Password=\"***\" />")]
    [InlineData("client_secret='x-1;2'&grant_type=client_credentials", "client_secret='***'&grant_type=client_credentials")]
    [InlineData("\"refresh_token\": \"\"", "\"refresh_token\": \"\"")]
    [InlineData("{\"secret\":\"a\\\"b\",\"next\":1}", "{\"secret\":\"***\",\"next\":1}")]
    [InlineData("Password=\"one\"\nnext line", "Password=\"***\"\nnext line")]
    public void Redact_RemovesSecretsAndPersonalData(string value, string expected)
    {
        Assert.Equal(expected, _redactor.Redact(value));
    }

    // An order or product number that is not a valid card number stays readable: it is what support needs.
    [Theory]
    [InlineData("Order 1234567890123 not found")]
    [InlineData("Timeout after 30000 ms")]
    [InlineData(null)]
    [InlineData("")]
    public void Redact_KeepsOtherValues(string value)
    {
        Assert.Equal(value, _redactor.Redact(value));
    }
}
