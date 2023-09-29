using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;

namespace XboxLib.IO;

public class BinaryReader : IDisposable
{
    private static Endian? _systemEndianness;

    public static Endian SystemEndianness =>
        _systemEndianness ??= BitConverter.IsLittleEndian ? Endian.Little : Endian.Big;

    public enum Endian
    {
        Little,
        Big
    };

    public Stream BaseStream { get; private set; }
    public Endian Endianness { get; set; }
    private readonly bool _leaveOpen;
    private byte[] _buf = new byte[8];

    public BinaryReader(Stream @base, Endian endian, bool leaveOpen = false)
    {
        BaseStream = @base;
        Endianness = endian;
        _leaveOpen = leaveOpen;
    }

    public BinaryReader(Stream @base, bool leaveOpen = false) : this(@base,
        BitConverter.IsLittleEndian ? Endian.Little : Endian.Big, leaveOpen)
    {
    }

    public void FillBuffer(int count)
    {
        for (var off = 0; off < count;)
        {
            var read = Read(_buf, off, count - off);
            if (read == 0) throw new EndOfStreamException();
            off += read;
        }
    }

    private byte[] CorrectEndianness(int count)
    {
        if (SystemEndianness == Endianness) return _buf;
        Array.Reverse(_buf, 0, count);
        return _buf;
    }

    private byte[] FillCorrectEndianness(int count)
    {
        FillBuffer(count);
        return CorrectEndianness(count);
    }

    public int Read([NotNull] byte[] buffer, int off, int len)
    {
        return BaseStream.Read(buffer, off, len);
    }

    public bool ReadBoolean()
    {
        return ReadByte() != 0;
    }

    public byte ReadByte()
    {
        FillBuffer(1);
        return _buf[0];
    }

    public byte[] ReadBytes(int count)
    {
        var buffer = new byte[count];
        for (int off = 0; off < count;)
        {
            var read = Read(buffer, off, count - off);
            if (read == 0) throw new EndOfStreamException();
            off += read;
        }

        return buffer;
    }

    public float ReadFloat()
    {
        return BitConverter.ToSingle(FillCorrectEndianness(4));
    }

    public double ReadDouble()
    {
        return BitConverter.ToDouble(FillCorrectEndianness(8));
    }

    public short ReadInt16()
    {
        return BitConverter.ToInt16(FillCorrectEndianness(2));
    }

    public int ReadInt32()
    {
        return BitConverter.ToInt32(FillCorrectEndianness(4));
    }

    public long ReadInt64()
    {
        return BitConverter.ToInt64(FillCorrectEndianness(8));
    }

    public sbyte ReadSByte()
    {
        return (sbyte)ReadByte();
    }

    public ushort ReadUInt16()
    {
        return BitConverter.ToUInt16(FillCorrectEndianness(2));
    }

    public uint ReadUInt32()
    {
        return BitConverter.ToUInt32(FillCorrectEndianness(4));
    }

    public ulong ReadUInt64()
    {
        return BitConverter.ToUInt64(FillCorrectEndianness(8));
    }

    public void Close()
    {
        BaseStream.Close();
    }

    public void Dispose()
    {
        _buf = null;
        if (_leaveOpen) return;
        Close();
    }
}