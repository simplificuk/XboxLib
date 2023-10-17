using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using XboxLib.IO;
using BinaryReader = XboxLib.IO.BinaryReader;

namespace XboxLib.Xex;

public class NormalBlockStream : Stream
{
    private BinaryReader _reader;
    private SHA1 _sha1;
    private NormalCompressionInfo _compressionInfo;
    private NormalCompressionInfo.Block _curBlock;
    private NormalCompressionInfo.Block? _nextBlock;
    private uint _blockRemaining, _chunkRemaining;

    public NormalBlockStream(Stream dataStream, NormalCompressionInfo compressionInfo)
    {
        _sha1 = SHA1.Create();
        _reader = new BinaryReader(new CryptoStream(dataStream, _sha1, CryptoStreamMode.Read, true),
            Endianness.Big);
        _compressionInfo = compressionInfo;
        _curBlock = _compressionInfo.FirstBlock;
    }

    private void InitBlock()
    {
        if (_nextBlock.HasValue)
        {
            _sha1.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            if (!_sha1.Hash!.SequenceEqual(_curBlock.Hash))
                throw new IOException("block hash mismatch");
            _curBlock = _nextBlock.Value;
            _nextBlock = null;
        }

        if (_curBlock.Size == 0) return;

        _sha1.Initialize();

        _nextBlock = NormalCompressionInfo.Block.Read(_reader);
        _blockRemaining = _curBlock.Size - 24;
    }

    private void NextChunk()
    {
        if (_blockRemaining < 2) return;
        do
        {
            _chunkRemaining = _reader.ReadUInt16();
            _blockRemaining -= 2;
            if (_chunkRemaining > _blockRemaining) throw new IOException("not enough remaining in block");
        } while (_chunkRemaining == 0 && _blockRemaining > 0);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var totalRead = 0;
        while (_curBlock.Size > 0 && totalRead < count)
        {
            if (_blockRemaining == 0) InitBlock();
            if (_chunkRemaining == 0) NextChunk();

            var available = (int)Math.Min(_chunkRemaining, count - totalRead);
            if (available == 0) continue;
            var read = _reader.Read(buffer, offset, available);
            if (read == 0) return totalRead;
            offset += read;
            totalRead += read;
            _blockRemaining -= (uint)read;
            _chunkRemaining -= (uint)read;
        }

        return totalRead;
    }

    public override void Flush()
    {
        throw new System.NotImplementedException();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new System.NotImplementedException();
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
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _reader.BaseStream.Length;

    public override long Position
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }
}