using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using XboxLib.IO;
using BinaryReader = System.IO.BinaryReader;

namespace XboxLib.Iso
{
    public sealed class XIso : IEnumerable<XIsoNode>, IDisposable
    {
        private const long XIsoSectorSize = 2048;

        public XIsoNode Root { get; private set; }
        private Stream Stream { get; set; }

        public List<XIsoNode> AllEntries()
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

        public Stream GetDataStream(XIsoNode node)
        {
            if (node.IsDirectory) throw new ArgumentException("directories do not have a data stream");
            return new ReadableSubStream(Stream, node.Position, node.Length);
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
            Stream?.Close();
            Stream = null;
        }

        public static XIso Read(Stream input)
        {
            var reader = new BinaryReader(input);
            
            // Find out offset from ISO type (original xbox, GDF, XGD3)
            const long headerOff = 32 * XIsoSectorSize;
            var rootOffset = -1L;
            foreach (var off in new uint[] { 0, 0x10000, 0xfd90000, 0x2080000, 0x18300000 })
            {
                input.Position = headerOff + off;
                if (Encoding.ASCII.GetString(reader.ReadBytes(20)) != "MICROSOFT*XBOX*MEDIA") continue;
                rootOffset = off;
                break;
            }

            if (rootOffset == -1)
            {
                throw new Exception("Invalid/unknown ISO format");
            }

            var dirTableSector = reader.ReadUInt32();
            var dirTableSize = reader.ReadUInt32();

            var xi = new XIso
            {
                Stream = input,
                Root = new XIsoNode(null, "", rootOffset + dirTableSector * XIsoSectorSize, dirTableSize, XIsoAttribute.Directory)
                    {
                        Root = true
                    }
            };
            xi.Root.Iso = xi;

            ReadDirectoryTable(xi.Root, reader, rootOffset);

            return xi;
        }

        private static void ReadDirectoryTable(XIsoNode parent, BinaryReader reader, long rootOffset)
        {
            var start = parent.Position;
            var end = start + parent.Length;

            reader.BaseStream.Position = start;

            while (reader.BaseStream.Position + 14 < end)
            {
                var lOffset = reader.ReadUInt16();
                var rOffset = reader.ReadUInt16();
                if (lOffset == 0xffff && rOffset == 0xffff)
                {
                    continue;
                }

                var startSector = reader.ReadUInt32();
                var fileSize = reader.ReadUInt32();
                var attrs = (XIsoAttribute)reader.ReadByte();
                var name = Encoding.ASCII.GetString(reader.ReadBytes(reader.ReadByte()));

                var pos = reader.BaseStream.Position;
                var node = new XIsoNode(parent, name, rootOffset + startSector * XIsoSectorSize, fileSize, attrs);
                if (attrs.HasFlag(XIsoAttribute.Directory))
                {
                    ReadDirectoryTable(node, reader, rootOffset);
                }

                reader.BaseStream.Position = pos + ((4 - (pos % 4)) % 4);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (Root != null)
            {
                Close();
            }
        }
        
        ~XIso() { Dispose(false); }
    }
}