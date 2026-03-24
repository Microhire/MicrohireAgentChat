using MicrohireAgentChat.Services.Persistence;

namespace MicrohireAgentChat.Tests;

public sealed class ContactLookupNormalizationTests
{
    [Theory]
    [InlineData("  User@EXAMPLE.com ", "user@example.com")]
    [InlineData(null, null)]
    [InlineData("", null)]
    public void NormalizeEmail_TrimsAndLowercases(string? input, string? expected)
    {
        Assert.Equal(expected, ContactLookupNormalization.NormalizeEmail(input));
    }

    [Theory]
    [InlineData("+61 412 345 678", "0412345678")]
    [InlineData("61412345678", "0412345678")]
    [InlineData("0412345678", "0412345678")]
    [InlineData("412345678", "0412345678")]
    [InlineData("123", null)]
    public void NormalizePhoneDigits_AuFormats(string? input, string? expected)
    {
        Assert.Equal(expected, ContactLookupNormalization.NormalizePhoneDigits(input));
    }

    [Theory]
    [InlineData("Acme Pty Ltd", "acme")]
    [InlineData("ACME LIMITED", "acme")]
    [InlineData("Foo  Inc.", "foo")]
    [InlineData("  Beta  Corp  ", "beta corp")]
    public void NormalizeOrganisationKey_StripsSuffixesAndWhitespace(string input, string expected)
    {
        Assert.Equal(expected, ContactLookupNormalization.NormalizeOrganisationKey(input));
    }
}
