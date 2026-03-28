using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

internal sealed class CardDbRecord
{
    public string Name { get; set; } = string.Empty;
    public int ManaCost { get; set; }
    public List<string> Colors { get; set; } = new List<string>();
    public List<string> ColorIdentity { get; set; } = new List<string>();
    public string Type { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsLand { get; set; }
    public string OracleText { get; set; } = string.Empty;
}

public class Card
{
    public string? Name { get; set; }
    public bool IsLand { get; set; }
    public bool IsCommander { get; set; }
    public int ManaCost { get; set; }
    public List<string> Colors { get; set; } = new List<string>();
    /// <summary>True color identity (W/U/B/R/G) including activated ability symbols. Used for legality filtering.</summary>
    public List<string> ColorIdentity { get; set; } = new List<string>();
    public string? Type { get; set; }
    public string? Category { get; set; }
    public string? OracleText { get; set; }
}

public class Deck
{
    public List<Card> Cards { get; } = new List<Card>();
    public Card? Commander { get; private set; }
    internal SpellbookComboReport? SpellbookReport { get; set; }

    public void AddCard(Card card)
    {
        if (card.IsCommander)
            Commander = card;

        Cards.Add(card);
    }

    public List<Card> GetShuffledDeck(Random? random = null)
    {
        random ??= new Random();
        var shuffled = Cards.Where(card => !card.IsCommander).ToList();
        for (int index = shuffled.Count - 1; index > 0; index--)
        {
            int swapIndex = random.Next(index + 1);
            (shuffled[index], shuffled[swapIndex]) = (shuffled[swapIndex], shuffled[index]);
        }

        return shuffled;
    }

    public int LandCount => Cards.Count(card => card.IsLand);

    /// <summary>Returns a set of color codes (W/U/B/R/G) representing the commander's color identity.
    /// Falls back to the commander's mana cost colors if no explicit color identity was loaded.</summary>
    public HashSet<string> GetCommanderColorIdentity()
    {
        if (Commander == null)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var identity = Commander.ColorIdentity.Count > 0 ? Commander.ColorIdentity : Commander.Colors;
        return new HashSet<string>(identity, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Returns true when the given card record is legal in the commander's color identity.
    /// Colorless cards (empty color identity and empty colors) are always legal.</summary>
    internal bool IsInColorIdentity(CardDbRecord record)
    {
        var cardIdentity = record.ColorIdentity.Count > 0 ? record.ColorIdentity : record.Colors;
        if (cardIdentity.Count == 0)
            return true; // colorless artifacts, lands, etc.
        var identity = GetCommanderColorIdentity();
        if (identity.Count == 0)
            return true; // no commander or identity unknown — allow anything
        return cardIdentity.All(c => identity.Contains(c));
    }

    public Dictionary<int, int> GetManaCurve()
    {
        return Cards
            .Where(card => !card.IsLand && !card.IsCommander)
            .GroupBy(card => card.ManaCost)
            .ToDictionary(group => group.Key, group => group.Count());
    }

    public Dictionary<string, int> GetManaBrackets()
    {
        var nonLands = Cards.Where(card => !card.IsLand && !card.IsCommander).ToList();
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
    public int CommanderCastTurn { get; set; }
    public bool CommanderCast { get; set; }
    public bool WonViaInfiniteCombo { get; set; }
    public string ComboLine { get; set; } = string.Empty;
    public int ActivatedAbilitiesUsed { get; set; }
    public int TriggeredAbilitiesResolved { get; set; }
    public List<string> ActionLog { get; set; } = new List<string>();
    public List<string> SpellbookComboAssemblies { get; set; } = new List<string>();
}

public class EvaluationResults
{
    public DeckArchetype SelectedArchetype { get; set; }
    public DeckArchetype DetectedArchetype { get; set; }
    public double EstimatedPowerLevel { get; set; }
    public string EstimatedBracket { get; set; } = string.Empty;
    public string PowerSummary { get; set; } = string.Empty;
    public List<string> PowerSignals { get; set; } = new List<string>();
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
    public double AverageCommanderCastTurn { get; set; }
    public double CommanderCastRate { get; set; }
    public double InfiniteComboWinRate { get; set; }
    public double AverageActivatedAbilitiesUsed { get; set; }
    public double AverageTriggeredAbilitiesResolved { get; set; }
    public List<string> ComboLines { get; set; } = new List<string>();
    public List<string> SpellbookKnownCombos { get; set; } = new List<string>();
    public List<string> SpellbookAlmostCombos { get; set; } = new List<string>();
    public List<string> SpellbookComboAssemblies { get; set; } = new List<string>();
    public double SpellbookComboAssemblyRate { get; set; }
    public List<string> SamplePlayPatterns { get; set; } = new List<string>();
    public int LearningGamesSeen { get; set; }
    public bool IsCedh { get; set; }
    public List<string> Recommendations { get; set; } = new List<string>();
    public List<DeckSuggestion> Suggestions { get; set; } = new List<DeckSuggestion>();
}

public enum DeckSuggestionKind
{
    Add,
    Cut,
    Swap,
    Info
}

public class DeckSuggestion
{
    public DeckSuggestionKind Kind { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string RoleTag { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public List<string> SuggestedAdds { get; set; } = new List<string>();
    public List<string> SuggestedCuts { get; set; } = new List<string>();
    public Dictionary<string, string> AddReasons { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> CutReasons { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
