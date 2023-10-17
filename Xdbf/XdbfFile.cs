using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualBasic;
using XboxLib.IO;
using BinaryReader = XboxLib.IO.BinaryReader;

namespace XboxLib.Xdbf;

public class XbdfFile
{
    public struct EntryHeader
    {
        public uint Magic;
        public uint Version;
        public uint Size;
        public ushort Count;

        public static EntryHeader Read(BinaryReader reader)
        {
            return new EntryHeader
            {
                Magic = reader.ReadUInt32(), Version = reader.ReadUInt32(), Size = reader.ReadUInt32(),
                Count = reader.ReadUInt16()
            };
        }
    }

    public enum Language : uint
    {
        Invalid = 0,
        English = 1,
        Japanese = 2,
        German = 3,
        French = 4,
        Spanish = 5,
        Italian = 6,
        Korean = 7,
        TChinese = 8,
        Portuguese = 9,
        SChinese = 10,
        Polish = 11,
        Russian = 12
    }

    public ulong TitleIdentifier = 0x8000;
    public ulong AchievementsIdentifier = 0x58414348;
    public ulong StcIdentifier = 0x58535443;
    public ulong StrIdentifier = 0x58535452;

    public enum Section : ushort
    {
        Metadata = 1,
        Image = 2,
        StringTable = 3
    }

    public struct Entry
    {
        public Section Section;
        public ulong Id;
        public uint OffsetSpecifier;
        public uint Length;

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
        public uint Offset;
        public uint Size;

        public static FreeSpace Read(BinaryReader reader)
        {
            return new FreeSpace { Offset = reader.ReadUInt32(), Size = reader.ReadUInt32() };
        }
    }

    private BinaryReader _reader;

    public uint Version { get; set; }
    public long DataOffset { get; set; }
    public Entry[] Entries { get; set; }
    public FreeSpace[] Free { get; set; }

    public Entry? FindEntry(Section section, ulong id)
    {
        return Entries.FirstOrDefault(e => e.Section == section && e.Id == id);
    }

    private void MoveToEntry(Entry e)
    {
        _reader.BaseStream.Position = DataOffset + e.OffsetSpecifier;
    }

    public byte[] ReadEntry(Section section, ulong id)
    {
        var e = FindEntry(section, id);
        if (e == null) return null;

        MoveToEntry(e.Value);
        var buffer = new byte[e.Value.Length];
        for (var i = 0; i < buffer.Length;)
        {
            var num = _reader.Read(buffer, i, buffer.Length - i);
            if (num == 0) throw new EndOfStreamException();
            i += num;
        }

        return buffer;
    }

    public string GetStringTableEntry(Language language, ushort stringId)
    {
        var langBlockEntry = FindEntry(Section.StringTable, (ulong)language);
        if (langBlockEntry == null) return "";

        MoveToEntry(langBlockEntry.Value);
        var header = EntryHeader.Read(_reader);
        if (header.Magic != StrIdentifier)
            throw new InvalidDataException($"string table is in unexpected format: {header.Magic:X}");
        if (header.Version != 1)
            throw new InvalidDataException($"string table has unsupported version: {header.Version}");

        for (var i = 0; i < header.Count; i++)
        {
            var id = _reader.ReadUInt16();
            var size = _reader.ReadUInt16();
            if (id == stringId)
            {
                return Encoding.UTF8.GetString(_reader.ReadBytes(size));
            }

            _reader.BaseStream.Position += size;
        }

        return "";
    }

    public Language GetDefaultLanguage()
    {
        var stcBlock = FindEntry(Section.Metadata, StcIdentifier);
        if (stcBlock == null)
            return Language.English;
        MoveToEntry(stcBlock.Value);

        var magic = _reader.ReadUInt32();
        if (magic != StcIdentifier)
        {
            throw new InvalidDataException($"STC magic unexpected: {magic:X}");
        }

        _reader.BaseStream.Position += 8;
        return (Language)_reader.ReadUInt32();
    }

    public string GetTitle(Language lang)
    {
        return GetStringTableEntry(lang, (ushort)TitleIdentifier);
    }

    public string GetTitle()
    {
        return GetTitle(GetDefaultLanguage());
    }

    public byte[] GetTitleImage()
    {
        return ReadEntry(Section.Image, TitleIdentifier);
    }

    public static XbdfFile Read(Stream stream)
    {
        var start = stream.Position;
        var reader = new BinaryReader(stream, Endianness.Big, true);

        var magic = Encoding.ASCII.GetString(reader.ReadBytes(4));
        if (magic == "FBDX")
        {
            reader.Endianness = Endianness.Little;
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
            _reader = reader,
            Version = version,
            Entries = entries,
            Free = free,
            DataOffset = start + (entryTableLength * 18 + freeSpaceTableLength * 8) + 24
        };
    }
}