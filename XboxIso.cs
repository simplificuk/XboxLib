using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace XboxIsoLib
{
    public sealed class XboxIso : IEnumerable<XIsoNode>
    {
        const int XISO_SECTOR_SIZE = 2048;
        
        public XIsoNode Root { get; set; }
        public Stream Stream { get; private set; }

        public List<XIsoNode> GetEntries()
        {
            var nodes = new List<XIsoNode>();
            var search = new Queue<XIsoNode>();

            Root.Children.ForEach(search.Enqueue);

            while (search.Count > 0)
            {
                var node = search.Dequeue();
                nodes.Add(node);
                node.Children.ForEach(search.Enqueue);
            }

            return nodes;
        }

        public byte[] Read(XIsoNode node)
        {
            Stream.Position = node.Position;

            var data = new byte[node.Length];
            var off = 0;
            while (off < data.Length)
            {
                var read = Stream.Read(data, off, data.Length - off);
                if (read == 0)
                    throw new EndOfStreamException();
                off += read;
            }

            return data;
        }

        public IEnumerator<XIsoNode> GetEnumerator()
        {
            return Root.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Root.GetEnumerator();
        }

        public void Close()
        {
            Root = null;
            Stream.Close();
            Stream = null;
        }

        public static XboxIso Read(Stream input)
        {
            var reader = new ExtendedReader(input);

            input.Position = 0x10000; // Header offset

            var header = reader.ReadString(20);

            if (header != "MICROSOFT*XBOX*MEDIA")
                throw new IOException("Invalid header");
            var dirTableSector = reader.ReadUInt32();
            var dirTableSize = reader.ReadUInt32();
            reader.Seek(2000, SeekOrigin.Current);
            header = reader.ReadString(20);
            if (header != "MICROSOFT*XBOX*MEDIA")
                throw new IOException("Invalid midder");

            var xi = new XboxIso { Stream = input, Root = new XIsoNode(null, "", dirTableSector * XISO_SECTOR_SIZE, dirTableSize, XIsoAttributes.Directory)};

            xi.Root.Iso = xi;
            xi.Root.Root = true;
            
            ReadDirectoryTable(xi.Root, reader, dirTableSector, dirTableSize);

            return xi;
        }

        private static void ReadDirectoryTable(XIsoNode parent, ExtendedReader reader, long sector, long size)
        {
            var start = sector * XISO_SECTOR_SIZE;
            var end = start + size;
            
            reader.Position = start;

            while (reader.Position + 14 < end)
            {
                var l_offset = reader.ReadUInt16();
                var r_offset = reader.ReadUInt16();
                if (l_offset == 0xffff && r_offset == 0xffff)
                {
                    continue;
                }
                var start_sector = reader.ReadUInt32();
                var file_size = reader.ReadUInt32();
                var attrs = (XIsoAttributes)reader.ReadByte();
                var stringLength = reader.ReadByte();
                var nameBytes = reader.ReadBytes(stringLength);
                var name = Encoding.ASCII.GetString(nameBytes);

                var pos = reader.Position;
                
                // Todo: this sometimes misses some entries?
                
                var node = new XIsoNode(parent, name, start_sector * XISO_SECTOR_SIZE, file_size, attrs);

                if (attrs.HasFlag(XIsoAttributes.Directory))
                {
                    ReadDirectoryTable(node, reader, start_sector, file_size);
                }

                reader.Position = pos + ((4 - (pos % 4)) % 4);
            }
        }

        internal class ExtendedReader
        {
            private readonly Stream _stream;
            private BinaryReader bin;

            public ExtendedReader(Stream stream)
            {
                _stream = stream;
                bin = new BinaryReader(stream);
            }

            public byte[] ReadFully(byte[] dest, int off, int len)
            {
                while (len > 0)
                {
                    var read = _stream.Read(dest, off, len);
                    if (read == 0)
                        throw new EndOfStreamException();
                    off += read;
                    len -= read;
                }

                return dest;
            }

            public string ReadString(int len)
            {
                return Encoding.ASCII.GetString(ReadFully(new byte[len], 0, len));
            }

            public int PeekChar()
            {
                return bin.PeekChar();
            }

            public int Read()
            {
                return bin.Read();
            }

            public bool ReadBoolean()
            {
                return bin.ReadBoolean();
            }

            public sbyte ReadSByte()
            {
                return bin.ReadSByte();
            }

            public char ReadChar()
            {
                return bin.ReadChar();
            }

            public short ReadInt16()
            {
                return bin.ReadInt16();
            }

            public ushort ReadUInt16()
            {
                return bin.ReadUInt16();
            }

            public int ReadInt32()
            {
                return bin.ReadInt32();
            }

            public uint ReadUInt32()
            {
                return bin.ReadUInt32();
            }

            public long ReadInt64()
            {
                return bin.ReadInt64();
            }

            public ulong ReadUInt64()
            {
                return bin.ReadUInt64();
            }

            public float ReadSingle()
            {
                return bin.ReadSingle();
            }

            public double ReadDouble()
            {
                return bin.ReadDouble();
            }

            public decimal ReadDecimal()
            {
                return bin.ReadDecimal();
            }

            public string ReadString()
            {
                return bin.ReadString();
            }

            public int Read(char[] buffer, int index, int count)
            {
                return bin.Read(buffer, index, count);
            }

            public char[] ReadChars(int count)
            {
                return bin.ReadChars(count);
            }

            public byte[] ReadBytes(int count)
            {
                return bin.ReadBytes(count);
            }

            public void CopyTo(Stream destination)
            {
                _stream.CopyTo(destination);
            }

            public void CopyTo(Stream destination, int bufferSize)
            {
                _stream.CopyTo(destination, bufferSize);
            }

            public void Close()
            {
                _stream.Close();
            }

            public int Read(byte[] array, int offset, int count)
            {
                return _stream.Read(array, offset, count);
            }

            public long Seek(long offset, SeekOrigin origin)
            {
                return _stream.Seek(offset, origin);
            }

            public void Write(byte[] array, int offset, int count)
            {
                _stream.Write(array, offset, count);
            }

            public IAsyncResult BeginRead(byte[] array, int offset, int numBytes, AsyncCallback userCallback,
                object stateObject)
            {
                return _stream.BeginRead(array, offset, numBytes, userCallback, stateObject);
            }

            public int EndRead(IAsyncResult asyncResult)
            {
                return _stream.EndRead(asyncResult);
            }

            public int ReadByte()
            {
                return _stream.ReadByte();
            }

            public IAsyncResult BeginWrite(byte[] array, int offset, int numBytes, AsyncCallback userCallback,
                object stateObject)
            {
                return _stream.BeginWrite(array, offset, numBytes, userCallback, stateObject);
            }

            public void EndWrite(IAsyncResult asyncResult)
            {
                _stream.EndWrite(asyncResult);
            }

            public void WriteByte(byte value)
            {
                _stream.WriteByte(value);
            }

            public bool CanRead
            {
                get { return _stream.CanRead; }
            }

            public bool CanWrite
            {
                get { return _stream.CanWrite; }
            }

            public bool CanSeek
            {
                get { return _stream.CanSeek; }
            }

            public long Length
            {
                get { return _stream.Length; }
            }

            public long Position
            {
                get { return _stream.Position; }
                set { _stream.Position = value; }
            }
        }
    }
}