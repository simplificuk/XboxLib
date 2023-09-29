using System.IO;

namespace XboxLib.Xex;

public enum EncryptionType : ushort
{
    None = 0,
    Normal = 1
}

public enum CompressionType : ushort
{
    None = 0,
    Basic = 1,
    Normal = 2,
    Delta = 3
}

public class CompressionInfo
{
    public CompressionType Type { get; set; }

    internal CompressionInfo(CompressionType type)
    {
        Type = type;
    }
}

public sealed class BasicCompressionInfo: CompressionInfo
{
    public struct Block
    {
        public uint DataSize { get; set; }
        public uint ZeroSize { get; set; }

        public uint Size => DataSize + ZeroSize;

        public static Block Read(XboxLib.IO.BinaryReader reader)
        {
            return new Block { DataSize = reader.ReadUInt32(), ZeroSize = reader.ReadUInt32() };
        }
    }

    public Block[] Blocks { get; set; }
    
    public BasicCompressionInfo(Block[] blocks): base(CompressionType.Basic)
    {
        Blocks = blocks;
    }
    
    public static BasicCompressionInfo Read(XboxLib.IO.BinaryReader reader, uint size)
    {
        var blockCount = (size - 8) / 8;
        var blocks = new Block[blockCount];
        for (var i = 0; i < blockCount; i++)
        {
            blocks[i] = Block.Read(reader);
        }

        return new BasicCompressionInfo(blocks);
    }
}

public sealed class NormalCompressionInfo : CompressionInfo
{
    public struct Block
    {
        public uint Size { get; set; }
        public byte[] Hash { get; set; }

        public static Block Read(XboxLib.IO.BinaryReader reader)
        {
            return new Block { Size = reader.ReadUInt32(), Hash = reader.ReadBytes(20) };
        }
    }
    
    public uint WindowSize { get; set; }
    public Block FirstBlock { get; set; }

    public NormalCompressionInfo(uint windowSize, Block firstBlock): base(CompressionType.Normal)
    {
        WindowSize = windowSize;
        FirstBlock = firstBlock;
    }
    
    public static NormalCompressionInfo Read(XboxLib.IO.BinaryReader reader)
    {
        return new NormalCompressionInfo(reader.ReadUInt32(), Block.Read(reader));
    }
}

public class FileFormatInfo
{
    public EncryptionType Encryption { get; set; }
    public CompressionInfo Compression { get; set; }
    
    public static FileFormatInfo Read(Stream stream)
    {
        using var reader = new XboxLib.IO.BinaryReader(stream, IO.BinaryReader.Endian.Big, true);
        var size = reader.ReadUInt32();
        var encryptionType = (EncryptionType) reader.ReadUInt16();
        var compressionType = (CompressionType)reader.ReadUInt16();
        var compression = compressionType switch
        {
            CompressionType.None => new CompressionInfo(CompressionType.None),
            CompressionType.Basic => BasicCompressionInfo.Read(reader, size),
            CompressionType.Normal => NormalCompressionInfo.Read(reader),
            _ => throw new InvalidDataException("unable to handle XEX compression that isn't basic or normal")
        };

        return new FileFormatInfo() { Encryption = encryptionType, Compression = compression };
    }
}