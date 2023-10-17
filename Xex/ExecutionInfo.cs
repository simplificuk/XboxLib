using XboxLib.IO;

namespace XboxLib.Xex;

public struct ExecutionInfo
{
    public uint MediaId;
    public uint Version;
    public uint BaseVersion;
    public uint TitleId;
    public byte Platform;
    public byte ExecutableType;
    public byte DiscNumber;
    public byte DiscCount;
    public uint SaveGameId;

    public static ExecutionInfo Read(BinaryReader reader)
    {
        return new ExecutionInfo
        {
            MediaId = reader.ReadUInt32(),
            Version = reader.ReadUInt32(),
            BaseVersion = reader.ReadUInt32(),
            TitleId = reader.ReadUInt32(),
            Platform = reader.ReadByte(),
            ExecutableType = reader.ReadByte(),
            DiscNumber = reader.ReadByte(),
            DiscCount = reader.ReadByte(),
            SaveGameId = reader.ReadUInt32()
        };
    }
}