namespace XboxLib.Stfs;

public enum XContentType : uint
{
    SavedGame = 0x00000001,
    MarketplaceContent = 0x00000002,
    Publisher = 0x00000003,
    Xbox360Title = 0x00001000,
    IptvPauseBuffer = 0x00002000,
    XnaCommunity = 0x00003000,
    InstalledGame = 0x00004000,
    XboxTitle = 0x00005000,
    SocialTitle = 0x00006000,
    GamesOnDemand = 0x00007000,
    SuStoragePack = 0x00008000,
    AvatarItem = 0x00009000,
    Profile = 0x00010000,
    GamerPicture = 0x00020000,
    Theme = 0x00030000,
    CacheFile = 0x00040000,
    StorageDownload = 0x00050000,
    XboxSavedGame = 0x00060000,
    XboxDownload = 0x00070000,
    GameDemo = 0x00080000,
    Video = 0x00090000,
    GameTitle = 0x000A0000,
    Installer = 0x000B0000,
    GameTrailer = 0x000C0000,
    ArcadeTitle = 0x000D0000,
    Xna = 0x000E0000,
    LicenseStore = 0x000F0000,
    Movie = 0x00100000,
    Tv = 0x00200000,
    MusicVideo = 0x00300000,
    GameVideo = 0x00400000,
    PodcastVideo = 0x00500000,
    ViralVideo = 0x00600000,
    CommunityGame = 0x02000000
}