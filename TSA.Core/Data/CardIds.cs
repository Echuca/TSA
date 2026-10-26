namespace TSA.Core.Data;

/// <summary>
/// Card numbers referenced by rules and events.
/// </summary>
public static class CardIds
{
    public const int AsiaScoring = 1;
    public const int EuropeScoring = 2;
    public const int MiddleEastScoring = 3;
    public const int DuckAndCover = 4;
    public const int FiveYearPlan = 5;
    public const int ChinaCard = 6;
    public const int SocialistGovernments = 7;
    public const int Fidel = 8;
    public const int VietnamRevolts = 9;
    public const int Blockade = 10;
    public const int KoreanWar = 11;
    public const int RomanianAbdication = 12;
    public const int ArabIsraeliWar = 13;
    public const int Comecon = 14;
    public const int Nasser = 15;
    public const int WarsawPact = 16;
    public const int DeGaulle = 17;
    public const int CapturedNaziScientist = 18;
    public const int TrumanDoctrine = 19;
    public const int OlympicGames = 20;
    public const int Nato = 21;
    public const int IndependentReds = 22;
    public const int MarshallPlan = 23;
    public const int IndoPakistaniWar = 24;
    public const int Containment = 25;
    public const int CiaCreated = 26;
    public const int UsJapanPact = 27;
    public const int SuezCrisis = 28;
    public const int EastEuropeanUnrest = 29;
    public const int Decolonization = 30;
    public const int RedScarePurge = 31;
    public const int UnIntervention = 32;
    public const int DeStalinization = 33;
    public const int NuclearTestBan = 34;
    public const int FormosanResolution = 35;
    public const int BrushWar = 36;
    public const int CentralAmericaScoring = 37;
    public const int SoutheastAsiaScoring = 38;
    public const int ArmsRace = 39;
    public const int CubanMissileCrisis = 40;
    public const int NuclearSubs = 41;
    public const int Quagmire = 42;
    public const int SaltNegotiations = 43;
    public const int BearTrap = 44;
    public const int Summit = 45;
    public const int HowILearned = 46;
    public const int Junta = 47;
    public const int KitchenDebates = 48;
    public const int MissileEnvy = 49;
    public const int WeWillBuryYou = 50;
    public const int BrezhnevDoctrine = 51;
    public const int PortugueseEmpire = 52;
    public const int SouthAfricanUnrest = 53;
    public const int Allende = 54;
    public const int WillyBrandt = 55;
    public const int MuslimRevolution = 56;
    public const int AbmTreaty = 57;
    public const int CulturalRevolution = 58;
    public const int FlowerPower = 59;
    public const int U2Incident = 60;
    public const int Opec = 61;
    public const int LoneGunman = 62;
    public const int ColonialRearGuards = 63;
    public const int PanamaCanal = 64;
    public const int CampDavid = 65;
    public const int PuppetGovernments = 66;
    public const int GrainSales = 67;
    public const int JohnPaul = 68;
    public const int DeathSquads = 69;
    public const int OasFounded = 70;
    public const int NixonChina = 71;
    public const int SadatExpels = 72;
    public const int ShuttleDiplomacy = 73;
    public const int VoiceOfAmerica = 74;
    public const int LiberationTheology = 75;
    public const int UssuriRiver = 76;
    public const int AskNot = 77;
    public const int AllianceForProgress = 78;
    public const int AfricaScoring = 79;
    public const int OneSmallStep = 80;
    public const int SouthAmericaScoring = 81;
    public const int IranianHostageCrisis = 82;
    public const int IronLady = 83;
    public const int ReaganBombsLibya = 84;
    public const int StarWars = 85;
    public const int NorthSeaOil = 86;
    public const int Reformer = 87;
    public const int MarineBarracks = 88;
    public const int Kal007 = 89;
    public const int Glasnost = 90;
    public const int Ortega = 91;
    public const int Terrorism = 92;
    public const int IranContra = 93;
    public const int Chernobyl = 94;
    public const int LatinDebtCrisis = 95;
    public const int TearDownThisWall = 96;
    public const int EvilEmpire = 97;
    public const int AldrichAmes = 98;
    public const int PershingII = 99;
    public const int Wargames = 100;
    public const int Solidarity = 101;
    public const int IranIraqWar = 102;
    public const int Defectors = 103;
    public const int CambridgeFive = 104;
    public const int SpecialRelationship = 105;
    public const int Norad = 106;
    public const int Che = 107;
    public const int OurManInTehran = 108;
    public const int SamanthaAndYuri = 109;
    public const int Awacs = 110;

    /// <summary>
    /// Cards that trigger Flower Power when the US plays them.
    /// </summary>
    public static readonly IReadOnlySet<int> WarCards =
        new HashSet<int> { KoreanWar, ArabIsraeliWar, IndoPakistaniWar, BrushWar, IranIraqWar };
}
