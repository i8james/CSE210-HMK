using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

internal sealed class GoldfishBot
{
    private readonly Deck _deck;
    private readonly Random _random = new Random();
    private readonly GoldfishLearningProfile _learningProfile;

    private sealed class TurnPlanResult
    {
        public int Score { get; set; }
        public List<GoldfishAction> Sequence { get; } = new List<GoldfishAction>();
    }

    private readonly DeckArchetype _archetype;
    private readonly string _primaryTribe;
    public int LearnedGames => _learningProfile.GamesPlayed;

    public GoldfishBot(Deck deck, DeckArchetype archetype)
    {
        _deck = deck;
        _archetype = archetype;
        _primaryTribe = DeckAnalysis.TryGetPrimaryTribe(deck, out string tribe, out _, out _) ? tribe : string.Empty;
        _learningProfile = GoldfishLearningStore.LoadProfile(deck);
    }

    public void SaveLearning()
    {
        GoldfishLearningStore.SaveProfile(_learningProfile);
    }

    public SimulationResult SimulateGame(int maxTurns, bool onDraw = false, CancellationToken cancellationToken = default)
    {
        int mulligansTaken;
        var library = _deck.GetShuffledDeck(_random);
        var hand = BuildOpeningHand(ref library, out mulligansTaken);

        int landsPlayed = 0;
        int missedLands = 0;
        int spellsCast = 0;
        int idleTurns = 0;
        int peakMana = 0;
        int manaProduced = 0;
        int manaSpent = 0;
        int earlyTurnActions = 0;
        int commanderCastTurn = 0;
        int activatedAbilitiesUsed = 0;
        int triggeredAbilitiesResolved = 0;
        bool wonViaInfiniteCombo = false;
        string comboLine = string.Empty;
        List<string> actionLog = new List<string>();
        int openingHandLands = hand.Count(card => card.IsLand);
        var battlefield = new List<Card>();
        var cardsCastThisGame = new List<Card>();
        int landsInPlay = 0;
        int permanentRamp = 0;
        bool commanderAvailable = _deck.Commander != null;
        var spellbookAssemblies = new List<string>();

        for (int turn = 1; turn <= maxTurns; turn++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (turn > 1 || onDraw)
                DrawCards(library, hand, 1);

            bool playedAnything = false;
            bool commanderWasAvailable = commanderAvailable;
            var state = new GoldfishGameState(library, hand, battlefield, _deck.Commander, commanderAvailable, turn, landsInPlay, permanentRamp, _deck.SpellbookReport?.Included);
            
            // Apply persistent effects from permanents at the beginning of turn
            ApplyBeginningOfTurnEffects(state);
            SpellbookComboTracker.Track(state);
            
            int manaAtStartOfTurn = state.ManaAvailable;
            peakMana = Math.Max(peakMana, state.ManaAvailable);
            manaProduced += manaAtStartOfTurn;

            while (true)
            {
                var bestAction = ChooseBestAction(state);
                if (bestAction == null || bestAction.Type == GoldfishActionType.PassPhase)
                    break;

                playedAnything = true;
                if (turn <= 3 && bestAction.Type != GoldfishActionType.PlayLand)
                    earlyTurnActions++;
                if ((bestAction.Type == GoldfishActionType.CastSpell || bestAction.Type == GoldfishActionType.CastCommander) && bestAction.Card != null)
                    cardsCastThisGame.Add(bestAction.Card);
                ExecuteAction(state, bestAction);
                if (state.InfiniteComboAchieved)
                    break;
            }

            if (state.InfiniteComboAchieved)
            {
                wonViaInfiniteCombo = true;
                comboLine = state.InfiniteComboLine;
                actionLog = state.ActionLog.Take(8).ToList();
                activatedAbilitiesUsed = state.ActivatedAbilitiesUsed;
                triggeredAbilitiesResolved = state.TriggeredAbilitiesResolved;
                hand = state.Hand;
                battlefield = state.Battlefield;
                library = state.Library;
                manaSpent += state.ManaSpent;
                spellsCast += state.SpellsCast;
                commanderAvailable = state.CommanderAvailable;
                landsPlayed += state.LandPlayedThisTurn ? 1 : 0;
                landsInPlay = state.LandsInPlay;
                permanentRamp = state.PermanentRamp;
                spellbookAssemblies = state.SpellbookComboAssemblies.ToList();
                break;
            }

            if (!state.LandPlayedThisTurn)
                missedLands++;

            landsPlayed += state.LandPlayedThisTurn ? 1 : 0;
            landsInPlay = state.LandsInPlay;
            permanentRamp = state.PermanentRamp;
            hand = state.Hand;
            battlefield = state.Battlefield;
            library = state.Library;
            manaSpent += state.ManaSpent;
            spellsCast += state.SpellsCast;
            commanderAvailable = state.CommanderAvailable;
            activatedAbilitiesUsed = state.ActivatedAbilitiesUsed;
            triggeredAbilitiesResolved = state.TriggeredAbilitiesResolved;
            wonViaInfiniteCombo = state.InfiniteComboAchieved;
            comboLine = state.InfiniteComboLine;
            actionLog = state.ActionLog.Take(8).ToList();
            spellbookAssemblies = state.SpellbookComboAssemblies.ToList();

            if (commanderWasAvailable && !commanderAvailable && commanderCastTurn == 0)
                commanderCastTurn = turn;

            if (!playedAnything)
                idleTurns++;
        }

        var result = new SimulationResult
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
            StrandedHighCostCards = hand.Count(card => !card.IsLand && card.ManaCost >= 5),
            CommanderCastTurn = commanderCastTurn,
            CommanderCast = commanderCastTurn > 0,
            WonViaInfiniteCombo = wonViaInfiniteCombo,
            ComboLine = comboLine,
            ActivatedAbilitiesUsed = activatedAbilitiesUsed,
            TriggeredAbilitiesResolved = triggeredAbilitiesResolved,
            ActionLog = actionLog,
            SpellbookComboAssemblies = spellbookAssemblies
        };

        LearnFromSimulation(result, cardsCastThisGame);
        return result;
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

    private GoldfishAction? ChooseBestAction(GoldfishGameState state)
    {
        var bestPlan = BuildBestTurnPlan(state, 0, GetPlanningDepth(state));
        if (!bestPlan.Sequence.Any())
            return null;

        return bestPlan.Sequence[0];
    }

    private static int GetPlanningDepth(GoldfishGameState state)
    {
        int playableCount = GoldfishLegalActionGenerator.GetLegalActions(state).Count(action => action.Type != GoldfishActionType.PassPhase);
        if (state.CommanderAvailable && state.Commander != null)
            playableCount++;
        if (state.Turn <= 3 && playableCount <= 7)
            return 3;
        if (state.Turn <= 5 && playableCount <= 9)
            return 2;
        return 1;
    }

    private TurnPlanResult BuildBestTurnPlan(GoldfishGameState state, int depth, int maxDepth)
    {
        int baselineScore = EvaluatePlanningState(state);
        var best = new TurnPlanResult { Score = baselineScore };

        var legalActions = GoldfishLegalActionGenerator.GetLegalActions(state)
            .Select(action => new
            {
                Action = action,
                Priority = ScoreActionForPriority(action, state)
            })
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.Action.ManaCost)
            .ThenBy(item => item.Action.Card?.Name)
            .Take(depth == 0 ? 6 : 4)
            .ToList();

        foreach (var candidate in legalActions)
        {
            if (candidate.Action.Type == GoldfishActionType.PassPhase)
                continue;
            if (candidate.Priority < 0)
                continue;

            var nextState = state.Clone();
            ExecuteAction(nextState, candidate.Action);
            int candidateScore = candidate.Priority + EvaluatePlanningState(nextState);

            TurnPlanResult futurePlan;
            if (depth + 1 < maxDepth && GoldfishLegalActionGenerator.GetLegalActions(nextState).Any(action => action.Type != GoldfishActionType.PassPhase))
                futurePlan = BuildBestTurnPlan(nextState, depth + 1, maxDepth);
            else
                futurePlan = new TurnPlanResult { Score = EvaluatePlanningState(nextState) };

            int totalScore = candidateScore + futurePlan.Score;
            if (totalScore <= best.Score)
                continue;

            best = new TurnPlanResult { Score = totalScore };
            best.Sequence.Add(candidate.Action);
            best.Sequence.AddRange(futurePlan.Sequence);
        }

        return best;
    }

    private int EvaluatePlanningState(GoldfishGameState state)
    {
        int battlefieldRamp = state.Battlefield.Count(card => DeckAnalysis.GetCategoryTags(card).Contains("Ramp"));
        int battlefieldDraw = state.Battlefield.Count(card => DeckAnalysis.GetCategoryTags(card).Contains("Card Draw"));
        int cheapFollowUps = state.Hand.Count(card => !card.IsLand && card.ManaCost <= Math.Max(2, state.Turn + 1));
        int strandedExpensiveCards = state.Hand.Count(card => !card.IsLand && card.ManaCost >= state.Turn + 4);
        int comboPieces = state.Hand.Concat(state.Battlefield).Count(card => CanAdvanceCombo(card, state.Hand, state.Battlefield));
        bool commanderOnBoard = state.Battlefield.Any(card => card.IsCommander);
        int commanderValue = commanderOnBoard ? ScoreCommanderPresence(state.Commander) : 0;

        return state.ManaSpent * 10
            + state.SpellsCast * 18
            + state.CardsDrawn * 12
            + battlefieldRamp * 20
            + battlefieldDraw * 16
            + cheapFollowUps * 4
            + comboPieces * 10
            + commanderValue
            - state.ManaAvailable * 8
            - strandedExpensiveCards * 7;
    }

    private int ScoreActionForPriority(GoldfishAction action, GoldfishGameState state)
    {
        return action.Type switch
        {
            GoldfishActionType.PlayLand => state.LandPlayedThisTurn ? -100 : (state.Turn <= 4 ? 220 : 160),
            GoldfishActionType.CastCommander => ScoreCommanderCast(state),
            GoldfishActionType.CastSpell when action.Card != null => ScoreCardForPriority(action.Card, state.Battlefield, state.Hand, state.Turn, state.ManaAvailable),
            GoldfishActionType.ActivateAbility when action.Card != null => ScoreActivatedAbility(action.Card, state),
            _ => -1
        };
    }

    private int ScoreActivatedAbility(Card card, GoldfishGameState state)
    {
        string oracle = (card.OracleText ?? string.Empty).ToLowerInvariant();
        int score = 25;
        if (oracle.Contains("draw")) score += 45;
        if (oracle.Contains("search") || oracle.Contains("fetch")) score += 48;
        if (oracle.Contains("create") && oracle.Contains("token")) score += 35;
        if (oracle.Contains("add {")) score += 40;
        if (oracle.Contains("proliferate")) score += 38;
        if (oracle.Contains("untap")) score += 28;
        if (CanRepresentInfiniteCombo(card, state.Hand, state.Battlefield)) score += 120;
        
        // Boost score for abilities on permanents that are known good cards
        var tags = DeckAnalysis.GetCategoryTags(card);
        if (tags.Contains("Ramp") || tags.Contains("Card Draw") || tags.Contains("Tutor"))
            score += 15;
        
        return score;
    }

    private int ScoreCommanderCast(GoldfishGameState state)
    {
        if (state.Commander == null)
            return -1;

        var commander = state.Commander;
        var tags = DeckAnalysis.GetCategoryTags(commander);
        int score = 75;

        if (state.Turn <= 5)
            score += 35;
        if (tags.Contains("Ramp") || tags.Contains("Card Draw") || tags.Contains("Tutor") || tags.Contains("Token Generation"))
            score += 45;
        if (_archetype == DeckArchetype.Tribal && !string.IsNullOrWhiteSpace(_primaryTribe) && DeckAnalysis.HasTribe(commander, _primaryTribe))
            score += 35;
        if (_archetype == DeckArchetype.Spellslinger && (DeckAnalysis.HasType(commander, "Wizard") || tags.Contains("Card Draw")))
            score += 18;
        if (_archetype == DeckArchetype.Combo && tags.Contains("Tutor"))
            score += 22;
        if (state.ManaAvailable == commander.ManaCost + state.CommanderTax)
            score += 12;
        score += (int)Math.Round(_learningProfile.CommanderBias);

        return score;
    }

    private int ScoreCommanderPresence(Card? commander)
    {
        if (commander == null)
            return 0;

        var tags = DeckAnalysis.GetCategoryTags(commander);
        int score = 18;
        if (tags.Contains("Ramp") || tags.Contains("Card Draw") || tags.Contains("Token Generation"))
            score += 20;
        if (_archetype == DeckArchetype.Tribal && !string.IsNullOrWhiteSpace(_primaryTribe) && DeckAnalysis.HasTribe(commander, _primaryTribe))
            score += 15;
        return score;
    }

    private static int EstimateImmediateRampGain(Card card, HashSet<string> tags, string oracle)
    {
        if (!DeckAnalysis.IsPermanent(card) || DeckAnalysis.HasType(card, "Creature"))
            return 0;
        if (!tags.Contains("Ramp"))
            return 0;
        if (oracle.Contains("{t}: add {"))
            return 1;
        if (card.ManaCost <= 1)
            return 1;
        return 0;
    }

    private static int EstimateTemporaryManaGain(string oracle)
    {
        if (oracle.Contains("add {c}{c}{c}"))
            return 3;
        if (oracle.Contains("add {c}{c}") || oracle.Contains("add {r}{r}") || oracle.Contains("add {g}{g}") || oracle.Contains("add {u}{u}") || oracle.Contains("add {w}{w}") || oracle.Contains("add {b}{b}"))
            return 2;
        if (oracle.Contains("add {"))
            return 1;
        return 0;
    }

    private int ScoreCardForPriority(Card card, IReadOnlyCollection<Card> battlefield, IReadOnlyCollection<Card> hand, int turn, int manaAvailable)
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
        
        // Bonus for permanents with persistent search effects
        if (tags.Contains("Tutor") && DeckAnalysis.IsPermanent(card) && (oracle.Contains("at the beginning") || oracle.Contains("whenever")))
            score += 32;

        if (tags.Contains("Card Draw"))
        {
            score += turn <= 4 ? 88 : 70;
            if (hand.Count <= 3)
                score += 24;
            // Bonus for permanents with persistent draw effects
            if (DeckAnalysis.IsPermanent(card) && (oracle.Contains("at the beginning") || oracle.Contains("whenever")))
                score += 35;
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

        score += ApplyArchetypeAdjustment(card, tags, battlefield);
        score += GetLearningAdjustment(tags, card.IsCommander);

        return score;
    }

    private int GetLearningAdjustment(HashSet<string> tags, bool isCommander)
    {
        double score = isCommander ? _learningProfile.CommanderBias : 0;
        foreach (var tag in tags)
        {
            if (_learningProfile.TagBiases.TryGetValue(tag, out double bias))
                score += bias;
        }

        return (int)Math.Round(score);
    }

    private int ApplyArchetypeAdjustment(Card card, HashSet<string> tags, IReadOnlyCollection<Card> battlefield)
    {
        int score = 0;
        switch (_archetype)
        {
            case DeckArchetype.Ramp:
                if (tags.Contains("Ramp")) score += 20;
                if (card.ManaCost >= 5) score += battlefield.Count(permanent => DeckAnalysis.GetCategoryTags(permanent).Contains("Ramp")) >= 2 ? 16 : 6;
                break;
            case DeckArchetype.Spellslinger:
                if (DeckAnalysis.HasType(card, "Instant") || DeckAnalysis.HasType(card, "Sorcery")) score += 26;
                if (tags.Contains("Card Draw") || tags.Contains("Tutor")) score += 12;
                if (tags.Contains("Creature") && card.ManaCost >= 4) score -= 10;
                break;
            case DeckArchetype.Tribal:
                if (!string.IsNullOrWhiteSpace(_primaryTribe) && DeckAnalysis.HasTribe(card, _primaryTribe)) score += 24;
                if (tags.Contains("Creature")) score += 8;
                break;
            case DeckArchetype.Tokens:
                if (tags.Contains("Token Generation")) score += 28;
                if (tags.Contains("Creature") || tags.Contains("Enchantment")) score += 8;
                break;
            case DeckArchetype.Combo:
                if (tags.Contains("Tutor")) score += 28;
                if (CanAdvanceCombo(card, battlefield, battlefield)) score += 24;
                if (tags.Contains("Creature") && card.ManaCost >= 5 && !tags.Contains("Tutor")) score -= 8;
                break;
        }

        return score;
    }

    private void ExecuteAction(GoldfishGameState state, GoldfishAction action)
    {
        switch (action.Type)
        {
            case GoldfishActionType.PlayLand when action.Card != null:
                state.Hand.Remove(action.Card);
                state.Battlefield.Add(action.Card);
                state.LandsInPlay++;
                state.LandPlayedThisTurn = true;
                state.ManaAvailable++;
                break;

            case GoldfishActionType.CastCommander when action.Card != null:
                state.ManaAvailable -= action.ManaCost;
                state.ManaSpent += action.ManaCost;
                state.SpellsCast++;
                state.CommanderAvailable = false;
                state.Battlefield.Add(action.Card);
                int commanderRamp = state.PermanentRamp;
                int commanderMana = state.ManaAvailable;
                ApplyCastEffects(action.Card, state.Library, state.Hand, state.Battlefield, state.Turn, ref commanderRamp, ref commanderMana);
                ResolveTriggeredAbilities(state, action.Card);
                state.ActionLog.Add($"T{state.Turn}: cast commander {action.Card.Name}");
                state.PermanentRamp = commanderRamp;
                state.ManaAvailable = commanderMana;
                break;

            case GoldfishActionType.CastSpell when action.Card != null:
                state.Hand.Remove(action.Card);
                state.ManaAvailable -= action.ManaCost;
                state.ManaSpent += action.ManaCost;
                state.SpellsCast++;
                if (DeckAnalysis.IsPermanent(action.Card))
                    state.Battlefield.Add(action.Card);
                else
                    state.Graveyard.Add(action.Card);

                var tags = DeckAnalysis.GetCategoryTags(action.Card);
                string oracle = (action.Card.OracleText ?? string.Empty).ToLowerInvariant();
                if (tags.Contains("Ramp") && !action.Card.IsLand)
                    state.ManaAvailable += EstimateImmediateRampGain(action.Card, tags, oracle);

                int handCountBefore = state.Hand.Count;
                int spellRamp = state.PermanentRamp;
                int spellMana = state.ManaAvailable;
                ApplyCastEffects(action.Card, state.Library, state.Hand, state.Battlefield, state.Turn, ref spellRamp, ref spellMana);
                ResolveTriggeredAbilities(state, action.Card);
                state.ActionLog.Add($"T{state.Turn}: cast {action.Card.Name}");
                state.PermanentRamp = spellRamp;
                state.ManaAvailable = spellMana;
                state.CardsDrawn += Math.Max(0, state.Hand.Count - handCountBefore);
                break;

            case GoldfishActionType.ActivateAbility when action.Card != null:
                state.ManaAvailable -= action.ManaCost;
                state.ManaSpent += action.ManaCost;
                ResolveActivatedAbility(state, action.Card);
                state.ActivatedAbilitiesUsed++;
                state.ActivatedThisTurn.Add(action.Card);
                state.ActionLog.Add($"T{state.Turn}: activated {action.Card.Name}");
                break;
        }

        SpellbookComboTracker.Track(state);
        if (!state.InfiniteComboAchieved)
            DetectInfiniteCombo(state, action.Card);
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

    private static bool CanRepresentInfiniteCombo(Card card, IReadOnlyCollection<Card> hand, IReadOnlyCollection<Card> battlefield)
    {
        return CanAdvanceCombo(card, hand, battlefield);
    }

    private void ApplyBeginningOfTurnEffects(GoldfishGameState state)
    {
        // Reset activated abilities per turn (but not for once-only abilities)
        state.ActivatedThisTurn.Clear();

        // Apply permanent-based draw effects
        foreach (var permanent in state.Battlefield)
        {
            if (HasPersistentDrawEffect(permanent, state))
            {
                string oracle = permanent.OracleText ?? string.Empty;
                int drawCount = EstimateDrawCount(permanent, oracle);
                DrawCards(state.Library, state.Hand, drawCount);
                state.TriggeredAbilitiesResolved++;
            }

            if (HasPersistentTutorEffect(permanent, state))
            {
                var target = ChooseTutorTarget(state.Library, state.Hand, state.Battlefield, state.Turn);
                if (target != null)
                {
                    state.Library.Remove(target);
                    state.Hand.Add(target);
                }
                state.TriggeredAbilitiesResolved++;
            }

            if (HasPersistentTokenEffect(permanent))
            {
                state.TriggeredAbilitiesResolved++;
            }
        }
    }

    private static bool HasPersistentDrawEffect(Card card, GoldfishGameState state)
    {
        if (card == null || string.IsNullOrWhiteSpace(card.OracleText))
            return false;

        string oracle = card.OracleText.ToLowerInvariant();
        
        // "at the beginning of your upkeep" effects that draw
        if ((oracle.Contains("at the beginning of your upkeep") || oracle.Contains("whenever") || oracle.Contains("at the beginning of your end step")) 
            && oracle.Contains("draw"))
            return true;

        // Triggered draw effects that happen often
        if (oracle.Contains("whenever you") && oracle.Contains("draw"))
            return true;

        // Cards that draw as part of their static ability per turn
        if (oracle.Contains("you draw an additional card") || oracle.Contains("as you draw cards"))
            return true;

        return false;
    }

    private static bool HasPersistentTutorEffect(Card card, GoldfishGameState state)
    {
        if (card == null || string.IsNullOrWhiteSpace(card.OracleText))
            return false;

        string oracle = card.OracleText.ToLowerInvariant();
        
        // "at the beginning" + tutor/search effects
        if ((oracle.Contains("at the beginning of your upkeep") || oracle.Contains("at the beginning of combat")) 
            && (oracle.Contains("choose") || oracle.Contains("search") || oracle.Contains("fetch")))
            return true;

        // Triggered tutors
        if (oracle.Contains("whenever") && (oracle.Contains("search your library") || oracle.Contains("fetch from your deck")))
            return true;

        return false;
    }

    private static bool HasPersistentTokenEffect(Card card)
    {
        if (card == null || string.IsNullOrWhiteSpace(card.OracleText))
            return false;

        string oracle = card.OracleText.ToLowerInvariant();
        
        // Tokens that get created per turn
        if ((oracle.Contains("at the beginning of your upkeep") || oracle.Contains("at the beginning of combat")) 
            && oracle.Contains("create") && oracle.Contains("token"))
            return true;

        return false;
    }

    private void LearnFromSimulation(SimulationResult result, IReadOnlyCollection<Card> cardsCastThisGame)
    {
        _learningProfile.GamesPlayed++;

        double reward = 0;
        reward += result.ManaProduced > 0 ? result.ManaSpent * 100.0 / result.ManaProduced : 0;
        reward -= result.IdleTurns * 8;
        reward -= result.MissedLands * 10;
        reward -= result.StrandedHighCostCards * 6;
        reward += result.EarlyTurnActions * 6;

        if (result.CommanderCast)
        {
            reward += 18;
            reward += Math.Max(0, 8 - result.CommanderCastTurn) * 2;
            _learningProfile.CommanderBias = ClampBias(_learningProfile.CommanderBias + 0.18);
        }
        else if (_deck.Commander != null)
        {
            _learningProfile.CommanderBias = ClampBias(_learningProfile.CommanderBias - 0.08);
        }

        double normalizedReward = Math.Clamp((reward - 45) / 60.0, -1.0, 1.0);
        foreach (var tag in cardsCastThisGame
            .Where(card => !card.IsLand)
            .SelectMany(card => DeckAnalysis.GetCategoryTags(card))
            .GroupBy(tag => tag, StringComparer.OrdinalIgnoreCase))
        {
            double currentBias = _learningProfile.TagBiases.TryGetValue(tag.Key, out double existingBias) ? existingBias : 0;
            double updatedBias = ClampBias(currentBias + normalizedReward * Math.Min(0.35, 0.1 + tag.Count() * 0.03));
            _learningProfile.TagBiases[tag.Key] = updatedBias;
        }
    }

    private static double ClampBias(double value)
    {
        return Math.Max(-20, Math.Min(20, value));
    }

    private void ApplyCastEffects(Card card, List<Card> library, List<Card> hand, List<Card> battlefield, int turn, ref int permanentRamp, ref int manaAvailable)
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

    private void ResolveActivatedAbility(GoldfishGameState state, Card card)
    {
        string oracle = (card.OracleText ?? string.Empty).ToLowerInvariant();
        if (oracle.Contains("draw"))
        {
            int handCountBefore = state.Hand.Count;
            DrawCards(state.Library, state.Hand, 1);
            state.CardsDrawn += Math.Max(0, state.Hand.Count - handCountBefore);
        }

        if (oracle.Contains("add {"))
            state.ManaAvailable += 1;

        if (oracle.Contains("create") && oracle.Contains("token"))
            state.TriggeredAbilitiesResolved++;

        if (oracle.Contains("proliferate"))
            state.TriggeredAbilitiesResolved++;
    }

    private void ResolveTriggeredAbilities(GoldfishGameState state, Card card)
    {
        string oracle = (card.OracleText ?? string.Empty).ToLowerInvariant();
        if (oracle.Contains("when ") || oracle.Contains("whenever") || oracle.Contains("at the beginning"))
        {
            state.TriggeredAbilitiesResolved++;
            if (oracle.Contains("draw"))
            {
                int handCountBefore = state.Hand.Count;
                DrawCards(state.Library, state.Hand, 1);
                state.CardsDrawn += Math.Max(0, state.Hand.Count - handCountBefore);
            }
            if (oracle.Contains("add {"))
                state.ManaAvailable += 1;
        }
    }

    private void DetectInfiniteCombo(GoldfishGameState state, Card? latestCard)
    {
        if (latestCard == null)
            return;

        var names = state.Battlefield
            .Where(card => !string.IsNullOrWhiteSpace(card.Name))
            .Select(card => card.Name!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        string joinedOracle = string.Join("\n", state.Battlefield.Select(card => card.OracleText ?? string.Empty)).ToLowerInvariant();

        bool hasUntapEngine = joinedOracle.Contains("untap all") || joinedOracle.Contains("untap target") || joinedOracle.Contains("untap another target permanent");
        bool hasManaEngine = joinedOracle.Contains("{t}: add {") || joinedOracle.Contains(": add {");
        bool hasDrawEngine = joinedOracle.Contains(": draw") || joinedOracle.Contains("whenever you draw");
        bool hasDamageOutlet = joinedOracle.Contains(": deal") || joinedOracle.Contains("each opponent loses");
        bool hasWinText = joinedOracle.Contains("you win the game") || joinedOracle.Contains("wins the game");

        if (hasWinText || (hasUntapEngine && hasManaEngine && (hasDrawEngine || hasDamageOutlet)))
        {
            state.InfiniteComboAchieved = true;
            state.InfiniteComboLine = hasWinText
                ? $"{latestCard.Name} enabled a direct win line."
                : $"Detected repeatable untap + mana engine with payoff from {latestCard.Name}.";
            state.ActionLog.Add($"T{state.Turn}: infinite combo detected - {state.InfiniteComboLine}");
        }
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

    private Card? ChooseTutorTarget(List<Card> library, IReadOnlyCollection<Card> hand, IReadOnlyCollection<Card> battlefield, int turn)
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
    private readonly DeckArchetype _selectedArchetype;
    private readonly bool _isCedh;
    private readonly GoldfishLearningProfile _learningProfile;

    private sealed class ArchetypeTargets
    {
        public int LandsMin { get; init; }
        public int LandsMax { get; init; }
        public int RampMin { get; init; }
        public int RampMax { get; init; }
        public int DrawMin { get; init; }
        public int DrawMax { get; init; }
        public int InteractionMin { get; init; }
        public int InteractionMax { get; init; }
        public int CreatureMin { get; init; }
        public int CreatureMax { get; init; }
        public int InstantSorceryMin { get; init; }
        public int InstantSorceryMax { get; init; }
        public int TutorMin { get; init; }
        public int TutorMax { get; init; }
        public string FocusSummary { get; init; } = string.Empty;
    }

    private sealed class PowerLevelAssessment
    {
        public double PowerLevel { get; init; }
        public string Bracket { get; init; } = string.Empty;
        public string Summary { get; init; } = string.Empty;
        public List<string> Signals { get; init; } = new List<string>();
    }

    public DeckEvaluator(Deck deck, DeckArchetype? archetypeOverride = null, bool isCedh = false)
    {
        _deck = deck;
        _isCedh = isCedh;
        _selectedArchetype = archetypeOverride ?? DeckAnalysis.DetectPrimaryArchetype(deck);
        _learningProfile = GoldfishLearningStore.LoadProfile(deck);
    }

    public EvaluationResults RunSimulations(int numSimulations, int maxTurns, bool onDraw = false, Action<int>? progressCallback = null, CancellationToken cancellationToken = default)
    {
        var bot = new GoldfishBot(_deck, _selectedArchetype);
        var results = new List<SimulationResult>(numSimulations);
        int lastReported = -1;

        for (int simulation = 0; simulation < numSimulations; simulation++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = bot.SimulateGame(maxTurns, onDraw, cancellationToken);
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
            SelectedArchetype = _selectedArchetype,
            DetectedArchetype = DeckAnalysis.DetectPrimaryArchetype(_deck),
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
            AverageStrandedHighCostCards = results.Average(result => result.StrandedHighCostCards),
            AverageCommanderCastTurn = results.Where(result => result.CommanderCast).Any() ? results.Where(result => result.CommanderCast).Average(result => result.CommanderCastTurn) : 0,
            CommanderCastRate = results.Count(result => result.CommanderCast) * 100.0 / results.Count,
            InfiniteComboWinRate = results.Count(result => result.WonViaInfiniteCombo) * 100.0 / results.Count,
            AverageActivatedAbilitiesUsed = results.Average(result => result.ActivatedAbilitiesUsed),
            AverageTriggeredAbilitiesResolved = results.Average(result => result.TriggeredAbilitiesResolved),
            ComboLines = results.Where(result => !string.IsNullOrWhiteSpace(result.ComboLine)).Select(result => result.ComboLine).Distinct(StringComparer.OrdinalIgnoreCase).Take(5).ToList(),
            SpellbookKnownCombos = _deck.SpellbookReport?.Included.Select(combo => combo.Summary).Take(8).ToList() ?? new List<string>(),
            SpellbookAlmostCombos = _deck.SpellbookReport?.AlmostIncluded.Select(combo => combo.Summary).Take(8).ToList() ?? new List<string>(),
            SpellbookComboAssemblies = results.SelectMany(result => result.SpellbookComboAssemblies).Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToList(),
            SpellbookComboAssemblyRate = results.Count(result => result.SpellbookComboAssemblies.Any()) * 100.0 / results.Count,
            SamplePlayPatterns = results.SelectMany(result => result.ActionLog).Where(log => !string.IsNullOrWhiteSpace(log)).Take(8).ToList(),
            LearningGamesSeen = bot.LearnedGames
        };

        evaluation.IsCedh = _isCedh;
        var powerAssessment = AssessPowerLevel(evaluation, _deck);
        evaluation.EstimatedPowerLevel = powerAssessment.PowerLevel;
        evaluation.EstimatedBracket = powerAssessment.Bracket;
        evaluation.PowerSummary = powerAssessment.Summary;
        evaluation.PowerSignals = powerAssessment.Signals;

        bot.SaveLearning();
        GenerateRecommendations(evaluation, _deck, maxTurns);
        return evaluation;
    }

    private PowerLevelAssessment AssessPowerLevel(EvaluationResults results, Deck deck)
    {
        if (_isCedh)
            return AssessCedhPowerLevel(results, deck);

        int rampCount = DeckAnalysis.CountRoleCards(deck, "Ramp");
        int drawCount = DeckAnalysis.CountRoleCards(deck, "Card Draw");
        int removalCount = DeckAnalysis.CountRoleCards(deck, "Removal");
        int counterCount = DeckAnalysis.CountRoleCards(deck, "Counterspell");
        int tutorCount = DeckAnalysis.CountRoleCards(deck, "Tutor");
        int comboCount = DeckAnalysis.DetectComboPieces(deck).Sum(group => group.Cards.Count);
        int instantSorceryCount = deck.Cards.Count(card => DeckAnalysis.IsNonLand(card) && (DeckAnalysis.HasType(card, "Instant") || DeckAnalysis.HasType(card, "Sorcery")));
        int fastManaCount = deck.Cards.Count(card => !card.IsLand
            && card.ManaCost <= 2
            && DeckAnalysis.GetCategoryTags(card).Contains("Ramp"));

        double score = 1.5;
        var positiveSignals = new List<string>();
        var limitingSignals = new List<string>();

        if (comboCount >= 6)
        {
            score += 1.5;
            positiveSignals.Add($"Dense combo package ({comboCount} combo-linked cards)");
        }
        else if (comboCount >= 3)
        {
            score += 0.8;
            positiveSignals.Add($"Noticeable combo angle ({comboCount} combo-linked cards)");
        }

        if (tutorCount >= 5)
        {
            score += 1.3;
            positiveSignals.Add($"High tutor density ({tutorCount}) increases consistency");
        }
        else if (tutorCount >= 2)
        {
            score += 0.6;
            positiveSignals.Add($"Some tutor support ({tutorCount}) improves access to engines");
        }

        if (fastManaCount >= 6)
        {
            score += 1.2;
            positiveSignals.Add($"Heavy cheap ramp / fast mana presence ({fastManaCount})");
        }
        else if (fastManaCount >= 3)
        {
            score += 0.6;
            positiveSignals.Add($"Good early acceleration package ({fastManaCount} cheap ramp pieces)");
        }

        if (results.AverageManaEfficiency >= 75)
        {
            score += 1.1;
            positiveSignals.Add($"High mana efficiency ({results.AverageManaEfficiency:F1}%)");
        }
        else if (results.AverageManaEfficiency >= 65)
        {
            score += 0.6;
            positiveSignals.Add($"Solid mana efficiency ({results.AverageManaEfficiency:F1}%)");
        }
        else if (results.AverageManaEfficiency < 55)
        {
            score -= 0.6;
            limitingSignals.Add($"Mana usage is inefficient ({results.AverageManaEfficiency:F1}%)");
        }

        if (results.AverageIdleTurns <= 0.8)
        {
            score += 0.8;
            positiveSignals.Add($"Very few idle turns ({results.AverageIdleTurns:F2})");
        }
        else if (results.AverageIdleTurns >= 2.2)
        {
            score -= 0.8;
            limitingSignals.Add($"Too many idle turns ({results.AverageIdleTurns:F2}) slow the deck down");
        }

        if (results.AverageEarlyTurnActions >= 2.5)
        {
            score += 0.8;
            positiveSignals.Add($"Strong early development by turn 3 ({results.AverageEarlyTurnActions:F2} actions)");
        }
        else if (results.AverageEarlyTurnActions < 1.5)
        {
            score -= 0.6;
            limitingSignals.Add($"Slow early setup ({results.AverageEarlyTurnActions:F2} actions by turn 3)");
        }

        if (drawCount >= 10)
        {
            score += 0.4;
            positiveSignals.Add($"Reliable card flow ({drawCount} draw pieces)");
        }
        else if (drawCount < 7)
        {
            score -= 0.3;
            limitingSignals.Add($"Card flow is light ({drawCount} draw pieces)");
        }

        if (removalCount + counterCount >= 15)
        {
            score += 0.5;
            positiveSignals.Add($"High interaction density ({removalCount + counterCount})");
        }
        else if (removalCount + counterCount < 9)
        {
            score -= 0.3;
            limitingSignals.Add($"Interaction is thin ({removalCount + counterCount})");
        }

        if (deck.Commander != null && results.CommanderCastRate >= 80)
        {
            score += 0.4;
            positiveSignals.Add($"Commander shows up reliably ({results.CommanderCastRate:F1}% cast rate)");
        }
        else if (deck.Commander != null && results.CommanderCastRate < 50)
        {
            score -= 0.4;
            limitingSignals.Add($"Commander is not entering play consistently ({results.CommanderCastRate:F1}% cast rate)");
        }

        if (_selectedArchetype == DeckArchetype.Combo && tutorCount + comboCount >= 8)
            score += 0.5;
        if (_selectedArchetype == DeckArchetype.Spellslinger && instantSorceryCount >= 24)
            score += 0.4;
        if (_selectedArchetype == DeckArchetype.Ramp && rampCount >= 12 && results.AveragePeakMana >= 7)
            score += 0.5;

        score = Math.Max(1.0, Math.Min(10.0, score));

        string bracket;
        string summary;
        if (score >= 8.3)
        {
            bracket = "Bracket 4 - High Power";
            summary = "Fast, consistent, and likely pushing toward high-power tables.";
        }
        else if (score >= 6.4)
        {
            bracket = "Bracket 3 - Optimized";
            summary = "Well-tuned and efficient, with clear game plans and stronger consistency tools.";
        }
        else if (score >= 4.3)
        {
            bracket = "Bracket 2 - Focused";
            summary = "Coherent and capable, but not yet operating at a highly optimized table speed.";
        }
        else
        {
            bracket = "Bracket 1 - Casual";
            summary = "More relaxed and less explosive; likely best at lower-pressure tables.";
        }

        var signals = positiveSignals.Take(3)
            .Concat(limitingSignals.Take(2))
            .ToList();
        if (!signals.Any())
            signals.Add("No single signal dominated the review; this list reads as balanced but not extreme.");

        return new PowerLevelAssessment
        {
            PowerLevel = score,
            Bracket = bracket,
            Summary = summary,
            Signals = signals
        };
    }

    private PowerLevelAssessment AssessCedhPowerLevel(EvaluationResults results, Deck deck)
    {
        int fastManaCount = deck.Cards.Count(c => !c.IsLand && c.ManaCost <= 1 && DeckAnalysis.GetCategoryTags(c).Contains("Ramp"));
        int tutorCount = DeckAnalysis.CountRoleCards(deck, "Tutor");
        int counterCount = DeckAnalysis.CountRoleCards(deck, "Counterspell");
        int comboCount = DeckAnalysis.DetectComboPieces(deck).Sum(g => g.Cards.Count);
        double avgNonLandCmc = deck.Cards.Where(c => !c.IsLand && !c.IsCommander).Any()
            ? deck.Cards.Where(c => !c.IsLand && !c.IsCommander).Average(c => c.ManaCost)
            : 3.0;

        double score = 4.5;
        var positiveSignals = new List<string>();
        var limitingSignals = new List<string>();

        if (fastManaCount >= 8) { score += 2.5; positiveSignals.Add($"Elite fast-mana package ({fastManaCount} 0-1 CMC ramp pieces)"); }
        else if (fastManaCount >= 5) { score += 1.5; positiveSignals.Add($"Strong fast-mana density ({fastManaCount} pieces)"); }
        else if (fastManaCount >= 3) { score += 0.6; }
        else { score -= 1.5; limitingSignals.Add($"Severely lacking fast mana ({fastManaCount} pieces) — not viable at cEDH tables"); }

        if (tutorCount >= 10) { score += 2.0; positiveSignals.Add($"Elite tutor density ({tutorCount}) — win conditions are consistently accessible"); }
        else if (tutorCount >= 7) { score += 1.2; positiveSignals.Add($"Strong tutor suite ({tutorCount}) for consistent game plans"); }
        else if (tutorCount >= 4) { score += 0.4; }
        else { score -= 1.5; limitingSignals.Add($"Tutor count is too low for cEDH ({tutorCount}) — needs 7+ to assemble win conditions"); }

        if (comboCount >= 8) { score += 1.5; positiveSignals.Add($"Dense combo architecture ({comboCount} combo-linked pieces)"); }
        else if (comboCount >= 4) { score += 0.7; positiveSignals.Add($"Defined combo win condition ({comboCount} combo pieces)"); }
        else { score -= 0.8; limitingSignals.Add($"Low combo density ({comboCount}) — cEDH requires a compact, tutorable two-card win condition"); }

        if (avgNonLandCmc < 2.0) { score += 1.5; positiveSignals.Add($"Elite avg non-land CMC ({avgNonLandCmc:F2}) — hallmark of optimized cEDH pace"); }
        else if (avgNonLandCmc < 2.5) { score += 0.7; positiveSignals.Add($"Low CMC profile ({avgNonLandCmc:F2}) supports fast deployment"); }
        else if (avgNonLandCmc > 3.0) { score -= 1.2; limitingSignals.Add($"High avg CMC ({avgNonLandCmc:F2}) — trim 4+ CMC spells not part of win conditions"); }
        else if (avgNonLandCmc > 2.5) { score -= 0.5; limitingSignals.Add($"CMC ({avgNonLandCmc:F2}) is marginally high for cEDH"); }

        if (counterCount >= 8) { score += 1.0; positiveSignals.Add($"Heavy counterspell suite ({counterCount}) provides strong reactive game"); }
        else if (counterCount >= 5) { score += 0.5; }
        else if (counterCount < 3) { score -= 0.7; limitingSignals.Add($"Counter suite is thin ({counterCount}) — add Force of Will, Fierce Guardianship, Pact of Negation"); }

        if (results.AverageManaEfficiency >= 80) { score += 0.8; positiveSignals.Add($"Exceptional mana efficiency ({results.AverageManaEfficiency:F1}%)"); }
        else if (results.AverageManaEfficiency < 60) { score -= 0.5; limitingSignals.Add($"Mana efficiency gap ({results.AverageManaEfficiency:F1}%) — trim dead cards and add low-CMC value"); }

        if (results.AverageEarlyTurnActions >= 3.0) { score += 0.8; positiveSignals.Add($"Excellent T1-T3 development ({results.AverageEarlyTurnActions:F2} avg actions)"); }
        else if (results.AverageEarlyTurnActions < 2.0) { score -= 0.8; limitingSignals.Add($"Slow early setup ({results.AverageEarlyTurnActions:F2} T1-T3 actions) — cEDH requires proactive early plays"); }

        score = Math.Max(1.0, Math.Min(10.0, score));

        string bracket, summary;
        if (score >= 8.5) { bracket = "cEDH Competitive"; summary = "Optimized for cEDH tables. Fast mana, high tutor density, and compact win conditions are all present."; }
        else if (score >= 7.0) { bracket = "High Power / Near-cEDH"; summary = "Close to cEDH but missing a few key optimizations."; }
        else if (score >= 5.5) { bracket = "Bracket 4 \u2014 Not cEDH-Ready"; summary = "Strong deck but missing cEDH fundamentals. Focus on fast mana, tutors, and lowering average CMC."; }
        else { bracket = "Below cEDH Threshold"; summary = "Not currently cEDH-viable. Needs major upgrades in fast mana, tutors, and a compact win condition."; }

        var signals = positiveSignals.Take(3).Concat(limitingSignals.Take(3)).ToList();
        if (!signals.Any()) signals.Add("Deck metrics are ambiguous for cEDH evaluation.");
        return new PowerLevelAssessment { PowerLevel = score, Bracket = bracket, Summary = summary, Signals = signals };
    }

    private static ArchetypeTargets GetCedhTargets(DeckArchetype archetype)
    {
        int landsMin = 27, landsMax = 32;
        int rampMin = 12, rampMax = 22;
        int drawMin = 12, drawMax = 20;
        int interactionMin = 15, interactionMax = 28;
        int tutorMin = 7, tutorMax = 16;
        int creatureMin, creatureMax, isMin, isMax;
        string focus;

        switch (archetype)
        {
            case DeckArchetype.Combo:
                creatureMin = 4; creatureMax = 16;
                isMin = 22; isMax = 40;
                tutorMin = 10; tutorMax = 18;
                focus = "cEDH Combo: assemble a two-card win by T2-T4 using fast mana and tutors; protect the setup with hard counters; trim anything not finding or protecting the combo.";
                break;
            case DeckArchetype.Spellslinger:
                creatureMin = 4; creatureMax = 14;
                isMin = 26; isMax = 42;
                focus = "cEDH Spellslinger: cantrip aggressively, chain cheap spells, and close games before T5 with a storm or chain-of-spells finish.";
                break;
            case DeckArchetype.Tribal:
                creatureMin = 20; creatureMax = 32;
                isMin = 14; isMax = 26;
                landsMin = 28; landsMax = 33;
                focus = "cEDH Tribal: maximize tribal synergy and deploy threats ahead of curve using fast mana; ensure the disruption suite can answer win conditions at speed.";
                break;
            case DeckArchetype.Ramp:
                creatureMin = 8; creatureMax = 20;
                isMin = 16; isMax = 28;
                rampMin = 16; rampMax = 26;
                focus = "cEDH Ramp: use rituals, mana rocks, and fast mana to generate explosive leads; convert to infinite mana or Eldrazi-type closers.";
                break;
            case DeckArchetype.Tokens:
                creatureMin = 12; creatureMax = 22;
                isMin = 16; isMax = 28;
                focus = "cEDH Tokens: deploy token engines quickly via fast mana and generate a board-wide win before T4; include protection from board wipes.";
                break;
            default:
                creatureMin = 6; creatureMax = 18;
                isMin = 22; isMax = 38;
                focus = "cEDH: prioritize fast mana, 8+ tutors, hard interaction (Force of Will, Fierce Guardianship), and a reliable T3-T4 win condition.";
                break;
        }

        return new ArchetypeTargets
        {
            LandsMin = landsMin, LandsMax = landsMax,
            RampMin = rampMin, RampMax = rampMax,
            DrawMin = drawMin, DrawMax = drawMax,
            InteractionMin = interactionMin, InteractionMax = interactionMax,
            CreatureMin = creatureMin, CreatureMax = creatureMax,
            InstantSorceryMin = isMin, InstantSorceryMax = isMax,
            TutorMin = tutorMin, TutorMax = tutorMax,
            FocusSummary = focus
        };
    }

    private static ArchetypeTargets GetArchetypeTargets(DeckArchetype archetype)
    {
        return archetype switch
        {
            DeckArchetype.Ramp => new ArchetypeTargets
            {
                LandsMin = 36,
                LandsMax = 39,
                RampMin = 11,
                RampMax = 16,
                DrawMin = 8,
                DrawMax = 13,
                InteractionMin = 10,
                InteractionMax = 18,
                CreatureMin = 16,
                CreatureMax = 28,
                InstantSorceryMin = 6,
                InstantSorceryMax = 16,
                TutorMin = 1,
                TutorMax = 5,
                FocusSummary = "Hit land drops, accelerate mana, and convert that mana into impactful top-end threats."
            },
            DeckArchetype.Spellslinger => new ArchetypeTargets
            {
                LandsMin = 34,
                LandsMax = 37,
                RampMin = 8,
                RampMax = 13,
                DrawMin = 10,
                DrawMax = 16,
                InteractionMin = 14,
                InteractionMax = 24,
                CreatureMin = 6,
                CreatureMax = 18,
                InstantSorceryMin = 20,
                InstantSorceryMax = 34,
                TutorMin = 1,
                TutorMax = 6,
                FocusSummary = "Chain cheap spells, keep cards flowing, and maintain enough interaction to reach payoff turns."
            },
            DeckArchetype.Tribal => new ArchetypeTargets
            {
                LandsMin = 35,
                LandsMax = 38,
                RampMin = 8,
                RampMax = 13,
                DrawMin = 8,
                DrawMax = 13,
                InteractionMin = 10,
                InteractionMax = 18,
                CreatureMin = 24,
                CreatureMax = 36,
                InstantSorceryMin = 6,
                InstantSorceryMax = 16,
                TutorMin = 0,
                TutorMax = 4,
                FocusSummary = "Maximize tribe density while preserving enough ramp, draw, and interaction to actually deploy the board."
            },
            DeckArchetype.Tokens => new ArchetypeTargets
            {
                LandsMin = 35,
                LandsMax = 38,
                RampMin = 9,
                RampMax = 14,
                DrawMin = 8,
                DrawMax = 14,
                InteractionMin = 10,
                InteractionMax = 18,
                CreatureMin = 16,
                CreatureMax = 28,
                InstantSorceryMin = 8,
                InstantSorceryMax = 18,
                TutorMin = 0,
                TutorMax = 4,
                FocusSummary = "Produce bodies early, scale them with anthem or payoff effects, and avoid overloading on non-synergy cards."
            },
            DeckArchetype.Combo => new ArchetypeTargets
            {
                LandsMin = 33,
                LandsMax = 37,
                RampMin = 8,
                RampMax = 13,
                DrawMin = 10,
                DrawMax = 16,
                InteractionMin = 10,
                InteractionMax = 18,
                CreatureMin = 8,
                CreatureMax = 22,
                InstantSorceryMin = 12,
                InstantSorceryMax = 24,
                TutorMin = 3,
                TutorMax = 8,
                FocusSummary = "Find combo pieces consistently, protect the setup turn, and trim cards that do not advance or defend the combo."
            },
            _ => new ArchetypeTargets
            {
                LandsMin = 35,
                LandsMax = 38,
                RampMin = 8,
                RampMax = 14,
                DrawMin = 8,
                DrawMax = 14,
                InteractionMin = 12,
                InteractionMax = 20,
                CreatureMin = 16,
                CreatureMax = 28,
                InstantSorceryMin = 8,
                InstantSorceryMax = 18,
                TutorMin = 0,
                TutorMax = 5,
                FocusSummary = "Maintain a balanced curve with enough mana, interaction, and card flow to play strong midgame Magic."
            }
        };
    }

    private void GenerateRecommendations(EvaluationResults results, Deck deck, int maxTurns)
    {
        var archetype = results.SelectedArchetype;
        var targets = _isCedh ? GetCedhTargets(archetype) : GetArchetypeTargets(archetype);
        if (_isCedh)
            GenerateCedhRecommendations(results, deck);

        void AddSuggestion(DeckSuggestionKind kind, string title, string details, double confidence, string roleTag = "", string source = "Simulator", IEnumerable<string>? adds = null, IEnumerable<string>? cuts = null, Dictionary<string, string>? addReasons = null, Dictionary<string, string>? cutReasons = null)
        {
            results.Suggestions.Add(new DeckSuggestion
            {
                Kind = kind,
                Title = title,
                Details = details,
                RoleTag = roleTag,
                Source = source,
                Confidence = Math.Max(0, Math.Min(1, confidence)),
                SuggestedAdds = adds?.Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).Take(5).ToList() ?? new List<string>(),
                SuggestedCuts = cuts?.Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).Take(5).ToList() ?? new List<string>(),
                AddReasons = addReasons ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                CutReasons = cutReasons ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            });
        }

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

        static string DescribeTag(string roleTag)
        {
            return roleTag.Equals("Card Draw", StringComparison.OrdinalIgnoreCase)
                ? "card draw"
                : roleTag.Equals("Board Wipe", StringComparison.OrdinalIgnoreCase)
                    ? "board wipes"
                    : roleTag.Equals("Token Generation", StringComparison.OrdinalIgnoreCase)
                        ? "token makers"
                        : roleTag.ToLowerInvariant();
        }

        int GetTargetMinForRole(string roleTag)
        {
            return roleTag switch
            {
                "Ramp" => targets.RampMin,
                "Card Draw" => targets.DrawMin,
                "Removal" => Math.Max(4, targets.InteractionMin - 4),
                "Counterspell" => Math.Max(2, targets.InteractionMin / 3),
                "Tutor" => targets.TutorMin,
                "Board Wipe" => Math.Max(2, targets.InteractionMin / 4),
                "Token Generation" => archetype == DeckArchetype.Tokens ? 7 : 3,
                _ => 0
            };
        }

        int GetCurrentRoleCount(string roleTag)
        {
            return DeckAnalysis.CountRoleCards(deck, roleTag);
        }

        int GetRoleNeedScore(string roleTag)
        {
            int score = Math.Max(0, GetTargetMinForRole(roleTag) - GetCurrentRoleCount(roleTag)) * 10;
            if (roleTag.Equals("Ramp", StringComparison.OrdinalIgnoreCase) && results.CommanderCastRate < 65)
                score += 4;
            if (roleTag.Equals("Card Draw", StringComparison.OrdinalIgnoreCase) && results.AverageManaEfficiency < 55)
                score += 3;
            if (roleTag.Equals("Removal", StringComparison.OrdinalIgnoreCase) && _isCedh)
                score += 2;
            if (roleTag.Equals("Counterspell", StringComparison.OrdinalIgnoreCase) && archetype == DeckArchetype.Spellslinger)
                score += 2;
            return score;
        }

        List<string> GetRolePriorityOrder()
        {
            return archetype switch
            {
                DeckArchetype.Spellslinger => new List<string> { "Card Draw", "Counterspell", "Removal", "Ramp", "Tutor", "Board Wipe", "Token Generation" },
                DeckArchetype.Combo => new List<string> { "Tutor", "Card Draw", "Counterspell", "Ramp", "Removal", "Board Wipe", "Token Generation" },
                DeckArchetype.Tokens => new List<string> { "Token Generation", "Card Draw", "Ramp", "Removal", "Board Wipe", "Protection" },
                DeckArchetype.Tribal => new List<string> { "Card Draw", "Ramp", "Removal", "Protection", "Board Wipe", "Tutor" },
                DeckArchetype.Ramp => new List<string> { "Card Draw", "Removal", "Ramp", "Tutor", "Board Wipe", "Counterspell" },
                _ => new List<string> { "Card Draw", "Removal", "Ramp", "Board Wipe", "Counterspell", "Tutor", "Token Generation" }
            };
        }

        string ChooseBestAddRole(params string[] excludedRoles)
        {
            var excluded = excludedRoles.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var priority = GetRolePriorityOrder();
            var supportedRoles = new[] { "Ramp", "Card Draw", "Removal", "Counterspell", "Board Wipe", "Tutor", "Token Generation" };

            return supportedRoles
                .Where(role => !excluded.Contains(role))
                .OrderByDescending(GetRoleNeedScore)
                .ThenBy(role =>
                {
                    int index = priority.FindIndex(item => item.Equals(role, StringComparison.OrdinalIgnoreCase));
                    return index < 0 ? int.MaxValue : index;
                })
                .FirstOrDefault() ?? "Card Draw";
        }

        (List<string> Names, Dictionary<string, string> Reasons) RankCutCandidates(string incomingRole, int maxResults = 5, int minManaCost = 0, string? preferTrimTag = null)
        {
            var ranked = deck.Cards
                .Where(card => !card.IsLand && !card.IsCommander && !string.IsNullOrWhiteSpace(card.Name))
                .GroupBy(card => card.Name!.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Select(card =>
                {
                    var tags = DeckAnalysis.GetCategoryTags(card);
                    double score = card.ManaCost;
                    var reasons = new List<string>();

                    if (card.ManaCost >= 5)
                    {
                        score += 3;
                        reasons.Add("high mana value");
                    }
                    else if (card.ManaCost <= 2)
                    {
                        score -= 2;
                    }

                    if (minManaCost > 0 && card.ManaCost >= minManaCost)
                    {
                        score += 2;
                    }

                    if (!string.IsNullOrWhiteSpace(preferTrimTag) && tags.Contains(preferTrimTag))
                    {
                        score += 5;
                        reasons.Add($"surplus {DescribeTag(preferTrimTag)} slot");
                    }

                    if (tags.Count == 0)
                    {
                        score += 3;
                        reasons.Add("few recognized functional tags");
                    }

                    if (!string.IsNullOrWhiteSpace(incomingRole) && tags.Contains(incomingRole))
                    {
                        score -= 6;
                    }

                    if (tags.Contains("Tutor"))
                        score -= 3;
                    if (tags.Contains("Card Draw"))
                        score -= 1.5;
                    if (tags.Contains("Ramp"))
                        score -= 1.5;
                    if (tags.Contains("Removal"))
                        score -= 1.25;
                    if (tags.Contains("Counterspell"))
                        score -= 1.25;

                    switch (archetype)
                    {
                        case DeckArchetype.Spellslinger:
                            if (DeckAnalysis.HasType(card, "Creature") && !tags.Contains("Token Generation"))
                            {
                                score += 2.5;
                                reasons.Add("less aligned with spellslinger plan");
                            }
                            if (DeckAnalysis.HasType(card, "Instant") || DeckAnalysis.HasType(card, "Sorcery"))
                                score -= 2;
                            break;
                        case DeckArchetype.Combo:
                            if (!tags.Contains("Tutor") && !tags.Contains("Card Draw") && !tags.Contains("Ramp") && !tags.Contains("Counterspell"))
                            {
                                score += 2;
                                reasons.Add("does not strongly advance combo assembly");
                            }
                            break;
                        case DeckArchetype.Tribal:
                            if (!DeckAnalysis.HasType(card, "Creature"))
                                score += 1.5;
                            break;
                        case DeckArchetype.Tokens:
                            if (!tags.Contains("Token Generation") && !tags.Contains("Protection"))
                                score += 1.5;
                            break;
                    }

                    string reason = reasons.Count > 0
                        ? string.Join(", ", reasons.Distinct(StringComparer.OrdinalIgnoreCase).Take(2))
                        : "lower-priority slot relative to the current plan";

                    return new { Name = card.Name!.Trim(), Score = score, Reason = reason };
                })
                .OrderByDescending(entry => entry.Score)
                .ThenBy(entry => entry.Name)
                .Take(maxResults)
                .ToList();

            return (
                ranked.Select(entry => entry.Name).ToList(),
                ranked.ToDictionary(entry => entry.Name, entry => entry.Reason, StringComparer.OrdinalIgnoreCase));
        }

        (List<string> Names, Dictionary<string, string> Reasons) GetRecommendedLands(int count)
        {
            var lands = LandRecommendationEngine.RecommendAdds(deck, Math.Max(1, count));
            return (lands.Select(land => land.Name).ToList(), LandRecommendationEngine.BuildAddReasons(lands));
        }

        results.Recommendations.Add("\n=== MANA & LAND ANALYSIS ===");
        results.Recommendations.Add($"Deck archetype model: {archetype}");
        if (results.DetectedArchetype != results.SelectedArchetype)
            results.Recommendations.Add($"Auto-detected archetype: {results.DetectedArchetype} (recommendations are using the selected model instead).");
        results.Recommendations.Add($"Plan focus: {targets.FocusSummary}");
        int landCount = deck.LandCount;
        int nonLandCount = deck.Cards.Count(card => !card.IsLand && !card.IsCommander);
        double landPercentage = landCount * 100.0 / deck.Cards.Count;

        if (results.AverageMissedLands > 2)
        {
            var landAdds = GetRecommendedLands((int)Math.Max(2, Math.Ceiling(results.AverageMissedLands)));
            var cuts = RankCutCandidates("Land", 5, minManaCost: 5);
            results.Recommendations.Add($"🚨 HIGH PRIORITY: Avg {results.AverageMissedLands:F2} missed land drops. Add {Math.Ceiling(results.AverageMissedLands)} more lands (currently {landCount} / {landPercentage:F1}%)");
            AddSuggestion(
                DeckSuggestionKind.Add,
                "Raise land count",
                $"The deck is missing too many land drops on average. Increase the mana base before tuning anything else with lands that actually match your commander's color identity.",
                0.95,
                roleTag: "Land",
                adds: landAdds.Names,
                cuts: cuts.Names,
                addReasons: landAdds.Reasons,
                cutReasons: cuts.Reasons);
        }
        else if (landCount < targets.LandsMin)
        {
            var landAdds = GetRecommendedLands(Math.Max(2, targets.LandsMin - landCount));
            var cuts = RankCutCandidates("Land", 5, minManaCost: 4);
            results.Recommendations.Add($"⚠️  Land count is low for a {archetype} shell: {landCount} lands vs target {targets.LandsMin}-{targets.LandsMax}.");
            AddSuggestion(
                DeckSuggestionKind.Swap,
                "Convert clunky slots into lands",
                $"Your land count is below the normal {archetype} range, so smoother opening hands should help more than extra top end. These adds stay inside your commander's colors.",
                0.86,
                roleTag: "Land",
                adds: landAdds.Names,
                cuts: cuts.Names,
                addReasons: landAdds.Reasons,
                cutReasons: cuts.Reasons);
        }
        else if (landCount > targets.LandsMax && results.AverageMissedLands < 0.5)
        {
            string addRole = ChooseBestAddRole();
            results.Recommendations.Add($"💡 Land count is high for a {archetype} shell: {landCount} lands vs target {targets.LandsMin}-{targets.LandsMax}. You can likely convert a few lands into action spells.");
            AddSuggestion(
                DeckSuggestionKind.Swap,
                "Cash in excess lands for action",
                "The mana base looks comfortable already, so some extra lands can become cards that advance your core plan.",
                0.74,
                roleTag: addRole,
                cuts: new[] { "1-2 lands" },
                cutReasons: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["1-2 lands"] = "Land count is above the target range and goldfishing is not missing land drops."
                });
        }
        else if (results.AverageMissedLands < 0.2)
            results.Recommendations.Add($"💡 Excess lands: Avg {results.AverageMissedLands:F2} missed drops suggests {landCount - 2}-{landCount - 4} lands might work better");
        else
            results.Recommendations.Add($"✓ Lands optimal: {landCount} lands ({landPercentage:F1}%) - {results.AverageMissedLands:F2} avg missed drops");

        if (results.AverageMulligans > 0.75)
            results.Recommendations.Add($"⚠️ Opening hands are shaky: average mulligans {results.AverageMulligans:F2}. Add early plays, more lands, or smoother ramp.");
        else
            results.Recommendations.Add($"✓ Opening hand stability looks solid: average mulligans {results.AverageMulligans:F2}.");

        if (results.SpellbookKnownCombos.Any())
            results.Recommendations.Add($"Spellbook combos found in list: {string.Join(", ", results.SpellbookKnownCombos.Take(3))}");
        if (results.SpellbookComboAssemblyRate > 0)
            results.Recommendations.Add($"Spellbook combo assembly rate: {results.SpellbookComboAssemblyRate:F1}% of goldfish games assembled at least one listed combo.");

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
        {
            string addRole = ChooseBestAddRole("Land");
            var cuts = RankCutCandidates(addRole, 5, minManaCost: 5);
            results.Recommendations.Add($"📊 Top-heavy curve detected. Add {Math.Ceiling((highCost - lowCost) / 2.0)} low-cost spells.");
            AddSuggestion(
                DeckSuggestionKind.Swap,
                "Lower the curve",
                "The deck is carrying more expensive cards than its early-game infrastructure supports. Replace some of the top end with cheap setup pieces.",
                0.89,
                roleTag: addRole,
                cuts: cuts.Names,
                cutReasons: cuts.Reasons);
        }
        else if (lowCost > highCost * 2)
            results.Recommendations.Add($"📊 Consider adding {Math.Ceiling((lowCost - highCost) / 3.0)} higher-cost finishers.");
        else
            results.Recommendations.Add("✓ Mana curve is well-balanced");

        results.Recommendations.Add("\n=== CREATURE & SPELL BREAKDOWN ===");
        int creatureCount = deck.Cards.Count(card => !card.IsLand && DeckAnalysis.HasType(card, "Creature"));
        int instantCount = deck.Cards.Count(card => !card.IsLand && DeckAnalysis.HasType(card, "Instant"));
        int sorceryCount = deck.Cards.Count(card => !card.IsLand && DeckAnalysis.HasType(card, "Sorcery"));
        int instantSorceryCount = instantCount + sorceryCount;
        int artifactCount = deck.Cards.Count(card => !card.IsLand && DeckAnalysis.HasType(card, "Artifact"));
        int enchantmentCount = deck.Cards.Count(card => !card.IsLand && DeckAnalysis.HasType(card, "Enchantment"));
        int planeswalkerCount = deck.Cards.Count(card => !card.IsLand && DeckAnalysis.HasType(card, "Planeswalker"));
        int tokenCount = deck.Cards.Count(card => !card.IsLand && DeckAnalysis.GetCategoryTags(card).Contains("Token Generation"));
        int tutorCount = deck.Cards.Count(card => !card.IsLand && DeckAnalysis.GetCategoryTags(card).Contains("Tutor"));
        bool hasPrimaryTribe = DeckAnalysis.TryGetPrimaryTribe(deck, out string primaryTribe, out int primaryTribeCount, out _);

        if (creatureCount < targets.CreatureMin)
            results.Recommendations.Add($"⚠️  Creatures: {creatureCount} - below the {archetype} target of {targets.CreatureMin}-{targets.CreatureMax}.");
        else if (creatureCount <= targets.CreatureMax)
            results.Recommendations.Add($"✓ Creatures: {creatureCount} - aligned with the {archetype} target band of {targets.CreatureMin}-{targets.CreatureMax}.");
        else
            results.Recommendations.Add($"⚠️  Creatures: {creatureCount} - above the {archetype} target of {targets.CreatureMin}-{targets.CreatureMax}; some slots may be better used on support pieces.");

        if (instantSorceryCount < targets.InstantSorceryMin)
            results.Recommendations.Add($"⚠️  Instants/Sorceries: {instantSorceryCount} - low for a {archetype} plan (target {targets.InstantSorceryMin}-{targets.InstantSorceryMax}). Current examples: {FormatNameList(GetTopNamesByType("Instant").Concat(GetTopNamesByType("Sorcery")))}");
        else if (instantSorceryCount <= targets.InstantSorceryMax)
            results.Recommendations.Add($"✓ Instants/Sorceries: {instantSorceryCount} - fits the {archetype} target band (examples: {FormatNameList(GetTopNamesByType("Instant").Concat(GetTopNamesByType("Sorcery")))})");
        else
            results.Recommendations.Add($"💡 Instants/Sorceries: {instantSorceryCount} - above the {archetype} target of {targets.InstantSorceryMin}-{targets.InstantSorceryMax}. Make sure those spells still advance your main plan.");

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

        int drawMin = targets.DrawMin;
        int drawMax = targets.DrawMax;
        int interactionMin = targets.InteractionMin;
        int interactionMax = targets.InteractionMax;
        int removalMin = Math.Max(4, interactionMin - 4);
        int removalMax = Math.Max(removalMin, interactionMax - 4);

        results.Recommendations.Add($"Removal: {removalCount} (target {removalMin}-{removalMax})");
        if (removalCount < removalMin)
        {
            var cuts = RankCutCandidates("Removal", 5, minManaCost: 4);
            results.Recommendations.Add($"⚠️  Run more removal: add ~{removalMin - removalCount} to {removalMin - removalCount + 2} pieces of interaction. Current removal cards: {FormatNameList(GetTopNamesByTag("Removal"))}");
            AddSuggestion(
                DeckSuggestionKind.Add,
                "Increase removal density",
                "You are below the interaction floor, so the deck will struggle to answer opposing threats consistently.",
                0.84,
                roleTag: "Removal",
                cuts: cuts.Names,
                cutReasons: cuts.Reasons);
        }
        else if (removalCount > removalMax)
        {
            string addRole = ChooseBestAddRole("Removal");
            var cuts = RankCutCandidates(addRole, 5, preferTrimTag: "Removal");
            results.Recommendations.Add($"💡 Removal is high: you can trim ~{Math.Max(1, removalCount - removalMax)} for proactive threats/synergy. Trim candidates: {FormatNameList(GetTrimCandidatesByTag("Removal"))}");
            AddSuggestion(
                DeckSuggestionKind.Swap,
                "Trim excess removal",
                "Your removal package is heavier than the target band, so some of those slots can become synergy or pressure.",
                0.72,
                roleTag: addRole,
                cuts: cuts.Names,
                cutReasons: cuts.Reasons);
        }
        else
            results.Recommendations.Add($"✓ Removal count is in a strong range (cards: {FormatNameList(GetTopNamesByTag("Removal"))}).");

        results.Recommendations.Add($"Card Draw: {drawCount} (target {drawMin}-{drawMax})");
        if (drawCount < drawMin)
        {
            var cuts = RankCutCandidates("Card Draw", 5, minManaCost: 4);
            results.Recommendations.Add($"⚠️  Add card draw: include ~{drawMin - drawCount} to {drawMin - drawCount + 2} more repeatable draw effects. Current draw cards: {FormatNameList(GetTopNamesByTag("Card Draw"))}");
            AddSuggestion(
                DeckSuggestionKind.Add,
                "Add more card flow",
                "The list needs more sustained access to cards so it can keep converting mana into action.",
                0.82,
                roleTag: "Card Draw",
                cuts: cuts.Names,
                cutReasons: cuts.Reasons);
        }
        else if (drawCount > drawMax)
        {
            string addRole = ChooseBestAddRole("Card Draw");
            var cuts = RankCutCandidates(addRole, 5, preferTrimTag: "Card Draw");
            results.Recommendations.Add($"💡 Draw is high: trim ~{Math.Max(1, drawCount - drawMax)} draw spells if the deck feels low on board impact. Trim candidates: {FormatNameList(GetTrimCandidatesByTag("Card Draw"))}");
            AddSuggestion(
                DeckSuggestionKind.Swap,
                "Trade excess draw for impact",
                "You have enough draw already; some of those slots can become cards that pressure the board or reinforce the deck's plan.",
                0.67,
                roleTag: addRole,
                cuts: cuts.Names,
                cutReasons: cuts.Reasons);
        }
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
        int rampMin = targets.RampMin;
        int rampMax = targets.RampMax;
        
        // Analyze ramp by type
        var rampBreakdown = DeckAnalysis.GetRampTypeBreakdown(deck);
        var earlyRamp = DeckAnalysis.GetEarlyRampCards(deck);
        int earlyRampCount = earlyRamp.Count;
        int targetEarlyRamp = Math.Max(2, rampMin - 2);
        
        // Show ramp composition
        string rampComposition = $"Creatures: {rampBreakdown[DeckAnalysis.RampType.CreatureRamp]}, " +
                               $"Artifacts: {rampBreakdown[DeckAnalysis.RampType.ArtifactRamp]}, " +
                               $"Enchantments: {rampBreakdown[DeckAnalysis.RampType.EnchantmentRamp]}, " +
                               $"Spells: {rampBreakdown[DeckAnalysis.RampType.SpellRamp]}";
        results.Recommendations.Add($"Ramp composition: {rampComposition} | Early (0-2): {earlyRampCount}/{targetEarlyRamp}");
        
        if (rampCount < rampMin)
        {
            var cuts = RankCutCandidates("Ramp", 5, minManaCost: 4);
            results.Recommendations.Add($"⚠️  Ramp is low ({rampCount} / target {rampMin}-{rampMax}): add ~{rampMin - rampCount} mana accelerators. Current ramp: {FormatNameList(GetTopNamesByTag("Ramp"))}");
            
            // Smart recommendation based on what's missing
            string rampAdvice = "";
            if (earlyRampCount < targetEarlyRamp)
                rampAdvice = $"Prioritize early ramp (0-2 mana) like mana dorks and cheap rocks.";
            else if (rampBreakdown[DeckAnalysis.RampType.ArtifactRamp] < 2)
                rampAdvice = "Add mana rocks (artifacts) for consistent acceleration.";
            else if (rampBreakdown[DeckAnalysis.RampType.CreatureRamp] < 2)
                rampAdvice = "Add creature ramp (mana dorks) for early plays.";
            
            AddSuggestion(
                DeckSuggestionKind.Add,
                "Add early ramp",
                rampAdvice.Length > 0 ? rampAdvice : "The deck is short on acceleration, which is hurting both commander timing and overall mana efficiency.",
                0.88,
                roleTag: "Ramp",
                cuts: cuts.Names,
                cutReasons: cuts.Reasons);
        }
        else if (rampCount > rampMax)
        {
            string addRole = ChooseBestAddRole("Ramp");
            var cuts = RankCutCandidates(addRole, 5, preferTrimTag: "Ramp");
            results.Recommendations.Add($"💡 Ramp is high ({rampCount}): trim ~{Math.Max(1, rampCount - rampMax)} for more threats. Trim candidates: {FormatNameList(GetTrimCandidatesByTag("Ramp"))}");
            AddSuggestion(
                DeckSuggestionKind.Swap,
                "Trim extra ramp for payoffs",
                "The deck has enough acceleration already, so a few ramp slots can turn into stronger payoffs or interaction.",
                0.69,
                roleTag: addRole,
                cuts: cuts.Names,
                cutReasons: cuts.Reasons);
        }
        else
        {
            // Offer targeted improvements even when ramp count is on target
            string healthReport = $"Ramp is healthy ({rampCount} / target {rampMin}-{rampMax})";
            
            if (earlyRampCount < targetEarlyRamp)
            {
                healthReport += $", but early ramp is light ({earlyRampCount}/{targetEarlyRamp}). Consider swapping slower ramps for 0-2 CMC pieces.";
            }
            else if (rampBreakdown[DeckAnalysis.RampType.CreatureRamp] == 0 && rampBreakdown[DeckAnalysis.RampType.ArtifactRamp] > 3)
            {
                healthReport += ". Consider adding creature ramp for some redundancy.";
            }
            
            results.Recommendations.Add($"✓ {healthReport}");
        }

        results.Recommendations.Add("\n=== ARCHETYPE FIT ===");
        results.Recommendations.Add($"Primary plan check: {targets.FocusSummary}");
        if (tutorCount < targets.TutorMin)
            results.Recommendations.Add($"⚠️  Tutors: {tutorCount} - below the {archetype} target of {targets.TutorMin}-{targets.TutorMax}. Add more consistency if the deck relies on specific engines or finishers.");
        else if (tutorCount > targets.TutorMax)
            results.Recommendations.Add($"💡 Tutors: {tutorCount} - above the {archetype} target of {targets.TutorMin}-{targets.TutorMax}. Some tutor slots may be weaker than direct payoff cards.");
        else
            results.Recommendations.Add($"✓ Tutors: {tutorCount} - consistent with the {archetype} target band of {targets.TutorMin}-{targets.TutorMax}.");

        switch (archetype)
        {
            case DeckArchetype.Spellslinger when creatureCount > targets.CreatureMax:
                results.Recommendations.Add("⚠️  This spellslinger build is carrying too many creatures. Trim lower-impact bodies for cheap cantrips, interaction, or engine pieces.");
                break;
            case DeckArchetype.Ramp when highCost < 10:
                results.Recommendations.Add("💡 The ramp package is present, but the payoff band is light. Consider more 5+ mana cards that actually reward acceleration.");
                break;
            case DeckArchetype.Tribal when !hasPrimaryTribe:
                results.Recommendations.Add("⚠️  Tribal model selected, but the list does not show a strong tribe concentration yet. Increase creature-type overlap or switch the model.");
                break;
            case DeckArchetype.Tribal:
                results.Recommendations.Add($"✓ Tribal density check: {primaryTribeCount} members of the leading tribe ({primaryTribe}) found in the current list.");
                break;
            case DeckArchetype.Tokens when tokenCount < 7:
                results.Recommendations.Add("⚠️  Tokens model selected, but there are not many token generators. Add more repeatable token makers or pivot to a different model.");
                break;
            case DeckArchetype.Combo when tutorCount + drawCount < 14:
                results.Recommendations.Add("⚠️  Combo shells need more selection and assembly tools. Increase tutors or card draw so your engine shows up more often.");
                break;
        }

        results.Recommendations.Add("\n=== PLAYABILITY METRICS ===");
        double cardsPerTurn = maxTurns > 0 ? results.AverageCardsPlayable / maxTurns : 0;
        results.Recommendations.Add($"Average cards playable per turn: {cardsPerTurn:F2}");
        results.Recommendations.Add($"Average idle turns: {results.AverageIdleTurns:F2} out of {maxTurns}");
        results.Recommendations.Add($"Average mulligans: {results.AverageMulligans:F2}");
        results.Recommendations.Add($"Mana efficiency: {results.AverageManaEfficiency:F1}% of produced mana converted into casts");
        results.Recommendations.Add($"Early turn actions (T1-T3): {results.AverageEarlyTurnActions:F2}");
        results.Recommendations.Add($"Stranded 5+ mana cards in hand at game end: {results.AverageStrandedHighCostCards:F2}");
        if (deck.Commander != null)
            results.Recommendations.Add($"Commander cast rate: {results.CommanderCastRate:F1}% | Average cast turn: {(results.AverageCommanderCastTurn > 0 ? $"T{results.AverageCommanderCastTurn:F2}" : "not cast")}");

        if (results.AverageIdleTurns > 2)
        {
            string addRole = ChooseBestAddRole("Land");
            var cuts = RankCutCandidates(addRole, 5, minManaCost: 4);
            results.Recommendations.Add("⚠️  Too many idle turns. Consider adding more low-cost cards or mana acceleration.");
            AddSuggestion(
                DeckSuggestionKind.Swap,
                "Reduce idle turns",
                "Goldfish lines are stalling too often. Lower-cost ramp, draw, and removal should make more turns productive.",
                0.85,
                roleTag: addRole,
                cuts: cuts.Names,
                cutReasons: cuts.Reasons);
        }
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
        {
            string addRole = ChooseBestAddRole("Land");
            var cuts = RankCutCandidates(addRole, 5, minManaCost: 5);
            results.Recommendations.Add($"💡 Expensive cards are getting stranded in hand. Trim a few 5+ mana spells or add more ramp. Likely trim candidates: {FormatNameList(deck.Cards.Where(card => !card.IsLand && card.ManaCost >= 5).OrderByDescending(card => card.ManaCost).Select(card => card.Name ?? string.Empty))}");
            AddSuggestion(
                DeckSuggestionKind.Swap,
                "Swap stranded top-end cards",
                "Several expensive cards are staying stuck in hand, which usually means the curve and ramp package are out of sync.",
                0.9,
                roleTag: addRole,
                cuts: cuts.Names,
                cutReasons: cuts.Reasons);
        }

        if (deck.Commander != null && results.CommanderCastRate < 65)
            results.Recommendations.Add("⚠️  The commander is not coming down often enough in goldfish lines. Add more ramp, reduce clunky early plays, or lower the density of reactive spells that crowd out commander turns.");
    }

    private void GenerateCedhRecommendations(EvaluationResults results, Deck deck)
    {
        static string FormatList(IEnumerable<string> names)
        {
            var list = names.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).Take(6).ToList();
            return list.Any() ? string.Join(", ", list) : "none found";
        }

        results.Recommendations.Add("\n=== \u2694 cEDH COMPETITIVE EVALUATION ===");
        results.Recommendations.Add("Evaluating against cEDH standards: fast mana, tutors, low avg CMC, compact win conditions, hard interaction.");

        // Fast mana (0-1 CMC ramp)
        int fastManaCount = deck.Cards.Count(c => !c.IsLand && c.ManaCost <= 1 && DeckAnalysis.GetCategoryTags(c).Contains("Ramp"));
        string fastManaNames = FormatList(deck.Cards
            .Where(c => !c.IsLand && c.ManaCost <= 1 && DeckAnalysis.GetCategoryTags(c).Contains("Ramp"))
            .Select(c => c.Name ?? string.Empty));
        results.Recommendations.Add($"\n[cEDH] Fast Mana (0-1 CMC ramp): {fastManaCount} pieces \u2014 {fastManaNames}");
        if (fastManaCount < 5)
            results.Recommendations.Add("🚨 cEDH PRIORITY: Add more fast mana. Minimum package: Sol Ring, Mana Crypt, Jeweled Lotus, Chrome Mox or Mox Diamond, Dark Ritual or similar.");
        else if (fastManaCount < 8)
            results.Recommendations.Add("⚠️  Fast mana present but below cEDH elite tier. Consider Mana Vault, Lion's Eye Diamond, Mox Diamond, Jeweled Lotus.");
        else
            results.Recommendations.Add("✓ Fast mana package meets cEDH expectations.");

        // Tutors
        int tutorCount = DeckAnalysis.CountRoleCards(deck, "Tutor");
        string tutorNames = FormatList(deck.Cards
            .Where(c => !c.IsLand && DeckAnalysis.GetCategoryTags(c).Contains("Tutor"))
            .Select(c => c.Name ?? string.Empty));
        results.Recommendations.Add($"\n[cEDH] Tutors: {tutorCount} \u2014 {tutorNames}");
        if (tutorCount < 5)
            results.Recommendations.Add("🚨 cEDH PRIORITY: Tutor density is critical. Add Demonic Tutor, Vampiric Tutor, Imperial Seal, Mystical Tutor, Enlightened Tutor (by color).");
        else if (tutorCount < 8)
            results.Recommendations.Add("⚠️  Tutor suite is developing. cEDH typically wants 8-12+ tutors for consistent assembly.");
        else
            results.Recommendations.Add("✓ Tutor density is cEDH-caliber.");

        // Counterspell suite
        int counterCount = DeckAnalysis.CountRoleCards(deck, "Counterspell");
        string counterNames = FormatList(deck.Cards
            .Where(c => !c.IsLand && DeckAnalysis.GetCategoryTags(c).Contains("Counterspell"))
            .Select(c => c.Name ?? string.Empty));
        results.Recommendations.Add($"\n[cEDH] Counterspells: {counterCount} \u2014 {counterNames}");
        if (counterCount < 5)
            results.Recommendations.Add("⚠️  Counter suite is thin. Add: Force of Will, Force of Negation, Fierce Guardianship, Pact of Negation, Counterspell, Dispel.");
        else
            results.Recommendations.Add("✓ Counter suite is solid for cEDH.");

        // Average CMC
        var nonLandSpells = deck.Cards.Where(c => !c.IsLand && !c.IsCommander).ToList();
        double avgCmc = nonLandSpells.Any() ? nonLandSpells.Average(c => c.ManaCost) : 0;
        results.Recommendations.Add($"\n[cEDH] Avg non-land CMC: {avgCmc:F2}  (target: <2.5, elite: <2.0)");
        if (avgCmc > 3.0)
            results.Recommendations.Add("🚨 cEDH PRIORITY: Avg CMC is too high. Trim spells \u22655 CMC and replace with efficient 1-2 CMC equivalents — cEDH ends by T3-T4.");
        else if (avgCmc > 2.5)
            results.Recommendations.Add("⚠️  CMC is elevated. Each 5+ CMC card not in your win condition is likely dead at cEDH tables.");
        else if (avgCmc < 2.0)
            results.Recommendations.Add("✓ Low CMC profile — this list can develop at cEDH speed.");
        else
            results.Recommendations.Add("✓ CMC acceptable. Continue trimming cards above 3 that don't contribute to wins.");

        // Combo / win conditions
        var comboPieces = DeckAnalysis.DetectComboPieces(deck);
        int comboCount = comboPieces.Sum(g => g.Cards.Count);
        results.Recommendations.Add($"\n[cEDH] Combo density: {comboCount} combo-linked cards across {comboPieces.Count} cluster(s)");
        if (comboCount < 3)
            results.Recommendations.Add("🚨 cEDH PRIORITY: No functional combo detected. cEDH needs a compact tutored two-card win (e.g., Thassa's Oracle + Demonic Consultation, Isochron Scepter + Dramatic Reversal + mana).");
        else if (comboCount < 6)
            results.Recommendations.Add("⚠️  Combo exists but may not be streamlined. Ensure all pieces are tutorable and protected.");
        else
            results.Recommendations.Add("✓ Combo architecture is on track. Verify every piece has 2+ tutor lines.");

        // Spot removal
        int removalCount = DeckAnalysis.CountRoleCards(deck, "Removal");
        results.Recommendations.Add($"\n[cEDH] Targeted removal: {removalCount}");
        if (removalCount < 6)
            results.Recommendations.Add("⚠️  Low removal. cEDH staples: Swords to Plowshares, Path to Exile, Abrupt Decay, Cyclonic Rift, Nature's Claim, Assassin's Trophy.");
        else
            results.Recommendations.Add("✓ Removal count meets cEDH minimum. Prioritize instant-speed and low-CMC options.");

        // Early development
        results.Recommendations.Add($"\n[cEDH] T1-T3 avg actions: {results.AverageEarlyTurnActions:F2} (cEDH target: \u22652.5)");
        if (results.AverageEarlyTurnActions < 2.5)
            results.Recommendations.Add("⚠️  Early development is below cEDH pace. Add more 0-2 CMC plays so the deck impacts the game in the first three turns.");
        else
            results.Recommendations.Add("✓ Early game development is competitive-pace.");

        results.Recommendations.Add("\n[cEDH] Staple reference by role:");
        results.Recommendations.Add("  Fast Mana: Mana Crypt, Jeweled Lotus, Chrome Mox, Mox Diamond, Dark Ritual, Elvish Spirit Guide");
        results.Recommendations.Add("  Tutors: Demonic Tutor, Vampiric Tutor, Imperial Seal, Mystical Tutor, Ranger-Captain of Eos");
        results.Recommendations.Add("  Protection: Force of Will, Fierce Guardianship, Pact of Negation, Swan Song, Silence");
        results.Recommendations.Add("  Win Cons: Thassa's Oracle + Consultation, Isochron Scepter + Dramatic Reversal, Ad Nauseam, Food Chain");
    }
}
