namespace TSA.Core.Enums;

public enum Region
{
    Europe,
    Asia,
    MiddleEast,
    Africa,
    CentralAmerica,
    SouthAmerica
}

/// <summary>
/// Sub-regions. Austria and Finland are in both Eastern and Western Europe.
/// </summary>
[Flags]
public enum SubRegion
{
    None = 0,
    WesternEurope = 1,
    EasternEurope = 2,
    SoutheastAsia = 4
}

public static class RegionExtensions
{
    public static string Label(this Region region) => region switch
    {
        Region.MiddleEast => "Middle East",
        Region.CentralAmerica => "Central America",
        Region.SouthAmerica => "South America",
        _ => region.ToString()
    };

    /// <summary>
    /// DEFCON level at or below which coups and realignments are banned in the region.
    /// </summary>
    public static int DefconRestrictionLevel(this Region region) => region switch
    {
        Region.Europe => 4,
        Region.Asia => 3,
        Region.MiddleEast => 2,
        _ => 0
    };
}
