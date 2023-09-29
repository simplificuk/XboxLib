using System;
using System.ComponentModel;
using System.IO;
using System.Text;
using Microsoft.VisualBasic;
using BinaryReader = XboxLib.IO.BinaryReader;

namespace XboxLib.Xdbf;

public class XbdfFile
{
    private Stream _stream;
    
    public enum Section : ushort
    {
        Metadata = 1,
        Image = 2,
        StringTable = 3
    }

    public struct Entry
    {
        public Section Section { get; set; }
        public ulong Id { get; set; }
        public uint OffsetSpecifier { get; set; }
        public uint Length { get; set; }

        public static Entry Read(BinaryReader reader)
        {
            return new Entry()
            {
                Section = (Section)reader.ReadUInt16(), Id = reader.ReadUInt64(), OffsetSpecifier = reader.ReadUInt32(),
                Length = reader.ReadUInt32()
            };
        }
    }

    public struct FreeSpace
    {
        public uint Offset { get; set; }
        public uint Size { get; set; }

        public static FreeSpace Read(BinaryReader reader)
        {
            return new FreeSpace { Offset = reader.ReadUInt32(), Size = reader.ReadUInt32() };
        }
    }

    public uint Version { get; set; }
    public long DataOffset { get; set; }
    public Entry[] Entries { get; set; }
    public FreeSpace[] Free { get; set; }

    public byte[] GetEntry(Section section, ulong id)
    {
        foreach (var e in Entries)
        {
            if (e.Section != section || e.Id != id) continue;

            _stream.Position = DataOffset + e.OffsetSpecifier;
            var buffer = new byte[e.Length];
            for (var i = 0; i < buffer.Length;)
            {
                var num = _stream.Read(buffer, i, buffer.Length - i);
                if (num == 0) throw new EndOfStreamException();
                i += num;
            }

            return buffer;
        }

        return Array.Empty<byte>();
    }

    public static XbdfFile Read(Stream stream)
    {
        using var reader = new BinaryReader(stream, BinaryReader.Endian.Big, true);

        var magic = Encoding.ASCII.GetString(reader.ReadBytes(4));
        if (magic == "FBDX")
        {
            reader.Endianness = BinaryReader.Endian.Little;
        }
        else if (magic != "XDBF")
        {
            throw new InvalidDataException("not an XDBF file");
        }

        var version = reader.ReadUInt32();
        var entryTableLength = reader.ReadUInt32();
        var entryCount = reader.ReadUInt32();
        var freeSpaceTableLength = reader.ReadUInt32();
        var freeSpaceTableCount = reader.ReadUInt32();
        var entries = new Entry[entryCount];
        for (var i = 0; i < entries.Length; i++)
        {
            entries[i] = Entry.Read(reader);
        }

        var free = new FreeSpace[freeSpaceTableCount];
        for (var i = 0; i < freeSpaceTableCount; i++)
        {
            free[i] = FreeSpace.Read(reader);
        }

        return new XbdfFile
        {
            _stream = stream,
            Version = version,
            Entries = entries,
            Free = free,
            DataOffset = stream.Position
        };
    }
}