namespace IndexThinking.Client;

/// <summary>
/// Incremental state machine that separates inline XML-tag reasoning (e.g. <c>&lt;think&gt;…&lt;/think&gt;</c>)
/// from answer text in a streaming response, tolerating tags that are split across chunk boundaries
/// (<c>&lt;thi</c> + <c>nk&gt;</c>). Open-source / local providers (DeepSeek, Qwen3, vLLM-served models, etc.)
/// emit reasoning inline in the text delta rather than as a separate reasoning channel; this lets a single
/// consumer render live reasoning vs. answer without writing a bespoke tag splitter.
/// </summary>
/// <remarks>
/// Stateful and NOT thread-safe — use one instance per stream. Native reasoning providers that already emit
/// a distinct reasoning channel never carry these tags in their text deltas, so feeding their text through
/// this separator is a no-op (everything is classified as answer text and passes through unchanged).
/// </remarks>
internal sealed class StreamingReasoningSeparator
{
    private readonly string _startTag;
    private readonly string _endTag;
    private bool _inReasoning;
    private string _buffer = string.Empty;

    public StreamingReasoningSeparator(string startTag, string endTag)
    {
        if (string.IsNullOrEmpty(startTag)) throw new ArgumentException("Start tag must be non-empty.", nameof(startTag));
        if (string.IsNullOrEmpty(endTag)) throw new ArgumentException("End tag must be non-empty.", nameof(endTag));
        _startTag = startTag;
        _endTag = endTag;
    }

    /// <summary>
    /// Feeds a text delta and returns the classified segments to emit now. A trailing run that could be the
    /// start of a tag is withheld (buffered) until the next delta or <see cref="Flush"/> disambiguates it.
    /// </summary>
    public IReadOnlyList<Segment> Process(string delta)
    {
        var segments = new List<Segment>();
        if (string.IsNullOrEmpty(delta) && _buffer.Length == 0)
            return segments;

        var work = _buffer + delta;
        _buffer = string.Empty;

        var pos = 0;
        while (pos < work.Length)
        {
            var token = _inReasoning ? _endTag : _startTag;
            var idx = work.IndexOf(token, pos, StringComparison.Ordinal);
            if (idx >= 0)
            {
                if (idx > pos)
                    segments.Add(new Segment(work[pos..idx], _inReasoning));
                pos = idx + token.Length;
                _inReasoning = !_inReasoning;
                continue;
            }

            // No complete token from pos onward. Emit what is unambiguously text, and buffer the longest
            // trailing run that could be the prefix of a tag split across the chunk boundary.
            var remaining = work[pos..];
            var keep = LongestTokenPrefixSuffixLength(remaining, token);
            var emitLen = remaining.Length - keep;
            if (emitLen > 0)
                segments.Add(new Segment(remaining[..emitLen], _inReasoning));
            _buffer = remaining[emitLen..];
            break;
        }

        return segments;
    }

    /// <summary>
    /// Returns any buffered text at end-of-stream (an unterminated partial tag is treated as literal text,
    /// classified by the current state), or <c>null</c> if nothing is buffered.
    /// </summary>
    public Segment? Flush()
    {
        if (_buffer.Length == 0)
            return null;
        var segment = new Segment(_buffer, _inReasoning);
        _buffer = string.Empty;
        return segment;
    }

    /// <summary>
    /// Length of the longest suffix of <paramref name="text"/> that equals a prefix of <paramref name="token"/>
    /// (strictly shorter than the full token — a full match is handled by the caller's IndexOf).
    /// </summary>
    private static int LongestTokenPrefixSuffixLength(string text, string token)
    {
        var max = Math.Min(text.Length, token.Length - 1);
        for (var k = max; k > 0; k--)
        {
            if (text.AsSpan(text.Length - k).SequenceEqual(token.AsSpan(0, k)))
                return k;
        }
        return 0;
    }

    /// <summary>A classified text segment: reasoning vs. answer.</summary>
    public readonly record struct Segment(string Text, bool IsReasoning);
}
