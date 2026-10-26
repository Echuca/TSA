using System.Text.Json;
using System.Text.Json.Serialization;
using TSA.Core.Data;
using TSA.Core.Enums;

namespace TSA.Core.Models;

/// <summary>
/// Everything that changes during a game. Plain data so it can be serialized for
/// undo snapshots and saved in browser storage.
/// </summary>
public sealed class GameState
{
    public bool OptionalCards { get; set; }

    /// <summary>Extra influence the US places at setup (tournament bid).</summary>
    public int UsBonusInfluence { get; set; }

    public int Turn { get; set; } = 1;

    public GamePhase Phase { get; set; } = GamePhase.Setup;

    /// <summary>0 = USSR placement, 1 = US placement, 2 = US bonus placement.</summary>
    public int SetupStep { get; set; }

    public Side PhasingPlayer { get; set; } = Side.USSR;

    /// <summary>Valid values: 1-5. 1 means nuclear war.</summary>
    public int Defcon { get; set; } = 5;

    /// <summary>Positive = US ahead, negative = USSR ahead.</summary>
    public int VictoryPoints { get; set; }

    public Side? Winner { get; set; }

    public string? GameOverReason { get; set; }

    [JsonIgnore]
    public bool GameOver => Phase == GamePhase.GameOver;

    public Side ChinaCardOwner { get; set; } = Side.USSR;

    public bool ChinaCardFaceUp { get; set; } = true;

    public List<Country> Countries { get; set; } = [];

    public Dictionary<int, CardLocation> CardLocations { get; set; } = [];

    public List<ActiveEffect> Effects { get; set; } = [];

    public List<LogEntry> Log { get; set; } = [];

    public PlayerState US { get; set; } = new() { Side = Side.US };

    public PlayerState USSR { get; set; } = new() { Side = Side.USSR };

    /// <summary>Set when DEFCON drops to 2 during the current action round (for NORAD).</summary>
    public bool DefconMovedTo2ThisRound { get; set; }

    [JsonIgnore]
    public CardEra Era => Turn >= 8 ? CardEra.LateWar : Turn >= 4 ? CardEra.MidWar : CardEra.EarlyWar;

    public static GameState NewGame(bool optionalCards = false, int usBonusInfluence = 0)
    {
        var data = GameData.Instance;
        var state = new GameState
        {
            OptionalCards = optionalCards,
            UsBonusInfluence = usBonusInfluence,
            Countries = data.Countries.Select(Models.Country.FromDefinition).ToList()
        };

        foreach (var card in data.Cards.Where(c => !c.IsChinaCard))
        {
            var inGame = card.Era == CardEra.EarlyWar && (optionalCards || !card.Optional);
            state.CardLocations[card.Id] = inGame ? CardLocation.Available : CardLocation.NotInGame;
        }

        return state;
    }

    public Country Country(string id) =>
        Countries.FirstOrDefault(c => c.Id == id)
        ?? throw new ArgumentException($"Unknown country '{id}'.");

    public PlayerState Player(Side side) => side switch
    {
        Side.US => US,
        Side.USSR => USSR,
        _ => throw new ArgumentOutOfRangeException(nameof(side))
    };

    public CardLocation LocationOf(int cardId) =>
        CardLocations.TryGetValue(cardId, out var location) ? location : CardLocation.NotInGame;

    public void MoveCard(int cardId, CardLocation location)
    {
        if (cardId != CardIds.ChinaCard)
        {
            CardLocations[cardId] = location;
        }
    }

    /// <summary>Adds VP for <paramref name="side"/> (moves the marker in their favour).</summary>
    public void AddVp(Side side, int amount)
    {
        if (amount == 0 || side == Side.Neutral)
        {
            return;
        }

        VictoryPoints += side == Side.US ? amount : -amount;
        AddLog(side, $"{side.Label()} {(amount > 0 ? "gains" : "loses")} {Math.Abs(amount)} VP (VP now {VpLabel(VictoryPoints)}).");
    }

    public static string VpLabel(int vp) => vp switch
    {
        > 0 => $"US +{vp}",
        < 0 => $"USSR +{-vp}",
        _ => "0"
    };

    public bool HasEffect(int cardId) => Effects.Any(e => e.CardId == cardId);

    public bool HasEffect(int cardId, Side side) => Effects.Any(e => e.CardId == cardId && e.Side == side);

    public ActiveEffect? GetEffect(int cardId) => Effects.FirstOrDefault(e => e.CardId == cardId);

    public void AddEffect(int cardId, Side side, bool thisTurnOnly = false, string? data = null)
    {
        Effects.RemoveAll(e => e.CardId == cardId && e.Side == side);
        Effects.Add(new ActiveEffect { CardId = cardId, Side = side, ThisTurnOnly = thisTurnOnly, Data = data });
    }

    public void RemoveEffect(int cardId) => Effects.RemoveAll(e => e.CardId == cardId);

    public string RoundLabel => Phase switch
    {
        GamePhase.Setup => "Setup",
        GamePhase.Headline => "Headline",
        GamePhase.ActionRounds => $"AR {Player(PhasingPlayer).ActionRoundsTaken + 1}",
        GamePhase.TurnEnd => "End of turn",
        GamePhase.GameOver => "Game over",
        _ => "Start of turn"
    };

    public void AddLog(Side? side, string text) =>
        Log.Add(new LogEntry { Turn = Turn, Round = RoundLabel, Side = side, Text = text });

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public string Serialize() => JsonSerializer.Serialize(this, SerializerOptions);

    public static GameState Deserialize(string json) =>
        JsonSerializer.Deserialize<GameState>(json, SerializerOptions)
        ?? throw new InvalidOperationException("Invalid saved game.");

    public GameState Clone() => Deserialize(Serialize());
}
