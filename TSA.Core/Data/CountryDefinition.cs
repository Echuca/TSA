using TSA.Core.Enums;

namespace TSA.Core.Data;

/// <summary>
/// A country as printed on the map. Loaded from countries.json.
/// </summary>
public sealed class CountryDefinition
{
    /// <summary>
    /// Unique identifier used internally. Example: "west_germany"
    /// </summary>
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public Region Region { get; set; }

    public List<SubRegion> SubRegions { get; set; } = [];

    public int Stability { get; set; }

    public bool Battleground { get; set; }

    /// <summary>
    /// The superpower space this country is connected to, if any.
    /// </summary>
    public Side? AdjacentSuperpower { get; set; }

    /// <summary>
    /// Ids of connected countries (brown and red dashed lines).
    /// </summary>
    public List<string> Adjacent { get; set; } = [];
}
