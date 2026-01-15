using System.ClientModel;
using System.Text.Json;
using DotNetEnv;
using IndexThinking.Client;
using IndexThinking.Extensions;
using Microsoft.Extensions.AI;
using OpenAI;

// Load environment variables from .env file
Env.Load(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".env"));

var builder = WebApplication.CreateBuilder(args);

// Configure IndexThinking services
builder.Services.AddIndexThinkingAgents();
builder.Services.AddIndexThinkingContext();
builder.Services.AddIndexThinkingInMemoryStorage();
builder.Services.AddIndexThinkingMetrics();

// Get provider from environment or configuration
var provider = builder.Configuration["Provider"] ?? "gpustack";

// Register chat client based on provider
RegisterChatClient(builder.Services, provider);

var app = builder.Build();

// Chat endpoint - non-streaming
app.MapPost("/chat", async (ChatRequest request, IChatClient chatClient) =>
{
    var messages = new List<ChatMessage>
    {
        new(ChatRole.User, request.Message)
    };

    if (!string.IsNullOrEmpty(request.SystemPrompt))
    {
        messages.Insert(0, new ChatMessage(ChatRole.System, request.SystemPrompt));
    }

    var options = new ChatOptions();
    options.AdditionalProperties ??= [];
    options.AdditionalProperties["SessionId"] = request.SessionId ?? Guid.NewGuid().ToString("N")[..8];

    var response = await chatClient.GetResponseAsync(messages, options);

    var thinkingContent = response.GetThinkingContent();
    var metrics = response.GetTurnMetrics();

    return Results.Ok(new ChatResponse
    {
        Text = response.Text ?? "",
        ThinkingTokens = thinkingContent?.TokenCount ?? 0,
        ThinkingSummary = thinkingContent?.IsSummarized == true ? thinkingContent.Text[..Math.Min(200, thinkingContent.Text.Length)] : null,
        InputTokens = metrics?.InputTokens ?? 0,
        OutputTokens = metrics?.OutputTokens ?? 0,
        ContinuationCount = metrics?.ContinuationCount ?? 0
    });
});

// Chat endpoint - streaming
app.MapPost("/chat/stream", async (ChatRequest request, IChatClient chatClient, HttpContext context) =>
{
    var messages = new List<ChatMessage>
    {
        new(ChatRole.User, request.Message)
    };

    if (!string.IsNullOrEmpty(request.SystemPrompt))
    {
        messages.Insert(0, new ChatMessage(ChatRole.System, request.SystemPrompt));
    }

    var options = new ChatOptions();
    options.AdditionalProperties ??= [];
    options.AdditionalProperties["SessionId"] = request.SessionId ?? Guid.NewGuid().ToString("N")[..8];

    context.Response.ContentType = "text/event-stream";
    context.Response.Headers.CacheControl = "no-cache";
    context.Response.Headers.Connection = "keep-alive";

    await foreach (var update in chatClient.GetStreamingResponseAsync(messages, options))
    {
        if (!string.IsNullOrEmpty(update.Text))
        {
            var data = JsonSerializer.Serialize(new { text = update.Text });
            await context.Response.WriteAsync($"data: {data}\n\n");
            await context.Response.Body.FlushAsync();
        }
    }

    await context.Response.WriteAsync("data: [DONE]\n\n");
});

// Health check
app.MapGet("/health", () => Results.Ok(new { status = "healthy", provider }));

Console.WriteLine("===========================================");
Console.WriteLine("  IndexThinking Web API Sample");
Console.WriteLine($"  Provider: {provider.ToUpperInvariant()}");
Console.WriteLine($"  Endpoints: POST /chat, POST /chat/stream");
Console.WriteLine("===========================================");

app.Run();

static void RegisterChatClient(IServiceCollection services, string provider)
{
    switch (provider.ToLowerInvariant())
    {
        case "gpustack":
            RegisterGpuStackClient(services);
            break;
        case "openai":
            RegisterOpenAIClient(services);
            break;
        default:
            Console.WriteLine($"Unknown provider: {provider}. Using GPUStack.");
            RegisterGpuStackClient(services);
            break;
    }
}

static void RegisterGpuStackClient(IServiceCollection services)
{
    var url = Environment.GetEnvironmentVariable("GPUSTACK_URL")
        ?? throw new InvalidOperationException("GPUSTACK_URL not set");
    var apiKey = Environment.GetEnvironmentVariable("GPUSTACK_APIKEY")
        ?? throw new InvalidOperationException("GPUSTACK_APIKEY not set");
    var model = Environment.GetEnvironmentVariable("GPUSTACK_MODEL") ?? "gpt-oss-20b";

    var credential = new ApiKeyCredential(apiKey);
    var options = new OpenAIClientOptions { Endpoint = new Uri(url) };
    var openAiClient = new OpenAIClient(credential, options);

    services.AddChatClient(sp =>
    {
        var innerClient = openAiClient.GetChatClient(model).AsIChatClient();
        return new ChatClientBuilder(innerClient)
            .UseIndexThinking()
            .Build(sp);
    });
}

static void RegisterOpenAIClient(IServiceCollection services)
{
    var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
        ?? throw new InvalidOperationException("OPENAI_API_KEY not set");
    var model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini";

    var openAiClient = new OpenAIClient(apiKey);

    services.AddChatClient(sp =>
    {
        var innerClient = openAiClient.GetChatClient(model).AsIChatClient();
        return new ChatClientBuilder(innerClient)
            .UseIndexThinking()
            .Build(sp);
    });
}

// Request/Response models
public record ChatRequest(
    string Message,
    string? SystemPrompt = null,
    string? SessionId = null
);

public record ChatResponse
{
    public required string Text { get; init; }
    public int ThinkingTokens { get; init; }
    public string? ThinkingSummary { get; init; }
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public int ContinuationCount { get; init; }
}
