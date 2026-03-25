using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

internal sealed class GoldfishBot
{
    private readonly Deck _deck;
    private readonly Random _random = new Random();

    public GoldfishBot(Deck deck)
    {
        _deck = deck;
    }

    public SimulationResult SimulateGame(int maxTurns, bool onDraw = false)
    {
        int mulligansTaken;
        var library = _deck.GetShuffledDeck(_random);
        var hand = BuildOpeningHand(ref library, out mulligansTaken);

        int landsInPlay = 0;
        int landsPlayed = 0;
        int missedLands = 0;
        int spellsCast = 0;
        int idleTurns = 0;
        int permanentRamp = 0;
        int peakMana = 0;
        int manaProduced = 0;
        int manaSpent = 0;
        int earlyTurnActions = 0;
        int openingHandLands = hand.Count(card => card.IsLand);
        var battlefield = new List<Card>();

        for (int turn = 1; turn <= maxTurns; turn++)
        {
            if (turn > 1 || onDraw)
                DrawCards(library, hand, 1);

            bool playedAnything = false;
            PlayBestLand(hand, ref landsInPlay, ref landsPlayed, ref missedLands, ref playedAnything);

            int manaAvailable = landsInPlay + permanentRamp;
            int manaAtStartOfTurn = manaAvailable;
            peakMana = Math.Max(peakMana, manaAvailable);
            manaProduced += manaAtStartOfTurn;

            while (true)
            {
                var bestSpell = ChooseBestSpellToCast(hand, battlefield, turn, manaAvailable);
                if (bestSpell == null)
                    break;

                int spellCost = Math.Max(0, bestSpell.ManaCost);
                hand.Remove(bestSpell);
                manaAvailable -= spellCost;
                manaSpent += spellCost;
                spellsCast++;
                playedAnything = true;
                if (turn <= 3)
                    earlyTurnActions++;

                if (DeckAnalysis.IsPermanent(bestSpell))
                    battlefield.Add(bestSpell);

                ApplyCastEffects(bestSpell, library, hand, battlefield, turn, ref permanentRamp, ref manaAvailable);
            }

            if (!playedAnything)
                idleTurns++;
        }

        return new SimulationResult
        {
            MissedLands = missedLands,
            LandsPlayed = landsPlayed,
            CardsPlayable = spellsCast,
            IdleTurns = idleTurns,
            SpellsCast = spellsCast,
            OpeningHandLands = openingHandLands,
            BrickHand = openingHandLands == 0,
            FloodHand = openingHandLands >= 5,
            PeakMana = peakMana,
            MulligansTaken = mulligansTaken,
            ManaProduced = manaProduced,
            ManaSpent = manaSpent,
            EarlyTurnActions = earlyTurnActions,
            StrandedHighCostCards = hand.Count(card => !card.IsLand && card.ManaCost >= 5)
        };
    }

    private List<Card> BuildOpeningHand(ref List<Card> library, out int mulligansTaken)
    {
        for (int mulligans = 0; mulligans <= 2; mulligans++)
        {
            var trialLibrary = _deck.GetShuffledDeck(_random);
            var trialHand = new List<Card>();
            DrawCards(trialLibrary, trialHand, 7);

            if (ShouldKeepOpeningHand(trialHand, mulligans) || mulligans == 2)
            {
                var bottomed = ChooseBottomCards(trialHand, mulligans);
                foreach (var card in bottomed)
                {
                    trialHand.Remove(card);
                    trialLibrary.Add(card);
                }

                mulligansTaken = mulligans;
                library = trialLibrary;
                return trialHand;
            }
        }

        mulligansTaken = 0;
        return new List<Card>();
    }

    private static bool ShouldKeepOpeningHand(List<Card> hand, int mulligansTaken)
    {
        int lands = hand.Count(card => card.IsLand);
        int cheapSpells = hand.Count(card => !card.IsLand && card.ManaCost <= 3);
        int rampPieces = hand.Count(card => DeckAnalysis.GetCategoryTags(card).Contains("Ramp"));
        int drawPieces = hand.Count(card => DeckAnalysis.GetCategoryTags(card).Contains("Card Draw"));

        if (lands >= 2 && lands <= 4 && cheapSpells >= 2)
            return true;

        if (lands == 1 && rampPieces >= 1 && cheapSpells >= 2)
            return true;

        if (mulligansTaken >= 1 && lands >= 2 && lands <= 5 && (cheapSpells >= 1 || drawPieces >= 1))
            return true;

        return false;
    }

    private static List<Card> ChooseBottomCards(List<Card> hand, int count)
    {
        return hand
            .OrderBy(card => OpeningHandBottomScore(card))
            .Take(count)
            .ToList();
    }

    private static int OpeningHandBottomScore(Card card)
    {
        var tags = DeckAnalysis.GetCategoryTags(card);
        int score = 0;
        if (card.IsLand)
            score += 40;
        if (tags.Contains("Ramp"))
            score += 90;
        if (tags.Contains("Card Draw"))
            score += 70;
        if (tags.Contains("Tutor"))
            score += 65;
        if (tags.Contains("Removal") || tags.Contains("Counterspell"))
            score -= 25;
        score -= card.ManaCost * 5;
        return score;
    }

    private static void DrawCards(List<Card> library, List<Card> hand, int count)
    {
        for (int draw = 0; draw < count && library.Count > 0; draw++)
        {
            hand.Add(library[0]);
            library.RemoveAt(0);
        }
    }

    private static void PlayBestLand(List<Card> hand, ref int landsInPlay, ref int landsPlayed, ref int missedLands, ref bool playedAnything)
    {
        var landInHand = hand.FirstOrDefault(card => card.IsLand);
        if (landInHand == null)
        {
            missedLands++;
            return;
        }

        hand.Remove(landInHand);
        landsInPlay++;
        landsPlayed++;
        playedAnything = true;
    }

    private static Card? ChooseBestSpellToCast(List<Card> hand, List<Card> battlefield, int turn, int manaAvailable)
    {
        var playable = hand
            .Where(card => !card.IsLand && card.ManaCost <= manaAvailable)
            .Select(card => new { Card = card, Score = ScoreCardForPriority(card, battlefield, hand, turn, manaAvailable) })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Card.ManaCost)
            .ThenBy(item => item.Card.Name)
            .ToList();

        if (!playable.Any())
            return null;

        return playable[0].Score < 0 ? null : playable[0].Card;
    }

    private static int ScoreCardForPriority(Card card, IReadOnlyCollection<Card> battlefield, IReadOnlyCollection<Card> hand, int turn, int manaAvailable)
    {
        string oracle = card.OracleText ?? string.Empty;
        var tags = DeckAnalysis.GetCategoryTags(card);
        int battlefieldRampPieces = battlefield.Count(permanent => DeckAnalysis.GetCategoryTags(permanent).Contains("Ramp"));
        bool proactiveAlternativeExists = hand.Any(other => !ReferenceEquals(other, card)
            && !other.IsLand
            && other.ManaCost <= manaAvailable
            && !IsReactiveOnly(other));
        int score = 20;

        if (oracle.IndexOf("you win the game", StringComparison.OrdinalIgnoreCase) >= 0
            || oracle.IndexOf("wins the game", StringComparison.OrdinalIgnoreCase) >= 0)
            return 220;

        if (tags.Contains("Ramp"))
        {
            score += turn <= 4 ? 120 : 60;
            if (hand.Any(other => !other.IsLand && other.ManaCost >= 5))
                score += 18;
            if (battlefieldRampPieces >= 3)
                score -= 28;
            score -= card.ManaCost * 6;
        }

        if (tags.Contains("Tutor"))
            score += HasComboPiecesInHandOrBoard(hand, battlefield) ? 105 : 80;

        if (tags.Contains("Card Draw"))
        {
            score += turn <= 4 ? 88 : 70;
            if (hand.Count <= 3)
                score += 24;
        }

        if (tags.Contains("Protection") && HasImportantPermanent(battlefield))
            score += 75;

        if (tags.Contains("Creature"))
            score += Math.Max(15, 52 - Math.Abs(card.ManaCost - turn) * 6);

        if (tags.Contains("Enchantment") || tags.Contains("Artifact"))
            score += 30;

        if (tags.Contains("Board Wipe"))
            score += turn >= 5 ? 10 : -20;

        if (tags.Contains("Removal"))
            score += proactiveAlternativeExists ? -18 : 14;

        if (tags.Contains("Counterspell"))
            score += proactiveAlternativeExists ? -30 : -4;

        if (DeckAnalysis.HasType(card, "Planeswalker"))
            score += 35;

        if (oracle.IndexOf("extra turn", StringComparison.OrdinalIgnoreCase) >= 0)
            score += 140;

        if (CanAdvanceCombo(card, hand, battlefield))
            score += 60;

        if (!IsReactiveOnly(card) && card.ManaCost == manaAvailable)
            score += 12;

        if (turn <= 2 && card.ManaCost >= 5)
            score -= 35;

        return score;
    }

    private static bool IsReactiveOnly(Card card)
    {
        var tags = DeckAnalysis.GetCategoryTags(card);
        return tags.Contains("Removal") || tags.Contains("Counterspell") || tags.Contains("Board Wipe") || tags.Contains("Protection");
    }

    private static bool HasComboPiecesInHandOrBoard(IReadOnlyCollection<Card> hand, IReadOnlyCollection<Card> battlefield)
    {
        return hand.Concat(battlefield).Any(card => CanAdvanceCombo(card, hand, battlefield));
    }

    private static bool HasImportantPermanent(IReadOnlyCollection<Card> battlefield)
    {
        return battlefield.Any(card =>
        {
            var tags = DeckAnalysis.GetCategoryTags(card);
            return tags.Contains("Card Draw") || tags.Contains("Ramp") || tags.Contains("Creature") || tags.Contains("Token Generation");
        });
    }

    private static bool CanAdvanceCombo(Card card, IReadOnlyCollection<Card> hand, IReadOnlyCollection<Card> battlefield)
    {
        string oracle = (card.OracleText ?? string.Empty).ToLowerInvariant();
        bool thisCardIsPiece = oracle.Contains("you win the game")
            || oracle.Contains("take an extra turn")
            || oracle.Contains("storm")
            || oracle.Contains("untap all")
            || (oracle.Contains("untap target") && (oracle.Contains("creature") || oracle.Contains("permanent")))
            || (oracle.Contains("sacrifice") && (oracle.Contains(": add") || oracle.Contains(": draw") || oracle.Contains(": deal")));

        if (!thisCardIsPiece)
            return false;

        return hand.Concat(battlefield).Any(other => !ReferenceEquals(other, card) && !string.IsNullOrWhiteSpace(other.OracleText));
    }

    private static void ApplyCastEffects(Card card, List<Card> library, List<Card> hand, List<Card> battlefield, int turn, ref int permanentRamp, ref int manaAvailable)
    {
        var tags = DeckAnalysis.GetCategoryTags(card);
        string oracle = (card.OracleText ?? string.Empty).ToLowerInvariant();

        if (tags.Contains("Ramp") && !card.IsLand)
            permanentRamp += EstimatePermanentRamp(card, tags);

        if (tags.Contains("Card Draw"))
            DrawCards(library, hand, EstimateDrawCount(card, oracle));

        if (tags.Contains("Tutor"))
        {
            var target = ChooseTutorTarget(library, hand, battlefield, turn);
            if (target != null)
            {
                library.Remove(target);
                hand.Add(target);
            }
        }

        if (!tags.Contains("Ramp") && oracle.Contains("add {") && !DeckAnalysis.IsPermanent(card))
            manaAvailable += 1;
    }

    private static int EstimatePermanentRamp(Card card, HashSet<string> tags)
    {
        if (!DeckAnalysis.IsPermanent(card))
            return 0;
        if (card.ManaCost <= 1)
            return 2;
        if (card.ManaCost == 2)
            return 1;
        if (card.ManaCost == 3 && tags.Contains("Creature"))
            return 1;
        return 0;
    }

    private static int EstimateDrawCount(Card card, string oracle)
    {
        if (oracle.Contains("draw three"))
            return 3;
        if (oracle.Contains("draw two"))
            return 2;
        if (oracle.Contains("draw a card") || oracle.Contains("draw card") || oracle.Contains("investigate"))
            return 1;
        return card.ManaCost <= 2 ? 1 : 2;
    }

    private static Card? ChooseTutorTarget(List<Card> library, IReadOnlyCollection<Card> hand, IReadOnlyCollection<Card> battlefield, int turn)
    {
        var libraryCards = library.Where(card => !card.IsLand).ToList();
        if (!libraryCards.Any())
            return null;

        var comboTarget = libraryCards.FirstOrDefault(card => CanAdvanceCombo(card, hand, battlefield));
        if (comboTarget != null)
            return comboTarget;

        if (turn <= 3)
        {
            var ramp = libraryCards.FirstOrDefault(card => DeckAnalysis.GetCategoryTags(card).Contains("Ramp"));
            if (ramp != null)
                return ramp;
        }

        var draw = libraryCards.FirstOrDefault(card => DeckAnalysis.GetCategoryTags(card).Contains("Card Draw"));
        if (draw != null)
            return draw;

        return libraryCards
            .OrderByDescending(card => ScoreCardForPriority(card, battlefield, hand, turn, turn + 2))
            .ThenBy(card => card.ManaCost)
            .FirstOrDefault();
    }
}

public class DeckEvaluator
{
    private readonly Deck _deck;

    public DeckEvaluator(Deck deck)
    {
        _deck = deck;
    }

    public EvaluationResults RunSimulations(int numSimulations, int maxTurns, bool onDraw = false, Action<int>? progressCallback = null)
    {
        var bot = new GoldfishBot(_deck);
        var results = new List<SimulationResult>(numSimulations);
        int lastReported = -1;

        for (int simulation = 0; simulation < numSimulations; simulation++)
        {
            var result = bot.SimulateGame(maxTurns, onDraw);
            results.Add(result);

            if (progressCallback != null)
            {
                int progressValue = simulation + 1;
                int scaled = (int)Math.Round(progressValue * 1000.0 / numSimulations);
                if (scaled != lastReported || simulation == numSimulations - 1)
                {
                    progressCallback(progressValue);
                    lastReported = scaled;
                }
            }
        }

        var evaluation = new EvaluationResults
        {
            AverageMissedLands = results.Average(result => result.MissedLands),
            AverageLandsPlayed = results.Average(result => result.LandsPlayed),
            AverageCardsPlayable = results.Average(result => result.CardsPlayable),
            AverageIdleTurns = results.Average(result => result.IdleTurns),
            AverageSpellsCast = results.Average(result => result.SpellsCast),
            AverageOpeningHandLands = results.Average(result => result.OpeningHandLands),
            BrickHandPercent = results.Count(result => result.BrickHand) * 100.0 / results.Count,
            FloodHandPercent = results.Count(result => result.FloodHand) * 100.0 / results.Count,
            AveragePeakMana = results.Average(result => result.PeakMana),
            AverageMulligans = results.Average(result => result.MulligansTaken),
            AverageManaProduced = results.Average(result => result.ManaProduced),
            AverageManaSpent = results.Average(result => result.ManaSpent),
            AverageManaEfficiency = results.Average(result => result.ManaProduced > 0 ? result.ManaSpent * 100.0 / result.ManaProduced : 0),
            AverageEarlyTurnActions = results.Average(result => result.EarlyTurnActions),
            AverageStrandedHighCostCards = results.Average(result => result.StrandedHighCostCards)
        };

        GenerateRecommendations(evaluation, _deck, maxTurns);
        return evaluation;
    }

    private void GenerateRecommendations(EvaluationResults results, Deck deck, int maxTurns)
    {
        static string FormatNameList(IEnumerable<string> names)
        {
            var list = names.Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).Take(5).ToList();
            return list.Any() ? string.Join(", ", list) : "none found";
        }

        List<string> GetTopNamesByTag(string tag)
        {
            return deck.Cards
                .Where(card => !card.IsLand && DeckAnalysis.GetCategoryTags(card).Contains(tag) && !string.IsNullOrWhiteSpace(card.Name))
                .GroupBy(card => card.Name!.Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .Select(group => group.Key)
                .Take(5)
                .ToList();
        }

        List<string> GetTrimCandidatesByTag(string tag)
        {
            return deck.Cards
                .Where(card => !card.IsLand && DeckAnalysis.GetCategoryTags(card).Contains(tag) && !string.IsNullOrWhiteSpace(card.Name))
                .OrderByDescending(card => card.ManaCost)
                .ThenBy(card => card.Name)
                .Select(card => card.Name!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToList();
        }

        List<string> GetTopNamesByType(string type)
        {
            return deck.Cards
                .Where(card => !card.IsLand && DeckAnalysis.HasType(card, type) && !string.IsNullOrWhiteSpace(card.Name))
                .GroupBy(card => card.Name!.Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .Select(group => group.Key)
                .Take(5)
                .ToList();
        }

        results.Recommendations.Add("\n=== MANA & LAND ANALYSIS ===");
        int landCount = deck.LandCount;
        int nonLandCount = deck.Cards.Count - landCount;
        double landPercentage = landCount * 100.0 / deck.Cards.Count;

        if (results.AverageMissedLands > 2)
            results.Recommendations.Add($"🚨 HIGH PRIORITY: Avg {results.AverageMissedLands:F2} missed land drops. Add {Math.Ceiling(results.AverageMissedLands)} more lands (currently {landCount} / {landPercentage:F1}%)");
        else if (results.AverageMissedLands < 0.2)
            results.Recommendations.Add($"💡 Excess lands: Avg {results.AverageMissedLands:F2} missed drops suggests {landCount - 2}-{landCount - 4} lands might work better");
        else
            results.Recommendations.Add($"✓ Lands optimal: {landCount} lands ({landPercentage:F1}%) - {results.AverageMissedLands:F2} avg missed drops");

        if (results.AverageMulligans > 0.75)
            results.Recommendations.Add($"⚠️ Opening hands are shaky: average mulligans {results.AverageMulligans:F2}. Add early plays, more lands, or smoother ramp.");
        else
            results.Recommendations.Add($"✓ Opening hand stability looks solid: average mulligans {results.AverageMulligans:F2}.");

        results.Recommendations.Add("\n=== MANA CURVE PROJECTION ===");
        var manaCurve = deck.GetManaCurve();
        int lowCost = manaCurve.Where(kvp => kvp.Key <= 2).Sum(kvp => kvp.Value);
        int midCost = manaCurve.Where(kvp => kvp.Key >= 3 && kvp.Key <= 4).Sum(kvp => kvp.Value);
        int highCost = manaCurve.Where(kvp => kvp.Key >= 5).Sum(kvp => kvp.Value);
        double lowPct = nonLandCount > 0 ? lowCost * 100.0 / nonLandCount : 0;
        double midPct = nonLandCount > 0 ? midCost * 100.0 / nonLandCount : 0;
        double highPct = nonLandCount > 0 ? highCost * 100.0 / nonLandCount : 0;
        var barString = "░░░░░░░░░░";
        results.Recommendations.Add($"Early (0-2): {lowCost} cards ({lowPct:F1}%) - {barString.Substring(0, Math.Min(barString.Length, (int)(lowPct / 10)))}");
        results.Recommendations.Add($"Mid (3-4):   {midCost} cards ({midPct:F1}%) - {barString.Substring(0, Math.Min(barString.Length, (int)(midPct / 10)))}");
        results.Recommendations.Add($"Late (5+):   {highCost} cards ({highPct:F1}%) - {barString.Substring(0, Math.Min(barString.Length, (int)(highPct / 10)))}");

        if (highCost > lowCost * 1.5)
            results.Recommendations.Add($"📊 Top-heavy curve detected. Add {Math.Ceiling((highCost - lowCost) / 2.0)} low-cost spells.");
        else if (lowCost > highCost * 2)
            results.Recommendations.Add($"📊 Consider adding {Math.Ceiling((lowCost - highCost) / 3.0)} higher-cost finishers.");
        else
            results.Recommendations.Add("✓ Mana curve is well-balanced");

        results.Recommendations.Add("\n=== CREATURE & SPELL BREAKDOWN ===");
        int creatureCount = deck.Cards.Count(card => !card.IsLand && DeckAnalysis.HasType(card, "Creature"));
        int instantCount = deck.Cards.Count(card => !card.IsLand && DeckAnalysis.HasType(card, "Instant"));
        int sorceryCount = deck.Cards.Count(card => !card.IsLand && DeckAnalysis.HasType(card, "Sorcery"));
        int artifactCount = deck.Cards.Count(card => !card.IsLand && DeckAnalysis.HasType(card, "Artifact"));
        int enchantmentCount = deck.Cards.Count(card => !card.IsLand && DeckAnalysis.HasType(card, "Enchantment"));
        int planeswalkerCount = deck.Cards.Count(card => !card.IsLand && DeckAnalysis.HasType(card, "Planeswalker"));

        if (creatureCount < 10)
            results.Recommendations.Add($"⚠️  Creatures: {creatureCount} - Add {10 - creatureCount} for better board presence");
        else if (creatureCount < 20)
            results.Recommendations.Add($"✓ Creatures: {creatureCount} - Good foundation for board development");
        else if (creatureCount <= 28)
            results.Recommendations.Add($"✓ Creatures: {creatureCount} - Strong creature count");
        else
            results.Recommendations.Add($"⚠️  Creatures: {creatureCount} - Consider focusing on instant/sorcery strategy");

        if (instantCount + sorceryCount >= 12)
            results.Recommendations.Add($"✓ Instants/Sorceries: {instantCount + sorceryCount} - Good spell base for interaction (examples: {FormatNameList(GetTopNamesByType("Instant").Concat(GetTopNamesByType("Sorcery")))})");
        else
            results.Recommendations.Add($"⚠️  Instants/Sorceries: {instantCount + sorceryCount} - Consider increasing to ~12+ for better interaction windows. Current examples: {FormatNameList(GetTopNamesByType("Instant").Concat(GetTopNamesByType("Sorcery")))}");

        if (artifactCount > 0)
            results.Recommendations.Add($"Artifacts: {artifactCount} - {(artifactCount < 5 ? "Utility pieces" : "Strong artifact synergy")}");
        if (enchantmentCount > 0)
            results.Recommendations.Add($"Enchantments: {enchantmentCount}");
        if (planeswalkerCount > 0)
            results.Recommendations.Add($"Planeswalkers: {planeswalkerCount}");

        results.Recommendations.Add("\n=== INTERACTION & CARD ADVANTAGE ===");
        int removalCount = deck.Cards.Count(card => !card.IsLand && DeckAnalysis.GetCategoryTags(card).Contains("Removal"));
        int drawCount = deck.Cards.Count(card => !card.IsLand && DeckAnalysis.GetCategoryTags(card).Contains("Card Draw"));
        int counterCount = deck.Cards.Count(card => !card.IsLand && DeckAnalysis.GetCategoryTags(card).Contains("Counterspell"));
        int interactionCount = removalCount + counterCount;

        const int removalMin = 8;
        const int removalMax = 14;
        const int drawMin = 8;
        const int drawMax = 14;
        const int interactionMin = 12;
        const int interactionMax = 20;

        results.Recommendations.Add($"Removal: {removalCount} (target {removalMin}-{removalMax})");
        if (removalCount < removalMin)
            results.Recommendations.Add($"⚠️  Run more removal: add ~{removalMin - removalCount} to {removalMin - removalCount + 2} pieces of interaction. Current removal cards: {FormatNameList(GetTopNamesByTag("Removal"))}");
        else if (removalCount > removalMax)
            results.Recommendations.Add($"💡 Removal is high: you can trim ~{Math.Max(1, removalCount - removalMax)} for proactive threats/synergy. Trim candidates: {FormatNameList(GetTrimCandidatesByTag("Removal"))}");
        else
            results.Recommendations.Add($"✓ Removal count is in a strong range (cards: {FormatNameList(GetTopNamesByTag("Removal"))}).");

        results.Recommendations.Add($"Card Draw: {drawCount} (target {drawMin}-{drawMax})");
        if (drawCount < drawMin)
            results.Recommendations.Add($"⚠️  Add card draw: include ~{drawMin - drawCount} to {drawMin - drawCount + 2} more repeatable draw effects. Current draw cards: {FormatNameList(GetTopNamesByTag("Card Draw"))}");
        else if (drawCount > drawMax)
            results.Recommendations.Add($"💡 Draw is high: trim ~{Math.Max(1, drawCount - drawMax)} draw spells if the deck feels low on board impact. Trim candidates: {FormatNameList(GetTrimCandidatesByTag("Card Draw"))}");
        else
            results.Recommendations.Add($"✓ Card draw count looks healthy (cards: {FormatNameList(GetTopNamesByTag("Card Draw"))}).");

        results.Recommendations.Add($"Total Interaction (Removal + Counterspell): {interactionCount} (target {interactionMin}-{interactionMax})");
        if (interactionCount < interactionMin)
            results.Recommendations.Add($"⚠️  Increase interaction by ~{interactionMin - interactionCount} cards so you can answer more threats.");
        else if (interactionCount > interactionMax)
            results.Recommendations.Add($"💡 Interaction is heavy: trim ~{Math.Max(1, interactionCount - interactionMax)} pieces for win-cons or synergy.");
        else
            results.Recommendations.Add("✓ Interaction density is balanced.");

        results.Recommendations.Add("\n=== RAMP ANALYSIS ===");
        int rampCount = deck.Cards.Count(card => !card.IsLand && DeckAnalysis.GetCategoryTags(card).Contains("Ramp"));
        const int rampMin = 8;
        const int rampMax = 14;
        if (rampCount < rampMin)
            results.Recommendations.Add($"⚠️  Ramp is low ({rampCount} / target {rampMin}-{rampMax}): add ~{rampMin - rampCount} mana accelerators. Current ramp: {FormatNameList(GetTopNamesByTag("Ramp"))}");
        else if (rampCount > rampMax)
            results.Recommendations.Add($"💡 Ramp is high ({rampCount}): trim ~{Math.Max(1, rampCount - rampMax)} for more threats. Trim candidates: {FormatNameList(GetTrimCandidatesByTag("Ramp"))}");
        else
            results.Recommendations.Add($"✓ Ramp is healthy ({rampCount} / target {rampMin}-{rampMax}): {FormatNameList(GetTopNamesByTag("Ramp"))}");

        results.Recommendations.Add("\n=== PLAYABILITY METRICS ===");
        double cardsPerTurn = maxTurns > 0 ? results.AverageCardsPlayable / maxTurns : 0;
        results.Recommendations.Add($"Average cards playable per turn: {cardsPerTurn:F2}");
        results.Recommendations.Add($"Average idle turns: {results.AverageIdleTurns:F2} out of {maxTurns}");
        results.Recommendations.Add($"Average mulligans: {results.AverageMulligans:F2}");
        results.Recommendations.Add($"Mana efficiency: {results.AverageManaEfficiency:F1}% of produced mana converted into casts");
        results.Recommendations.Add($"Early turn actions (T1-T3): {results.AverageEarlyTurnActions:F2}");
        results.Recommendations.Add($"Stranded 5+ mana cards in hand at game end: {results.AverageStrandedHighCostCards:F2}");

        if (results.AverageIdleTurns > 2)
            results.Recommendations.Add("⚠️  Too many idle turns. Consider adding more low-cost cards or mana acceleration.");
        else if (results.AverageIdleTurns < 0.5)
            results.Recommendations.Add("✓ Excellent consistency - plenty to play each turn!");

        if (results.AverageManaEfficiency < 55)
            results.Recommendations.Add("⚠️  Mana efficiency is low. The deck is leaving too much mana unused, which usually means too many reactive spells, too many expensive cards, or not enough cheap card flow.");
        else if (results.AverageManaEfficiency >= 75)
            results.Recommendations.Add("✓ Mana efficiency is strong. Your goldfish turns are spending mana consistently.");

        if (results.AverageEarlyTurnActions < 1.5)
            results.Recommendations.Add("⚠️  Early development is light. Add more 1-3 mana plays so the deck starts affecting the game sooner.");
        else if (results.AverageEarlyTurnActions >= 2.4)
            results.Recommendations.Add("✓ Early-game setup is reliable. The deck usually gets on board or advances its plan by turn 3.");

        if (results.AverageStrandedHighCostCards > 2.0)
            results.Recommendations.Add($"💡 Expensive cards are getting stranded in hand. Trim a few 5+ mana spells or add more ramp. Likely trim candidates: {FormatNameList(deck.Cards.Where(card => !card.IsLand && card.ManaCost >= 5).OrderByDescending(card => card.ManaCost).Select(card => card.Name ?? string.Empty))}");
    }
}
