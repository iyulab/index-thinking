using IndexThinking.Reduction;
using Xunit;

namespace IndexThinking.Tests.Reduction;

/// <summary>
/// <see cref="ToolResultTrimmer"/> — a loop stores what it sent, so the trimmer is handed its own output on the next request:
/// that output has to be within the limit, or it is trimmed again and the marker's total describes the trimmed text.
/// </summary>
public class ToolResultTrimmerTests
{
    [Fact]
    public void Trim_ManyShortLines_KeepsHeadAndTailAroundAMarker()
    {
        var trimmer = new ToolResultTrimmer(maxChars: 1_000, keepHeadLines: 3, keepTailLines: 2);
        var text = string.Join('\n', Enumerable.Range(1, 200).Select(i => $"line {i}"));

        var trimmed = trimmer.Trim(text);

        Assert.StartsWith("line 1\nline 2\nline 3\n", trimmed, StringComparison.Ordinal);
        Assert.EndsWith("line 199\nline 200", trimmed, StringComparison.Ordinal);
        Assert.Contains($"195 lines omitted ({text.Length:N0} chars total)", trimmed, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]   // one long line: the character cut
    [InlineData(80)]  // more lines than head + tail, each long: head + tail alone are over the limit
    public void Trim_OutputIsWithinTheLimit_AndTrimmingItAgainChangesNothing(int lineCount)
    {
        var trimmer = new ToolResultTrimmer(maxChars: 2_000, keepHeadLines: 50, keepTailLines: 20);
        var text = string.Join('\n', Enumerable.Range(1, lineCount).Select(i => new string((char)('a' + i % 26), 6_000 / lineCount + 100)));

        var once = trimmer.Trim(text);
        var twice = trimmer.Trim(once);

        Assert.True(once.Length <= trimmer.MaxChars, $"{once.Length} > {trimmer.MaxChars}");
        Assert.Equal(once, twice);
        Assert.Contains($"({text.Length:N0} chars total)", once, StringComparison.Ordinal);
    }

    [Fact]
    public void Trim_TextWithinTheLimit_IsReturnedAsItIs()
    {
        var trimmer = new ToolResultTrimmer(maxChars: 100);
        const string text = "short";

        Assert.Same(text, trimmer.Trim(text));
    }
}
