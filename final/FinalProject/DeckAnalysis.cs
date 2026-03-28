using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

public enum DeckArchetype
{
    Midrange,
    Ramp,
    Spellslinger,
    Tribal,
    Tokens,
    Combo
}

public static class DeckAnalysis
{
    private static readonly HashSet<string> NonLandCoreTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Creature",
        "Artifact",
        "Enchantment",
        "Instant",
        "Sorcery",
        "Planeswalker",
        "Battle"
    };

    private static bool IsLandByTypeLine(string? typeLine)
    {
        if (string.IsNullOrWhiteSpace(typeLine))
            return false;

        string normalized = typeLine.Replace("â€”", "—");
        var faces = normalized.Split(new[] { "//" }, StringSplitOptions.RemoveEmptyEntries);
        bool sawLandFace = false;
        bool sawNonLandFace = false;

        foreach (var face in faces)
        {
            string front = face;
            int dashIndex = front.IndexOf('—');
            if (dashIndex >= 0)
                front = front.Substring(0, dashIndex);

            var tokens = front
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(token => token.Trim())
                .ToList();
            if (!tokens.Any())
                continue;

            bool faceHasLand = tokens.Contains("Land", StringComparer.OrdinalIgnoreCase);
            bool faceHasNonLandCore = tokens.Any(token => NonLandCoreTypes.Contains(token));

            if (faceHasLand)
                sawLandFace = true;
            if (faceHasNonLandCore)
                sawNonLandFace = true;
        }

        return sawLandFace && !sawNonLandFace;
    }

    private static bool IsLandCard(Card card)
    {
        if (!string.IsNullOrWhiteSpace(card.Type))
            return IsLandByTypeLine(card.Type);

        return card.IsLand;
    }

    public static bool IsNonLand(Card card)
    {
        return !IsLandCard(card);
    }

    public static HashSet<string> GetCategoryTags(Card card)
    {
        if (card == null || string.IsNullOrWhiteSpace(card.Category))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return card.Category
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(tag => tag.Trim())
            .Where(tag => tag.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public static int CountRoleCards(Deck deck, string roleTag)
    {
        return deck.Cards.Count(card => IsNonLand(card) && GetCategoryTags(card).Contains(roleTag));
    }

    public static HashSet<string> ExtractTypeTokens(string? typeLine)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(typeLine))
            return tokens;

        string normalized = typeLine.Replace("â€”", "—");
        var faces = normalized.Split(new[] { "//" }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var face in faces)
        {
            string front = face;
            int dashIndex = front.IndexOf('—');
            if (dashIndex >= 0)
                front = front.Substring(0, dashIndex);

            foreach (var token in front.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                tokens.Add(token.Trim());
        }

        return tokens;
    }

    public static bool HasType(Card card, string type)
    {
        return ExtractTypeTokens(card.Type).Contains(type);
    }

    public static bool IsPermanent(Card card)
    {
        if (card.IsLand)
            return true;

        var typeTokens = ExtractTypeTokens(card.Type);
        return typeTokens.Contains("Creature")
            || typeTokens.Contains("Artifact")
            || typeTokens.Contains("Enchantment")
            || typeTokens.Contains("Planeswalker")
            || typeTokens.Contains("Battle");
    }

    public static Dictionary<string, int> GetCoreTypeCounts(Deck deck)
    {
        var coreTypes = new[] { "Creature", "Artifact", "Enchantment", "Instant", "Sorcery", "Planeswalker", "Battle" };
        var counts = coreTypes.ToDictionary(type => type, _ => 0, StringComparer.OrdinalIgnoreCase);

        foreach (var card in deck.Cards.Where(IsNonLand))
        {
            var typeTokens = ExtractTypeTokens(card.Type);
            var tags = GetCategoryTags(card);
            foreach (var type in coreTypes)
            {
                if (typeTokens.Contains(type) || tags.Contains(type))
                    counts[type]++;
            }
        }

        return counts
            .Where(kvp => kvp.Value > 0)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
    }

    public static Dictionary<string, int> GetCreatureTribeCounts(Deck deck)
    {
        var tribes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var card in deck.Cards.Where(IsNonLand))
        {
            var tags = GetCategoryTags(card);
            if (!tags.Contains("Creature"))
                continue;

            foreach (var tribeTag in tags.Where(tag => tag.StartsWith("Tribe:", StringComparison.OrdinalIgnoreCase)))
            {
                string tribe = tribeTag.Substring("Tribe:".Length).Trim();
                if (tribe.Length == 0)
                    continue;

                if (!tribes.ContainsKey(tribe))
                    tribes[tribe] = 0;
                tribes[tribe]++;
            }
        }

        return tribes;
    }

    public static bool TryGetPrimaryTribe(Deck deck, out string tribe, out int tribeCount, out int creatureCount)
    {
        tribe = string.Empty;
        tribeCount = 0;
        creatureCount = deck.Cards.Count(card => IsNonLand(card) && GetCategoryTags(card).Contains("Creature"));
        if (creatureCount == 0)
            return false;

        var tribes = GetCreatureTribeCounts(deck);
        if (!tribes.Any())
            return false;

        var top = tribes.OrderByDescending(kvp => kvp.Value).First();
        if (top.Value >= 6 || (double)top.Value / creatureCount >= 0.25)
        {
            tribe = top.Key;
            tribeCount = top.Value;
            return true;
        }

        return false;
    }

    public static List<(string Label, List<string> Cards)> DetectComboPieces(Deck deck)
    {
        var combos = new List<(string Label, List<string> Cards)>();

        List<string> Scan(Func<string, bool> oraclePredicate)
        {
            return deck.Cards
                .Where(card => !card.IsLand && !string.IsNullOrWhiteSpace(card.OracleText) && oraclePredicate(card.OracleText!.ToLowerInvariant()))
                .Select(card => card.Name ?? "?")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var winCons = Scan(oracle => oracle.Contains("you win the game") || oracle.Contains("wins the game") || oracle.Contains("loses the game"));
        if (winCons.Any())
            combos.Add(("Win Conditions", winCons));

        var extraTurns = Scan(oracle => oracle.Contains("take an extra turn"));
        if (extraTurns.Any())
            combos.Add(("Extra Turns", extraTurns));

        var storm = Scan(oracle => oracle.Contains("storm (when you cast this spell"));
        if (storm.Any())
            combos.Add(("Storm", storm));

        var untapPieces = Scan(oracle => oracle.Contains("untap all") || (oracle.Contains("untap target") && (oracle.Contains("creature") || oracle.Contains("permanent"))));
        if (untapPieces.Any())
            combos.Add(("Untap Enablers", untapPieces));

        var sacOutlets = Scan(oracle => oracle.Contains("sacrifice") && (oracle.Contains(": add") || oracle.Contains(": draw") || oracle.Contains(": deal")));
        if (sacOutlets.Any())
            combos.Add(("Sacrifice Outlets", sacOutlets));

        var tutors = deck.Cards
            .Where(card => !card.IsLand && GetCategoryTags(card).Contains("Tutor"))
            .Select(card => card.Name ?? "?")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (tutors.Any())
            combos.Add(("Tutors", tutors));

        return combos;
    }

    /// <summary>Categorizes ramp cards by type for smarter recommendations.</summary>
    public enum RampType
    {
        CreatureRamp,      // Mana-producing creatures (Llanowar Elf, etc.)
        ArtifactRamp,      // Mana rock artifacts
        EnchantmentRamp,   // Enchantment-based ramp (Fertile Ground, etc.)
        SpellRamp          // Sorcery or instant that generates mana
    }

    /// <summary>Gets the ramp type for a card if it's ramp.</summary>
    public static RampType? GetRampType(Card card)
    {
        if (card.IsLand || !GetCategoryTags(card).Contains("Ramp"))
            return null;

        if (HasType(card, "Creature"))
            return RampType.CreatureRamp;
        else if (HasType(card, "Artifact"))
            return RampType.ArtifactRamp;
        else if (HasType(card, "Enchantment"))
            return RampType.EnchantmentRamp;
        else if (HasType(card, "Instant") || HasType(card, "Sorcery"))
            return RampType.SpellRamp;
        
        return null;
    }

    /// <summary>Counts ramp cards by type, excluding lands entirely.</summary>
    public static Dictionary<RampType, int> GetRampTypeBreakdown(Deck deck)
    {
        var breakdown = new Dictionary<RampType, int>
        {
            { RampType.CreatureRamp, 0 },
            { RampType.ArtifactRamp, 0 },
            { RampType.EnchantmentRamp, 0 },
            { RampType.SpellRamp, 0 }
        };

        foreach (var card in deck.Cards.Where(c => !c.IsLand))
        {
            var rampType = GetRampType(card);
            if (rampType.HasValue)
                breakdown[rampType.Value]++;
        }

        return breakdown;
    }

    /// <summary>Returns early ramp (0-2 CMC) cards for when the deck needs fast acceleration.</summary>
    public static List<Card> GetEarlyRampCards(Deck deck)
    {
        return deck.Cards
            .Where(card => !card.IsLand && card.ManaCost <= 2 && GetCategoryTags(card).Contains("Ramp"))
            .OrderBy(card => card.ManaCost)
            .ThenBy(card => card.Name ?? "")
            .ToList();
    }

    public static bool HasTribe(Card card, string tribe)
    {
        return GetCategoryTags(card).Contains($"Tribe:{tribe}");
    }

    /// <summary>Returns true if this is a basic land (Plains, Island, Swamp, Mountain, Forest, Wastes).</summary>
    public static bool IsBasicLand(Card card)
    {
        if (card?.Type == null)
            return false;
        
        var typeTokens = ExtractTypeTokens(card.Type);
        if (!typeTokens.Contains("Land", StringComparer.OrdinalIgnoreCase))
            return false;
        
        // Check for basic land types in the type line
        var basicTypes = new[] { "Plains", "Island", "Swamp", "Mountain", "Forest", "Wastes" };
        return typeTokens.Any(type => basicTypes.Contains(type, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Returns true if this is a 0-cost artifact that can produce mana (mana rock).</summary>
    public static bool IsManaRock(Card card)
    {
        if (card.ManaCost != 0)
            return false;
        
        if (!HasType(card, "Artifact"))
            return false;
        
        // Check oracle text for mana production
        if (string.IsNullOrWhiteSpace(card.OracleText))
            return false;
        
        string oracle = card.OracleText.ToLowerInvariant();
        return oracle.Contains(": add") || oracle.Contains(":add");
    }

    /// <summary>Counts ramp pieces, excluding basic lands but including 0-cost mana rocks.</summary>
    public static int CountRealRamp(Deck deck)
    {
        // Count cards tagged as Ramp, excluding basic lands
        int taggedRamp = deck.Cards.Count(card => 
            IsNonLand(card) && 
            GetCategoryTags(card).Contains("Ramp") && 
            !IsBasicLand(card));
        
        // Add 0-cost mana rocks (artifacts that produce mana)
        int manaRocks = deck.Cards.Count(card => IsManaRock(card));
        
        return taggedRamp + manaRocks;
    }

    public static DeckArchetype DetectPrimaryArchetype(Deck deck)
    {
        int rampCount = CountRealRamp(deck);
        int drawCount = CountRoleCards(deck, "Card Draw");
        int tokenCount = CountRoleCards(deck, "Token Generation");
        int tutorCount = CountRoleCards(deck, "Tutor");
        int interactionCount = CountRoleCards(deck, "Removal") + CountRoleCards(deck, "Counterspell");
        int instantSorceryCount = deck.Cards.Count(card => IsNonLand(card) && (HasType(card, "Instant") || HasType(card, "Sorcery")));
        int comboCount = DetectComboPieces(deck).Sum(group => group.Cards.Count);
        double averageManaValue = deck.Cards.Where(IsNonLand).DefaultIfEmpty().Average(card => card == null ? 0 : card.ManaCost);

        if (comboCount >= 6 || (comboCount >= 4 && tutorCount >= 2))
            return DeckArchetype.Combo;

        if (instantSorceryCount >= 18 && drawCount + interactionCount >= 12)
            return DeckArchetype.Spellslinger;

        if (TryGetPrimaryTribe(deck, out _, out int tribeCount, out _) && tribeCount >= 8)
            return DeckArchetype.Tribal;

        if (tokenCount >= 7)
            return DeckArchetype.Tokens;

        if (rampCount >= 12 || (rampCount >= 9 && averageManaValue >= 3.5))
            return DeckArchetype.Ramp;

        return DeckArchetype.Midrange;
    }
}
