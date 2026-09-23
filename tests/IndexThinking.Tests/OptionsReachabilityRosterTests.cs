using System.Reflection;
using Iyu.Conventions.Testing;
using Xunit;

namespace IndexThinking.Tests;

/// <summary>
/// Every public option in this library is read by the library. An option nothing reads is a promise it does not keep:
/// a caller sets it, and nothing changes and nothing is reported. The roster fails both ways — a new unread option,
/// and a listed one that has since been wired — so each change is recorded on purpose.
/// </summary>
public class OptionsReachabilityRosterTests
{
    private static readonly Assembly[] Libraries = [Assembly.Load("IndexThinking")];

    /// <summary>Options accepted as unread today, each with the reason. Shrink this list; never grow it silently.</summary>
    private static readonly Dictionary<string, string[]> KnownUnread = new()
    {
        // A write-through convenience: its setter applies the value to DefaultContinuation, which the client reads.
        // The getter only echoes what was set, so the scan (which looks for getter reads) cannot see the effect.
        ["IndexThinking.Client.ThinkingChatClientOptions"] = ["MaxContextTokens"],
    };

    // The provider request models under Parsers.Models are wire DTOs (serialized by reflection), not options.
    private static bool IsOptions(Type type) =>
        OptionsTypes.NamedWith("Options", "Config", "Settings")(type) && type.Namespace != "IndexThinking.Parsers.Models";

    [Fact]
    public void EveryPublicOption_IsRead() =>
        OptionsReachability.Scan(Libraries, IsOptions).ShouldMatchRoster(KnownUnread);
}
