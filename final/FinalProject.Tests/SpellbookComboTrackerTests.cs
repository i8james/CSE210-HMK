using System.Collections.Generic;
using Xunit;

public class SpellbookComboTrackerTests
{
    [Fact]
    public void Track_RegistersAssemblyWhenKnownComboRequirementsAreMet()
    {
        var commander = new Card
        {
            Name = "Niv-Mizzet, Parun",
            IsCommander = true,
            ColorIdentity = new List<string> { "U", "R" },
            Colors = new List<string> { "U", "R" }
        };

        var combo = new SpellbookComboVariant
        {
            Id = "combo-1",
            Uses = new List<SpellbookComboUse>
            {
                new SpellbookComboUse { CardName = "Niv-Mizzet, Parun", Quantity = 1, ZoneLocations = new List<string> { "C" } },
                new SpellbookComboUse { CardName = "Curiosity", Quantity = 1, ZoneLocations = new List<string> { "B" } }
            },
            Produces = new List<string> { "Infinite damage" },
            Description = "Repeat the loop for infinite damage."
        };

        var state = new GoldfishGameState(
            new List<Card>(),
            new List<Card>(),
            new List<Card> { new Card { Name = "Curiosity" } },
            commander,
            commanderAvailable: true,
            turn: 4,
            landsInPlay: 4,
            permanentRamp: 0,
            knownCombos: new[] { combo });

        SpellbookComboTracker.Track(state);

        Assert.Contains("combo-1", state.AssembledSpellbookComboIds);
        Assert.Contains(state.SpellbookComboAssemblies, line => line.Contains("Niv-Mizzet, Parun", System.StringComparison.OrdinalIgnoreCase));
        Assert.True(state.InfiniteComboAchieved);
    }
}