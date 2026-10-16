namespace IndexThinking.Tokenization;

/// <summary>
/// Learns how far a counter's estimates are from what one model's provider reports, from each response's
/// <c>Usage.InputTokenCount</c> against the estimate for the same request, and scales later estimates by it. An approximate
/// counter is fitted to one tokenizer (o200k_base); a model with another one (most local models, most on Korean) is under- or
/// over-counted by a steady factor that this recovers within a few requests.
/// </summary>
/// <remarks>
/// One instance per model: the factor of one tokenizer is wrong for another. The factor moves by an exponential moving
/// average (each observation weighs <see cref="Weight"/>), is clamped to [<see cref="MinFactor"/>, <see cref="MaxFactor"/>],
/// and ignores requests smaller than <see cref="MinObservedTokens"/>, where a provider's own template tokens dominate.
/// </remarks>
public sealed class TokenEstimateCalibration
{
    /// <summary>How much one observation moves the factor.</summary>
    public const double Weight = 0.3;

    /// <summary>The lowest factor: an estimate is never scaled below three quarters of itself.</summary>
    public const double MinFactor = 0.75;

    /// <summary>The highest factor.</summary>
    public const double MaxFactor = 3.0;

    /// <summary>Requests estimated below this are not observed.</summary>
    public const int MinObservedTokens = 256;

    private readonly Lock _lock = new();
    private bool _observed;

    /// <summary>The factor estimates are multiplied by; 1 before any observation.</summary>
    public double Factor { get; private set; } = 1.0;

    /// <summary>Records what the provider counted for a request whose (unscaled) estimate was <paramref name="estimated"/>.</summary>
    public void Observe(int estimated, long actual)
    {
        if (estimated < MinObservedTokens || actual <= 0)
        {
            return;
        }

        var ratio = Math.Clamp(actual / (double)estimated, MinFactor, MaxFactor);
        lock (_lock)
        {
            Factor = _observed ? Math.Clamp((Factor * (1 - Weight)) + (ratio * Weight), MinFactor, MaxFactor) : ratio;
            _observed = true;
        }
    }

    /// <summary>The estimate scaled by what has been learned.</summary>
    public int Scale(int estimated) => (int)Math.Ceiling(estimated * Factor);
}
