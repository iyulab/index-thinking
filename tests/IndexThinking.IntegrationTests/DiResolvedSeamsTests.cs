using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using IndexThinking.Abstractions;
using IndexThinking.Client;
using IndexThinking.Diagnostics;
using IndexThinking.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IndexThinking.IntegrationTests;

/// <summary>
/// The DI form of <c>UseIndexThinking()</c> uses what the container holds: the metrics registered by
/// <c>AddIndexThinkingMetrics()</c> and the modifier registry registered by <c>AddIndexThinkingReasoningModifiers(…)</c>.
/// </summary>
public class DiResolvedSeamsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AddIndexThinkingMetrics_records_the_turns_of_a_client_built_with_UseIndexThinking()
    {
        var factory = new ScopedMeterFactory();
        var services = new ServiceCollection()
            .AddSingleton<IMeterFactory>(factory)
            .AddIndexThinkingAgents()
            .AddIndexThinkingMetrics()
            .BuildServiceProvider();

        long turns = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (ReferenceEquals(instrument.Meter.Scope, factory) && instrument.Name == "indexthinking.turns.total")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => Interlocked.Add(ref turns, value));
        listener.Start();

        var client = new ChatClientBuilder(new CapturingClient("hello")).UseIndexThinking().Build(services);
        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], cancellationToken: Ct);

        Interlocked.Read(ref turns).Should().Be(1);
    }

    [Fact]
    public async Task A_registry_from_AddIndexThinkingReasoningModifiers_is_the_one_the_client_uses()
    {
        var inner = new CapturingClient("hello");
        var services = new ServiceCollection()
            .AddIndexThinkingAgents()
            .AddIndexThinkingReasoningModifiers(registry =>
            {
                registry.Register(new MarkingModifier());
                registry.RegisterModelPrefix("acme-", MarkingModifier.Family);
            })
            .BuildServiceProvider();

        var client = new ChatClientBuilder(inner)
            .UseIndexThinking(o => { o.EnableReasoning = true; o.AutoDetectReasoningRequirement = false; })
            .Build(services);
        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], new ChatOptions { ModelId = "acme-1" }, Ct);

        // Computed first: a `?.` chain ending in `.Should()` skips the assertion itself when a link is null.
        var marked = inner.LastOptions?.AdditionalProperties?.ContainsKey(MarkingModifier.Flag) ?? false;
        marked.Should().BeTrue("the modifier registered in the container must reach the request");
    }

    [Fact]
    public async Task A_registered_registry_does_not_turn_reasoning_on_by_itself()
    {
        var inner = new CapturingClient("hello");
        var services = new ServiceCollection()
            .AddIndexThinkingAgents()
            .AddIndexThinkingReasoningModifiers(registry =>
            {
                registry.Register(new MarkingModifier());
                registry.RegisterModelPrefix("acme-", MarkingModifier.Family);
            })
            .BuildServiceProvider();

        var client = new ChatClientBuilder(inner).UseIndexThinking().Build(services);
        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], new ChatOptions { ModelId = "acme-1" }, Ct);

        (inner.LastOptions?.AdditionalProperties?.ContainsKey(MarkingModifier.Flag) ?? false).Should().BeFalse(
            "EnableReasoning is off by default; registering modifiers only says how to turn it on");
    }

    private sealed class MarkingModifier : IReasoningRequestModifier
    {
        public const string Family = "acme";
        public const string Flag = "acme_reasoning";

        public string ProviderFamily => Family;

        public bool SupportsModel(string? modelId) => modelId?.StartsWith("acme-", StringComparison.Ordinal) == true;

        public ChatOptions EnableReasoning(ChatOptions? options, string? modelId)
        {
            var result = options?.Clone() ?? new ChatOptions();
            result.AdditionalProperties ??= [];
            result.AdditionalProperties[Flag] = true;
            return result;
        }
    }

    private sealed class ScopedMeterFactory : IMeterFactory
    {
        private readonly List<Meter> _meters = [];

        public Meter Create(MeterOptions options)
        {
            var meter = new Meter(new MeterOptions(options.Name) { Version = options.Version, Scope = this });
            _meters.Add(meter);
            return meter;
        }

        public void Dispose() => _meters.ForEach(m => m.Dispose());
    }

    private sealed class CapturingClient(string answer) : IChatClient
    {
        public ChatOptions? LastOptions { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            LastOptions = options;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, answer)) { FinishReason = ChatFinishReason.Stop });
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            LastOptions = options;
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, answer) { FinishReason = ChatFinishReason.Stop };
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
