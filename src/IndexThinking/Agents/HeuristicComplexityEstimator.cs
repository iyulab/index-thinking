using System.Text.RegularExpressions;
using IndexThinking.Abstractions;
using IndexThinking.Core;
using Microsoft.Extensions.AI;

namespace IndexThinking.Agents;

/// <summary>
/// Heuristic-based complexity estimator using text analysis.
/// </summary>
/// <remarks>
/// Analyzes:
/// - Message length and count
/// - Keyword detection (debug, analyze, research, explain, etc.)
/// - Code block presence
/// - Question complexity markers
///
/// Based on TALE research showing that simple tasks need less reasoning tokens.
/// </remarks>
public sealed partial class HeuristicComplexityEstimator : IComplexityEstimator
{
    private readonly ITokenCounter _tokenCounter;

    // Complexity signal keywords (case-insensitive matching)
    private static readonly string[] ResearchKeywords =
        ["research", "investigate", "comprehensive", "in-depth", "analyze thoroughly", "deep dive", "exhaustive"];
    private static readonly string[] ComplexKeywords =
        ["debug", "fix", "refactor", "optimize", "implement", "design", "architect", "troubleshoot", "diagnose"];
    private static readonly string[] ModerateKeywords =
        ["explain", "summarize", "describe", "compare", "list", "outline", "clarify", "review"];

    // Threshold constants
    private const int ShortMessageTokens = 50;
    private const int LongMessageTokens = 500;
    private const int CodeBlockBoost = 1;

    // Budget configurations per complexity level
    private static readonly BudgetConfig SimpleBudget = new()
    {
        ThinkingBudget = 1024,
        AnswerBudget = 2048,
        MaxContinuations = 2
    };

    private static readonly BudgetConfig ModerateBudget = new()
    {
        ThinkingBudget = 4096,
        AnswerBudget = 4096,
        MaxContinuations = 3
    };

    private static readonly BudgetConfig ComplexBudget = new()
    {
        ThinkingBudget = 8192,
        AnswerBudget = 4096,
        MaxContinuations = 5
    };

    private static readonly BudgetConfig ResearchBudget = new()
    {
        ThinkingBudget = 16384,
        AnswerBudget = 8192,
        MaxContinuations = 7
    };

    /// <summary>
    /// Creates a new heuristic complexity estimator.
    /// </summary>
    /// <param name="tokenCounter">Token counter for message length analysis.</param>
    public HeuristicComplexityEstimator(ITokenCounter tokenCounter)
    {
        _tokenCounter = tokenCounter ?? throw new ArgumentNullException(nameof(tokenCounter));
    }

    /// <inheritdoc />
    public TaskComplexity Estimate(IReadOnlyList<ChatMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        if (messages.Count == 0)
        {
            return TaskComplexity.Simple;
        }

        // Get the latest user message for primary analysis
        var userMessage = messages
            .LastOrDefault(m => m.Role == ChatRole.User);

        if (userMessage is null)
        {
            return TaskComplexity.Simple;
        }

        var text = GetMessageText(userMessage);
        var score = CalculateComplexityScore(text, messages.Count);

        return score switch
        {
            >= 4 => TaskComplexity.Research,
            >= 2 => TaskComplexity.Complex,
            >= 1 => TaskComplexity.Moderate,
            _ => TaskComplexity.Simple
        };
    }

    /// <inheritdoc />
    public BudgetConfig GetRecommendedBudget(TaskComplexity complexity)
    {
        return complexity switch
        {
            TaskComplexity.Simple => SimpleBudget,
            TaskComplexity.Moderate => ModerateBudget,
            TaskComplexity.Complex => ComplexBudget,
            TaskComplexity.Research => ResearchBudget,
            _ => ModerateBudget
        };
    }

    private int CalculateComplexityScore(string text, int messageCount)
    {
        var score = 0;
        var lowerText = text.ToLowerInvariant();

        // Count research keyword matches (multiple matches = stronger signal)
        var researchMatches = ResearchKeywords.Count(k => lowerText.Contains(k, StringComparison.OrdinalIgnoreCase));
        if (researchMatches >= 2)
        {
            score += 4; // Strong research signal - directly triggers Research level
        }
        else if (researchMatches == 1)
        {
            score += 2;
        }

        // Check for complex keywords
        if (ComplexKeywords.Any(k => lowerText.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            score += 2;
        }

        // Check for moderate keywords
        if (ModerateKeywords.Any(k => lowerText.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            score += 1;
        }

        // Check for code blocks
        if (CodeBlockRegex().IsMatch(text))
        {
            score += CodeBlockBoost;
        }

        // Message length analysis
        var tokenCount = _tokenCounter.Count(text);
        if (tokenCount > LongMessageTokens)
        {
            score += 1;
        }
        else if (tokenCount < ShortMessageTokens)
        {
            score -= 1;
        }

        // Multi-turn conversation boost
        if (messageCount > 4)
        {
            score += 1;
        }

        // Ensure score doesn't go below 0
        return Math.Max(0, score);
    }

    private static string GetMessageText(ChatMessage message)
    {
        if (message.Text is not null)
        {
            return message.Text;
        }

        // Concatenate text from all content parts
        return string.Join(" ", message.Contents
            .OfType<TextContent>()
            .Select(c => c.Text));
    }

    [GeneratedRegex(@"```[\s\S]*?```|`[^`]+`", RegexOptions.Compiled)]
    private static partial Regex CodeBlockRegex();
}
