using TSA.Core.Data;
using TSA.Core.Engine;
using TSA.Core.Enums;
using TSA.Core.Models;
using TSA.Core.Rules;

namespace TSA.Core.Events;

public static class EarlyWarEvents
{
    internal static void Register(Dictionary<int, Func<EventContext, Task>> handlers)
    {
        handlers[CardIds.DuckAndCover] = DuckAndCover;
        handlers[CardIds.FiveYearPlan] = FiveYearPlan;
        handlers[CardIds.SocialistGovernments] = SocialistGovernments;
        handlers[CardIds.Fidel] = ctx => TakeControl(ctx, "cuba");
        handlers[CardIds.VietnamRevolts] = VietnamRevolts;
        handlers[CardIds.Blockade] = Blockade;
        handlers[CardIds.KoreanWar] = ctx => CardEvents.WarAsync(ctx, ctx.Country("south_korea"), 0, 4, 2, 2);
        handlers[CardIds.RomanianAbdication] = ctx => TakeControl(ctx, "romania");
        handlers[CardIds.ArabIsraeliWar] = ArabIsraeliWar;
        handlers[CardIds.Comecon] = Comecon;
        handlers[CardIds.Nasser] = Nasser;
        handlers[CardIds.WarsawPact] = WarsawPact;
        handlers[CardIds.DeGaulle] = DeGaulle;
        handlers[CardIds.CapturedNaziScientist] = CapturedNaziScientist;
        handlers[CardIds.TrumanDoctrine] = TrumanDoctrine;
        handlers[CardIds.OlympicGames] = OlympicGames;
        handlers[CardIds.Nato] = ctx => Effect(ctx, "NATO is in effect.");
        handlers[CardIds.IndependentReds] = IndependentReds;
        handlers[CardIds.MarshallPlan] = MarshallPlan;
        handlers[CardIds.IndoPakistaniWar] = ctx => ChooseWar(ctx, "india", "pakistan");
        handlers[CardIds.Containment] = ctx => Effect(ctx, "US Operations cards get +1 this turn (max 4).", thisTurnOnly: true);
        handlers[CardIds.CiaCreated] = CiaCreated;
        handlers[CardIds.UsJapanPact] = UsJapanPact;
        handlers[CardIds.SuezCrisis] = SuezCrisis;
        handlers[CardIds.EastEuropeanUnrest] = EastEuropeanUnrest;
        handlers[CardIds.Decolonization] = Decolonization;
        handlers[CardIds.RedScarePurge] = RedScarePurge;
        handlers[CardIds.DeStalinization] = DeStalinization;
        handlers[CardIds.NuclearTestBan] = NuclearTestBan;
        handlers[CardIds.FormosanResolution] = ctx => Effect(ctx, "Taiwan counts as a Battleground for scoring while the US controls it.");
        handlers[CardIds.Defectors] = ctx =>
        {
            ctx.Log("Defectors only has an effect as a US headline.");
            return Task.CompletedTask;
        };
        handlers[CardIds.CambridgeFive] = CambridgeFive;
        handlers[CardIds.SpecialRelationship] = SpecialRelationship;
        handlers[CardIds.Norad] = ctx => Effect(ctx, "NORAD is in effect.");
    }

    /// <summary>Records an ongoing effect for the event's card.</summary>
    internal static Task Effect(EventContext ctx, string description, bool thisTurnOnly = false, Side? side = null, string? data = null)
    {
        ctx.State.AddEffect(ctx.CardId, side ?? ctx.Player, thisTurnOnly, data);
        ctx.Log(description);
        return Task.CompletedTask;
    }

    /// <summary>Fidel / Romanian Abdication: remove all opponent influence and take control.</summary>
    private static Task TakeControl(EventContext ctx, string countryId)
    {
        var country = ctx.Country(countryId);
        country.SetInfluence(ctx.Opponent, 0);
        country.GainControl(ctx.Player);
        ctx.Log($"{ctx.Player.Label()} takes control of {country.Name}.");
        return Task.CompletedTask;
    }

    private static Task DuckAndCover(EventContext ctx)
    {
        Defcon.Degrade(ctx.State, 1, ctx.Phasing);
        ctx.State.AddVp(Side.US, 5 - ctx.State.Defcon);
        return Task.CompletedTask;
    }

    private static async Task FiveYearPlan(EventContext ctx)
    {
        var cardId = await CardEvents.AskDiscard(ctx, Side.USSR,
            "Five Year Plan: which card did the USSR randomly discard?", c => !c.IsChinaCard, "USSR has no cards");

        if (cardId is not { } id)
        {
            return;
        }

        var card = GameData.Instance.Card(id);

        if (card.Side == Side.US && CardEvents.WhyNot(ctx.State, id, Side.US) is null)
        {
            ctx.Log($"{card.Name} is a US event and occurs.");
            var inner = await ctx.Runner.ResolveEventAsync(id, Side.US, ctx.Phasing, ctx.IsHeadline);
            ctx.Runner.Dispose(id, eventOccurred: true, inner.Destination, Side.USSR);
        }
        else
        {
            CardEvents.Discard(ctx, id, Side.USSR);
        }
    }

    private static Task SocialistGovernments(EventContext ctx) =>
        ctx.RemoveInfluence("Socialist Governments: remove 3 US Influence from Western Europe (max 2 per country)",
            Side.US, ctx.Countries(c => c.IsIn(SubRegion.WesternEurope)), 3, 2);

    private static Task VietnamRevolts(EventContext ctx)
    {
        ctx.Country("vietnam").AddInfluence(Side.USSR, 2);
        return Effect(ctx, "+2 USSR in Vietnam. USSR gets +1 Op for cards played entirely in Southeast Asia this turn.", thisTurnOnly: true);
    }

    private static async Task Blockade(EventContext ctx)
    {
        var discard = await CardEvents.AskDiscard(ctx, Side.US,
            "Blockade: does the US discard a card worth 3+ Ops? Choose it, or choose none.",
            c => !c.IsChinaCard && !c.Scoring && OpsCalculator.Value(ctx.State, Side.US, c.Id) >= 3,
            "No discard: remove all US Influence from West Germany");

        if (discard is { } id)
        {
            CardEvents.Discard(ctx, id, Side.US);
        }
        else
        {
            ctx.Country("west_germany").SetInfluence(Side.US, 0);
            ctx.Log("All US Influence removed from West Germany.");
        }
    }

    private static Task ArabIsraeliWar(EventContext ctx)
    {
        var israel = ctx.Country("israel");
        return CardEvents.WarAsync(ctx, israel, israel.IsControlledByUS ? 1 : 0, 4, 2, 2);
    }

    private static Task Comecon(EventContext ctx) =>
        ctx.AddInfluence("COMECON: add 1 USSR Influence in each of four non-US-controlled Eastern European countries",
            Side.USSR, ctx.Countries(c => c.IsIn(SubRegion.EasternEurope) && !c.IsControlledByUS), 4, 1);

    private static Task Nasser(EventContext ctx)
    {
        var egypt = ctx.Country("egypt");
        egypt.AddInfluence(Side.USSR, 2);
        egypt.RemoveInfluence(Side.US, (egypt.UsInfluence + 1) / 2);
        ctx.Log($"Egypt is now US {egypt.UsInfluence} / USSR {egypt.UssrInfluence}.");
        return Task.CompletedTask;
    }

    private static async Task WarsawPact(EventContext ctx)
    {
        var easternEurope = ctx.Countries(c => c.IsIn(SubRegion.EasternEurope)).ToList();

        var choice = await ctx.Ask.Option(Side.USSR, "Warsaw Pact: choose one",
            "Remove all US Influence from four Eastern European countries",
            "Add 5 USSR Influence in Eastern Europe (max 2 per country)");

        if (choice == 0)
        {
            var chosen = await ctx.ChooseCountries("Choose up to four Eastern European countries to clear of US Influence",
                easternEurope.Where(c => c.UsInfluence > 0), 1, 4);

            foreach (var id in chosen)
            {
                ctx.Country(id).SetInfluence(Side.US, 0);
                ctx.Log($"All US Influence removed from {ctx.Country(id).Name}.");
            }
        }
        else
        {
            await ctx.AddInfluence("Add 5 USSR Influence in Eastern Europe (max 2 per country)", Side.USSR, easternEurope, 5, 2);
        }

        await Effect(ctx, "Warsaw Pact formed: NATO may now be played.");
    }

    private static Task DeGaulle(EventContext ctx)
    {
        var france = ctx.Country("france");
        france.RemoveInfluence(Side.US, 2);
        france.AddInfluence(Side.USSR, 1);
        return Effect(ctx, "-2 US, +1 USSR in France. NATO no longer protects France.");
    }

    private static Task CapturedNaziScientist(EventContext ctx)
    {
        SpaceRace.Advance(ctx.State, ctx.Player, 1);
        return Task.CompletedTask;
    }

    private static async Task TrumanDoctrine(EventContext ctx)
    {
        var target = await ctx.ChooseCountry("Truman Doctrine: remove all USSR Influence from one uncontrolled European country",
            ctx.Countries(c => c.Region == Region.Europe && c.Controller == Side.Neutral && c.UssrInfluence > 0), "No valid country");

        if (target is not null)
        {
            ctx.Country(target).SetInfluence(Side.USSR, 0);
            ctx.Log($"All USSR Influence removed from {ctx.Country(target).Name}.");
        }
    }

    private static async Task OlympicGames(EventContext ctx)
    {
        var sponsor = ctx.Player;
        var opponent = ctx.Opponent;

        var participate = await ctx.Ask.YesNo(opponent, $"Olympic Games sponsored by {sponsor.Label()}: does the {opponent.Label()} participate or boycott?",
            "Participate", "Boycott");

        if (!participate)
        {
            ctx.Log($"{opponent.Label()} boycotts the Olympics.");
            Defcon.Degrade(ctx.State, 1, ctx.Phasing);
            await ctx.Runner.ConductOperationsAsync(sponsor, OpsSource.Virtual(4));
            return;
        }

        while (true)
        {
            var sponsorRoll = await ctx.Roll($"Olympic Games: {sponsor.Label()} (sponsor, +2) roll", roller: sponsor);
            var opponentRoll = await ctx.Roll($"Olympic Games: {opponent.Label()} roll", roller: opponent);

            if (sponsorRoll + 2 == opponentRoll)
            {
                ctx.Log("Olympic Games tie — reroll.");
                continue;
            }

            var winner = sponsorRoll + 2 > opponentRoll ? sponsor : opponent;
            ctx.Log($"Olympic Games: {sponsor.Label()} {sponsorRoll + 2} vs {opponent.Label()} {opponentRoll}.");
            ctx.State.AddVp(winner, 2);
            return;
        }
    }

    private static async Task IndependentReds(EventContext ctx)
    {
        string[] ids = ["yugoslavia", "romania", "bulgaria", "hungary", "czechoslovakia"];

        var target = await ctx.ChooseCountry("Independent Reds: raise US Influence to equal USSR Influence in one country",
            ids.Select(ctx.Country).Where(c => c.UssrInfluence > c.UsInfluence), "No valid country");

        if (target is not null)
        {
            var country = ctx.Country(target);
            country.SetInfluence(Side.US, country.UssrInfluence);
            ctx.Log($"US Influence in {country.Name} raised to {country.UsInfluence}.");
        }
    }

    private static async Task MarshallPlan(EventContext ctx)
    {
        await ctx.AddInfluence("Marshall Plan: add 1 US Influence in each of seven non-USSR-controlled Western European countries",
            Side.US, ctx.Countries(c => c.IsIn(SubRegion.WesternEurope) && !c.IsControlledByUSSR), 7, 1);

        await Effect(ctx, "Marshall Plan: NATO may now be played.");
    }

    internal static async Task ChooseWar(EventContext ctx, string first, string second)
    {
        var a = ctx.Country(first);
        var b = ctx.Country(second);

        var choice = await ctx.Ask.Option(ctx.Player, $"{ctx.Card.Name}: which country is invaded?",
            $"{a.Name} is invaded (by {b.Name})", $"{b.Name} is invaded (by {a.Name})");

        await CardEvents.WarAsync(ctx, choice == 0 ? a : b, 0, 4, 2, 2);
    }

    private static async Task CiaCreated(EventContext ctx)
    {
        ctx.Log("The USSR reveals their hand to the US.");
        await ctx.Runner.ConductOperationsAsync(Side.US, OpsSource.Virtual(1));
    }

    private static Task UsJapanPact(EventContext ctx)
    {
        ctx.Country("japan").GainControl(Side.US);
        return Effect(ctx, "US controls Japan. USSR may no longer coup or realign in Japan.");
    }

    private static Task SuezCrisis(EventContext ctx) =>
        ctx.RemoveInfluence("Suez Crisis: remove 4 US Influence from France, the UK and Israel (max 2 per country)",
            Side.US, new[] { "france", "uk", "israel" }.Select(ctx.Country), 4, 2);

    private static async Task EastEuropeanUnrest(EventContext ctx)
    {
        var amount = ctx.State.Era == CardEra.LateWar ? 2 : 1;

        var chosen = await ctx.ChooseCountries($"East European Unrest: choose three Eastern European countries to remove {amount} USSR Influence from",
            ctx.Countries(c => c.IsIn(SubRegion.EasternEurope) && c.UssrInfluence > 0), 3, 3);

        foreach (var id in chosen)
        {
            ctx.Country(id).RemoveInfluence(Side.USSR, amount);
            ctx.Log($"-{amount} USSR in {ctx.Country(id).Name}.");
        }
    }

    private static Task Decolonization(EventContext ctx) =>
        ctx.AddInfluence("Decolonization: add 1 USSR Influence in each of four African and/or Southeast Asian countries",
            Side.USSR, ctx.Countries(c => c.Region == Region.Africa || c.IsIn(SubRegion.SoutheastAsia)), 4, 1);

    private static Task RedScarePurge(EventContext ctx) =>
        Effect(ctx, $"{ctx.Opponent.Label()} Operations cards are -1 this turn.", thisTurnOnly: true, side: ctx.Opponent);

    private static async Task DeStalinization(EventContext ctx)
    {
        var removed = await ctx.ChangeInfluence("De-Stalinization: remove up to 4 USSR Influence to relocate",
            Side.USSR, remove: true, ctx.Countries(c => c.UssrInfluence > 0), 4, upTo: true);

        if (removed > 0)
        {
            await ctx.AddInfluence($"De-Stalinization: place the {removed} Influence in non-US-controlled countries (max 2 per country)",
                Side.USSR, ctx.Countries(c => !c.IsControlledByUS), removed, 2);
        }
    }

    private static Task NuclearTestBan(EventContext ctx)
    {
        ctx.State.AddVp(ctx.Player, Math.Max(0, ctx.State.Defcon - 2));
        Defcon.Improve(ctx.State, 2);
        return Task.CompletedTask;
    }

    private static async Task CambridgeFive(EventContext ctx)
    {
        string[] regions = ["Europe", "Asia", "Middle East", "Africa", "Central America", "South America", "Southeast Asia"];
        var options = regions.Prepend("The US holds no scoring cards").ToArray();

        var choice = await ctx.Ask.Option(Side.USSR, "The Cambridge Five: the US shows its scoring cards. Choose a region named on one of them.", options);

        if (choice == 0)
        {
            return;
        }

        Func<Country, bool> inRegion = choice switch
        {
            1 => c => c.Region == Region.Europe,
            2 => c => c.Region == Region.Asia,
            3 => c => c.Region == Region.MiddleEast,
            4 => c => c.Region == Region.Africa,
            5 => c => c.Region == Region.CentralAmerica,
            6 => c => c.Region == Region.SouthAmerica,
            _ => c => c.IsIn(SubRegion.SoutheastAsia)
        };

        await ctx.AddInfluence("Add 1 USSR Influence in that region", Side.USSR, ctx.Countries(inRegion), 1);
    }

    private static async Task SpecialRelationship(EventContext ctx)
    {
        if (!ctx.Country("uk").IsControlledByUS)
        {
            ctx.Log("The US does not control the UK: no effect.");
            return;
        }

        if (ctx.State.HasEffect(CardIds.Nato))
        {
            await ctx.AddInfluence("Special Relationship: add 2 US Influence in one Western European country",
                Side.US, ctx.Countries(c => c.IsIn(SubRegion.WesternEurope)), 2, 2);
            ctx.State.AddVp(Side.US, 2);
        }
        else
        {
            await ctx.AddInfluence("Special Relationship: add 1 US Influence in a country adjacent to the UK",
                Side.US, ctx.Country("uk").AdjacentCountries.Select(ctx.Country), 1);
        }
    }
}
