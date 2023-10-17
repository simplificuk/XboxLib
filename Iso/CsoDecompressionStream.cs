using System;
using System.IO;
using System.Text;
using K4os.Compression.LZ4;

namespace XboxLib.Iso;

public sealed class CsoDecompressionStream : Stream
{
    private readonly BinaryReader _base;

    // compressed iso info
    private readonly long _totalBytes;
    private readonly uint _blockSize;
    private readonly byte _align;
    private readonly uint[] _blockIndex;

    // for reading/writing
    private int _currentBlock = 0;
    private int _offsetInBlock = 0;
    private readonly byte[] _decompressionBuffer;
    private readonly byte[] _currentBlockData;

    public CsoDecompressionStream(Stream source)
    {
        _base = new BinaryReader(source);
        if (!Encoding.ASCII.GetString(_base.ReadBytes(4)).Equals("CISO"))
            throw new IOException("Not a valid CISO file");
        var headerSize = _base.ReadUInt32();
        _totalBytes = _base.ReadInt64();
        _blockSize = _base.ReadUInt32();
        _decompressionBuffer = new byte[_blockSize + 4];
        var version = _base.ReadByte();
        if (version > 2) throw new IOException($"Unsupported CISO version: {version}");
        _align = _base.ReadByte();
        _base.BaseStream.Position += 2;

        source.Position = headerSize;
        _blockIndex = new uint[(int)(_totalBytes / _blockSize) + 1];
        for (var i = 0; i < _blockIndex.Length; i++)
        {
            _blockIndex[i] = _base.ReadUInt32();
        }

        _currentBlockData = new byte[_blockSize];
    }

    public override void Flush()
    {
        throw new NotImplementedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = 0;
        while (read < count)
        {
            var remaining = count - read;
            var remainingInBlock = _blockSize - _offsetInBlock;
            var canRead = (int) Math.Min(remaining, remainingInBlock);
            Array.Copy(_currentBlockData, _offsetInBlock, buffer, offset, canRead);
            _offsetInBlock += canRead;
            offset += canRead;
            read += canRead;

            if (_offsetInBlock == _blockSize)
            {
                ReadBlock(_currentBlock + 1);
            }
        }

        return read;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var realOffset = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => Position + offset,
            SeekOrigin.End => Length + offset,
            _ => offset
        };

        var block = (int)(realOffset / _blockSize);

        ReadBlock(block);
        _offsetInBlock = (int)(realOffset % _blockSize);

        return Position;
    }

    private long OffsetForBlock(int block)
    {
        return (_blockIndex[block] & 0x7fffffff) << _align;
    }

    private void ReadBlock(int block)
    {
        if (block > _blockIndex.Length)
            throw new EndOfStreamException();

        var blockOffset = OffsetForBlock(block);

        var size = (int)(OffsetForBlock(block + 1) - blockOffset);

        _base.BaseStream.Position = blockOffset;

        if ((_blockIndex[block] & 0x80000000) != 0)
        {
            // Lz4 compressed block
            var outOff = 0;
            while (true)
            {
                var bSize = _base.ReadInt32();
                if (bSize == 0)
                {
                    break;
                }

                var uncompressed = false;
                if (bSize < 0)
                {
                    uncompressed = true;
                    bSize = -bSize;
                }

                for (var off = 0; off < bSize;)
                {
                    off += _base.Read(_decompressionBuffer, off, bSize - off);
                }

                if (uncompressed)
                {
                    Array.Copy(_decompressionBuffer, 0, _currentBlockData, outOff, bSize);
                    outOff += bSize;
                }
                else
                {
                    var decompressed = LZ4Codec.Decode(_decompressionBuffer, 0, bSize, _currentBlockData, outOff,
                        (int)_blockSize - outOff);
                    if (decompressed < 0)
                    {
                        throw new Exception("FML");
                    }

                    outOff += decompressed;
                }

                if (outOff == _blockSize)
                {
                    break;
                }
            }
        }
        else
        {
            for (var off = 0; off < size;)
            {
                off += _base.Read(_currentBlockData, off, size - off);
            }
        }

        _currentBlock = block;
        _offsetInBlock = 0;
    }

    public override void SetLength(long value)
    {
        throw new System.NotImplementedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new System.NotImplementedException();
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _totalBytes;

    public override long Position
    {
        get => _currentBlock * _blockSize + _offsetInBlock;
        set => Seek(value, SeekOrigin.Begin);
    }

    private new void Dispose(bool disposing)
    {
        _base.Dispose();
    }
}