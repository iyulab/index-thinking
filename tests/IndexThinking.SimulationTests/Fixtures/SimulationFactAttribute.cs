using DotNetEnv;
using Xunit;

namespace IndexThinking.SimulationTests.Fixtures;

/// <summary>
/// Marks a test as a simulation test that requires real API access.
/// Tests with this attribute are skipped in CI environments.
/// </summary>
public class SimulationFactAttribute : FactAttribute
{
    private static readonly Lazy<bool> _envLoaded = new(() =>
    {
        // Try to load .env file
        var current = Directory.GetCurrentDirectory();
        for (var i = 0; i < 10; i++)
        {
            var envPath = Path.Combine(current, ".env");
            if (File.Exists(envPath))
            {
                Env.Load(envPath);
                return true;
            }

            var parent = Directory.GetParent(current);
            if (parent is null) break;
            current = parent.FullName;
        }
        return false;
    });

    public SimulationFactAttribute()
    {
        // Force env loading
        _ = _envLoaded.Value;
    }
}

/// <summary>
/// Requires GPUStack to be configured via environment variables.
/// </summary>
public sealed class GpuStackFactAttribute : SimulationFactAttribute
{
    public GpuStackFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GPUSTACK_URL")) ||
            string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GPUSTACK_APIKEY")))
        {
            Skip = "GPUStack not configured. Set GPUSTACK_URL and GPUSTACK_APIKEY in .env file.";
        }
    }
}

/// <summary>
/// Requires OpenAI to be configured via environment variables.
/// </summary>
public sealed class OpenAIFactAttribute : SimulationFactAttribute
{
    public OpenAIFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY")))
        {
            Skip = "OpenAI not configured. Set OPENAI_API_KEY in .env file.";
        }
    }
}

/// <summary>
/// Requires Anthropic to be configured via environment variables.
/// </summary>
public sealed class AnthropicFactAttribute : SimulationFactAttribute
{
    public AnthropicFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")))
        {
            Skip = "Anthropic not configured. Set ANTHROPIC_API_KEY in .env file.";
        }
    }
}

/// <summary>
/// Requires Google to be configured via environment variables.
/// </summary>
public sealed class GoogleFactAttribute : SimulationFactAttribute
{
    public GoogleFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GOOGLE_API_KEY")))
        {
            Skip = "Google not configured. Set GOOGLE_API_KEY in .env file.";
        }
    }
}

/// <summary>
/// Requires any LLM provider to be configured via environment variables.
/// </summary>
public sealed class AnyProviderFactAttribute : SimulationFactAttribute
{
    public AnyProviderFactAttribute()
    {
        var hasGpuStack = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GPUSTACK_URL")) &&
                          !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GPUSTACK_APIKEY"));
        var hasOpenAI = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
        var hasAnthropic = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"));
        var hasGoogle = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GOOGLE_API_KEY"));

        if (!hasGpuStack && !hasOpenAI && !hasAnthropic && !hasGoogle)
        {
            Skip = "No LLM provider configured. Set GPUSTACK_*, OPENAI_API_KEY, ANTHROPIC_API_KEY, or GOOGLE_API_KEY in .env file.";
        }
    }
}

/// <summary>
/// Marks a test as a simulation theory (parameterized test) that requires real API access.
/// </summary>
public sealed class SimulationTheoryAttribute : TheoryAttribute
{
    private static readonly Lazy<bool> _envLoaded = new(() =>
    {
        var current = Directory.GetCurrentDirectory();
        for (var i = 0; i < 10; i++)
        {
            var envPath = Path.Combine(current, ".env");
            if (File.Exists(envPath))
            {
                Env.Load(envPath);
                return true;
            }

            var parent = Directory.GetParent(current);
            if (parent is null) break;
            current = parent.FullName;
        }
        return false;
    });

    public SimulationTheoryAttribute()
    {
        _ = _envLoaded.Value;
    }
}
