#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using XboxLib.Compression;
using XboxLib.Extensions;
using Aes = System.Security.Cryptography.Aes;
using BinaryReader = XboxLib.IO.BinaryReader;

namespace XboxLib.Xex
{
    [Flags]
    public enum ModuleFlags : uint
    {
        Title = 1 << 1,
        ExportsToTitle = 1 << 2,
        SystemDebugger = 1 << 3,
        DllModule = 1 << 4,
        Patch = 1 << 5,
        PatchFull = 1 << 6,
        PatchDelta = 1 << 7,
        UserMode = 1 << 8
    }
    
    public enum KnownHeaderIds : uint
    {
        ResourceInfo = 0x2ff,
        FileFormat = 0x3ff,
        BaseReference = 0x405,
        DeltaPatchDescriptor = 0x5ff,
        BoundingPath = 0x80ff,
        DeviceId = 0x8105,
        OriginalBaseAddress = 0x10001,
        EntryPoint = 0x10100,
        ImageBaseAddress = 0x10201,
        ImportLibraries = 0x103ff,
        ChecksumTimestamp = 0x18002,
        EnabledForCallcap = 0x18102,
        EnabledForFastcap = 0x18200,
        OriginalPeName = 0x183ff,
        StaticLibraries = 0x200ff,
        TlsInfo = 0x20104,
        DefaultStackSize = 0x20200,
        DefaultFilesystemCacheSize = 0x20301,
        DefaultHeapSize = 0x20401,
        PageHeapSizeAndFlags = 0x28002,
        SystemFlags = 0x30000,
        ExecutionInfo = 0x40006,
        ServiceIdList = 0x401ff,
        TitleWorkspaceSize = 0x40201,
        GameRatings = 0x40310,
        LanKey = 0x40404,
        Xbox360Logo = 0x405ff,
        MultidiscMediaIds = 0x406ff,
        AlternateTitleIds = 0x407ff,
        AdditionalTitleMemory = 0x40801,
        ExportsByName = 0xe10402
    }

    public class Header
    {
        public uint Id { get; set; }
        public uint Address { get; set; }
    }

    public class Resource
    {
        public string Name { get; set; }
        public uint Address { get; set; }
        public uint Size { get; set; }
    }

    public sealed class XexFile : IDisposable
    {
        public static readonly byte[] RetailKey = {
            0x20, 0xB1, 0x85, 0xA5, 0x9D, 0x28, 0xFD, 0xC3,
            0x40, 0x58, 0x3F, 0xBB, 0x08, 0x96, 0xBF, 0x91
        };

        public static readonly byte[] DevkitKey =
        {
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        };

        private Stream _stream;
        public IDictionary<uint, Header> Headers { get; private set; }
        public ModuleFlags Flags { get; set; }
        public uint PeDataAddress { get; set; }
        public uint SecurityAddress { get; set; }
        public bool IsRetailSigned { get; set; }

        public uint BaseAddress => Headers.GetValue((uint) KnownHeaderIds.OriginalBaseAddress)?.Address ?? SecurityInfo.LoadAddress;

        public uint ImageBaseAddress =>
            Headers.GetValue((uint)KnownHeaderIds.ImageBaseAddress)?.Address ?? 0;

        public uint EntryPoint => Headers.GetValue((uint)KnownHeaderIds.EntryPoint)?.Address ?? 0;

        public bool IsExecutable => (Flags & ModuleFlags.Title) != 0;
        public bool IsPatch => (Flags & (ModuleFlags.Patch | ModuleFlags.PatchDelta | ModuleFlags.PatchFull)) != 0;

        public FileFormatInfo? FileFormat
        {
            get
            {
                var header = Headers.GetValue((uint)KnownHeaderIds.FileFormat);
                if (header == null) return null;
                _stream.Position = header.Address;
                return FileFormatInfo.Read(_stream);
            }
        }

        public SecurityInfo SecurityInfo { get; set; }

        public byte[] EncryptionKey
        {
            get
            {
                var aes = GetAesConfig(IsRetailSigned ? RetailKey: DevkitKey);
                return aes.DecryptCbc(SecurityInfo.AesKey, aes.IV, PaddingMode.None);
            }
        }

        public ExecutionInfo? ExecutionInfo
        {
            get
            {
                var header = Headers.GetValue((uint)KnownHeaderIds.ExecutionInfo);
                if (header == null) return null;
                _stream.Position = header.Address;
                return ExecutionInfo.Read(_stream);
            }
        }

        public IDictionary<string, Resource> Resources {
            get {
                var header = Headers.GetValue((uint)KnownHeaderIds.ResourceInfo);
                if (header == null) return new Dictionary<string, Resource>();
                _stream.Position = header.Address;
                using var reader = new BinaryReader(_stream, BinaryReader.Endian.Big, true);
                var numResources = (reader.ReadUInt32() - 4) / 16;
                var res = new Dictionary<string, Resource>((int) numResources);
                for (var i = 0; i < numResources; i++)
                {
                    var name = Encoding.ASCII.GetString(reader.ReadBytes(8));
                    var address = reader.ReadUInt32();
                    var size = reader.ReadUInt32();
                    var r = new Resource() { Name = name, Address = address, Size = size };
                    res.Add(r.Name, r);
                    Console.WriteLine($"\t: {r.Name}: {r.Address:X}-{r.Address+size:X} ({size}b)");
                    
                }

                return res;
            }
        }

        public Aes GetAesConfig(byte[] key)
        {
            var aes = Aes.Create();
            aes.BlockSize = 128;
            aes.KeySize = 128;
            aes.Padding = PaddingMode.None;
            aes.Mode = CipherMode.CBC;
            aes.IV = new byte[16];
            aes.Key = key;
            return aes;
        }

        public byte[] GetPeImage()
        {
            var formatInfo = FileFormat;
            if (formatInfo == null) return Array.Empty<byte>();
            
            _stream.Position = PeDataAddress;
            var dataStream = formatInfo.Encryption switch
            {
                EncryptionType.None => _stream,
                EncryptionType.Normal => new CryptoStream(_stream, GetAesConfig(EncryptionKey).CreateDecryptor(),
                    CryptoStreamMode.Read, true),
                _ => throw new ArgumentOutOfRangeException($"unknown encryption type: {formatInfo.Encryption}")
            };
            using var dataReader = new BinaryReader(dataStream, BinaryReader.Endian.Big, true);

            var compressedDataSize = (int) (_stream.Length - PeDataAddress);
            return formatInfo.Compression.Type switch
            {
                CompressionType.None => dataReader.ReadBytes(compressedDataSize),
                CompressionType.Basic => DecodeBasic(dataReader, formatInfo),
                CompressionType.Normal => DecodeNormal(dataReader, formatInfo, compressedDataSize),
                _ => throw new NotImplementedException($"unsupported compression type: {formatInfo.Compression.Type}")
            };
        }

        private byte[] DecodeBasic(BinaryReader dataReader, FileFormatInfo formatInfo)
        {
            var compressionInfo = (BasicCompressionInfo)formatInfo.Compression;
            var uncompressedSize = compressionInfo.Blocks.Aggregate(0L, (current, block) => current + block.Size);
            var dest = new byte[uncompressedSize];
            var destOff = 0;

            foreach (var block in compressionInfo.Blocks)
            {
                for (var i = 0; i < block.DataSize;)
                {
                    var read = dataReader.Read(dest, destOff + i, (int) block.DataSize - i);
                    if (read == 0) throw new EndOfStreamException();
                    i += read;
                }
                
                destOff += (int) block.Size;
            }

            return dest;
        }
        
        private byte[] DecodeNormal(BinaryReader reader, FileFormatInfo formatInfo, int compressedSize)
        {
            var compressionInfo = (NormalCompressionInfo)formatInfo.Compression;
            
            var deblockStream = new NormalBlockStream(reader.BaseStream, compressionInfo);
            var dest = new MemoryStream();
            var decompressor = new LzxDecompression();
            decompressor.Init(15, 0, compressedSize, compressedSize * 100, false, null);
            try
            {
                decompressor.Decompress(deblockStream, dest, compressedSize * 100);
            }
            catch (EndOfStreamException e)
            {
                // Ignore
            }

            return dest.ToArray();
        }

        private XexFile()
        {
        }

        public static XexFile Read(Stream stream, bool is_retail = true)
        {
            var bin = new BinaryReader(stream, BinaryReader.Endian.Big);
            
            if (!Encoding.ASCII.GetString(bin.ReadBytes(4)).Equals("XEX2"))
                throw new InvalidDataException("Invalid Xex file");

            var flags = (ModuleFlags) bin.ReadUInt32();
            var peDataOffset = bin.ReadUInt32();
            var reserved = bin.ReadUInt32();
            var securityInfoOffset = bin.ReadUInt32();
            var headerCount = bin.ReadUInt32();

            var headers = new Dictionary<uint, Header>((int) headerCount);
            for (var i = 0; i < headerCount; i++)
            {
                var id = bin.ReadUInt32();
                headers.Add(id, new Header(){ Id = id, Address = bin.ReadUInt32()});
            }

            stream.Position = securityInfoOffset;
            var securityInfo = SecurityInfo.Read(bin);

            return new XexFile()
            {
                _stream = stream,
                IsRetailSigned = is_retail,
                Headers = headers,
                Flags = flags,
                SecurityAddress = securityInfoOffset,
                PeDataAddress = peDataOffset,
                SecurityInfo = securityInfo
            };
        }

        public void Close()
        {
            _stream.Close();
        }

        public void Dispose()
        {
            Close();
        }
    }
}