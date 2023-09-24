using System;

namespace XboxLib.Xbe
{
    [Flags]
    public enum GameRegion: uint
    {
        NotApplicable = 0x00000001,
        Japan = 0x00000002,
        RestOfWorld = 0x00000004,
        Manufacturing = 0x80000000
    }
}
