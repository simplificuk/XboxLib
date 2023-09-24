using System.IO;
using System.Text;

namespace XboxLib.Graphics
{
    public static class ImageType
    {
        public const string DDS = "DDS ";
        public const string XPR = "XPR0";
    } 
    
    public class XprHeader
    {
        public readonly uint FileSize;
        public readonly uint HeaderSize;
        public readonly string MagicBytes;
        public readonly uint TextureCommon;
        public readonly uint TextureData;
        public readonly byte TextureFormat;
        public readonly uint TextureLock;
        public readonly byte TextureMisc1;
        public readonly byte TextureRes1;
        public readonly byte TextureRes2;

        public XprHeader()
        {
        }

        public XprHeader(BinaryReader br)
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