using System;
using System.Drawing;
using System.IO;
using SkiaSharp;

namespace XboxLib.Graphics
{
    public class DdsImage
    {
        private const int DdpfAlphapixels = 0x00000001;
        private const int DdpfAlpha = 0x00000002;
        private const int DdpfFourcc = 0x00000004;
        private const int DdpfRgb = 0x00000040;
        private const int DdpfYuv = 0x00000200;
        private const int DdpfLuminance = 0x00020000;
        private const int DdsdMipmapcount = 0x00020000;
        private const int FourccDxt1 = 0x31545844; // DXT1
        private const int FourccDx10 = 0x30315844; // DX10
        private const int FourccDxt5 = 0x35545844; // DXT5

        private readonly DdsHeader _header = new();

        public readonly SKImage[] Images;

        public DdsImage(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var r = new BinaryReader(ms);
            var dwMagic = r.ReadInt32();
            if (dwMagic != 0x20534444)
            {
                throw new Exception("This is not a DDS!");
            }

            Read_DDS_HEADER(_header, r);

            if ((_header.PixelFormat.DwFlags & DdpfFourcc) != 0 && _header.PixelFormat.DwFourCc == FourccDx10 /*DX10*/)
            {
                throw new Exception("DX10 not supported yet!");
            }

            var mipMapCount = 1;
            if ((_header.DwFlags & DdsdMipmapcount) != 0)
                mipMapCount = Math.Max(_header.DwMipMapCount, 1);
            Images = new SKImage[mipMapCount];

            var size = 0;
            for (var i = 0; i < mipMapCount; i++)
            {
                var w = _header.DwWidth / (2 * i + 1);
                var h = _header.DwHeight / (2 * i + 1);
                var blocksWide = Math.Max((w + 3) / 4, 1);
                var blocksHigh = Math.Max((h + 3) / 4, 1);
                size += blocksWide * blocksHigh * 8;
            }

            for (var i = 0; i < mipMapCount; ++i)
            {
                var w = _header.DwWidth / (2 * i + 1);
                var h = _header.DwHeight / (2 * i + 1);

                DecodeFormat imageFormat;

                if ((_header.PixelFormat.DwFlags & DdpfRgb) != 0)
                {
                    imageFormat = DecodeFormat.Argb;
                }
                else if ((_header.PixelFormat.DwFlags & DdpfFourcc) != 0)
                {
                    imageFormat = _header.PixelFormat.DwFourCc switch
                    {
                        FourccDxt1 => DecodeFormat.Dxt1,
                        FourccDxt5 => DecodeFormat.Dxt5,
                        _ => throw new Exception(
                            $"0x{_header.PixelFormat.DwFourCc:X} texture compression not implemented.")
                    };
                }
                else
                {
                    throw new Exception("Unsure how to decode DDS in this format");
                }

                Images[i] = ReadImage(r.ReadBytes(size), w, h, imageFormat);
            }
        }

        public enum DecodeFormat
        {
            Argb,
            Dxt1,
            Dxt2,
            Dxt5
        }

        public static SKImage ReadImage(byte[] data, int w, int h, DecodeFormat format)
        {
            var pixels = new Color[w * h];

            switch (format)
            {
                case DecodeFormat.Argb:
                    for (int srcOff = 0, destOff = 0; srcOff < data.Length; srcOff += 4, destOff += 1)
                    {
                        pixels[destOff] = Color.FromArgb(BitConverter.ToInt32(data, srcOff));
                    }

                    break;
                case DecodeFormat.Dxt1:
                    DecompressBlocked(data, w, h, pixels, 8, DecompressBlockDxt1);
                    break;
                case DecodeFormat.Dxt2:
                    DecompressBlocked(data, w, h, pixels, 16, DecompressBlockDxt2);
                    break;
                case DecodeFormat.Dxt5:
                    DecompressBlocked(data, w, h, pixels, 16, DecompressBlockDxt5);
                    break;
                default: throw new Exception("Unknown dds format");
            }

            var pixelData = new byte[w * h * 4];
            for (int srcOff = 0, destOff = 0; srcOff < pixels.Length; srcOff++, destOff += 4)
            {
                pixelData[destOff + 0] = pixels[srcOff].B;
                pixelData[destOff + 1] = pixels[srcOff].G;
                pixelData[destOff + 2] = pixels[srcOff].R;
                pixelData[destOff + 3] = pixels[srcOff].A;
            }

            return SKImage.FromPixelCopy(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul), pixelData);
        }

        private static void DecompressBlocked(byte[] data, int w, int h, Color[] pixels, int blockSize,
            Action<int, int, int, byte[], int, int, int, Color[]> decompress)
        {
            var blockCountX = (w + 3) / 4;
            var blockCountY = (h + 3) / 4;
            var blockWidth = (w < 4) ? w : 4;
            var blockHeight = (h < 4) ? w : 4;

            var blockOff = 0;
            for (var j = 0; j < blockCountY; j++)
            {
                for (var i = 0; i < blockCountX; i++)
                {
                    decompress(i * 4, j * 4, w, data, blockOff, blockWidth, blockHeight, pixels);
                    blockOff += blockSize;
                }
            }
        }

        #region DXT1

        private static void DecompressBlockDxt1Internal(int x, int y, int width, byte[] blockData, int blockOff,
            int blockWidth, int blockHeight, Color[] pixels, byte[] alphaValues)
        {
            var color0 = BitConverter.ToUInt16(blockData, blockOff + 0);
            var color1 = BitConverter.ToUInt16(blockData, blockOff + 2);

            var temp = (color0 >> 11) * 255 + 16;
            var r0 = (byte)((temp / 32 + temp) / 32);
            temp = ((color0 & 0x07E0) >> 5) * 255 + 32;
            var g0 = (byte)((temp / 64 + temp) / 64);
            temp = (color0 & 0x001F) * 255 + 16;
            var b0 = (byte)((temp / 32 + temp) / 32);

            temp = (color1 >> 11) * 255 + 16;
            var r1 = (byte)((temp / 32 + temp) / 32);
            temp = ((color1 & 0x07E0) >> 5) * 255 + 32;
            var g1 = (byte)((temp / 64 + temp) / 64);
            temp = (color1 & 0x001F) * 255 + 16;
            var b1 = (byte)((temp / 32 + temp) / 32);

            var code = BitConverter.ToUInt32(blockData, blockOff + 4);

            for (var by = 0; by < blockHeight; by++)
            {
                for (var bx = 0; bx < blockWidth; bx++)
                {
                    if (x + bx >= width) continue;
                    var bidx = 4 * by + bx;

                    var alpha = alphaValues[bidx];
                    pixels[(y + by) * width + x + bx] = ((code >> 2 * bidx) & 0x03) switch
                    {
                        0 => Color.FromArgb(alpha, r0, g0, b0),
                        1 => Color.FromArgb(alpha, r1, g1, b1),
                        2 => color0 > color1
                            ? Color.FromArgb(alpha, (2 * r0 + r1) / 3, (2 * g0 + g1) / 3, (2 * b0 + b1) / 3)
                            : Color.FromArgb(alpha, (r0 + r1) / 2, (g0 + g1) / 2, (b0 + b1) / 2),
                        3 => color0 > color1
                            ? Color.FromArgb(alpha, (r0 + 2 * r1) / 3, (g0 + 2 * g1) / 3, (b0 + 2 * b1) / 3)
                            : Color.FromArgb(alpha, 0, 0, 0),
                        _ => Color.FromArgb(0)
                    };;
                }
            }
        }

        private static readonly byte[] Dxt1Alpha =
        {
            0xff, 0xff, 0xff, 0xff,
            0xff, 0xff, 0xff, 0xff,
            0xff, 0xff, 0xff, 0xff,
            0xff, 0xff, 0xff, 0xff,
        };

        private static void DecompressBlockDxt1(int x, int y, int width, byte[] blockData, int blockOff, int blockWidth,
            int blockHeight, Color[] pixels)
        {
            DecompressBlockDxt1Internal(x, y, width, blockData, blockOff, blockWidth, blockHeight, pixels, Dxt1Alpha);
        }

        #endregion

        #region DXT3

        private static void DecompressBlockDxt2(int x, int y, int width, byte[] blockData, int blockOff, int blockWidth,
            int blockHeight, Color[] pixels)
        {
            var alphaValues = new byte[16];
            for (var i = 0; i < 4; i++)
            {
                alphaValues[i * 4 + 0] = (byte)(((blockData[blockOff + 0] >> 0) & 0xF) * 17);
                alphaValues[i * 4 + 1] = (byte)(((blockData[blockOff + 0] >> 4) & 0xF) * 17);
                alphaValues[i * 4 + 2] = (byte)(((blockData[blockOff + 1] >> 0) & 0xF) * 17);
                alphaValues[i * 4 + 3] = (byte)(((blockData[blockOff + 1] >> 4) & 0xF) * 17);
                blockOff += 2;
            }

            DecompressBlockDxt1Internal(x, y, width, blockData, blockOff, blockWidth, blockHeight, pixels, alphaValues);
        }

        #endregion

        #region DXT5

        private static void DecompressBlockDxt5(int x, int y, int width, byte[] blockData, int blockOff, int blockWidth,
            int blockHeight, Color[] pixels)
        {
            var alpha0 = blockData[blockOff + 0];
            var alpha1 = blockData[blockOff + 1];

            var alphaCode1 = BitConverter.ToUInt32(blockData, blockOff + 4);
            var alphaCode2 = BitConverter.ToUInt16(blockData, blockOff + 2);

            var color0 = BitConverter.ToUInt16(blockData, blockOff + 8);
            var color1 = BitConverter.ToUInt16(blockData, blockOff + 10);

            var temp = (color0 >> 11) * 255 + 16;
            var r0 = (byte)((temp / 32 + temp) / 32);
            temp = ((color0 & 0x07E0) >> 5) * 255 + 32;
            var g0 = (byte)((temp / 64 + temp) / 64);
            temp = (color0 & 0x001F) * 255 + 16;
            var b0 = (byte)((temp / 32 + temp) / 32);

            temp = (color1 >> 11) * 255 + 16;
            var r1 = (byte)((temp / 32 + temp) / 32);
            temp = ((color1 & 0x07E0) >> 5) * 255 + 32;
            var g1 = (byte)((temp / 64 + temp) / 64);
            temp = (color1 & 0x001F) * 255 + 16;
            var b1 = (byte)((temp / 32 + temp) / 32);

            var code = BitConverter.ToUInt32(blockData, blockOff + 12);

            for (var j = 0; j < 4; j++)
            {
                for (var i = 0; i < 4; i++)
                {
                    var alphaCodeIndex = 3 * (4 * j + i);

                    var alphaCode = alphaCodeIndex switch
                    {
                        <= 12 => (alphaCode2 >> alphaCodeIndex) & 0x07,
                        15 => (int)((uint)(alphaCode2 >> 15) | ((alphaCode1 << 1) & 0x06)),
                        _ => (int)((alphaCode1 >> (alphaCodeIndex - 16)) & 0x07)
                    };

                    var finalAlpha = alphaCode switch
                    {
                        0 => alpha0,
                        1 => alpha1,
                        _ => alpha0 > alpha1
                            ? (byte)(((8 - alphaCode) * alpha0 + (alphaCode - 1) * alpha1) / 7)
                            : (alphaCode switch
                            {
                                6 => (byte)0,
                                7 => (byte)255,
                                _ => (byte)(((6 - alphaCode) * alpha0 + (alphaCode - 1) * alpha1) / 5)
                            })
                    };

                    pixels[(y + j) * width + x + i] = (byte)((code >> 2 * (4 * j + i)) & 0x03) switch
                    {
                        0 => Color.FromArgb(finalAlpha, r0, g0, b0),
                        1 => Color.FromArgb(finalAlpha, r1, g1, b1),
                        2 => Color.FromArgb(finalAlpha, (2 * r0 + r1) / 3, (2 * g0 + g1) / 3, (2 * b0 + b1) / 3),
                        3 => Color.FromArgb(finalAlpha, (r0 + 2 * r1) / 3, (g0 + 2 * g1) / 3, (b0 + 2 * b1) / 3),
                        _ => new Color()
                    };
                }
            }
        }

        #endregion

        private void Read_DDS_HEADER(DdsHeader h, BinaryReader r)
        {
            r.ReadInt32();
            h.DwFlags = r.ReadInt32();
            h.DwHeight = r.ReadInt32();
            h.DwWidth = r.ReadInt32();
            h.DwPitchOrLinearSize = r.ReadInt32();
            h.DwDepth = r.ReadInt32();
            h.DwMipMapCount = r.ReadInt32();
            for (var i = 0; i < 11; ++i)
            {
                h.DwReserved1[i] = r.ReadInt32();
            }

            Read_DDS_PIXELFORMAT(h.PixelFormat, r);
            h.DwCaps = r.ReadInt32();
            h.DwCaps2 = r.ReadInt32();
            h.DwCaps3 = r.ReadInt32();
            h.DwCaps4 = r.ReadInt32();
            h.DwReserved2 = r.ReadInt32();
        }

        private void Read_DDS_PIXELFORMAT(DdsPixelFormat p, BinaryReader r)
        {
            p.DwSize = r.ReadInt32();
            p.DwFlags = r.ReadInt32();
            p.DwFourCc = r.ReadInt32();
            p.DwRgbBitCount = r.ReadInt32();
            p.DwRBitMask = r.ReadInt32();
            p.DwGBitMask = r.ReadInt32();
            p.DwBBitMask = r.ReadInt32();
            p.DwABitMask = r.ReadInt32();
        }
    }

    class DdsHeader
    {
        public int DwFlags;

        /*	DDPF_ALPHAPIXELS   0x00000001
            DDPF_ALPHA   0x00000002
            DDPF_FOURCC   0x00000004
            DDPF_RGB   0x00000040
            DDPF_YUV   0x00000200
            DDPF_LUMINANCE   0x00020000
         */
        public int DwHeight;
        public int DwWidth;
        public int DwPitchOrLinearSize;
        public int DwDepth;
        public int DwMipMapCount;
        public readonly int[] DwReserved1 = new int[11];
        public readonly DdsPixelFormat PixelFormat = new();
        public int DwCaps;
        public int DwCaps2;
        public int DwCaps3;
        public int DwCaps4;
        public int DwReserved2;
    }

    class DdsPixelFormat
    {
        public int DwSize;
        public int DwFlags;
        public int DwFourCc;
        public int DwRgbBitCount;
        public int DwRBitMask;
        public int DwGBitMask;
        public int DwBBitMask;
        public int DwABitMask;
    }
}