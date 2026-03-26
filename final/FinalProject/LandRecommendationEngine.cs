using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

internal sealed class LandRecommendation
{
    public string Name { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}

internal static class LandRecommendationEngine
{
    private static readonly Dictionary<string, string> BasicLandByColor = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["W"] = "Plains",
        ["U"] = "Island",
        ["B"] = "Swamp",
        ["R"] = "Mountain",
        ["G"] = "Forest"
    };

    public static List<LandRecommendation> RecommendAdds(Deck deck, int maxResults)
    {
        if (maxResults <= 0)
            return new List<LandRecommendation>();

        var identity = deck.GetCommanderColorIdentity();
        var inDeck = deck.Cards
            .Where(card => !string.IsNullOrWhiteSpace(card.Name))
            .Select(card => card.Name!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var recommendations = new List<LandRecommendation>();

        void AddUnique(string name, string reason, bool allowDuplicate = false)
        {
            if (!allowDuplicate && inDeck.Contains(name))
                return;
            if (recommendations.Any(entry => entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return;

            recommendations.Add(new LandRecommendation { Name = name, Reason = reason });
        }

        if (identity.Count == 0)
        {
            AddUnique("Wastes", "Untapped colorless source that never conflicts with color identity.", allowDuplicate: true);
            return recommendations.Take(maxResults).ToList();
        }

        if (identity.Count >= 2)
        {
            AddUnique("Command Tower", "Best general-purpose Commander fixer; taps for any color in your commander's identity.");
            AddUnique("Exotic Orchard", "Usually fixes multiple colors in multiplayer without entering tapped.");
            AddUnique("Path of Ancestry", "Color-fixes any commander deck and is especially strong if your deck has tribal overlap.");

            if (identity.Count >= 3)
            {
                AddUnique("Mana Confluence", "Five-color fixing with no color mismatch risk.");
                AddUnique("City of Brass", "Reliable all-color source for multicolor commander decks.");
                AddUnique("Reflecting Pool", "Scales well once the mana base already covers multiple colors.");
            }
        }

        foreach (var (color, landName) in GetBasicOrder(deck))
            AddUnique(landName, $"Untapped {DescribeColor(color)} source that matches your current spell-color demand.", allowDuplicate: true);

        return recommendations.Take(maxResults).ToList();
    }

    public static Dictionary<string, string> BuildAddReasons(IEnumerable<LandRecommendation> lands)
    {
        return lands.ToDictionary(land => land.Name, land => land.Reason, StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<(string Color, string LandName)> GetBasicOrder(Deck deck)
    {
        var spellWeights = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["W"] = 0,
            ["U"] = 0,
            ["B"] = 0,
            ["R"] = 0,
            ["G"] = 0
        };

        foreach (var card in deck.Cards.Where(card => !card.IsLand && !card.IsCommander))
        {
            var colors = card.ColorIdentity.Count > 0 ? card.ColorIdentity : card.Colors;
            foreach (var color in colors.Where(BasicLandByColor.ContainsKey))
                spellWeights[color]++;
        }

        return spellWeights
            .Where(entry => entry.Value > 0)
            .OrderByDescending(entry => entry.Value)
            .ThenBy(entry => entry.Key)
            .Select(entry => (entry.Key, BasicLandByColor[entry.Key]));
    }

    private static string DescribeColor(string color)
    {
        return color.ToUpperInvariant() switch
        {
            "W" => "white",
            "U" => "blue",
            "B" => "black",
            "R" => "red",
            "G" => "green",
            _ => "matching"
        };
    }
}