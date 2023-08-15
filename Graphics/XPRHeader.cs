using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XboxIsoLib.Graphics
{
    public static class ImageType
    {
        public const string DDS = "DDS ";
        public const string XPR = "XPR0";
    } 
    
    public class XPRHeader
    {
        public uint FileSize;
        public uint HeaderSize;
        public string MagicBytes;
        public uint TextureCommon;
        public uint TextureData;
        public byte TextureFormat;
        public uint TextureLock;
        public byte TextureMisc1;
        public byte TextureRes1;
        public byte TextureRes2;

        public XPRHeader()
        {
        }

        public XPRHeader(BinaryReader br)
        {
            MagicBytes = Encoding.ASCII.GetString(br.ReadBytes(4));
            if (!MagicBytes.Equals(ImageType.XPR))
                throw new InvalidDataException("Not an XPR file");
            FileSize = br.ReadUInt32();
            HeaderSize = br.ReadUInt32();
            TextureCommon = br.ReadUInt32();
            TextureData = br.ReadUInt32();
            TextureLock = br.ReadUInt32();
            TextureMisc1 = br.ReadByte();
            TextureFormat = br.ReadByte();
            TextureRes1 = br.ReadByte();
            TextureRes2 = br.ReadByte();
        }
    }
}