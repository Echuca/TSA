using TSA.Core.Data;
using TSA.Core.Enums;
using TSA.Core.Events;
using TSA.Core.Models;
using TSA.Core.Rules;

namespace TSA.Core.Engine;

/// <summary>
/// Operations to be conducted, from a played card or an event ("as if a 4 Ops card").
/// </summary>
public sealed record OpsSource
{
    /// <summary>Card played for Ops; modifiers apply. Null for event-granted Ops.</summary>
    public int? CardId { get; init; }

    /// <summary>Ops value used when <see cref="CardId"/> is null.</summary>
    public int BaseOps { get; init; }

    public bool AllowInfluence { get; init; } = true;

    public bool AllowCoup { get; init; } = true;

    public bool AllowRealign { get; init; } = true;

    /// <summary>Event coups: no Military Ops, DEFCON region restrictions ignored.</summary>
    public bool Free { get; init; }

    /// <summary>Restricts targets (e.g. Junta: Central/South America only).</summary>
    public Func<Country, bool>? Filter { get; init; }

    public static OpsSource FromCard(int cardId) => new() { CardId = cardId };

    public static OpsSource Virtual(int ops) => new() { BaseOps = ops };
}

/// <summary>
/// Runs the game. The flow is a plain async method that pauses on each player decision;
/// it can be restarted from any checkpoint because all progress lives in <see cref="GameState"/>.
/// </summary>
public sealed class GameRunner
{
    private readonly Func<Task> _checkpoint;
    private Dictionary<Side, IReadOnlySet<string>>? _roundOrigins;

    public GameRunner(GameState state, IDecisionProvider provider, Func<Task>? checkpoint = null, CancellationToken cancellationToken = default)
    {
        State = state;
        Ask = new Decisions(provider, cancellationToken);
        _checkpoint = checkpoint ?? (() => Task.CompletedTask);
    }

    public GameState State { get; }

    public Decisions Ask { get; }

    private static GameData Data => GameData.Instance;

    public async Task RunAsync()
    {
        try
        {
            while (State.Phase != GamePhase.GameOver)
            {
                switch (State.Phase)
                {
                    case GamePhase.Setup:
                        await SetupStepAsync();
                        break;
                    case GamePhase.TurnStart:
                        StartTurn();
                        break;
                    case GamePhase.Headline:
                        await HeadlineAsync();
                        break;
                    case GamePhase.ActionRounds:
                        await NextActionRoundAsync();
                        break;
                    case GamePhase.TurnEnd:
                        await EndTurnAsync();
                        break;
                }
            }
        }
        catch (GameOverException)
        {
            // State already records the winner.
        }

        await _checkpoint();
    }

    // ---------------------------------------------------------------- Setup

    public static GameState CreateGame(bool optionalCards = false, int usBonusInfluence = 0)
    {
        var state = GameState.NewGame(optionalCards, usBonusInfluence);

        void Place(string id, Side side, int amount) => state.Country(id).AddInfluence(side, amount);

        Place("syria", Side.USSR, 1);
        Place("iraq", Side.USSR, 1);
        Place("north_korea", Side.USSR, 3);
        Place("east_germany", Side.USSR, 3);
        Place("finland", Side.USSR, 1);

        Place("canada", Side.US, 2);
        Place("iran", Side.US, 1);
        Place("israel", Side.US, 1);
        Place("japan", Side.US, 1);
        Place("australia", Side.US, 4);
        Place("philippines", Side.US, 1);
        Place("south_korea", Side.US, 1);
        Place("panama", Side.US, 1);
        Place("south_africa", Side.US, 1);
        Place("uk", Side.US, 5);

        state.AddLog(null, "New game. Fixed starting influence placed.");
        return state;
    }

    private async Task SetupStepAsync()
    {
        await _checkpoint();

        switch (State.SetupStep)
        {
            case 0:
                await PlaceSetupInfluence(Side.USSR, 6, State.Countries.Where(c => c.IsIn(SubRegion.EasternEurope)),
                    "USSR setup: place 6 Influence anywhere in Eastern Europe");
                State.SetupStep = 1;
                break;

            case 1:
                await PlaceSetupInfluence(Side.US, 7, State.Countries.Where(c => c.IsIn(SubRegion.WesternEurope)),
                    "US setup: place 7 Influence anywhere in Western Europe");
                State.SetupStep = 2;
                break;

            default:
                if (State.UsBonusInfluence > 0)
                {
                    await PlaceSetupInfluence(Side.US, State.UsBonusInfluence, State.Countries.Where(c => c.UsInfluence > 0),
                        $"US bid: place {State.UsBonusInfluence} extra Influence in countries that already have US Influence");
                }

                State.Phase = GamePhase.TurnStart;
                break;
        }
    }

    private async Task PlaceSetupInfluence(Side side, int amount, IEnumerable<Country> eligible, string prompt)
    {
        var request = InfluenceRequests.Fixed(side, prompt, side, remove: false, eligible, amount);
        var allocation = await Ask.Influence(request);
        ApplyInfluence(side, remove: false, allocation, side);
    }

    // ---------------------------------------------------------------- Turn structure

    public int HandSize => State.Turn <= 3 ? 8 : 9;

    public int ActionRoundsFor(Side side)
    {
        var rounds = State.Turn <= 3 ? 6 : 7;

        if (SpaceRace.HasAbility(State, side, 8) ||
            (side == Side.US && State.GetEffect(CardIds.NorthSeaOil)?.Data == State.Turn.ToString()))
        {
            rounds++;
        }

        return rounds;
    }

    private void StartTurn()
    {
        Defcon.Improve(State, 1);

        if (State.Turn == 4)
        {
            AddEraCards(CardEra.MidWar);
        }

        if (State.Turn == 8)
        {
            AddEraCards(CardEra.LateWar);
        }

        foreach (var player in new[] { State.US, State.USSR })
        {
            player.ActionRoundsTaken = 0;
            player.SpaceRaceAttemptsThisTurn = 0;
            player.HeadlineCardId = null;
        }

        State.Phase = GamePhase.Headline;
        State.AddLog(null, $"Turn {State.Turn} begins. Deal cards so each hand has {HandSize} (not counting The China Card).");
    }

    private void AddEraCards(CardEra era)
    {
        foreach (var card in Data.Cards.Where(c => c.Era == era && (State.OptionalCards || !c.Optional)))
        {
            if (State.LocationOf(card.Id) == CardLocation.NotInGame)
            {
                State.CardLocations[card.Id] = CardLocation.Available;
            }
        }

        State.AddLog(null, $"Shuffle the {(era == CardEra.MidWar ? "Mid" : "Late")} War cards into the draw pile (not the discards).");
    }

    private async Task EndTurnAsync()
    {
        await _checkpoint();

        foreach (var side in new[] { Side.US, Side.USSR })
        {
            var shortfall = State.Defcon - State.Player(side).MilitaryOps;

            if (shortfall > 0)
            {
                State.AddLog(side, $"{side.Label()} is {shortfall} short of required Military Ops.");
                State.AddVp(side.Opponent(), shortfall);
            }
        }

        Victory.CheckAutomaticVictory(State);

        await HeldScoringCardCheckAsync();

        State.US.MilitaryOps = 0;
        State.USSR.MilitaryOps = 0;
        State.Effects.RemoveAll(e => e.ThisTurnOnly);
        State.ChinaCardFaceUp = true;

        if (State.Turn >= 10)
        {
            Scoring.ApplyFinal(State);
            return;
        }

        State.Turn++;
        State.Phase = GamePhase.TurnStart;
    }

    /// <summary>
    /// Sequence of play F: players reveal held cards. Scoring cards may never be held;
    /// a held one is either scored now (friendly play) or loses the game (tournament rule 11.1.2).
    /// </summary>
    private async Task HeldScoringCardCheckAsync()
    {
        while (true)
        {
            var unplayed = PlayableCards().Where(id => Data.Card(id).Scoring).ToList();
            var noneLeft = unplayed.Count == 0 ? "All scoring cards are played or discarded" : null;

            var detail = unplayed.Count == 0
                ? "All scoring cards in the current deck have been played or discarded."
                : $"Scoring cards not yet played: {string.Join(", ", unplayed.Select(id => Data.Card(id).Name))}. Scoring cards may never be held.";

            var choice = await Ask.Option(null, $"End of turn {State.Turn}: both players reveal their held cards. Is either player holding a scoring card?",
            [
                new DecisionOption("No scoring cards held"),
                new DecisionOption("USSR is holding a scoring card: score it now", noneLeft),
                new DecisionOption("US is holding a scoring card: score it now", noneLeft),
                new DecisionOption("USSR is holding a scoring card: USSR loses (tournament rule)", noneLeft),
                new DecisionOption("US is holding a scoring card: US loses (tournament rule)", noneLeft)
            ], detail);

            if (choice == 0)
            {
                return;
            }

            var holder = choice is 1 or 3 ? Side.USSR : Side.US;

            if (choice >= 3)
            {
                Victory.EndGame(State, holder.Opponent(), $"{holder.Label()} held a scoring card at the end of the turn (tournament rule 11.1.2).");
            }

            var cardId = await Ask.Card(new CardRequest
            {
                Actor = holder,
                Prompt = $"Which scoring card was the {holder.Label()} holding?",
                Eligible = unplayed
            });

            State.AddLog(holder, $"{holder.Label()} was holding {Data.Card(cardId!.Value).Name}; it is scored now.");
            await ScoreAsync(holder, cardId.Value);
            Victory.CheckAutomaticVictory(State);
        }
    }

    // ---------------------------------------------------------------- Headline

    private async Task HeadlineAsync()
    {
        await _checkpoint();

        // The player with "Man in Earth Orbit" sees the opponent's headline first.
        var first = SpaceRace.HasAbility(State, Side.USSR, 4) ? Side.US : Side.USSR;

        foreach (var side in new[] { first, first.Opponent() })
        {
            var note = SpaceRace.HasAbility(State, side.Opponent(), 4)
                ? $" {side.Opponent().Label()} has Man in Earth Orbit: reveal this headline first."
                : "";

            State.Player(side).HeadlineCardId = await Ask.Card(new CardRequest
            {
                Actor = side,
                Prompt = $"{side.Label()}: choose your headline card.{note}",
                Detail = "Players choose secretly, then reveal together. The China Card cannot be headlined.",
                Eligible = PlayableCards()
            });
        }

        var usCard = State.US.HeadlineCardId!.Value;
        var ussrCard = State.USSR.HeadlineCardId!.Value;
        State.AddLog(null, $"Headlines: US {Data.Card(usCard).Name}, USSR {Data.Card(ussrCard).Name}.");

        var order = new[] { (Side.US, usCard), (Side.USSR, ussrCard) }
            .OrderByDescending(h => HeadlineValue(h.Item2))
            .ThenBy(h => h.Item1 == Side.US ? 0 : 1)
            .ToList();

        var defectorsCancel = usCard == CardIds.Defectors;

        foreach (var (side, cardId) in order)
        {
            if (side == Side.USSR && defectorsCancel)
            {
                State.AddLog(Side.US, $"Defectors cancels the USSR headline {Data.Card(cardId).Name}.");
                State.MoveCard(cardId, CardLocation.Discard);
                continue;
            }

            State.PhasingPlayer = side;
            await HeadlineCardAsync(side, cardId);
            Victory.CheckAutomaticVictory(State);
        }

        State.Phase = GamePhase.ActionRounds;
    }

    private static int HeadlineValue(int cardId)
    {
        var card = Data.Card(cardId);
        return card.Scoring ? 0 : card.Ops;
    }

    private async Task HeadlineCardAsync(Side side, int cardId)
    {
        var card = Data.Card(cardId);

        if (card.Scoring)
        {
            await ScoreAsync(side, cardId);
            return;
        }

        if (cardId is CardIds.UnIntervention or CardIds.Defectors)
        {
            State.AddLog(side, $"{card.Name} has no effect as a headline here.");
            State.MoveCard(cardId, CardLocation.Discard);
            return;
        }

        var implementer = card.Side == Side.Neutral ? side : card.Side;
        var whyNot = CardEvents.WhyNot(State, cardId, implementer);

        if (whyNot is not null)
        {
            State.AddLog(side, $"{card.Name}: event cannot occur ({whyNot}).");
            State.MoveCard(cardId, CardLocation.Discard);
            return;
        }

        var context = await ResolveEventAsync(cardId, implementer, side, headline: true);
        Dispose(cardId, eventOccurred: true, context.Destination, side);
    }

    // ---------------------------------------------------------------- Action rounds

    private async Task NextActionRoundAsync()
    {
        var us = State.US.ActionRoundsTaken;
        var ussr = State.USSR.ActionRoundsTaken;
        var usMax = ActionRoundsFor(Side.US);
        var ussrMax = ActionRoundsFor(Side.USSR);

        Side? next = ussr <= us && ussr < ussrMax ? Side.USSR
            : us < usMax ? Side.US
            : ussr < ussrMax ? Side.USSR
            : null;

        if (next is null)
        {
            State.Phase = GamePhase.TurnEnd;
            return;
        }

        await ActionRoundAsync(next.Value);
    }

    private async Task ActionRoundAsync(Side side)
    {
        State.PhasingPlayer = side;
        State.DefconMovedTo2ThisRound = false;
        await _checkpoint();

        _roundOrigins = new()
        {
            [Side.US] = InfluenceRules.PlacementOrigins(State, Side.US),
            [Side.USSR] = InfluenceRules.PlacementOrigins(State, Side.USSR)
        };

        await OfferMissileCrisisCancelAsync(side);

        var trapped = side == Side.US ? State.HasEffect(CardIds.Quagmire) : State.HasEffect(CardIds.BearTrap);

        if (trapped)
        {
            await TrappedRoundAsync(side);
        }
        else
        {
            await NormalRoundAsync(side);
        }

        await NoradAsync();
        Victory.CheckAutomaticVictory(State);

        State.Player(side).ActionRoundsTaken++;
        _roundOrigins = null;
    }

    private async Task NormalRoundAsync(Side side)
    {
        if (State.HasEffect(CardIds.MissileEnvy, side))
        {
            State.RemoveEffect(CardIds.MissileEnvy);
            State.AddLog(side, $"{side.Label()} must play Missile Envy for Operations.");
            await PlayCardAsync(side, CardIds.MissileEnvy, forcedOps: true);
            return;
        }

        var eligible = PlayableCards();

        if (State.ChinaCardOwner == side && State.ChinaCardFaceUp)
        {
            eligible.Insert(0, CardIds.ChinaCard);
        }

        var cardId = await Ask.Card(new CardRequest
        {
            Actor = side,
            Prompt = $"{side.Label()} action round {State.Player(side).ActionRoundsTaken + 1}: which card is played?",
            Eligible = eligible,
            NoneLabel = "Pass (no cards left)"
        });

        if (cardId is null)
        {
            State.AddLog(side, $"{side.Label()} has no cards and passes.");
            AwardWeWillBuryYou(side);
            return;
        }

        await PlayCardAsync(side, cardId.Value);
    }

    /// <summary>Quagmire / Bear Trap: discard a 2+ Ops card and roll 1-4 to escape.</summary>
    private async Task TrappedRoundAsync(Side side)
    {
        var trapCard = side == Side.US ? CardIds.Quagmire : CardIds.BearTrap;
        var trapName = Data.Card(trapCard).Name;

        var discards = PlayableCards().Where(id => !Data.Card(id).Scoring && OpsCalculator.Value(State, side, id) >= 2).ToList();
        var scoringCards = PlayableCards().Where(id => Data.Card(id).Scoring).ToList();

        var choice = await Ask.Option(side, $"{side.Label()} is caught in {trapName}.",
        [
            new DecisionOption("Discard a card worth 2+ Ops and roll", discards.Count == 0 ? "No such cards left" : null),
            new DecisionOption("Play a scoring card", scoringCards.Count == 0 ? "No scoring cards left" : null),
            new DecisionOption("No 2+ Ops cards in hand: pass")
        ], Data.Card(trapCard).Text);

        AwardWeWillBuryYou(side);

        switch (choice)
        {
            case 0:
                var discard = await Ask.Card(new CardRequest
                {
                    Actor = side,
                    Prompt = "Which card is discarded? (its event does not occur)",
                    Eligible = discards
                });

                State.MoveCard(discard!.Value, CardLocation.Discard);
                var roll = await Ask.Die(side, $"Roll to escape {trapName}", "Escape on 1-4");

                if (roll <= 4)
                {
                    State.RemoveEffect(trapCard);
                    State.AddLog(side, $"{side.Label()} discards {Data.Card(discard.Value).Name}, rolls {roll} and escapes {trapName}.");
                }
                else
                {
                    State.AddLog(side, $"{side.Label()} discards {Data.Card(discard.Value).Name}, rolls {roll}: still trapped.");
                }

                break;

            case 1:
                var scoring = await Ask.Card(new CardRequest
                {
                    Actor = side,
                    Prompt = "Which scoring card?",
                    Eligible = scoringCards
                });

                await ScoreAsync(side, scoring!.Value);
                break;

            default:
                State.AddLog(side, $"{side.Label()} has no eligible cards and passes.");
                break;
        }
    }

    private async Task OfferMissileCrisisCancelAsync(Side side)
    {
        if (!State.HasEffect(CardIds.CubanMissileCrisis, side))
        {
            return;
        }

        var countries = side == Side.USSR
            ? new[] { State.Country("cuba") }
            : new[] { State.Country("west_germany"), State.Country("turkey") };

        var options = new List<DecisionOption> { new("Continue (no coups this turn!)") };
        options.AddRange(countries.Select(c => new DecisionOption(
            $"Remove 2 Influence from {c.Name} to cancel the crisis",
            c.Influence(side) >= 2 ? null : "Not enough influence")));

        var choice = await Ask.Option(side, $"Cuban Missile Crisis: any {side.Label()} coup this turn loses the game.", options);

        if (choice > 0)
        {
            var country = countries[choice - 1];
            country.RemoveInfluence(side, 2);
            State.RemoveEffect(CardIds.CubanMissileCrisis);
            State.AddLog(side, $"{side.Label()} removes 2 Influence from {country.Name}; Cuban Missile Crisis cancelled.");
        }
    }

    private async Task NoradAsync()
    {
        if (!State.HasEffect(CardIds.Norad) || !State.DefconMovedTo2ThisRound || !State.Country("canada").IsControlledByUS)
        {
            return;
        }

        var target = await Ask.Country(new CountryRequest
        {
            Actor = Side.US,
            Prompt = "NORAD: add 1 US Influence to a country that already has US Influence",
            Eligible = State.Countries.Where(c => c.UsInfluence > 0).Select(c => c.Id).ToList(),
            NoneLabel = "Skip"
        });

        if (target is not null)
        {
            State.Country(target).AddInfluence(Side.US, 1);
            State.AddLog(Side.US, $"NORAD: +1 US Influence in {State.Country(target).Name}.");
        }
    }

    private void AwardWeWillBuryYou(Side side)
    {
        if (side == Side.US && State.HasEffect(CardIds.WeWillBuryYou))
        {
            State.RemoveEffect(CardIds.WeWillBuryYou);
            State.AddLog(Side.USSR, "We Will Bury You: the US did not play UN Intervention.");
            State.AddVp(Side.USSR, 3);
        }
    }

    // ---------------------------------------------------------------- Card play

    private enum Usage
    {
        Event,
        Operations,
        SpaceRace
    }

    /// <summary>
    /// Cards that could be in a hand or the draw pile (not discarded, removed or in play).
    /// </summary>
    public List<int> PlayableCards() =>
        State.CardLocations
            .Where(kv => kv.Value == CardLocation.Available)
            .Select(kv => kv.Key)
            .OrderBy(id => Data.Card(id).Name)
            .ToList();

    public List<int> DiscardedCards(bool includeScoring = false) =>
        State.CardLocations
            .Where(kv => kv.Value == CardLocation.Discard && (includeScoring || !Data.Card(kv.Key).Scoring))
            .Select(kv => kv.Key)
            .OrderBy(id => Data.Card(id).Name)
            .ToList();

    /// <summary>
    /// Plays a card as an action: event, operations or space race.
    /// </summary>
    public async Task PlayCardAsync(Side side, int cardId, bool forcedOps = false)
    {
        var card = Data.Card(cardId);
        var opponent = side.Opponent();

        if (card.Scoring)
        {
            AwardWeWillBuryYou(side);
            await ScoreAsync(side, cardId);
            return;
        }

        var opsValue = OpsCalculator.Value(State, side, cardId);
        var maxOps = OpsCalculator.MaxValue(State, side, cardId);
        var opsLabel = maxOps > opsValue ? $"{opsValue}-{maxOps}" : opsValue.ToString();
        var opponentEvent = card.Side == opponent;

        var usages = new List<(Usage Usage, DecisionOption Option)>();

        if (!opponentEvent && !card.IsChinaCard)
        {
            var whyNot = forcedOps ? "Must be used for Operations" : CardEvents.WhyNot(State, cardId, card.Side == Side.Neutral ? side : card.Side);
            var label = cardId == CardIds.UnIntervention ? "Event: cancel an opponent's event and use its Ops" : "Event";
            usages.Add((Usage.Event, new DecisionOption(label, whyNot)));
        }

        var opsText = $"Operations ({opsLabel} Ops)";

        if (opponentEvent)
        {
            var oppWhyNot = CardEvents.WhyNot(State, cardId, opponent);
            opsText += oppWhyNot is null ? $" — {opponent.Label()} event will occur" : $" — {opponent.Label()} event cannot occur ({oppWhyNot})";
        }

        usages.Add((Usage.Operations, new DecisionOption(opsText)));
        usages.Add((Usage.SpaceRace, new DecisionOption("Space Race",
            forcedOps ? "Must be used for Operations" : SpaceRace.WhyNot(State, side, opsValue))));

        var usage = usages[await Ask.Option(side, $"{side.Label()} plays {card.Name}: how is it used?",
            usages.Select(u => u.Option).ToList(), card.Text)].Usage;

        State.AddLog(side, $"{side.Label()} plays {card.Name} for {usage switch { Usage.Event => "the event", Usage.SpaceRace => "the Space Race", _ => "Operations" }}.");

        if (!(usage == Usage.Event && cardId == CardIds.UnIntervention))
        {
            AwardWeWillBuryYou(side);
        }

        if (usage != Usage.SpaceRace)
        {
            if (side == Side.US && CardIds.WarCards.Contains(cardId) && State.HasEffect(CardIds.FlowerPower))
            {
                State.AddLog(Side.USSR, "Flower Power: the US played a War card.");
                State.AddVp(Side.USSR, 2);
            }

            if (side == Side.USSR && cardId == CardIds.Defectors)
            {
                State.AddLog(Side.US, "Defectors played by the USSR.");
                State.AddVp(Side.US, 1);
            }
        }

        switch (usage)
        {
            case Usage.Event when cardId == CardIds.UnIntervention:
                await UnInterventionAsync(side);
                break;

            case Usage.Event:
            {
                var context = await ResolveEventAsync(cardId, card.Side == Side.Neutral ? side : card.Side, side, headline: false);
                Dispose(cardId, eventOccurred: true, context.Destination, side);
                break;
            }

            case Usage.SpaceRace:
            {
                var box = SpaceRace.NextBox(State, side)!;
                var roll = await Ask.Die(side, $"Space Race: roll for {box.Name}", $"Success on 1-{box.MaxRoll}");
                SpaceRace.Attempt(State, side, roll);
                Dispose(cardId, eventOccurred: false, null, side);
                break;
            }

            default:
                await OperationsWithEventAsync(side, cardId);
                break;
        }
    }

    private async Task OperationsWithEventAsync(Side side, int cardId)
    {
        var card = Data.Card(cardId);
        var opponent = side.Opponent();
        var eventFires = card.Side == opponent && CardEvents.WhyNot(State, cardId, opponent) is null;
        var eventFirst = false;
        EventContext? context = null;

        if (eventFires)
        {
            eventFirst = await Ask.Option(side, $"{card.Name}: the {opponent.Label()} event occurs. When?",
                "Event first, then my Operations", "My Operations first, then the event") == 0;
        }

        if (eventFirst)
        {
            context = await ResolveEventAsync(cardId, opponent, side, headline: false);
        }

        await ConductOperationsAsync(side, OpsSource.FromCard(cardId));

        if (eventFires && !eventFirst)
        {
            context = await ResolveEventAsync(cardId, opponent, side, headline: false);
        }

        Dispose(cardId, eventFires, context?.Destination, side);
    }

    private async Task UnInterventionAsync(Side side)
    {
        var opponent = side.Opponent();

        if (side == Side.US && State.HasEffect(CardIds.WeWillBuryYou))
        {
            State.RemoveEffect(CardIds.WeWillBuryYou);
            State.AddLog(Side.US, "UN Intervention stops We Will Bury You.");
        }

        var cancelled = await Ask.Card(new CardRequest
        {
            Actor = side,
            Prompt = $"UN Intervention: which {opponent.Label()} event card is played with it?",
            Eligible = PlayableCards().Where(id => Data.Card(id).Side == opponent).ToList()
        });

        var cancelledId = cancelled!.Value;
        State.AddLog(side, $"UN Intervention cancels {Data.Card(cancelledId).Name}.");

        if (State.HasEffect(CardIds.U2Incident))
        {
            State.AddLog(Side.USSR, "U2 Incident: UN Intervention was played.");
            State.AddVp(Side.USSR, 1);
        }

        await ConductOperationsAsync(side, OpsSource.FromCard(cancelledId));

        State.MoveCard(cancelledId, CardLocation.Discard);
        State.MoveCard(CardIds.UnIntervention, CardLocation.Discard);
    }

    public async Task<EventContext> ResolveEventAsync(int cardId, Side implementer, Side phasing, bool headline)
    {
        var context = new EventContext
        {
            Runner = this,
            CardId = cardId,
            Player = implementer,
            Phasing = phasing,
            IsHeadline = headline
        };

        State.AddLog(implementer, $"Event: {Data.Card(cardId).Name} ({implementer.Label()}).");
        await CardEvents.ResolveAsync(context);
        return context;
    }

    /// <summary>
    /// Moves a played card to the discard pile, out of the game, or in front of the player.
    /// </summary>
    public void Dispose(int cardId, bool eventOccurred, CardLocation? destination, Side playedBy)
    {
        var card = Data.Card(cardId);

        if (card.IsChinaCard)
        {
            State.ChinaCardOwner = playedBy.Opponent();
            State.ChinaCardFaceUp = false;
            State.AddLog(playedBy, $"The China Card passes to the {playedBy.Opponent().Label()} face down.");

            if (playedBy == Side.US && State.HasEffect(CardIds.FormosanResolution))
            {
                State.RemoveEffect(CardIds.FormosanResolution);
                State.MoveCard(CardIds.FormosanResolution, CardLocation.Discard);
                State.AddLog(Side.US, "Formosan Resolution is discarded.");
            }

            return;
        }

        var location = destination ?? (eventOccurred
            ? card.RemoveAfterEvent ? CardLocation.Removed
            : card.Permanent ? CardLocation.InPlay
            : CardLocation.Discard
            : CardLocation.Discard);

        State.MoveCard(cardId, location);
    }

    public async Task ScoreAsync(Side side, int cardId)
    {
        State.AddLog(side, $"{side.Label()} plays {Data.Card(cardId).Name}.");

        if (cardId == CardIds.SoutheastAsiaScoring)
        {
            Scoring.ApplySoutheastAsia(State);
            State.MoveCard(cardId, CardLocation.Removed);
        }
        else
        {
            State.MoveCard(cardId, CardLocation.Discard);
            Scoring.Apply(State, Scoring.ScoringCards[cardId], finalScoring: false);
        }

        await Task.CompletedTask;
    }

    // ---------------------------------------------------------------- Operations

    public async Task ConductOperationsAsync(Side side, OpsSource source)
    {
        int Value(OpsScope scope) => source.CardId is { } id ? OpsCalculator.Value(State, side, id, scope) : source.BaseOps;

        bool Allowed(Country c) => source.Filter?.Invoke(c) ?? true;

        var origins = _roundOrigins?[side] ?? InfluenceRules.PlacementOrigins(State, side);

        var influenceTargets = State.Countries
            .Where(c => Allowed(c) && InfluenceRules.CanPlaceWithOps(State, side, c, origins))
            .ToList();

        var coupTargets = State.Countries
            .Where(c => Allowed(c) && CoupRules.WhyNot(State, side, c, source.Free) is null)
            .ToList();

        var realignTargets = State.Countries
            .Where(c => Allowed(c) && RealignmentRules.WhyNot(State, side, c, source.Free) is null)
            .ToList();

        var options = new List<DecisionOption>
        {
            new("Place Influence", !source.AllowInfluence ? "Not allowed" : influenceTargets.Count == 0 ? "No legal countries" : null),
            new("Coup", !source.AllowCoup ? "Not allowed" : coupTargets.Count == 0 ? "No legal targets" : null),
            new("Realignment rolls", !source.AllowRealign ? "Not allowed" : realignTargets.Count == 0 ? "No legal targets" : null)
        };

        if (options.All(o => !o.Enabled))
        {
            State.AddLog(side, $"{side.Label()} has no legal way to use the Operations.");
            return;
        }

        var maxOps = Value(new OpsScope(true, true));
        var baseOps = Value(OpsScope.None);
        var opsLabel = maxOps > baseOps ? $"{baseOps} ({maxOps} if all in Asia)" : baseOps.ToString();

        var choice = await Ask.Option(side, $"{side.Label()}: use {opsLabel} Ops for?", options);

        switch (choice)
        {
            case 0:
                await PlaceInfluenceWithOpsAsync(side, influenceTargets, Value);
                break;
            case 1:
                await CoupAsync(side, coupTargets, Value, source.Free);
                break;
            default:
                await RealignAsync(side, source, Value, Allowed);
                break;
        }
    }

    private async Task PlaceInfluenceWithOpsAsync(Side side, List<Country> eligible, Func<OpsScope, int> value)
    {
        string? Validate(IReadOnlyDictionary<string, int> allocation)
        {
            var cost = InfluenceRules.PlacementCost(State, side, allocation);
            var ops = value(InfluenceRules.ScopeOf(State, allocation));

            if (cost > ops)
            {
                return $"Costs {cost} Ops but only {ops} available";
            }

            if (cost < ops)
            {
                // Must spend all Ops unless no further marker is affordable.
                foreach (var country in eligible)
                {
                    var next = allocation.ToDictionary(kv => kv.Key, kv => kv.Value);
                    next[country.Id] = next.GetValueOrDefault(country.Id) + 1;

                    if (InfluenceRules.PlacementCost(State, side, next) <= value(InfluenceRules.ScopeOf(State, next)))
                    {
                        return $"{ops - cost} Ops still to spend";
                    }
                }
            }

            return null;
        }

        var request = new InfluenceRequest
        {
            Actor = side,
            Prompt = $"{side.Label()}: place Influence (1 Op each, 2 Ops in opponent-controlled countries)",
            Detail = "Influence must go in or next to countries that had your Influence at the start of this action round.",
            InfluenceSide = side,
            Eligible = eligible.Select(c => c.Id).ToList(),
            Validate = Validate,
            Status = allocation =>
                $"Ops used {InfluenceRules.PlacementCost(State, side, allocation)} / {value(InfluenceRules.ScopeOf(State, allocation))}"
        };

        var placement = await Ask.Influence(request);
        ApplyInfluence(side, remove: false, placement, side);
    }

    private async Task CoupAsync(Side side, List<Country> targets, Func<OpsScope, int> value, bool free)
    {
        var reasons = State.Countries
            .Where(c => c.Influence(side.Opponent()) > 0 && !targets.Contains(c))
            .ToDictionary(c => c.Id, c => CoupRules.WhyNot(State, side, c, free) ?? "Not allowed");

        var warning = State.HasEffect(CardIds.CubanMissileCrisis, side)
            ? "WARNING: Cuban Missile Crisis is in effect — a coup now loses the game!"
            : "Coup: roll + Ops, minus 2x stability. Battleground coups degrade DEFCON.";

        var targetId = await Ask.Country(new CountryRequest
        {
            Actor = side,
            Prompt = $"{side.Label()}: choose the coup target",
            Detail = warning,
            Eligible = targets.Select(c => c.Id).ToList(),
            Reasons = reasons
        });

        var country = State.Country(targetId!);
        var ops = value(OpsScope.For(country));
        var needed = country.Stability * 2 - ops - CoupRules.Modifier(State, side, country) + 1;
        var target = needed <= 1 ? "Always succeeds" : needed > 6 ? "Cannot succeed" : $"Succeeds on {needed}+";

        var roll = await Ask.Die(side, $"{side.Label()} coup in {country.Name} with {ops} Ops: enter the die roll", target);
        CoupRules.Resolve(State, side, country, ops, roll, free);
    }

    private async Task RealignAsync(Side side, OpsSource source, Func<OpsScope, int> value, Func<Country, bool> allowed)
    {
        var targets = new List<Country>();

        while (true)
        {
            var eligible = State.Countries
                .Where(c => allowed(c) && RealignmentRules.WhyNot(State, side, c, source.Free) is null)
                .Where(c => targets.Count + 1 <= value(OpsScope.For(targets.Append(c))))
                .ToList();

            if (eligible.Count == 0)
            {
                break;
            }

            var remaining = value(OpsScope.For(targets.Append(eligible[0]))) - targets.Count;

            var targetId = await Ask.Country(new CountryRequest
            {
                Actor = side,
                Prompt = $"{side.Label()}: realignment roll {targets.Count + 1} (about {remaining} left) — choose a target",
                Detail = "Each player rolls; +1 per adjacent controlled country, +1 for more influence, +1 if adjacent to your superpower.",
                Eligible = eligible.Select(c => c.Id).ToList()
            });

            var country = State.Country(targetId!);
            targets.Add(country);

            var usMod = RealignmentRules.Modifier(State, Side.US, country);
            var ussrMod = RealignmentRules.Modifier(State, Side.USSR, country);
            var usRoll = await Ask.Die(Side.US, $"Realignment in {country.Name}: US die roll (modifier {usMod:+0;-0;0})");
            var ussrRoll = await Ask.Die(Side.USSR, $"Realignment in {country.Name}: USSR die roll (modifier {ussrMod:+0;-0;0})");

            RealignmentRules.Resolve(State, side, country, usRoll, ussrRoll);
        }
    }

    /// <summary>Applies an influence allocation and logs it.</summary>
    public void ApplyInfluence(Side influenceSide, bool remove, IReadOnlyDictionary<string, int> allocation, Side actor)
    {
        var parts = new List<string>();

        foreach (var (id, amount) in allocation.Where(kv => kv.Value > 0))
        {
            var country = State.Country(id);

            if (remove)
            {
                country.RemoveInfluence(influenceSide, amount);
            }
            else
            {
                country.AddInfluence(influenceSide, amount);
            }

            parts.Add($"{country.Name} {(remove ? "-" : "+")}{amount}");
        }

        if (parts.Count > 0)
        {
            State.AddLog(actor, $"{influenceSide.Label()} Influence: {string.Join(", ", parts)}.");
        }
    }
}
