using TSA.Core.Enums;

namespace TSA.Components;

/// <summary>
/// Pixel positions on wwwroot/images/board.jpg (2448 x 1584) for drawing counters on the real map.
/// Measured from the board image; adjust here if a counter sits off its box.
/// </summary>
public static class BoardLayout
{
    public const double Width = 2448;
    public const double Height = 1584;

    /// <summary>Country box size and where the US (left) and USSR (right) influence areas sit inside it.</summary>
    public const int BoxWidth = 95;
    public const int BoxHeight = 63;
    public const int InfluenceCenterY = 40;
    public const int UsCenterX = 24;
    public const int UssrCenterX = 71;

    public const int InfluenceTokenSize = 36;
    public const int TrackTokenSize = 44;

    /// <summary>Top-left corner of each country box.</summary>
    public static readonly IReadOnlyDictionary<string, (int X, int Y)> Boxes = new Dictionary<string, (int, int)>
    {
        ["canada"] = (402, 345), ["uk"] = (811, 259), ["norway"] = (926, 117), ["sweden"] = (1071, 180), ["finland"] = (1210, 121),
        ["denmark"] = (952, 190), ["benelux"] = (891, 333), ["france"] = (872, 419), ["spain_portugal"] = (796, 521), ["italy"] = (1013, 481),
        ["greece"] = (1148, 559), ["turkey"] = (1338, 490), ["austria"] = (1042, 406), ["west_germany"] = (997, 333), ["east_germany"] = (1034, 262),
        ["poland"] = (1144, 262), ["czechoslovakia"] = (1124, 333), ["hungary"] = (1147, 406), ["romania"] = (1253, 406), ["yugoslavia"] = (1122, 483),
        ["bulgaria"] = (1232, 483),

        ["lebanon"] = (1276, 563), ["syria"] = (1379, 561), ["israel"] = (1258, 634), ["iraq"] = (1379, 634), ["iran"] = (1482, 634),
        ["libya"] = (1101, 699), ["egypt"] = (1209, 708), ["jordan"] = (1326, 708), ["gulf_states"] = (1447, 707), ["saudi_arabia"] = (1414, 778),

        ["afghanistan"] = (1610, 589), ["pakistan"] = (1610, 681), ["india"] = (1720, 730), ["burma"] = (1852, 743), ["laos_cambodia"] = (1954, 755),
        ["thailand"] = (1912, 828), ["vietnam"] = (2018, 829), ["malaysia"] = (1961, 941), ["indonesia"] = (2135, 1029), ["philippines"] = (2174, 827),
        ["australia"] = (2137, 1156), ["japan"] = (2258, 630), ["taiwan"] = (2129, 715), ["south_korea"] = (2173, 562), ["north_korea"] = (2153, 490),

        ["morocco"] = (824, 655), ["algeria"] = (930, 624), ["tunisia"] = (1038, 615), ["west_african_states"] = (814, 751), ["saharan_states"] = (974, 776),
        ["ivory_coast"] = (882, 889), ["nigeria"] = (1014, 878), ["cameroon"] = (1062, 961), ["zaire"] = (1187, 997), ["angola"] = (1094, 1083),
        ["zimbabwe"] = (1223, 1119), ["botswana"] = (1189, 1196), ["south_africa"] = (1138, 1275), ["se_african_states"] = (1327, 1064),
        ["kenya"] = (1313, 966), ["somalia"] = (1418, 903), ["ethiopia"] = (1302, 871), ["sudan"] = (1226, 795),

        ["mexico"] = (84, 642), ["guatemala"] = (176, 717), ["el_salvador"] = (142, 795), ["honduras"] = (249, 790), ["nicaragua"] = (354, 791),
        ["costa_rica"] = (238, 862), ["panama"] = (355, 862), ["cuba"] = (361, 697), ["haiti"] = (465, 762), ["dominican_republic"] = (568, 762),

        ["venezuela"] = (482, 872), ["colombia"] = (424, 949), ["ecuador"] = (313, 982), ["peru"] = (373, 1062), ["bolivia"] = (483, 1129),
        ["chile"] = (425, 1216), ["argentina"] = (460, 1357), ["paraguay"] = (544, 1208), ["uruguay"] = (578, 1299), ["brazil"] = (666, 1055)
    };

    /// <summary>Centre of turn box 1-10.</summary>
    public static (int X, int Y) Turn(int turn) => ((int)Math.Round(1699 + 74.4 * (Math.Clamp(turn, 1, 10) - 1)), 108);

    private static readonly int[] ActionRoundX = [470, 525, 581, 636, 692, 748, 804, 862];

    /// <summary>Centre of action round box 1-8.</summary>
    public static (int X, int Y) ActionRound(int round) => (ActionRoundX[Math.Clamp(round, 1, 8) - 1], 126);

    private static readonly int[] SpaceX = [1698, 1781, 1864, 1945, 2027, 2108, 2190, 2272, 2353];

    /// <summary>Centre of space race box 0 (Start) - 8 (Space Station).</summary>
    public static (int X, int Y) Space(int position) => (SpaceX[Math.Clamp(position, 0, 8)], 282);

    /// <summary>Centre of DEFCON box 5..1 (1 = nuclear war).</summary>
    public static (int X, int Y) Defcon(int level) => (762 + 75 * (5 - Math.Clamp(level, 1, 5)), 1274);

    /// <summary>Centre of Military Operations box 0-5.</summary>
    public static (int X, int Y) MilitaryOps(int ops) => (762 + 75 * Math.Clamp(ops, 0, 5), 1444);

    private static readonly int[] VpColumnX = [1484, 1548, 1613, 1677, 1741, 1805, 1870, 1934, 1998];
    private static readonly int[] VpRowY = [1210, 1279, 1345, 1413, 1482];

    /// <summary>Centre of the VP track box for a score (positive = US).</summary>
    public static (int X, int Y) VictoryPoints(int vp) => vp switch
    {
        >= 20 => (1966, VpRowY[4]),
        >= 13 => (VpColumnX[vp - 13], VpRowY[4]),
        >= 4 => (VpColumnX[vp - 4], VpRowY[3]),
        >= 1 => (VpColumnX[vp + 5], VpRowY[2]),
        0 => (1741, VpRowY[2]),
        >= -3 => (VpColumnX[3 + vp], VpRowY[2]),
        >= -12 => (VpColumnX[12 + vp], VpRowY[1]),
        >= -19 => (VpColumnX[21 + vp], VpRowY[0]),
        _ => (1517, VpRowY[0])
    };

    /// <summary>Region scoring boxes that get a "No Realignment or Coup Attempts" marker at low DEFCON.</summary>
    public static readonly IReadOnlyDictionary<Region, (int X, int Y)> RegionBoxes = new Dictionary<Region, (int, int)>
    {
        [Region.Europe] = (1115, 105),
        [Region.Asia] = (1834, 660),
        [Region.MiddleEast] = (1594, 834)
    };

    /// <summary>Card reminder counters printed on the counter sheet.</summary>
    public static readonly IReadOnlySet<int> EventTokens = new HashSet<int>
    {
        16, 17, 21, 23, 27, 35, 42, 43, 44, 55, 59, 65, 68, 73, 82, 83, 86, 87, 96, 97
    };

    /// <summary>
    /// Splits influence into counters (largest counter is 8), e.g. 11 → [8, 3].
    /// </summary>
    public static IEnumerable<int> Counters(int influence)
    {
        while (influence > 8)
        {
            yield return 8;
            influence -= 8;
        }

        if (influence > 0)
        {
            yield return influence;
        }
    }

    public static string Percent(double value, double total) =>
        (value / total * 100).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "%";
}
