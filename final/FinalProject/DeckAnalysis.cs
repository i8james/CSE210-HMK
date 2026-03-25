using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

public static class DeckAnalysis
{
    public static bool IsNonLand(Card card)
    {
        return !card.IsLand && (card.Type == null || card.Type.IndexOf("Land", StringComparison.OrdinalIgnoreCase) < 0);
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
}
