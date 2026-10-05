using IskBattle.Domain.Text;

namespace IskBattle.Domain.Tests;

public class NameRulesTests
{
    [Theory]
    [InlineData("Anna")]
    [InlineData("Åsa-Britt Öberg")]
    [InlineData("J. Doe")]
    [InlineData("Kompisligan 2026 🚀")]
    [InlineData("Ett. Två")]
    public void Ordinary_names_are_allowed(string name) =>
        Assert.Equal(NameError.None, NameRules.Validate(name, 50));

    [Theory]
    [InlineData("")]
    [InlineData("abcdef")]
    public void Length_is_checked(string name) =>
        Assert.Equal(NameError.Length, NameRules.Validate(name, 5));

    [Theory]
    [InlineData("Logga in på evil.se")]
    [InlineData("https://evil.example")]
    [InlineData("www.evil")]
    [InlineData("anna@example")]
    [InlineData("Rad1\nRad2")]
    [InlineData("Tab\there")]
    [InlineData("Anna\x202Eevil")]  // right-to-left override
    [InlineData("An\x200Bna")]       // zero-width space
    [InlineData("Anna\x2028")]       // line separator
    public void Links_and_invisible_characters_are_not_allowed(string name) =>
        Assert.Equal(NameError.NotAllowed, NameRules.Validate(name, 50));
}
