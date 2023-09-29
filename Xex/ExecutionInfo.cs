using System.IO;

public class ExecutionInfo
{
    public uint MediaId { get; set; }
    public uint Version { get; set; }
    public uint BaseVersion { get; set; }
    public uint TitleId { get; set; }
    public byte Platform { get; set; }
    public byte ExecutableType { get; set; }
    public byte DiscNumber { get; set; }
    public byte DiscCount { get; set; }

    public static ExecutionInfo Read(Stream stream)
    {
        using var reader = new XboxLib.IO.BinaryReader(stream, XboxLib.IO.BinaryReader.Endian.Big, true);
        return new ExecutionInfo()
        {
            MediaId = reader.ReadUInt32(),
            Version = reader.ReadUInt32(),
            BaseVersion = reader.ReadUInt32(),
            TitleId = reader.ReadUInt32(),
            Platform = reader.ReadByte(),
            ExecutableType = reader.ReadByte(),
            DiscNumber = reader.ReadByte(),
            DiscCount = reader.ReadByte()
        };
    }
}