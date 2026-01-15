using System.ClientModel;
using DotNetEnv;
using IndexThinking.Client;
using IndexThinking.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI;

// Load environment variables from .env file
Env.Load(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".env"));

var builder = Host.CreateApplicationBuilder(args);

// Configure IndexThinking services
builder.Services.AddIndexThinkingAgents();
builder.Services.AddIndexThinkingContext();
builder.Services.AddIndexThinkingInMemoryStorage();
builder.Services.AddIndexThinkingMetrics();

// Get provider from environment or command line
var provider = args.Length > 0 ? args[0].ToLowerInvariant() : "gpustack";

// Register chat client based on provider
RegisterChatClient(builder.Services, provider);

var host = builder.Build();

// Get the chat client
var chatClient = host.Services.GetRequiredService<IChatClient>();

Console.WriteLine("===========================================");
Console.WriteLine("  IndexThinking Console Sample");
Console.WriteLine($"  Provider: {provider.ToUpperInvariant()}");
Console.WriteLine("===========================================");
Console.WriteLine();
Console.WriteLine("Type 'exit' to quit, 'clear' to reset conversation.");
Console.WriteLine();

var sessionId = Guid.NewGuid().ToString("N")[..8];
var messages = new List<ChatMessage>();

while (true)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.Write("You: ");
    Console.ResetColor();

    var input = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(input))
        continue;

    if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
        break;

    if (input.Equals("clear", StringComparison.OrdinalIgnoreCase))
    {
        messages.Clear();
        Console.WriteLine("[Conversation cleared]");
        Console.WriteLine();
        continue;
    }

    messages.Add(new ChatMessage(ChatRole.User, input));

    try
    {
        var options = new ChatOptions();
        options.AdditionalProperties ??= [];
        options.AdditionalProperties["SessionId"] = sessionId;

        var response = await chatClient.GetResponseAsync(messages, options);

        // Display response
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("Assistant: ");
        Console.ResetColor();
        Console.WriteLine(response.Text);
        Console.WriteLine();

        // Display thinking content if available
        var thinkingContent = response.GetThinkingContent();
        if (thinkingContent is not null)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"[Thinking: {thinkingContent.TokenCount} tokens]");
            if (!thinkingContent.IsSummarized && thinkingContent.Text.Length < 500)
            {
                Console.WriteLine($"  {thinkingContent.Text[..Math.Min(200, thinkingContent.Text.Length)]}...");
            }
            Console.ResetColor();
            Console.WriteLine();
        }

        // Display metrics if available
        var metrics = response.GetTurnMetrics();
        if (metrics is not null)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"[Tokens: input={metrics.InputTokens}, thinking={metrics.ThinkingTokens}, output={metrics.OutputTokens}]");
            if (metrics.ContinuationCount > 0)
            {
                Console.WriteLine($"[Continuations: {metrics.ContinuationCount}]");
            }
            Console.ResetColor();
            Console.WriteLine();
        }

        // Add assistant response to history
        messages.Add(new ChatMessage(ChatRole.Assistant, response.Text ?? ""));
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error: {ex.Message}");
        Console.ResetColor();
        Console.WriteLine();
    }
}

Console.WriteLine("Goodbye!");

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
        case "anthropic":
            Console.WriteLine("Anthropic provider requires Microsoft.Extensions.AI.Anthropic package.");
            Console.WriteLine("Falling back to GPUStack.");
            RegisterGpuStackClient(services);
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
