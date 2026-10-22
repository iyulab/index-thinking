using System.Xml.Linq;
using AwesomeAssertions;
using IndexThinking.Stores;
using Xunit;

namespace IndexThinking.Tests;

/// <summary>
/// <c>IndexThinking</c> carries no SQLite. Before 0.23.0 every consumer shipped <c>Microsoft.Data.Sqlite</c> and the native
/// <c>e_sqlite3</c> for every RID, for a store most hosts never register; the store went to a satellite package, which is no
/// longer published. Two facts, because each alone misses a way back in: a package reference nothing
/// uses still ships as a nuspec dependency, and an assembly can arrive without a package reference of its own.
/// </summary>
public class CoreDependencyWeightTests
{
    private static bool IsSqlite(string name)
        => name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("SQLitePCL", StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void Core_project_has_no_package_reference_to_sqlite()
    {
        var csproj = Path.Combine(RepositoryRoot(), "src", "IndexThinking", "IndexThinking.csproj");

        var references = XDocument.Load(csproj).Descendants("PackageReference")
            .Select(e => (string?)e.Attribute("Include") ?? string.Empty)
            .ToList();

        references.Should().NotBeEmpty("the scan must read the real project file");
        references.Where(IsSqlite).Should().BeEmpty("a consumer would ship SQLite native binaries for every RID");
    }

    [Fact]
    public void Core_assembly_references_no_sqlite()
    {
        var referenced = typeof(InMemoryThinkingStateStore).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToList();

        referenced.Should().Contain("Microsoft.Extensions.AI.Abstractions", "positive control: the scan sees real references");
        referenced.Where(IsSqlite).Should().BeEmpty();
    }

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "IndexThinking.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException($"IndexThinking.slnx not found above {AppContext.BaseDirectory}.");
    }
}
