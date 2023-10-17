using XboxLib.IO;

namespace XboxLib.Xex;

public struct PageDescriptor
{
    public uint Value;
    public byte Info => (byte)((Value >> 28) & 0xF);
    public uint PageCount => (Value >> 4) & 0x0fffffff;
    public byte[] Digest;

    public static PageDescriptor Read(BinaryReader reader)
    {
        return new PageDescriptor
        {
            Value = reader.ReadUInt32(),
            Digest = reader.ReadBytes(20)
        };
    }
    
    public static PageDescriptor[] ReadAll(BinaryReader reader)
    {
        var count = reader.ReadUInt32();
        var descriptors = new PageDescriptor[count];
        for (var i = 0; i < count; i++)
        {
            descriptors[i] = Read(reader);
        }

        return descriptors;
    }
}

public class SecurityInfo
{
    public uint HeaderSize { get; set; }
    public uint ImageSize { get; set; }
    public byte[] RsaSignature { get; set; }
    public uint Unknown { get; set; }
    public uint ImageFlags { get; set; }
    public uint LoadAddress { get; set; }
    public byte[] SectionDigest { get; set; }
    public uint ImportTableCount { get; set; }
    public byte[] ImportTableDigest { get; set; }
    public byte[] Xgd2MediaId { get; set; }
    public byte[] AesKey { get; set; }
    public uint ExportTable { get; set; }
    public byte[] HeaderDigest { get; set; }
    public uint Region { get; set; }
    public uint AllowedMediaTypes { get; set; }
    public uint PageDescriptorCount { get; set; }
    public PageDescriptor[] PageDescriptors { get; set; }

    public static SecurityInfo Read(BinaryReader reader)
    {
        return new SecurityInfo
        {
            HeaderSize = reader.ReadUInt32(),
            ImageSize = reader.ReadUInt32(),
            RsaSignature = reader.ReadBytes(256),
            Unknown = reader.ReadUInt32(),
            ImageFlags = reader.ReadUInt32(),
            LoadAddress = reader.ReadUInt32(),
            SectionDigest = reader.ReadBytes(20),
            ImportTableCount = reader.ReadUInt32(),
            ImportTableDigest = reader.ReadBytes(20),
            Xgd2MediaId = reader.ReadBytes(16),
            AesKey = reader.ReadBytes(16),
            ExportTable = reader.ReadUInt32(),
            HeaderDigest = reader.ReadBytes(20),
            Region = reader.ReadUInt32(),
            AllowedMediaTypes = reader.ReadUInt32(),
            PageDescriptors = PageDescriptor.ReadAll(reader)
        };
    }
}