namespace XboxLib.Xbe
{
    public enum GameRating: uint
    {
        Pending = 0x00,
        Adult = 0x01,
        Mature = 0x02,
        Teen = 0x03,
        Everyone = 0x04,
        KidsToAdults = 0x05,
        EarlyChildhoold = 0x06,
        Null = 0xffffffff
    }
}
