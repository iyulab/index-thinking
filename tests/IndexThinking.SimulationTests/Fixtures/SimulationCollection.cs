using Xunit;

namespace IndexThinking.SimulationTests.Fixtures;

/// <summary>
/// Collection definition for simulation tests.
/// Ensures shared fixture across all simulation test classes.
/// </summary>
[CollectionDefinition("Simulation")]
public class SimulationCollection : ICollectionFixture<SimulationTestFixture>
{
    // This class has no code, and is never created.
    // Its purpose is simply to be the place to apply [CollectionDefinition]
    // and all the ICollectionFixture<> interfaces.
}
