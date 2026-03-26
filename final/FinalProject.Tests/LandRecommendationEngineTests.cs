using System.Collections.Generic;
using System.Linq;
using Xunit;

public class LandRecommendationEngineTests
{
    [Fact]
    public void RecommendAdds_StaysWithinCommanderIdentity()
    {
        var deck = new Deck();
        deck.AddCard(new Card
        {
            Name = "Aesi, Tyrant of Gyre Strait",
            IsCommander = true,
            ColorIdentity = new List<string> { "G", "U" },
            Colors = new List<string> { "G", "U" }
        });
        deck.AddCard(new Card { Name = "Counterspell", ManaCost = 2, ColorIdentity = new List<string> { "U", "U" }, Colors = new List<string> { "U" } });
        deck.AddCard(new Card { Name = "Cultivate", ManaCost = 3, ColorIdentity = new List<string> { "G" }, Colors = new List<string> { "G" } });

        var results = LandRecommendationEngine.RecommendAdds(deck, 5).Select(result => result.Name).ToList();

        Assert.Contains("Command Tower", results);
        Assert.DoesNotContain("Plains", results);
        Assert.DoesNotContain("Swamp", results);
        Assert.Contains(results, land => land == "Island" || land == "Forest");
    }
}