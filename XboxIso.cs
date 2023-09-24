using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace XboxIsoLib
{
    public sealed class XboxIso : IEnumerable<XIsoNode>
    {
        const long XISO_SECTOR_SIZE = 2048;

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
            var reader = new BinaryReader(input);
            
            // Find out offset from ISO type (original xbox, GDF, XGD3)
            var headerOff = 32 * XISO_SECTOR_SIZE;
            var rootOffset = -1L;
            foreach (var off in new uint[] { 0, 0xfd90000, 0x2080000 })
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

            var xi = new XboxIso
            {
                Stream = input,
                Root = new XIsoNode(null, "", rootOffset + dirTableSector * XISO_SECTOR_SIZE, dirTableSize, XIsoAttributes.Directory)
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

                var pos = reader.BaseStream.Position;

                // Todo: this sometimes misses some entries?

                var node = new XIsoNode(parent, name, rootOffset + start_sector * XISO_SECTOR_SIZE, file_size, attrs);

                if (attrs.HasFlag(XIsoAttributes.Directory))
                {
                    ReadDirectoryTable(node, reader, rootOffset);
                }

                reader.BaseStream.Position = pos + ((4 - (pos % 4)) % 4);
            }
        }
    }
}