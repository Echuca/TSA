using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using TSA.Core.Enums;

namespace TSA.Core.Data;

/// <summary>
/// Static map and card data, loaded once from the JSON files embedded in this assembly.
/// </summary>
public sealed class GameData
{
    private static readonly Lazy<GameData> LazyInstance = new(Load);

    public static GameData Instance => LazyInstance.Value;

    public IReadOnlyList<CountryDefinition> Countries { get; }

    public IReadOnlyDictionary<string, CountryDefinition> CountriesById { get; }

    public IReadOnlyList<CardDefinition> Cards { get; }

    public IReadOnlyDictionary<int, CardDefinition> CardsById { get; }

    private GameData(List<CountryDefinition> countries, List<CardDefinition> cards)
    {
        Countries = countries;
        CountriesById = countries.ToDictionary(c => c.Id);
        Cards = cards;
        CardsById = cards.ToDictionary(c => c.Id);
    }

    public CardDefinition Card(int id) => CardsById[id];

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static GameData Load()
    {
        var countries = ReadResource<List<CountryDefinition>>("countries.json");
        var cards = ReadResource<List<CardDefinition>>("cards.json");

        return new GameData(countries, cards);
    }

    private static T ReadResource<T>(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"TSA.Core.Data.{fileName}";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");

        return JsonSerializer.Deserialize<T>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"Unable to load '{fileName}'.");
    }
}
