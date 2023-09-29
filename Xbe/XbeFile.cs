using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using SkiaSharp;
using XboxLib.Graphics;

namespace XboxLib.Xbe
{
    [Flags]
    public enum SectionFlag : uint
    {
        Writable = 0x01,
        Preload = 0x02,
        Executable = 0x04,
        InsertedFile = 0x08,
        HeadPageReadOnly = 0x10,
        TailPageReadOnly = 0x20
    }

    public sealed class XbeFile : IDisposable
    {
        private Stream _stream;

        public IDictionary<string, Section> Sections { get; private set; }

        public uint TitleId { get; set; }
        public string TitleName { get; set; }
        public uint BaseAddress { get; set; }
        public uint CertAddress { get; set; }
        public uint SectionAddress { get; set; }

        public MediaFlags MediaFlags { get; set; }
        public GameRegion Region { get; set; }
        public uint DiscNumber { get; set; }
        public GameRating Rating { get; set; }
        public SKImage TitleImage => ReadImage(SectionData("$$XTIMAGE"));
        public SKImage SaveImage => ReadImage(SectionData("$$XSIMAGE"));

        public Dictionary<string, Dictionary<string, string>> TitleInfo
        {
            get
            {
                var xtInfo = SectionData("$$XTINFO");
                if (xtInfo == null)
                    return null;
                
                var decoded = Encoding.Unicode.GetString(xtInfo);

                var data = new Dictionary<string, Dictionary<string, string>>();
                Dictionary<string, string> sectionData = null;
                
                using (var lineEnumerator = decoded.Substring(1).Split('\n').AsEnumerable().GetEnumerator())
                {
                    while(lineEnumerator.MoveNext())
                    {
                        var line = lineEnumerator.Current.Trim();
                        if (line == "") continue;

                        if (line.StartsWith("[") && line.EndsWith("]"))
                        {
                            var section = line.Substring(1, line.Length - 2);
                            sectionData = new Dictionary<string, string>();
                            data[section] = sectionData;
                            continue;
                        } else if (sectionData == null) continue;

                        var parts = line.Split(new char[] { '=' }, 2);
                        sectionData[parts[0]] = parts[1];
                    }
                }

                return data;
            }
        }
        
        public static XbeFile Read(Stream stream)
        {
            var bin = new XboxLib.IO.BinaryReader(stream);
            
            if (!Encoding.ASCII.GetString(bin.ReadBytes(4)).Equals("XBEH"))
                throw new InvalidDataException("Invalid XBE file");

            stream.Seek(0x104, SeekOrigin.Begin);
            var baseAddress = bin.ReadUInt32();
            var headerSize = bin.ReadUInt32();
            var imageSize = bin.ReadUInt32();
            var imageHeaderSize = bin.ReadUInt32();
            var timeDate = bin.ReadUInt32();

            // Get cert & section offsets/counts
            var certAddress = bin.ReadUInt32();
            var numSections = bin.ReadUInt32();
            var sectionAddress = bin.ReadUInt32();
            var initFlags = bin.ReadUInt32();
            var entryPoint = bin.ReadUInt32();

            // Read certificate info
            stream.Seek(certAddress - baseAddress, SeekOrigin.Begin);
            var certSize = bin.ReadUInt32();
            var certTime = bin.ReadUInt32();
            var titleId = bin.ReadUInt32();
            var rawTitleName = bin.ReadBytes(80);
            var titleName = Encoding.Unicode.GetString(rawTitleName).TrimEnd('\0');
            stream.Seek((certAddress - baseAddress) + 0x9c, SeekOrigin.Begin);
            var mediaFlags = (MediaFlags)bin.ReadUInt32();
            var region = (GameRegion)bin.ReadUInt32();
            var rating = (GameRating)bin.ReadUInt32();
            var discNumber = bin.ReadUInt32();
            var version = bin.ReadUInt32();


            var sections = new Dictionary<string, Section>();
            // Read section info
            for (var i = 0; i < numSections; i++)
            {
                stream.Seek(sectionAddress - baseAddress + 56 * i, SeekOrigin.Begin);

                var flags = bin.ReadUInt32();
                var virtualAddress = bin.ReadUInt32();
                var virtualSize = bin.ReadUInt32();
                var dataOff = bin.ReadUInt32();
                var sectionSize = bin.ReadUInt32();
                var nameOff = bin.ReadUInt32();
                stream.Seek(nameOff - baseAddress, SeekOrigin.Begin);
                var sectionName = "";
                byte b;
                while ((b = bin.ReadByte()) != 0)
                    sectionName += (char)b;

                sections[sectionName] = new Section(sectionName, (SectionFlag)flags, virtualAddress, virtualSize, dataOff, sectionSize);
            }

            return new XbeFile
            {
                _stream = stream,
                Sections = sections,
                BaseAddress = baseAddress,
                CertAddress = certAddress,
                DiscNumber = discNumber,
                MediaFlags = mediaFlags,
                Rating = rating,
                Region = region,
                SectionAddress = sectionAddress,
                TitleId = titleId,
                TitleName = titleName,
            };
        }

        public void Close()
        {
            this._stream.Close();
        }

        public void Dispose()
        {
            this.Close();
        }

        public byte[] SectionData(string name)
        {
            Section section = null;
            if(!Sections.TryGetValue(name, out section)) { 
                return null;
            }

            _stream.Position = section.RawAddress;
            var data = new byte[section.RawSize];
            for (var totalRead = 0; totalRead < section.RawSize;)
            {
                var read = _stream.Read(data, totalRead, (int) section.RawSize - totalRead);
                if (read == 0)
                {
                    throw new DataException();
                }

                totalRead += read;
            }

            return data;
        }

        private static SKImage ReadImage(byte[] data)
        {
            if (data == null || data.Length < 4) return null;
            return Encoding.ASCII.GetString(data, 0, 4) switch
            {
                ImageType.XPR => new XprImage(data).AsImage(),
                ImageType.DDS => new DdsImage(data).Images[0],
                _ => SKImage.FromEncodedData(data)
            };
        }

        public sealed class Section
        {
            public SectionFlag Flags { get; }
            public string Name { get; }
            public uint VirtualAddress { get; }
            public uint VirtualSize { get; }
            public uint RawAddress { get;  }
            public uint RawSize { get;  }

            public Section(string name, SectionFlag flags, uint virtualAddress, uint virtualSize, uint address, uint size)
            {
                Name = name;
                Flags = flags;
                VirtualAddress = virtualAddress;
                VirtualSize = virtualSize;
                RawAddress = address;
                RawSize = size;
            }
        }
    }
}