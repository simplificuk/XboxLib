using System;
using System.IO;

namespace XboxLib.IO;

public class ReadableSubStream: Stream
{
    public Stream Base { get; private set; }
    private readonly long _start;
    private readonly long _len;
    private long _pos;
    
    public ReadableSubStream(Stream source, long position, long length)
    {
        Base = source;
        _start = position;
        _pos = 0;
        _len = length;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        Position = _pos;
        var read = Base.Read(buffer, offset, Math.Min(count, (int) (_len - _pos)));
        _pos += read;
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => Position + offset,
            SeekOrigin.End => _len + offset,
            _ => Position
        };

        Position = newPosition;

        return newPosition;
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }
    
    public override void Flush()
    {
        throw new NotSupportedException();
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _len;

    public override long Position
    {
        get => _pos;
        set
        {
            _pos = value;
            Base.Position = _start + _pos;
        }
    }
}