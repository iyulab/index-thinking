using System.ClientModel;
using Anthropic;
using DotNetEnv;
using GeminiDotnet;
using GeminiDotnet.Extensions.AI;
using IndexThinking.Client;
using IndexThinking.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;

namespace IndexThinking.SimulationTests.Fixtures;

/// <summary>
/// Fixture for simulation tests that loads environment variables
/// and provides configured chat clients for various providers.
/// </summary>
public class SimulationTestFixture : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private bool _disposed;

    public SimulationTestFixture()
    {
        // Load .env file from solution root
        var envPath = FindEnvFile();
        if (envPath is not null)
        {
            Env.Load(envPath);
            IsConfigured = true;
        }
        else
        {
            IsConfigured = false;
        }

        // Build service provider
        var services = new ServiceCollection();
        services.AddIndexThinkingAgents();
        services.AddIndexThinkingContext();
        services.AddIndexThinkingInMemoryStorage();
        services.AddIndexThinkingMetrics();

        _serviceProvider = services.BuildServiceProvider();
    }

    /// <summary>
    /// Whether environment variables are properly configured.
    /// </summary>
    public bool IsConfigured { get; }

    /// <summary>
    /// The service provider with IndexThinking services registered.
    /// </summary>
    public IServiceProvider ServiceProvider => _serviceProvider;

    /// <summary>
    /// Gets a value indicating whether GPUStack is configured.
    /// </summary>
    public bool HasGpuStack =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GPUSTACK_URL")) &&
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GPUSTACK_APIKEY"));

    /// <summary>
    /// Gets a value indicating whether OpenAI is configured.
    /// </summary>
    public bool HasOpenAI =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY"));

    /// <summary>
    /// Gets a value indicating whether Anthropic is configured.
    /// </summary>
    public bool HasAnthropic =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"));

    /// <summary>
    /// Gets a value indicating whether Google is configured.
    /// </summary>
    public bool HasGoogle =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GOOGLE_API_KEY"));

    /// <summary>
    /// Creates an IChatClient for GPUStack with IndexThinking pipeline.
    /// </summary>
    public IChatClient CreateGpuStackClient()
    {
        var url = Environment.GetEnvironmentVariable("GPUSTACK_URL")
            ?? throw new InvalidOperationException("GPUSTACK_URL not set");
        var apiKey = Environment.GetEnvironmentVariable("GPUSTACK_APIKEY")
            ?? throw new InvalidOperationException("GPUSTACK_APIKEY not set");
        var model = Environment.GetEnvironmentVariable("GPUSTACK_MODEL") ?? "gpt-oss-20b";

        var credential = new ApiKeyCredential(apiKey);
        var options = new OpenAIClientOptions { Endpoint = new Uri(url) };
        var openAiClient = new OpenAIClient(credential, options);

        var innerClient = openAiClient.GetChatClient(model).AsIChatClient();
        return new ChatClientBuilder(innerClient)
            .UseIndexThinking()
            .Build(_serviceProvider);
    }

    /// <summary>
    /// Creates an IChatClient for OpenAI with IndexThinking pipeline.
    /// </summary>
    public IChatClient CreateOpenAIClient(string? modelOverride = null)
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? throw new InvalidOperationException("OPENAI_API_KEY not set");
        var model = modelOverride ?? Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini";

        var openAiClient = new OpenAIClient(apiKey);
        var innerClient = openAiClient.GetChatClient(model).AsIChatClient();

        return new ChatClientBuilder(innerClient)
            .UseIndexThinking()
            .Build(_serviceProvider);
    }

    /// <summary>
    /// Creates a raw IChatClient for GPUStack without IndexThinking pipeline.
    /// Useful for comparing behavior.
    /// </summary>
    public IChatClient CreateRawGpuStackClient()
    {
        var url = Environment.GetEnvironmentVariable("GPUSTACK_URL")
            ?? throw new InvalidOperationException("GPUSTACK_URL not set");
        var apiKey = Environment.GetEnvironmentVariable("GPUSTACK_APIKEY")
            ?? throw new InvalidOperationException("GPUSTACK_APIKEY not set");
        var model = Environment.GetEnvironmentVariable("GPUSTACK_MODEL") ?? "gpt-oss-20b";

        var credential = new ApiKeyCredential(apiKey);
        var options = new OpenAIClientOptions { Endpoint = new Uri(url) };
        var openAiClient = new OpenAIClient(credential, options);

        return openAiClient.GetChatClient(model).AsIChatClient();
    }

    /// <summary>
    /// Creates a raw IChatClient for OpenAI without IndexThinking pipeline.
    /// </summary>
    public IChatClient CreateRawOpenAIClient(string? modelOverride = null)
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? throw new InvalidOperationException("OPENAI_API_KEY not set");
        var model = modelOverride ?? Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini";

        var openAiClient = new OpenAIClient(apiKey);
        return openAiClient.GetChatClient(model).AsIChatClient();
    }

    /// <summary>
    /// Creates an IChatClient for Anthropic with IndexThinking pipeline.
    /// </summary>
    public IChatClient CreateAnthropicClient(string? modelOverride = null)
    {
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
            ?? throw new InvalidOperationException("ANTHROPIC_API_KEY not set");
        var model = modelOverride ?? "claude-sonnet-4-20250514";

        var anthropicClient = new AnthropicClient(new() { ApiKey = apiKey });
        var innerClient = anthropicClient.AsIChatClient(model);

        return new ChatClientBuilder(innerClient)
            .UseIndexThinking()
            .Build(_serviceProvider);
    }

    /// <summary>
    /// Creates a raw IChatClient for Anthropic without IndexThinking pipeline.
    /// </summary>
    public IChatClient CreateRawAnthropicClient(string? modelOverride = null)
    {
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
            ?? throw new InvalidOperationException("ANTHROPIC_API_KEY not set");
        var model = modelOverride ?? "claude-sonnet-4-20250514";

        var anthropicClient = new AnthropicClient(new() { ApiKey = apiKey });
        return anthropicClient.AsIChatClient(model);
    }

    /// <summary>
    /// Creates an IChatClient for Google Gemini with IndexThinking pipeline.
    /// </summary>
    public IChatClient CreateGoogleClient(string? modelOverride = null)
    {
        var apiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY")
            ?? throw new InvalidOperationException("GOOGLE_API_KEY not set");
        var model = modelOverride ?? "gemini-2.0-flash";

        var options = new GeminiClientOptions
        {
            ApiKey = apiKey,
            ModelId = model
        };
        var innerClient = new GeminiChatClient(options);

        return new ChatClientBuilder(innerClient)
            .UseIndexThinking()
            .Build(_serviceProvider);
    }

    /// <summary>
    /// Creates a raw IChatClient for Google Gemini without IndexThinking pipeline.
    /// </summary>
    public IChatClient CreateRawGoogleClient(string? modelOverride = null)
    {
        var apiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY")
            ?? throw new InvalidOperationException("GOOGLE_API_KEY not set");
        var model = modelOverride ?? "gemini-2.0-flash";

        var options = new GeminiClientOptions
        {
            ApiKey = apiKey,
            ModelId = model
        };
        return new GeminiChatClient(options);
    }

    private static string? FindEnvFile()
    {
        // Try to find .env file starting from current directory and going up
        var current = Directory.GetCurrentDirectory();

        for (var i = 0; i < 10; i++)
        {
            var envPath = Path.Combine(current, ".env");
            if (File.Exists(envPath))
            {
                return envPath;
            }

            var parent = Directory.GetParent(current);
            if (parent is null)
            {
                break;
            }

            current = parent.FullName;
        }

        return null;
    }

    public void Dispose()
    {
        if (_disposed) return;

        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
