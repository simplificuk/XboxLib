using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace XboxLib.IO;

public enum Endianness
{
    Little,
    Big
};

public interface IReadOp
{
    public Endianness Endianness { get; set; }
    public int Read([NotNull] byte[] buffer, int off, int len);
    public bool ReadBoolean();
    public byte ReadByte();
    public sbyte ReadSByte();
    public byte[] ReadBytes(int count);
    public float ReadFloat();
    public double ReadDouble();
    public short ReadInt16();
    public int ReadInt32();
    public long ReadInt64();
    public ushort ReadUInt16();
    public int ReadUInt24();
    public uint ReadUInt32();
    public ulong ReadUInt64();
}

internal class ReaderImpl : IReadOp
{
    private Stream BaseStream { get; set; }
    private byte[] _buf;
    
    public Endianness Endianness { get; set; }

    internal ReaderImpl(Stream stream, byte[] buf, Endianness endianness)
    {
        BaseStream = stream;
        _buf = buf;
        Endianness = endianness;
    }
    
    private void FillBuffer(int count)
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
        if (BinaryReader.SystemEndianness == Endianness) return _buf;
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

    public int ReadUInt24()
    {
        var bytes = FillCorrectEndianness(3);
        return (bytes[0] << 16) | (bytes[1] << 8) | bytes[2];
    }
    
    public uint ReadUInt32()
    {
        return BitConverter.ToUInt32(FillCorrectEndianness(4));
    }

    public ulong ReadUInt64()
    {
        return BitConverter.ToUInt64(FillCorrectEndianness(8));
    }
}

public sealed class BinaryReader : IDisposable, IReadOp
{
    public static readonly Endianness SystemEndianness = BitConverter.IsLittleEndian ? Endianness.Little : Endianness.Big;

    public Stream BaseStream { get; }
    private IReadOp _readOpImplementation;
    public Endianness Endianness
    {
        get => _readOpImplementation.Endianness;
        set => _readOpImplementation.Endianness = value;
    }
    private readonly bool _leaveOpen;
    private byte[] _buf = new byte[8];
    
    public int Read(byte[] buffer, int off, int len)
    {
        return _readOpImplementation.Read(buffer, off, len);
    }

    public bool ReadBoolean()
    {
        return _readOpImplementation.ReadBoolean();
    }

    public byte ReadByte()
    {
        return _readOpImplementation.ReadByte();
    }

    public sbyte ReadSByte()
    {
        return _readOpImplementation.ReadSByte();
    }

    public byte[] ReadBytes(int count)
    {
        return _readOpImplementation.ReadBytes(count);
    }

    public float ReadFloat()
    {
        return _readOpImplementation.ReadFloat();
    }

    public double ReadDouble()
    {
        return _readOpImplementation.ReadDouble();
    }

    public short ReadInt16()
    {
        return _readOpImplementation.ReadInt16();
    }

    public int ReadInt32()
    {
        return _readOpImplementation.ReadInt32();
    }

    public long ReadInt64()
    {
        return _readOpImplementation.ReadInt64();
    }

    public ushort ReadUInt16()
    {
        return _readOpImplementation.ReadUInt16();
    }

    public int ReadUInt24()
    {
        return _readOpImplementation.ReadUInt24();
    }

    public uint ReadUInt32()
    {
        return _readOpImplementation.ReadUInt32();
    }

    public ulong ReadUInt64()
    {
        return _readOpImplementation.ReadUInt64();
    }

    public BinaryReader(Stream @base, Endianness endianness, bool leaveOpen = false)
    {
        BaseStream = @base;
        _leaveOpen = leaveOpen;
        _readOpImplementation = new ReaderImpl(BaseStream, _buf, endianness);
    }

    public BinaryReader(Stream @base, bool leaveOpen = false) : this(@base, SystemEndianness, leaveOpen)
    {
    }

    public IReadOp Inverted => new ReaderImpl(BaseStream, _buf,
        _readOpImplementation.Endianness == Endianness.Big ? Endianness.Little : Endianness.Big);
    

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