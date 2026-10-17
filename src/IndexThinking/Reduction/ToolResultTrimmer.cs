using System.Globalization;

namespace IndexThinking.Reduction;

/// <summary>
/// Shortens a tool result over a size limit to its first and last lines, with a marker saying how much was left out —
/// the start of a result (headers, the first matches) and its end (totals, the final error) are what a model usually
/// needs. A result without enough lines is cut at the limit instead.
/// </summary>
public sealed class ToolResultTrimmer
{
    /// <summary>Creates a trimmer.</summary>
    /// <param name="maxChars">Longest result, in characters, kept whole. Default 30 000.</param>
    /// <param name="keepHeadLines">Lines kept from the start of a longer result. Default 50.</param>
    /// <param name="keepTailLines">Lines kept from its end. Default 20.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxChars"/> is not positive, or a line count is negative.</exception>
    public ToolResultTrimmer(int maxChars = 30_000, int keepHeadLines = 50, int keepTailLines = 20)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxChars);
        ArgumentOutOfRangeException.ThrowIfNegative(keepHeadLines);
        ArgumentOutOfRangeException.ThrowIfNegative(keepTailLines);
        MaxChars = maxChars;
        KeepHeadLines = keepHeadLines;
        KeepTailLines = keepTailLines;
    }

    /// <summary>Longest result kept whole.</summary>
    public int MaxChars { get; }

    /// <summary>Lines kept from the start of a longer result.</summary>
    public int KeepHeadLines { get; }

    /// <summary>Lines kept from the end of a longer result.</summary>
    public int KeepTailLines { get; }

    /// <summary>
    /// <paramref name="text"/> itself when it is within <see cref="MaxChars"/>; otherwise its first and last lines around a
    /// «lines omitted» marker, or — when it has too few lines for that — its first <see cref="MaxChars"/> characters and a
    /// «truncated» marker. The result is within <see cref="MaxChars"/> (unless the marker alone is longer), so trimming it
    /// again returns it unchanged — a loop that stores what it sent hands it back on the next request.
    /// </summary>
    public string Trim(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length <= MaxChars)
        {
            return text;
        }

        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var keep = KeepHeadLines + KeepTailLines;
        if (lines.Length > keep && keep > 0)
        {
            var byLines = string.Concat(
                string.Join('\n', lines.AsSpan(0, KeepHeadLines).ToArray()),
                string.Create(CultureInfo.InvariantCulture, $"\n\n[... {lines.Length - keep:N0} lines omitted ({text.Length:N0} chars total) ...]\n\n"),
                string.Join('\n', lines.AsSpan(lines.Length - KeepTailLines).ToArray()));
            if (byLines.Length <= MaxChars)
            {
                return byLines;
            }
        }

        // Too few lines, or lines so long that the head and tail alone are over the limit: the first characters, with room
        // left for the marker
        var marker = string.Create(CultureInfo.InvariantCulture, $"\n[... truncated ({text.Length:N0} chars total) ...]");
        return string.Concat(text.AsSpan(0, Math.Max(0, MaxChars - marker.Length)), marker);
    }
}
