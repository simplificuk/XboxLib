using System;
using System.IO;
using System.Text;
using K4os.Compression.LZ4;

namespace XboxLib.Iso
{
    public sealed class CsoCompressionStream : Stream
    {
        public const int CisoHeaderSize = 0x18;
        public const uint CisoCompressionMarker = 0x80000000;
        public const uint FileSplitBoundary = 0xFFBF6000;
        
        private readonly BinaryWriter _writeBase; // For writing the compressed data
        private readonly int _blockSize;
        private long _totalBytes;
        private bool _writtenHeader;
        private int _numBlocks;
        private uint[] _blockIndex;
        private readonly byte[] _compressionBuffer;
        private readonly byte[] _currentBlockData;
        private int _currentBlock;
        private int _offsetInBlock;
        private int _currentBlockSize;
        private readonly int _version = 2;
        private readonly int _align = 2;
        private readonly int _alignBytes;
        private readonly LZ4Level _compressionLevel;

        public CsoCompressionStream(Stream baseStream, long totalBytes, LZ4Level compressionLevel = LZ4Level.L12_MAX, int blockSize = 2048)
        {
            if (!baseStream.CanSeek || !baseStream.CanWrite)
                throw new ArgumentException("supplied stream must support seeking and writing - write to a temporary file or memory stream before writing to a network pipe");
            _writeBase = new BinaryWriter(baseStream);
            _blockSize = blockSize;
            _compressionBuffer = new byte[_blockSize * 2];  // 2x block size to account for worst-case scenario
            _currentBlockData = new byte[_blockSize];
            _alignBytes = 1 << _align;
            _compressionLevel = compressionLevel;
            SetLength(totalBytes);
        }

        private void WriteCsoHeader()
        {
            _writtenHeader = true;
            _writeBase.BaseStream.Position = 0;
            _writeBase.Write(Encoding.ASCII.GetBytes("CISO"));
            _writeBase.Write(CisoHeaderSize);
            _writeBase.Write(_totalBytes);
            _writeBase.Write(_blockSize);
            _writeBase.Write((byte)_version);  // Set CSO version to 2
            _writeBase.Write((byte)_align);  // Set alignment to 2
            _writeBase.Write((short)0); // Write two zero bytes for padding
        }

        private void WriteBlockIndex()
        {
            _writeBase.Seek(CisoHeaderSize, SeekOrigin.Begin);
            foreach (var b in _blockIndex)
            {
                _writeBase.Write(b);
            }
        }

        public long GetSplitOffset(long offset)
        {
            while (offset > FileSplitBoundary)
                offset -= FileSplitBoundary;
            return offset;
        }

        private int AlignmentFor(long offset, int alignment)
        {
            return (alignment - (int) (offset % alignment)) % alignment;
        }

        private void Pad(int padBytes)
        {
            for (var i = 0; i < padBytes; i++)
            {
                _writeBase.Write((byte) 0);
            }
        }

        private void WriteBlock()
        {
            // Compress our block
            var compressedSize = LZ4Codec.Encode(_currentBlockData, 0, _currentBlockSize, _compressionBuffer, 0,
                _compressionBuffer.Length, _compressionLevel);
                    
            // Align
            Pad(AlignmentFor(Position, _alignBytes));
                    
            // Record our block position
            // Todo: this is hacky - the data stream will have to be split at a higher level, maybe that should also
            // adjust the block index to account for the split?
            _blockIndex[_currentBlock] = (uint) (GetSplitOffset(Position) >> _align);
                
            // Write block
            if (compressedSize + 12 >= _currentBlockSize)
            {
                // Use the raw data
                _writeBase.Write(_currentBlockData, 0, _currentBlockSize);
            }
            else
            {
                // Use the compressed data
                _blockIndex[_currentBlock] |= CisoCompressionMarker;
                _writeBase.Write(compressedSize);
                _writeBase.Write(_compressionBuffer, 0, compressedSize);
            }
            
            // Next
            _currentBlock += 1;
            _currentBlockSize = _blockSize;
            _offsetInBlock = 0;
            
            // Set the correct size for the last block
            if (_currentBlock == _numBlocks - 1)
            {
                _currentBlockSize = (int) (_totalBytes - ((long) _blockSize * _currentBlock));
            } else if (_currentBlock == _numBlocks)
            {
                // After writing the last block, update the block index
                _writeBase.BaseStream.Seek(0, SeekOrigin.End);
                _blockIndex[_numBlocks] = (uint) (GetSplitOffset(Position) >> _align);
                Pad(AlignmentFor(Position, 1024));
                WriteBlockIndex();
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if ((_currentBlock - 1) * _blockSize + _offsetInBlock + count > _totalBytes)
                throw new EndOfStreamException("attempted to write more data than initially specified");
            
            if (!_writtenHeader)
            {
                WriteCsoHeader();
                WriteBlockIndex();
            }
            
            while (count > 0)
            {
                var toCopy = Math.Min(count, _currentBlockSize - _offsetInBlock);
                Array.Copy(buffer, offset, _currentBlockData, _offsetInBlock, toCopy);
                count -= toCopy;
                offset += toCopy;
                _offsetInBlock += toCopy;
                
                if (_offsetInBlock == _currentBlockSize) WriteBlock();
            }
        }

        public override void Flush()
        {
            _writeBase.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            if (_writtenHeader) throw new IOException("length must be set before initial write");
            
            _totalBytes = value;
            _currentBlockSize = (int) Math.Min(_blockSize, _totalBytes);
            _numBlocks = (int) (_totalBytes / _blockSize);
            _blockIndex = new uint[_numBlocks + 1];
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _totalBytes;
        public override long Position
        {
            get => _writeBase.BaseStream.Position;
            set => throw new NotSupportedException();
        }

        private new void Dispose(bool disposing)
        {
            _writeBase.Dispose();
        }
    }
}