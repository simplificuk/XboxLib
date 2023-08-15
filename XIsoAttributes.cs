using System;

namespace XboxIsoLib
{
    [Flags]
    public enum XIsoAttributes
    {
        ReadOnly = 0x01,
        Hidden = 0x02,
        System = 0x04,
        // What's 0x08?
        Directory = 0x10,
        Archive = 0x20,
        Nor = 0x40 // What?
    }
}
