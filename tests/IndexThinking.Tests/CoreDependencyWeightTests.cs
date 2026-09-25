using System.Xml.Linq;
using AwesomeAssertions;
using IndexThinking.Stores;
using Xunit;

namespace IndexThinking.Tests;

/// <summary>
/// <c>IndexThinking</c> carries no SQLite: the persistent store lives in <c>IndexThinking.Sqlite</c> (0.23.0). Before
/// the split every consumer shipped <c>Microsoft.Data.Sqlite</c> and the native <c>e_sqlite3</c> for every RID, for a
/// store most hosts never register. Two facts, because each alone misses a way back in: a package reference nothing
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
        references.Where(IsSqlite).Should().BeEmpty("SQLite ships with IndexThinking.Sqlite, not with every consumer");
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

    [Fact]
    public void Satellite_assembly_references_sqlite()
    {
        // Positive control for the name filter: the store's own assembly is where SQLite belongs.
        typeof(SqliteThinkingStateStore).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Should().Contain(name => IsSqlite(name));
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
