using System;
using System.IO;
using System.Linq;
using System.Text;
using XboxLib.IO;
using XboxLib.Xex;
using BinaryReader = XboxLib.IO.BinaryReader;

namespace XboxLib.Stfs;

public enum ContentVolumeType : uint
{
    Stfs = 0,
    Svod = 1
}

public struct XContentLicense
{
    public ulong LicenseeId;
    public uint LicenseBits;
    public uint LicenseFlags;

    public static XContentLicense Read(BinaryReader reader)
    {
        return new XContentLicense()
        {
            LicenseeId = reader.ReadUInt64(),
            LicenseBits = reader.ReadUInt32(),
            LicenseFlags = reader.ReadUInt32()
        };
    }
}

public struct XContentHeader
{
    public uint Header;
    public byte[] Signature;
    public XContentLicense[] Licenses;
    public byte[] ContentId;
    public uint HeaderSize;

    public static XContentHeader Read(BinaryReader reader)
    {
        return new XContentHeader()
        {
            Header = reader.ReadUInt32(),
            Signature = reader.ReadBytes(552),
            Licenses = Enumerable.Range(0, 16).Select(_ => XContentLicense.Read(reader)).ToArray(),
            ContentId = reader.ReadBytes(20),
            HeaderSize = reader.ReadUInt32()
        };
    }
}

public struct StfsVolumeDescriptor
{
    [Flags]
    public enum Flag : byte
    {
        ReadOnly = 1 << 7,
        RootActiveIndex = 1 << 6,
        DirectoryOverAllocated = 1 << 5,
        DirectoryIndexBoundsValid = 1 << 4
    }

    public byte Version;
    public Flag Flags;
    public ushort FileTableBlockCount;
    public int FileTableBlockNumber;
    public byte[] TopHashTableHash;
    public uint TotalBlockCount;
    public uint FreeBlockCount;

    public static StfsVolumeDescriptor Read(BinaryReader reader)
    {
        var len = reader.ReadByte();
        if (len != 36) throw new IOException("invalid/unsupported STFS volume descriptor size");

        return new StfsVolumeDescriptor()
        {
            Version = reader.ReadByte(),
            Flags = (Flag)reader.ReadByte(),
            FileTableBlockCount = reader.Inverted.ReadUInt16(),
            FileTableBlockNumber = reader.Inverted.ReadUInt24(),
            TopHashTableHash = reader.ReadBytes(20),
            TotalBlockCount = reader.ReadUInt32(),
            FreeBlockCount = reader.ReadUInt32()
        };
    }
}

public struct XContentMediaData
{
    public byte[] SeriesId;
    public byte[] SeasonId;
    public ushort SeasonNumber;
    public ushort EpisodeNumber;
};

public struct XContentAvatarAssetData
{
    public uint SubCategory;
    public uint Colorable;
    public byte[] AssetId;
    public byte SkeletonVersionMask;
    public byte[] Reserved;
}

[Flags]
public enum XContentAttribute : byte
{
    ProfileTransfer = 1 << 7,
    DeviceTransfer = 1 << 6,
    MoveOnlyTransfer = 1 << 5,
    KinectEnabled = 1 << 4,
    DisableNetworkStorage = 1 << 3,
    DeepLinkSupported = 1 << 2
}

public struct XContentMetadata
{
    public const uint ThumbnailLengthV1 = 0x4000;
    public const uint ThumbnailLengthV2 = 0x3D00;
    public const int NumLanguagesV1 = 9;
    public const int NumLanguagesV2 = 12;

    public XContentType Type;
    public uint Version;
    public ulong ContentSize;
    public ExecutionInfo ExecutionInfo;
    public byte[] ConsoleId;
    public ulong ProfileId;
    public uint DataFileCount;
    public ulong DataFileSize;
    public ContentVolumeType VolumeType;
    public ulong OnlineCreator;
    public uint Category;
    public byte[] Reserved;
    public byte[] DeviceId;
    public string[] DisplayName;
    public string[] Description;
    public string Publisher;
    public string TitleName;
    public XContentAttribute Attribute;
    public byte[] Thumbnail;
    public byte[] TitleThumbnail;


    // These types overlap...
    //public XContentMediaData MediaData;

    //public XContentAvatarAssetData AvatarAssetData;

    // This will be either Stfs or SVOD
    public StfsVolumeDescriptor StfsDescriptor;

    public static XContentMetadata Read(BinaryReader reader)
    {
        var metadata = new XContentMetadata
        {
            Type = (XContentType)reader.ReadUInt32(),
            Version = reader.ReadUInt32(),
            ContentSize = reader.ReadUInt64(),
            ExecutionInfo = ExecutionInfo.Read(reader),
            ConsoleId = reader.ReadBytes(5),
            ProfileId = reader.ReadUInt64()
        };
        var descriptorBytes = reader.ReadBytes(36);
        metadata.DataFileCount = reader.ReadUInt32();
        metadata.DataFileSize = reader.ReadUInt64();
        metadata.VolumeType = (ContentVolumeType)reader.ReadUInt32();

        metadata.StfsDescriptor = metadata.VolumeType switch
        {
            ContentVolumeType.Stfs => StfsVolumeDescriptor.Read(new BinaryReader(new MemoryStream(descriptorBytes),
                Endianness.Big)),
            ContentVolumeType.Svod => throw new NotImplementedException(),
            _ => throw new NotImplementedException(),
        };

        metadata.OnlineCreator = reader.ReadUInt64();
        metadata.Category = reader.ReadUInt32();
        metadata.Reserved = reader.ReadBytes(32);

        var metadataV2 = reader.ReadBytes(36);

        metadata.DeviceId = reader.ReadBytes(20);

        metadata.DisplayName = Enumerable.Range(0, NumLanguagesV1)
            .Select(_ => Encoding.BigEndianUnicode.GetString(reader.ReadBytes(256)).TrimEnd('\0')).ToArray();
        metadata.Description = Enumerable.Range(0, NumLanguagesV1)
            .Select(_ => Encoding.BigEndianUnicode.GetString(reader.ReadBytes(256)).TrimEnd('\0')).ToArray();
        metadata.Publisher = Encoding.BigEndianUnicode.GetString(reader.ReadBytes(128)).TrimEnd('\0');
        metadata.TitleName = Encoding.BigEndianUnicode.GetString(reader.ReadBytes(128)).TrimEnd('\0');
        metadata.Attribute = (XContentAttribute)reader.ReadByte();
        var thumbnailSize = reader.ReadUInt32();
        var titleThumbnailSize = reader.ReadUInt32();
        metadata.Thumbnail = reader.ReadBytes((int)thumbnailSize);
        reader.BaseStream.Position += (metadata.Version == 1 ? ThumbnailLengthV1 : ThumbnailLengthV2) - thumbnailSize;
        // Extended display names
        if (metadata.Version >= 2)
        {
            metadata.DisplayName = metadata.DisplayName.Concat(Enumerable.Range(0, NumLanguagesV2 - NumLanguagesV1)
                .Select(_ => Encoding.BigEndianUnicode.GetString(reader.ReadBytes(256)).TrimEnd('\0'))).ToArray();
        }

        metadata.TitleThumbnail = reader.ReadBytes((int)titleThumbnailSize);
        reader.BaseStream.Position +=
            (metadata.Version == 1 ? ThumbnailLengthV1 : ThumbnailLengthV2) - titleThumbnailSize;
        if (metadata.Version >= 2)
        {
            metadata.Description = metadata.Description.Concat(Enumerable.Range(0, NumLanguagesV2 - NumLanguagesV1)
                .Select(_ => Encoding.BigEndianUnicode.GetString(reader.ReadBytes(256)).TrimEnd('\0'))).ToArray();
        }

        return metadata;
    }
}

public struct StfsHeader
{
    public XContentHeader Header;
    public XContentMetadata Metadata;

    public static StfsHeader Read(BinaryReader reader)
    {
        return new StfsHeader()
        {
            Header = XContentHeader.Read(reader),
            Metadata = XContentMetadata.Read(reader)
        };
    }
}

public struct StfsFile
{
    public const uint ConPackage = 0x434F4E20;
    public const uint PirsPackage = 0x50495253;
    public const uint LivePackage = 0x4C495645;

    public StfsHeader Header;

    public static StfsFile Read(BinaryReader reader)
    {
        return new StfsFile
        {
            Header = StfsHeader.Read(reader)
        };
    }
}