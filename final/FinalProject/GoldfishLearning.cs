using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

#nullable enable

internal sealed class GoldfishLearningProfile
{
    public string DeckKey { get; set; } = string.Empty;
    public int GamesPlayed { get; set; }
    public double CommanderBias { get; set; }
    public Dictionary<string, double> TagBiases { get; set; } = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, double> CardPreferences { get; set; } = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> SuggestionVotes { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
}

internal static class GoldfishLearningStore
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };
    private static readonly string FilePath = FizbanStorage.GetPath("goldfish_learning.json");
    private static readonly Dictionary<string, GoldfishLearningProfile> Cache = LoadAll();

    public static GoldfishLearningProfile LoadProfile(Deck deck)
    {
        string deckKey = ComputeDeckKey(deck);
        if (!Cache.TryGetValue(deckKey, out var profile))
        {
            profile = new GoldfishLearningProfile { DeckKey = deckKey };
            Cache[deckKey] = profile;
        }

        return profile;
    }

    public static void SaveProfile(GoldfishLearningProfile profile)
    {
        Cache[profile.DeckKey] = profile;
        string json = JsonSerializer.Serialize(Cache.Values.OrderBy(value => value.DeckKey).ToList(), JsonOptions);
        File.WriteAllText(FilePath, json);
    }

    public static double GetCardPreference(Deck deck, string? cardName)
    {
        if (string.IsNullOrWhiteSpace(cardName))
            return 0;

        var profile = LoadProfile(deck);
        return profile.CardPreferences.TryGetValue(cardName.Trim(), out double score) ? score : 0;
    }

    public static double GetRolePreference(Deck deck, string? roleTag)
    {
        if (string.IsNullOrWhiteSpace(roleTag))
            return 0;

        var profile = LoadProfile(deck);
        return profile.TagBiases.TryGetValue(roleTag.Trim(), out double score) ? score : 0;
    }

    public static void RecordSuggestionFeedback(Deck deck, DeckSuggestion suggestion, SuggestionFeedbackVote vote)
    {
        var profile = LoadProfile(deck);
        int delta = (int)vote;
        string suggestionKey = suggestion.Title.Trim();
        profile.SuggestionVotes[suggestionKey] = profile.SuggestionVotes.TryGetValue(suggestionKey, out int existingVote)
            ? existingVote + delta
            : delta;

        if (!string.IsNullOrWhiteSpace(suggestion.RoleTag))
        {
            profile.TagBiases[suggestion.RoleTag] = profile.TagBiases.TryGetValue(suggestion.RoleTag, out double existingRole)
                ? existingRole + (delta * 0.35)
                : delta * 0.35;
        }

        foreach (var add in suggestion.SuggestedAdds.NormalizeNames())
            profile.CardPreferences[add] = profile.CardPreferences.TryGetValue(add, out double existingAdd) ? existingAdd + delta : delta;

        foreach (var cut in suggestion.SuggestedCuts.NormalizeNames())
            profile.CardPreferences[cut] = profile.CardPreferences.TryGetValue(cut, out double existingCut) ? existingCut - delta : -delta;

        SaveProfile(profile);
    }

    public static SuggestionFeedbackSummary GetSuggestionFeedbackSummary(Deck deck, DeckSuggestion suggestion)
    {
        var profile = LoadProfile(deck);
        int total = profile.SuggestionVotes.TryGetValue(suggestion.Title.Trim(), out int votes) ? votes : 0;
        if (total > 0)
            return new SuggestionFeedbackSummary { Title = suggestion.Title, StatusText = "Liked", Tone = ColorVoteTone.Positive };
        if (total < 0)
            return new SuggestionFeedbackSummary { Title = suggestion.Title, StatusText = "Disliked", Tone = ColorVoteTone.Negative };
        return new SuggestionFeedbackSummary { Title = suggestion.Title, StatusText = "Not rated", Tone = ColorVoteTone.Neutral };
    }

    /// <summary>Export a deck's learning profile for backup or sharing.</summary>
    public static LearningProfileExport ExportProfile(Deck deck)
    {
        var profile = LoadProfile(deck);
        return new LearningProfileExport
        {
            CommanderName = deck.Commander?.Name ?? "Unknown",
            DeckKey = profile.DeckKey,
            GamesPlayed = profile.GamesPlayed,
            CommanderBias = profile.CommanderBias,
            TagBiases = new Dictionary<string, double>(profile.TagBiases),
            CardPreferences = new Dictionary<string, double>(profile.CardPreferences),
            SuggestionVotes = new Dictionary<string, int>(profile.SuggestionVotes)
        };
    }

    /// <summary>Import a previously exported learning profile for a deck.</summary>
    public static void ImportProfile(Deck deck, LearningProfileExport export)
    {
        var profile = LoadProfile(deck);
        profile.CommanderBias = export.CommanderBias;
        profile.GamesPlayed = export.GamesPlayed;
        profile.TagBiases = new Dictionary<string, double>(export.TagBiases, StringComparer.OrdinalIgnoreCase);
        profile.CardPreferences = new Dictionary<string, double>(export.CardPreferences, StringComparer.OrdinalIgnoreCase);
        profile.SuggestionVotes = new Dictionary<string, int>(export.SuggestionVotes, StringComparer.OrdinalIgnoreCase);
        SaveProfile(profile);
    }

    /// <summary>Reset all learning for a specific deck to defaults.</summary>
    public static void ResetProfile(Deck deck)
    {
        string deckKey = ComputeDeckKey(deck);
        var newProfile = new GoldfishLearningProfile { DeckKey = deckKey };
        Cache[deckKey] = newProfile;
        SaveProfile(newProfile);
    }

    /// <summary>Generate a human-readable summary of what the AI has learned about a deck.</summary>
    public static string GetLearningProfileSummary(Deck deck)
    {
        var profile = LoadProfile(deck);
        var sb = new StringBuilder();
        sb.AppendLine($"✦ Learning Profile for {deck.Commander?.Name ?? "Unknown Commander"}");
        sb.AppendLine($"Games played: {profile.GamesPlayed}");
        sb.AppendLine();

        if (profile.TagBiases.Count > 0)
        {
            sb.AppendLine("Role Preferences:");
            foreach (var (role, bias) in profile.TagBiases.OrderByDescending(kvp => Math.Abs(kvp.Value)))
            {
                string trend = bias > 0 ? "✓" : "✗";
                sb.AppendLine($"  {trend} {role}: {bias:+0.00;-0.00;0.00}");
            }
            sb.AppendLine();
        }

        if (profile.CardPreferences.Count > 0)
        {
            var topLiked = profile.CardPreferences.Where(kvp => kvp.Value > 0).OrderByDescending(kvp => kvp.Value).Take(5);
            var topDisliked = profile.CardPreferences.Where(kvp => kvp.Value < 0).OrderBy(kvp => kvp.Value).Take(5);

            if (topLiked.Any())
            {
                sb.AppendLine("Top Liked Cards:");
                foreach (var (card, pref) in topLiked)
                    sb.AppendLine($"  ✓ {card} ({pref:+0.0;-0.0;0.0})");
                sb.AppendLine();
            }

            if (topDisliked.Any())
            {
                sb.AppendLine("Top Disliked Cards:");
                foreach (var (card, pref) in topDisliked)
                    sb.AppendLine($"  ✗ {card} ({pref:+0.0;-0.0;0.0})");
                sb.AppendLine();
            }
        }

        if (profile.SuggestionVotes.Count > 0)
        {
            int liked = profile.SuggestionVotes.Count(kvp => kvp.Value > 0);
            int disliked = profile.SuggestionVotes.Count(kvp => kvp.Value < 0);
            int total = profile.SuggestionVotes.Count;
            sb.AppendLine($"Suggestion Feedback: {liked} liked, {disliked} disliked out of {total} rated");
        }

        return sb.ToString();
    }

    private static Dictionary<string, GoldfishLearningProfile> LoadAll()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new Dictionary<string, GoldfishLearningProfile>(StringComparer.OrdinalIgnoreCase);

            string json = File.ReadAllText(FilePath);
            var profiles = JsonSerializer.Deserialize<List<GoldfishLearningProfile>>(json, JsonOptions) ?? new List<GoldfishLearningProfile>();
            return profiles.ToDictionary(profile => profile.DeckKey, profile => profile, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, GoldfishLearningProfile>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string ComputeDeckKey(Deck deck)
    {
        string commander = deck.Commander?.Name?.Trim() ?? "no-commander";
        string cardSignature = string.Join("|", deck.Cards
            .Where(card => !card.IsCommander)
            .GroupBy(card => card.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key)
            .Select(group => $"{group.Key}:{group.Count()}"));

        string raw = commander + "||" + cardSignature;
        using var sha = SHA256.Create();
        byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }
}
