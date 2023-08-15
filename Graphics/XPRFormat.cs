using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XboxIsoLib.Graphics
{
    public enum XPRFormat : byte
    {
        D3DFMT_L8 = 0x00,
        D3DFMT_AL8 = 0x01,
        D3DFMT_A1R5G5B5 = 0x02,
        D3DFMT_X1R5G5B5 = 0x03,
        X_D3DFMT_A4R4G4B4 = 0x04,
        X_D3DFMT_R5G6B5 = 0x05,
        X_D3DFMT_A8R8G8B8 = 0x06,
        X_D3DFMT_X8R8G8B8 = 0x07,
        X_D3DFMT_P8 = 0x0B,
        X_D3DFMT_DXT1 = 0x0C,
        X_D3DFMT_DXT2 = 0x0E,
        X_D3DFMT_DXT3 = 0x0F,
        X_D3DFMT_LIN_R5G6B5 =0x11,
        X_D3DFMT_LIN_A8R8G8B8 = 0x12,
        X_D3DFMT_LIN_R8B8 = 0x16,
        X_D3DFMT_A8L8 = 0x1A,
        D3DFMT_LIN_A4R4G4B4 = 0x1D,
        X_D3DFMT_LIN_X8R8G8B8 = 0x1E,
        X_D3DFMT_YUV2 = 0x24,
        X_D3DFMT_V8U8 = 0x28,
        X_D3DFMT_D24S8 = 0x2A,
        X_D3DFMT_F24S8 = 0x2B,
        X_D3DFMT_D16 = 0x2C,
        X_D3DFMT_LIN_D24S8 = 0x2E,
        X_D3DFMT_LIN_D16 = 0x30,
        X_D3DFMT_V16U16 = 0x33,
        UNKNOWN_ARGB = 0x3C,
        X_D3DFMT_LIN_A8B8G8R8 = 0x3F,
        D3DFMT_VERTEXDATA = 0x64        
    }
}
