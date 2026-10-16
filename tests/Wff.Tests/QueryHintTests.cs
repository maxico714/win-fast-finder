using Wff.Engine;

namespace Wff.Tests;

public sealed class QueryHintTests
{
    [Theory]
    [InlineData("ex", "ext:")]
    [InlineData("e", "ext:")]
    [InlineData("s", "size:")]
    [InlineData("si", "size:")]
    [InlineData("d", "dm:")]
    [InlineData("dm", "dm:")]
    [InlineData("p", "path:")]
    [InlineData("pa", "path:")]
    [InlineData("pat", "path:")]
    [InlineData("ext:", null)]
    [InlineData("size:", null)]
    [InlineData("", null)]
    [InlineData("zzz", null)]
    [InlineData("report", null)]
    public void CompleteName_Completes_Strict_Prefixes_Only(string prefix, string? expected)
    {
        Assert.Equal(expected, QueryHint.CompleteName(prefix));
    }

    [Fact]
    public void SuggestNames_Returns_All_Directives_For_Empty_Prefix()
    {
        var got = QueryHint.SuggestNames("");
        Assert.Equal(new[] { "ext:", "size:", "dm:", "path:" },
            got.Select(s => s.Insert));
    }

    [Fact]
    public void SuggestNames_Filters_By_Prefix()
    {
        var got = QueryHint.SuggestNames("s");
        Assert.Single(got);
        Assert.Equal("size:", got[0].Insert);
        Assert.NotEmpty(got[0].Description);
    }

    [Theory]
    [InlineData("ext:pdf", "ext:", "pdf")]
    [InlineData("ext:", "ext:", "")]
    [InlineData("size:>10kb", "size:", ">10kb")]
    [InlineData("dm:<7d", "dm:", "<7d")]
    [InlineData("path:Downloads", "path:", "downloads")]
    [InlineData("path:", "path:", "")]
    public void SplitDirective_Splits_Name_And_Value(string token, string name, string value)
    {
        // NOTE: SplitDirective expects lowercased input.
        var got = QueryHint.SplitDirective(token.ToLowerInvariant());
        Assert.NotNull(got);
        Assert.Equal(name, got.Value.Name);
        Assert.Equal(value.ToLowerInvariant(), got.Value.Value);
    }

    [Theory]
    [InlineData("ex")]
    [InlineData("report")]
    [InlineData("*.pdf")]
    public void SplitDirective_Returns_Null_For_Non_Directives(string token)
    {
        Assert.Null(QueryHint.SplitDirective(token));
    }

    [Fact]
    public void SuggestExtensions_Orders_By_Frequency_Then_Name()
    {
        var paths = new[]
        {
            @"C:\a\b.pdf", @"C:\a\c.pdf", @"C:\a\d.pdf",
            @"C:\a\e.docx", @"C:\a\f.docx",
            @"C:\a\g.txt",
            @"C:\a\archive.tar.gz",
            @"C:\a\noext",
        };
        var got = QueryHint.SuggestExtensions(paths, "");
        // pdf x3, docx x2, then gz before txt alphabetically
        // (last-dot semantics, shared with the settings list).
        Assert.Equal(new[] { "pdf", "docx", "gz", "txt" },
            got.Select(s => s.Display));
        Assert.Equal("ext:pdf", got[0].Insert);
        Assert.Contains("files", got[0].Description);
    }

    [Fact]
    public void SuggestExtensions_Filters_By_Prefix()
    {
        var paths = new[] { @"C:\a\b.pdf", @"C:\a\c.docx" };
        var got = QueryHint.SuggestExtensions(paths, "d");
        Assert.Single(got);
        Assert.Equal("ext:docx", got[0].Insert);
    }

    [Fact]
    public void SuggestExtensions_Empty_When_Nothing_Matches()
    {
        var paths = new[] { @"C:\a\b.pdf" };
        Assert.Empty(QueryHint.SuggestExtensions(paths, "zzz"));
    }
}
