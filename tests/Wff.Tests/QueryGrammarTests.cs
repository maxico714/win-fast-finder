using Wff.Engine;

namespace Wff.Tests;

// Every documented size:/dm: form, valid and invalid. Invalid tokens must
// land in Query.Invalid (with a hint) and must NOT leak into free text.
public sealed class QueryGrammarTests
{
    private static Query Parse(string input, QueryOptions? options = null) =>
        QueryParser.Parse(input, options);

    [Theory]
    [InlineData("size:>10kb", 10 * 1024 + 1L, null)]
    [InlineData("size:>=10kb", 10 * 1024L, null)]
    [InlineData("size:<2mb", null, 2 * 1024 * 1024 - 1L)]
    [InlineData("size:<=2mb", null, 2 * 1024 * 1024L)]
    [InlineData("size:500kb", null, null)] // bare: mode-dependent, checked below
    public void Size_Operators_Parse(string input, long? min, long? max)
    {
        var q = Parse(input);
        Assert.Empty(q.Invalid);
        Assert.Equal(min, q.MinSizeBytes);
        Assert.Equal(max, q.MaxSizeBytes);
    }

    [Theory]
    [InlineData("size:>0.5mb", 524289L)]
    [InlineData("size:>1g", 1073741825L)]
    [InlineData("size:>1m", 1048577L)]
    [InlineData("size:>1k", 1025L)]
    [InlineData("size:>1t", 1099511627777L)]
    [InlineData("size:>1 gb", 1073741825L)] // spaced unit joins
    [InlineData("size:>1GB", 1073741825L)]
    public void Size_Decimals_ShortUnits_Spaces_Parse(string input, long min)
    {
        var q = Parse(input);
        Assert.Empty(q.Invalid);
        Assert.Equal(min, q.MinSizeBytes);
        Assert.Null(q.MaxSizeBytes);
    }

    [Theory]
    [InlineData("size:1.5gb", "1.5 GB")]
    [InlineData("size:1gb", "1 GB")] // default DisplayedSize: no byte range
    public void Size_Bare_Decimals_Use_Displayed_Target(string input, string target)
    {
        var q = Parse(input);
        Assert.Empty(q.Invalid);
        Assert.Null(q.MinSizeBytes);
        Assert.Equal(target, q.SizeDisplayTarget);
    }

    [Fact]
    public void Size_Range_Parses_Inclusive_OrderFree()
    {
        var q = Parse("size:1mb..5mb");
        Assert.Empty(q.Invalid);
        Assert.Equal(1048576L, q.MinSizeBytes);
        Assert.Equal(5 * 1048576L, q.MaxSizeBytes);
        var rev = Parse("size:5mb..1mb");
        Assert.Equal(1048576L, rev.MinSizeBytes);
        Assert.Equal(5 * 1048576L, rev.MaxSizeBytes);
    }

    [Theory]
    [InlineData("size:=", "size:=")]
    [InlineData("size:>", "size:>")]
    [InlineData("size:abc", "size:abc")]
    [InlineData("size:1.5x", "size:1.5x")]
    [InlineData("size:>1bb", "size:>1bb")]
    [InlineData("size:1.5.2mb", "size:1.5.2mb")]
    [InlineData("size:", "size:")]
    [InlineData("size:1mb..", "size:1mb..")]
    [InlineData("size:..5mb", "size:..5mb")]
    [InlineData("size:= gb", "size:=")] // must not throw: bare "=" has no second char
    public void Size_Invalid_Forms_Report_Not_Text(string input, string badToken)
    {
        var q = Parse(input);
        Assert.Contains(q.Invalid, x => x.Token == badToken);
        Assert.Contains("size:", q.Invalid[0].Message);
        Assert.DoesNotContain(badToken, q.Text);
        Assert.Null(q.MinSizeBytes);
        Assert.Null(q.MaxSizeBytes);
    }

    [Fact]
    public void Size_ExactBytes_Bare_Is_Byte_Exact()
    {
        var q = Parse("size:1mb", new QueryOptions { ExactSizeMode = "ExactBytes" });
        Assert.Empty(q.Invalid);
        Assert.Equal(1048576L, q.MinSizeBytes);
        Assert.Equal(1048576L, q.MaxSizeBytes);
    }

    [Fact]
    public void Size_DisplayedSize_Bare_Matches_Explorer_Text()
    {
        var q = Parse("size:1mb"); // default mode
        Assert.Empty(q.Invalid);
        Assert.Null(q.MinSizeBytes);
        Assert.Equal("1 MB", q.SizeDisplayTarget);
    }

    [Fact]
    public void Size_Percent1_Bare_Allows_One_Percent()
    {
        var q = Parse("size:1mb", new QueryOptions { ExactSizeMode = "Percent1" });
        Assert.Empty(q.Invalid);
        Assert.Equal((long)(1048576 * 0.99), q.MinSizeBytes);
        Assert.Equal((long)Math.Ceiling(1048576 * 1.01), q.MaxSizeBytes);
    }

    [Fact]
    public void Size_Equals_Sign_Is_Always_Byte_Exact()
    {
        var q = Parse("size:=1mb"); // default DisplayedSize, but = is exact
        Assert.Empty(q.Invalid);
        Assert.Equal(1048576L, q.MinSizeBytes);
        Assert.Equal(1048576L, q.MaxSizeBytes);
    }

    [Theory]
    [InlineData("dm:<7d")]
    [InlineData("dm:7d")]
    [InlineData("dm:>30d")]
    [InlineData("dm:7")]
    [InlineData("dm:<12h")]
    [InlineData("dm:<2w")]
    [InlineData("dm:<3m")]
    [InlineData("dm:<1y")]
    [InlineData("dm:today")]
    [InlineData("dm:yesterday")]
    [InlineData("dm:2024-05-10")]
    [InlineData("dm:>=2024-05-01")]
    [InlineData("dm:2024-05-01..2024-05-31")]
    [InlineData("dm:7d..30d")]
    public void Date_Valid_Forms_Parse_Without_Invalid(string input)
    {
        var q = Parse(input);
        Assert.Empty(q.Invalid);
        Assert.True(q.ModifiedAfterUtc != null || q.ModifiedBeforeUtc != null);
    }

    [Theory]
    [InlineData("dm:")]
    [InlineData("dm:xyz")]
    [InlineData("dm:2024-13-99")]
    [InlineData("dm:10/05/2024")]
    [InlineData("dm:7x")]
    public void Date_Invalid_Forms_Report_Not_Text(string input)
    {
        var q = Parse(input);
        Assert.Single(q.Invalid);
        Assert.Equal(input, q.Invalid[0].Token);
        Assert.DoesNotContain(input, q.Text);
        Assert.Null(q.ModifiedAfterUtc);
        Assert.Null(q.ModifiedBeforeUtc);
    }

    [Fact]
    public void Date_Relative_Cutoffs_Are_Sane()
    {
        var now = DateTime.UtcNow;
        var q = Parse("dm:<12h");
        Assert.NotNull(q.ModifiedAfterUtc);
        Assert.InRange(q.ModifiedAfterUtc!.Value, now - TimeSpan.FromHours(13), now - TimeSpan.FromHours(11));
        Assert.Null(q.ModifiedBeforeUtc);
        var old = Parse("dm:>30d");
        Assert.Null(old.ModifiedAfterUtc);
        Assert.NotNull(old.ModifiedBeforeUtc);
    }

    [Fact]
    public void Date_Absolute_Is_Whole_Local_Day()
    {
        var q = Parse("dm:2024-05-10");
        Assert.NotNull(q.ModifiedAfterUtc);
        Assert.NotNull(q.ModifiedBeforeUtc);
        var span = q.ModifiedBeforeUtc!.Value - q.ModifiedAfterUtc!.Value;
        Assert.Equal(TimeSpan.FromDays(1), span);
        // Local midnight boundaries converted to UTC.
        var localMidnight = new DateTime(2024, 5, 10, 0, 0, 0, DateTimeKind.Local).ToUniversalTime();
        Assert.Equal(localMidnight, q.ModifiedAfterUtc!.Value);
    }

    [Theory]
    [InlineData("size:>1")]
    [InlineData("size:>10m")]
    [InlineData("dm:<3")]
    [InlineData("dm:<30")]
    [InlineData("ext:")]
    public void Typing_Prefixes_Are_Valid_Or_Flagged_Never_Silent(string input)
    {
        // Intermediate keystrokes must never silently become filename text
        // for directive-looking tokens; ext: bare is flagged, the rest parse.
        var q = Parse(input);
        if (input.StartsWith("ext:"))
        {
            Assert.Single(q.Invalid);
        }
        else
        {
            Assert.Empty(q.Invalid);
            Assert.DoesNotContain(input, q.Text);
        }
    }
}
