using System;
using System.IO;
using System.Text;
using K4os.Compression.LZ4;

namespace XboxIsoLib;

public sealed class CsoStream : Stream, IDisposable, IAsyncDisposable
{
    private BinaryReader _base;

    // compressed iso info
    private uint headerSize;
    private long totalBytes;
    private uint blockSize;
    private byte version;
    private byte align;
    private int align_b => 1 << align;
    private int align_m => align_b - 1;
    private uint[] blockIndex;

    // for reading/writing
    private int currentBlock = 0;
    private int offsetInBlock = 0;
    private byte[] decompressionBuffer;
    private byte[] currentBlockData;

    public CsoStream(Stream source)
    {
        _base = new BinaryReader(source);
        if (!Encoding.ASCII.GetString(_base.ReadBytes(4)).Equals("CISO"))
            throw new IOException("Not a valid CISO file");
        headerSize = _base.ReadUInt32();
        totalBytes = _base.ReadInt64();
        blockSize = _base.ReadUInt32();
        decompressionBuffer = new byte[blockSize + 4];
        version = _base.ReadByte();
        if (version > 2) throw new IOException($"Unsupported CISO version: {version}");
        align = _base.ReadByte();
        _base.BaseStream.Position += 2;

        source.Position = headerSize;
        blockIndex = new uint[(int)(totalBytes / blockSize) + 1];
        for (var i = 0; i < blockIndex.Length; i++)
        {
            blockIndex[i] = _base.ReadUInt32();
        }

        currentBlockData = new byte[blockSize];
    }

    public override void Flush()
    {
        throw new System.NotImplementedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = 0;
        while (read < count)
        {
            var remaining = count - read;
            var remainingInBlock = blockSize - offsetInBlock;
            var canRead = (int) Math.Min(remaining, remainingInBlock);
            Array.Copy(currentBlockData, offsetInBlock, buffer, offset, canRead);
            offsetInBlock += canRead;
            offset += canRead;
            read += canRead;

            if (offsetInBlock == blockSize)
            {
                ReadBlock(currentBlock + 1);
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

        var block = (int) (realOffset / blockSize);

        ReadBlock(block);
        offsetInBlock = (int) (realOffset % blockSize);

        return Position;
    }

    private long offsetForBlock(int block)
    {
        return (blockIndex[block] & 0x7fffffff) << align;
    }

    private void ReadBlock(int block)
    {
        if (block > blockIndex.Length)
            throw new IndexOutOfRangeException();
        
        var blockOffset = offsetForBlock(block);

        var size = (int) (offsetForBlock(block + 1) - blockOffset);
        
        _base.BaseStream.Position = blockOffset;

        if ((blockIndex[block] & 0x80000000) == 0x80000000)
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
                    off += _base.Read(decompressionBuffer, off, bSize - off);
                }

                if (uncompressed)
                {
                    Array.Copy(decompressionBuffer, 0, currentBlockData, outOff, bSize);
                    outOff += bSize;
                }
                else
                {
                    var decompressed = LZ4Codec.Decode(decompressionBuffer, 0, bSize, currentBlockData, outOff, (int)blockSize - outOff);
                    if (decompressed < 0)
                    {
                        throw new Exception("FML");
                    }
                    outOff += decompressed;
                }

                if (outOff == blockSize)
                {
                    break;
                }
            }
        }
        else
        {
            for (var off = 0; off < size;)
            {
                off += _base.Read(currentBlockData, off, size - off);
            }
        }

        currentBlock = block;
        offsetInBlock = 0;
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
    public override long Length => totalBytes;

    public override long Position
    {
        get => currentBlock * blockSize + offsetInBlock;
        set => Seek(value, SeekOrigin.Begin);
    }

    private new void Dispose(bool disposing)
    {
        _base.Dispose();
    }
}