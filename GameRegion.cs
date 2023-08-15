using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XboxIsoLib
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
