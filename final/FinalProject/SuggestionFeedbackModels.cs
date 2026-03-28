using System;
using System.Collections.Generic;

#nullable enable

internal enum SuggestionFeedbackVote
{
    Disliked = -1,
    Liked = 1
}

internal sealed class SuggestionFeedbackSummary
{
    public string Title { get; init; } = string.Empty;
    public string StatusText { get; init; } = string.Empty;
    public ColorVoteTone Tone { get; init; }
}

internal enum ColorVoteTone
{
    Neutral,
    Positive,
    Negative
}

/// <summary>Tracks feedback for individual cards in a suggestion (add or cut).</summary>
internal sealed class CardFeedbackRecord
{
    public string CardName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;  // e.g., "Card Draw", "Removal"
    public int VoteCount { get; set; }  // +1 liked, -1 disliked
    public string? Reason { get; set; }  // Why was this suggested/cut
    public double ScoreContribution { get; set; }  // How much did this card contribute to the suggestion
}

/// <summary>Enhanced feedback summary with individual card details and scoring breakdown.</summary>
internal sealed class EnhancedSuggestionFeedback
{
    public string Title { get; set; } = string.Empty;
    public List<CardFeedbackRecord> AddedCards { get; set; } = new List<CardFeedbackRecord>();
    public List<CardFeedbackRecord> CutCards { get; set; } = new List<CardFeedbackRecord>();
    public double TotalScore { get; set; }
    public string ScoreBreakdown { get; set; } = string.Empty;  // "Adds: 35.5 | Cuts: -12.3 | Total: 23.2"
}

/// <summary>Learning profile export/import format for sharing and backup.</summary>
internal sealed class LearningProfileExport
{
    public string CommanderName { get; set; } = string.Empty;
    public string DeckKey { get; set; } = string.Empty;
    public int GamesPlayed { get; set; }
    public double CommanderBias { get; set; }
    public Dictionary<string, double> TagBiases { get; set; } = new Dictionary<string, double>();
    public Dictionary<string, double> CardPreferences { get; set; } = new Dictionary<string, double>();
    public Dictionary<string, int> SuggestionVotes { get; set; } = new Dictionary<string, int>();
    public string ExportDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
}
