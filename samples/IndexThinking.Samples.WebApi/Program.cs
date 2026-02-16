using System.ClientModel;
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

// Get provider from environment
var provider = Environment.GetEnvironmentVariable("AI_PROVIDER")?.ToLowerInvariant() ?? "gpustack";

// Register chat client based on provider
RegisterChatClient(builder.Services, provider);

var app = builder.Build();

// Chat endpoint
app.MapPost("/api/chat", async (ChatRequest request, IChatClient chatClient) =>
{
    var messages = new List<ChatMessage>();
    
    // Add conversation history
    if (request.History is not null)
    {
        foreach (var msg in request.History)
        {
            var role = string.Equals(msg.Role, "user", StringComparison.OrdinalIgnoreCase) ? ChatRole.User : ChatRole.Assistant;
            messages.Add(new ChatMessage(role, msg.Content));
        }
    }
    
    // Add current message
    messages.Add(new ChatMessage(ChatRole.User, request.Message));
    
    var options = new ChatOptions();
    options.AdditionalProperties ??= [];
    options.AdditionalProperties["SessionId"] = request.SessionId ?? Guid.NewGuid().ToString("N")[..8];
    
    var response = await chatClient.GetResponseAsync(messages, options);
    
    // Extract thinking and metrics if available
    var thinkingContent = response.GetThinkingContent();
    var metrics = response.GetTurnMetrics();
    
    return Results.Ok(new ChatResponse
    {
        Text = response.Text ?? "",
        Thinking = thinkingContent is not null ? new ThinkingInfo
        {
            TokenCount = thinkingContent.TokenCount,
            IsSummarized = thinkingContent.IsSummarized,
            Preview = thinkingContent.Text.Length > 200 
                ? thinkingContent.Text[..200] + "..." 
                : thinkingContent.Text
        } : null,
        Metrics = metrics is not null ? new MetricsInfo
        {
            InputTokens = metrics.InputTokens,
            ThinkingTokens = metrics.ThinkingTokens,
            OutputTokens = metrics.OutputTokens,
            ContinuationCount = metrics.ContinuationCount
        } : null
    });
});

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", provider }));

Console.WriteLine("===========================================");
Console.WriteLine("  IndexThinking WebApi Sample");
Console.WriteLine($"  Provider: {provider.ToUpperInvariant()}");
Console.WriteLine("===========================================");
Console.WriteLine();
Console.WriteLine("Endpoints:");
Console.WriteLine("  POST /api/chat - Send chat message");
Console.WriteLine("  GET  /health   - Health check");
Console.WriteLine();

app.Run();

static void RegisterChatClient(IServiceCollection services, string provider)
{
    switch (provider)
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

// Request/Response models — minimal API sample, top-level statements require global scope
#pragma warning disable CA1050 // Declare types in namespaces
public record ChatRequest
{
    public required string Message { get; init; }
    public string? SessionId { get; init; }
    public List<HistoryMessage>? History { get; init; }
}

public record HistoryMessage
{
    public required string Role { get; init; }
    public required string Content { get; init; }
}

public record ChatResponse
{
    public required string Text { get; init; }
    public ThinkingInfo? Thinking { get; init; }
    public MetricsInfo? Metrics { get; init; }
}

public record ThinkingInfo
{
    public int TokenCount { get; init; }
    public bool IsSummarized { get; init; }
    public string? Preview { get; init; }
}

public record MetricsInfo
{
    public int InputTokens { get; init; }
    public int ThinkingTokens { get; init; }
    public int OutputTokens { get; init; }
    public int ContinuationCount { get; init; }
}
