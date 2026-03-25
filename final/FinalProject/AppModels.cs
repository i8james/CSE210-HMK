using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

internal sealed class CardDbRecord
{
    public string Name { get; set; } = string.Empty;
    public int ManaCost { get; set; }
    public List<string> Colors { get; set; } = new List<string>();
    public string Type { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsLand { get; set; }
    public string OracleText { get; set; } = string.Empty;
}

public class Card
{
    public string? Name { get; set; }
    public bool IsLand { get; set; }
    public int ManaCost { get; set; }
    public List<string> Colors { get; set; } = new List<string>();
    public string? Type { get; set; }
    public string? Category { get; set; }
    public string? OracleText { get; set; }
}

public class Deck
{
    public List<Card> Cards { get; } = new List<Card>();

    public void AddCard(Card card)
    {
        Cards.Add(card);
    }

    public List<Card> GetShuffledDeck(Random? random = null)
    {
        random ??= new Random();
        var shuffled = new List<Card>(Cards);
        for (int index = shuffled.Count - 1; index > 0; index--)
        {
            int swapIndex = random.Next(index + 1);
            (shuffled[index], shuffled[swapIndex]) = (shuffled[swapIndex], shuffled[index]);
        }

        return shuffled;
    }

    public int LandCount => Cards.Count(card => card.IsLand);

    public Dictionary<int, int> GetManaCurve()
    {
        return Cards
            .Where(card => !card.IsLand)
            .GroupBy(card => card.ManaCost)
            .ToDictionary(group => group.Key, group => group.Count());
    }

    public Dictionary<string, int> GetManaBrackets()
    {
        var nonLands = Cards.Where(card => !card.IsLand).ToList();
        return new Dictionary<string, int>
        {
            ["0-2"] = nonLands.Count(card => card.ManaCost >= 0 && card.ManaCost <= 2),
            ["3-4"] = nonLands.Count(card => card.ManaCost >= 3 && card.ManaCost <= 4),
            ["5-6"] = nonLands.Count(card => card.ManaCost >= 5 && card.ManaCost <= 6),
            ["7+"] = nonLands.Count(card => card.ManaCost >= 7)
        };
    }
}

public class SimulationResult
{
    public int MissedLands { get; set; }
    public int LandsPlayed { get; set; }
    public int CardsPlayable { get; set; }
    public int IdleTurns { get; set; }
    public int SpellsCast { get; set; }
    public int OpeningHandLands { get; set; }
    public bool BrickHand { get; set; }
    public bool FloodHand { get; set; }
    public int PeakMana { get; set; }
    public int MulligansTaken { get; set; }
    public int ManaProduced { get; set; }
    public int ManaSpent { get; set; }
    public int EarlyTurnActions { get; set; }
    public int StrandedHighCostCards { get; set; }
}

public class EvaluationResults
{
    public double AverageMissedLands { get; set; }
    public double AverageLandsPlayed { get; set; }
    public double AverageCardsPlayable { get; set; }
    public double AverageIdleTurns { get; set; }
    public double AverageSpellsCast { get; set; }
    public double AverageOpeningHandLands { get; set; }
    public double BrickHandPercent { get; set; }
    public double FloodHandPercent { get; set; }
    public double AveragePeakMana { get; set; }
    public double AverageMulligans { get; set; }
    public double AverageManaProduced { get; set; }
    public double AverageManaSpent { get; set; }
    public double AverageManaEfficiency { get; set; }
    public double AverageEarlyTurnActions { get; set; }
    public double AverageStrandedHighCostCards { get; set; }
    public List<string> Recommendations { get; set; } = new List<string>();
}
