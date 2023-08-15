using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XboxIsoLib
{
    [Flags]
    public enum MediaFlags: uint
    {
        HardDisk = 0x01,
        DvdX2 = 0x02,
        DvdCd = 0x04,
        Cd = 0x08,
        Dvd5Ro = 0x10,
        Dvd9Ro = 0x20,
        Dvd5Rw = 0x40,
        Dvd9Rw = 0x80,
        Dongle = 0x100,
        MediaBoard = 0x200,
        NonsecureHardDisk = 0x40000000,
        NonsecureMode = 0x80000000
    }
}