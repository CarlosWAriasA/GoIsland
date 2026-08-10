using GoIsland.Api.Data;

namespace GoIsland.Api.Tests.Services;

public class SearchTextTests
{
    [Theory]
    [InlineData("Samaná", "samana")]
    [InlineData("SAMANÁ", "samana")]
    [InlineData("samana", "samana")]
    [InlineData("Bahía", "bahia")]
    [InlineData("Gastronomía", "gastronomia")]
    [InlineData("Español", "espanol")]
    [InlineData("Peñón", "penon")]
    [InlineData("Jarabacoa", "jarabacoa")]
    public void Normalize_RemovesDiacriticsAndLowercases(string value, string expected)
    {
        Assert.Equal(expected, SearchText.Normalize(value));
    }

    [Fact]
    public void Normalize_IsIdempotent()
    {
        var once = SearchText.Normalize("Bahía de Samaná");

        Assert.Equal(once, SearchText.Normalize(once));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeTerm_ReturnsNullWhenThereIsNoSearch(string? value)
    {
        Assert.Null(SearchText.NormalizeTerm(value));
    }

    [Fact]
    public void NormalizeTerm_TrimsBeforeNormalizing()
    {
        Assert.Equal("samana", SearchText.NormalizeTerm("  Samaná  "));
    }

    [Fact]
    public void EscapeLikePattern_EscapesWildcardsAndBackslash()
    {
        Assert.Equal("100\\%", SearchText.EscapeLikePattern("100%"));
        Assert.Equal("a\\_b", SearchText.EscapeLikePattern("a_b"));
        Assert.Equal("a\\\\b", SearchText.EscapeLikePattern("a\\b"));
    }

    [Fact]
    public void ToContainsPattern_WrapsTheTermWithWildcards()
    {
        Assert.Equal("%samana%", SearchText.ToContainsPattern("samana"));
    }

    [Fact]
    public void ToStartsWithPattern_AppendsASingleWildcard()
    {
        Assert.Equal("samana%", SearchText.ToStartsWithPattern("samana"));
    }
}
