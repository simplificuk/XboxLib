using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace XboxIsoLib
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

    public sealed class XbeFile: IDisposable
    {
        private Stream _stream;
        public IDictionary<string, Section> Sections { get;  }

        public uint TitleId { get; }
        public string TitleName { get; }

        public string FolderName
        {
            get
            {
                var a = TitleName.Replace("'", "");
                var b = Regex.Replace(a, "[^a-zA-Z0-9 \\-]+", " ");
                var c = Regex.Replace(b, "\\s+", " ");
                var d1 = Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(c.Trim());
                return d1.Substring(0, Math.Min(d1.Length, 42));
            }
        }
        public uint BaseAddress { get; }

        public MediaFlags MediaFlags { get; }
        public GameRegion Region { get; }
        public uint DiscNumber { get; }
        public GameRating Rating { get; }
        public byte[] TitleImage { get { return SectionData("$$XTIMAGE"); } }
        public byte[] SaveImage { get { return SectionData("$$XSIMAGE"); } }

        public Dictionary<string, Dictionary<string, string>> SaveInfo
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

        public XbeFile(Stream stream)
        {
            _stream = stream;
            
            var bin = new BinaryReader(stream);
            
            if (!Encoding.ASCII.GetString(bin.ReadBytes(4)).Equals("XBEH"))
                throw new InvalidDataException("Invalid XBE file");

            stream.Seek(0x104, SeekOrigin.Begin);
            var baseAddr = bin.ReadUInt32();

            // Get cert & section offsets/counts
            stream.Seek(0x118, SeekOrigin.Begin);
            var certoff = bin.ReadUInt16();
            bin.ReadUInt16();
            var numSections = bin.ReadUInt32();
            var sectionOff = bin.ReadUInt32();

            // Read certificate info
            stream.Seek(certoff, SeekOrigin.Begin);
            var certSize = bin.ReadUInt32();
            var certTime = bin.ReadUInt32();
            TitleId = bin.ReadUInt32();
            var rawTitleName = bin.ReadBytes(80);
            TitleName = Encoding.Unicode.GetString(rawTitleName).TrimEnd('\0');
            stream.Seek(certoff + 0x9c, SeekOrigin.Begin);
            MediaFlags = (MediaFlags)bin.ReadUInt32();
            Region = (GameRegion)bin.ReadUInt32();
            Rating = (GameRating)bin.ReadUInt32();
            DiscNumber = bin.ReadUInt32();
            var version = bin.ReadUInt32();


            var sections = new Dictionary<string, Section>();
            // Read section info
            for (var i = 0; i < numSections; i++)
            {
                stream.Seek((sectionOff - baseAddr) + (56 * i), SeekOrigin.Begin);

                var flags = bin.ReadUInt32();
                stream.Seek(0x8, SeekOrigin.Current);
                var dataOff = bin.ReadUInt32();
                var sectionSize = bin.ReadUInt32();
                var nameOff = bin.ReadUInt32();
                stream.Seek(nameOff - baseAddr, SeekOrigin.Begin);
                var sectionName = "";
                byte b;
                while ((b = bin.ReadByte()) != 0)
                    sectionName += (char)b;

                sections[sectionName] = new Section(sectionName, (SectionFlag)flags, dataOff, sectionSize);
            }

            Sections = new Dictionary<string, Section>(sections);
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

            _stream.Position = section.RawAddress - BaseAddress;
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

        // private static Image ReadImage(Stream stream, int offset, int size)
        // {
        //     stream.Seek(offset, SeekOrigin.Begin);
        //     var xtimage = new byte[size];
        //     var off = 0;
        //     while (off < xtimage.Length)
        //         off += stream.Read(xtimage, off, xtimage.Length - off);
        //
        //     var magic = Encoding.ASCII.GetString(xtimage, 0, 4);
        //
        //     if (magic == "XPR0")
        //     {
        //         var xpr = new XPR(xtimage);
        //         var dds = xpr.ConvertToDDS(64, 64);
        //         return dds.GetImage(dds.Type);
        //     } else if(magic == "DDS ")
        //     {
        //         var pfimage = Pfimage.FromStream(new MemoryStream(xtimage));
        //         System.Drawing.Imaging.PixelFormat format;
        //
        //         // Convert from Pfim's backend agnostic image format into GDI+'s image format
        //         switch (pfimage.Format)
        //         {
        //             case Pfim.ImageFormat.Rgba32:
        //                 format = System.Drawing.Imaging.PixelFormat.Format32bppArgb;
        //                 break;
        //             default:
        //                 // see the sample for more details
        //                 throw new NotImplementedException();
        //         }
        //
        //         // Pin pfim's data array so that it doesn't get reaped by GC, unnecessary
        //         // in this snippet but useful technique if the data was going to be used in
        //         // control like a picture box
        //         var handle = GCHandle.Alloc(pfimage.Data, GCHandleType.Pinned);
        //         try
        //         {
        //             var data = Marshal.UnsafeAddrOfPinnedArrayElement(pfimage.Data, 0);
        //             var bitmap = new Bitmap(pfimage.Width, pfimage.Height, pfimage.Stride, format, data);
        //             return bitmap;
        //         }
        //         finally
        //         {
        //             handle.Free();
        //         }
        //     }
        //
        //     return Bitmap.FromStream(new MemoryStream(xtimage));
        // }

        // private static ImageSource ImgToSource(Image image)
        // {
        //
        //     BitmapImage img = new BitmapImage();
        //
        //     using(MemoryStream stream = new MemoryStream())
        //     {
        //         image.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        //
        //         stream.Seek(0, SeekOrigin.Begin);
        //
        //         img.BeginInit();
        //         img.StreamSource = stream;
        //         img.CacheOption = BitmapCacheOption.OnLoad;
        //         img.EndInit();
        //     }
        //
        //     return img;
        // }

        public sealed class Section
        {
            public SectionFlag Flags { get; }
            public string Name { get; }
            public uint RawAddress { get;  }
            public uint RawSize { get;  }

            public Section(string name, SectionFlag flags, uint address, uint size)
            {
                Name = name;
                Flags = flags;
                RawAddress = address;
                RawSize = size;
            }
        }
    }
}