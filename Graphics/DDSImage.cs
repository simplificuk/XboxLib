using System;
using System.Drawing;
using System.IO;
using SkiaSharp;

namespace XboxIsoLib
{
    public class DDSImage
    {
        private const int DDPF_ALPHAPIXELS = 0x00000001;
        private const int DDPF_ALPHA = 0x00000002;
        private const int DDPF_FOURCC = 0x00000004;
        private const int DDPF_RGB = 0x00000040;
        private const int DDPF_YUV = 0x00000200;
        private const int DDPF_LUMINANCE = 0x00020000;
        private const int DDSD_MIPMAPCOUNT = 0x00020000;
        private const int FOURCC_DXT1 = 0x31545844;
        private const int FOURCC_DX10 = 0x30315844;
        private const int FOURCC_DXT5 = 0x35545844;

        public int dwMagic;
        private DDS_HEADER header = new DDS_HEADER();
        private DDS_HEADER_DXT10 header10 = null;//If the DDS_PIXELFORMAT dwFlags is set to DDPF_FOURCC and dwFourCC is set to "DX10"
        public byte[] bdata;//pointer to an array of bytes that contains the main surface data. 
        public byte[] bdata2;//pointer to an array of bytes that contains the remaining surfaces such as; mipmap levels, faces in a cube map, depths in a volume texture.

        public SKImage[] images;

        public DDSImage(byte[] rawdata)
        {
            using (MemoryStream ms = new MemoryStream(rawdata))
            {
                using (BinaryReader r = new BinaryReader(ms))
                {
                    dwMagic = r.ReadInt32();
                    if (dwMagic != 0x20534444)
                    {
                        throw new Exception("This is not a DDS!");
                    }

                    Read_DDS_HEADER(header, r);

                    if (((header.ddspf.dwFlags & DDPF_FOURCC) != 0) && (header.ddspf.dwFourCC == FOURCC_DX10 /*DX10*/))
                    {
                        throw new Exception("DX10 not supported yet!");
                    }

                    int mipMapCount = 1;
                    if ((header.dwFlags & DDSD_MIPMAPCOUNT) != 0)
                        mipMapCount = Math.Max(header.dwMipMapCount, 1);
                    images = new SKImage[mipMapCount];

                    var size = 0;
                    for (var i = 0; i < mipMapCount; i++)
                    {
                        int w = header.dwWidth / (2 * i + 1);
                        int h = header.dwHeight / (2 * i + 1);
                        var blocksWide = Math.Max((w + 3) / 4, 1);
                        var blocksHigh = Math.Max((h + 3) / 4, 1);
                        size += blocksWide * blocksHigh * 8;
                    }
                    
                    bdata = r.ReadBytes(size);


                    for (int i = 0; i < mipMapCount; ++i)
                    {
                        int w = header.dwWidth / (2 * i + 1);
                        int h = header.dwHeight / (2 * i + 1);

                        DecodeFormat imageFormat;
                        
                        if ((header.ddspf.dwFlags & DDPF_RGB) != 0)
                        {
                            imageFormat = DecodeFormat.ARGB;
                        }
                        else if ((header.ddspf.dwFlags & DDPF_FOURCC) != 0)
                        {
                            imageFormat = header.ddspf.dwFourCC switch
                            {
                                FOURCC_DXT1 => DecodeFormat.DXT1,
                                FOURCC_DXT5 => DecodeFormat.DXT5,
                                _ => throw new Exception(string.Format("0x{0} texture compression not implemented.",
                                    header.ddspf.dwFourCC.ToString("X")))
                            };
                        }
                        else
                        {
                            throw new Exception("Unsure how to decode DDS in this format");
                        }
                        
                        images[i] = ReadImage(bdata, w, h, imageFormat);
                    }

                }
            }
        }

        public enum DecodeFormat
        {
            ARGB,
            DXT1,
            DXT2,
            DXT5
        }

        public static SKImage ReadImage(byte[] data, int w, int h, DecodeFormat format)
        {
            Color[] pixels = new Color[w * h];

            switch (format)
            {
                case DecodeFormat.ARGB:
                    for (int srcOff = 0, destOff = 0; srcOff < data.Length; srcOff += 4, destOff += 1)
                    {
                        pixels[destOff] = Color.FromArgb(BitConverter.ToInt32(data, srcOff));
                    }

                    break;
                case DecodeFormat.DXT1:
                    UncompressBlocked(data, w, h, pixels, 8, DecompressBlockDXT1);
                    break;
                case DecodeFormat.DXT2:
                    UncompressBlocked(data, w, h, pixels, 16, DecompressBlockDXT2);
                    break;
                case DecodeFormat.DXT5:
                    UncompressBlocked(data, w, h, pixels, 16, DecompressBlockDXT5);
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

        private static void UncompressBlocked(byte[] data, int w, int h, Color[] pixels, int blockSize,
            Action<int, int, int, byte[], int, int, int, Color[]> decompress)
        {
            int blockCountX = (w + 3) / 4;
            int blockCountY = (h + 3) / 4;
            int blockWidth = (w < 4) ? w : 4;
            int blockHeight = (h < 4) ? w : 4;

            int blockOff = 0;
            for (int j = 0; j < blockCountY; j++)
            {
                for (int i = 0; i < blockCountX; i++)
                {
                    decompress(i * 4, j * 4, w, data, blockOff, blockWidth, blockHeight, pixels);
                    blockOff += blockSize;
                }
            }
        }
        
        #region DXT1

        private static void DecompressBlockDXT1Internal(int x, int y, int width, byte[] blockData, int blockOff,
            int blockWidth, int blockHeight, Color[] pixels, byte[] alphaValues)
        {
            ushort color0 = BitConverter.ToUInt16(blockData, blockOff + 0);
            ushort color1 = BitConverter.ToUInt16(blockData, blockOff + 2);

            int temp;

            temp = (color0 >> 11) * 255 + 16;
            byte r0 = (byte)((temp / 32 + temp) / 32);
            temp = ((color0 & 0x07E0) >> 5) * 255 + 32;
            byte g0 = (byte)((temp / 64 + temp) / 64);
            temp = (color0 & 0x001F) * 255 + 16;
            byte b0 = (byte)((temp / 32 + temp) / 32);

            temp = (color1 >> 11) * 255 + 16;
            byte r1 = (byte)((temp / 32 + temp) / 32);
            temp = ((color1 & 0x07E0) >> 5) * 255 + 32;
            byte g1 = (byte)((temp / 64 + temp) / 64);
            temp = (color1 & 0x001F) * 255 + 16;
            byte b1 = (byte)((temp / 32 + temp) / 32);

            uint code = BitConverter.ToUInt32(blockData, blockOff + 4);
            
            for (int by = 0; by < blockHeight; by++)
            {
                for (int bx = 0; bx < blockWidth; bx++)
                {
                    Color finalColor = Color.FromArgb(0);
                    var bidx = 4 * by + bx;
                    byte positionCode = (byte)((code >> 2 * bidx) & 0x03);

                    var alpha = alphaValues[bidx];
                    
                    if (color0 > color1)
                    {
                        switch (positionCode)
                        {
                            case 0:
                                finalColor = Color.FromArgb(alpha, r0, g0, b0);
                                break;
                            case 1:
                                finalColor = Color.FromArgb(alpha, r1, g1, b1);
                                break;
                            case 2:
                                finalColor = Color.FromArgb(alpha, (2 * r0 + r1) / 3, (2 * g0 + g1) / 3, (2 * b0 + b1) / 3);
                                break;
                            case 3:
                                finalColor = Color.FromArgb(alpha, (r0 + 2 * r1) / 3, (g0 + 2 * g1) / 3, (b0 + 2 * b1) / 3);
                                break;
                        }
                    }
                    else
                    {
                        switch (positionCode)
                        {
                            case 0:
                                finalColor = Color.FromArgb(alpha, r0, g0, b0);
                                break;
                            case 1:
                                finalColor = Color.FromArgb(alpha, r1, g1, b1);
                                break;
                            case 2:
                                finalColor = Color.FromArgb(alpha, (r0 + r1) / 2, (g0 + g1) / 2, (b0 + b1) / 2);
                                break;
                            case 3:
                                finalColor = Color.FromArgb(alpha, 0, 0, 0);
                                break;
                        }
                    }

                    if (x + bx < width)
                    {
                        pixels[((y + by) * width + (x + bx))] = finalColor;
                    }
                }
            }
        }

        private static byte[] dxt1Alpha = {
            0xff, 0xff, 0xff, 0xff,
            0xff, 0xff, 0xff, 0xff,
            0xff, 0xff, 0xff, 0xff,
            0xff, 0xff, 0xff, 0xff,
        };
        
        private static void DecompressBlockDXT1(int x, int y, int width, byte[] blockData, int blockOff, int blockWidth, int blockHeight, Color[] pixels)
        {
            DecompressBlockDXT1Internal(x, y ,width, blockData, blockOff, blockWidth, blockHeight, pixels, dxt1Alpha);
        }
        #endregion
        
        #region DXT3
        private static void DecompressBlockDXT2(int x, int y, int width, byte[] blockData, int blockOff, int blockWidth, int blockHeight, Color[] pixels)
        {
            var alphaValues = new byte[16];
            for (var i = 0; i < 4; i++)
            {
                alphaValues [i * 4 + 0] = (byte) ((((blockData[blockOff + 0]) >> 0) & 0xF) * 17);
                alphaValues [i * 4 + 1] = (byte) ((((blockData[blockOff + 0]) >> 4) & 0xF) * 17);
                alphaValues [i * 4 + 2] = (byte) ((((blockData[blockOff + 1]) >> 0) & 0xF) * 17);
                alphaValues [i * 4 + 3] = (byte) ((((blockData[blockOff + 1]) >> 4) & 0xF) * 17);
                blockOff += 2;
            }
            
            DecompressBlockDXT1Internal(x, y, width, blockData, blockOff, blockWidth, blockHeight, pixels, alphaValues);
        }

        #endregion
        
        #region DXT5
        private static void DecompressBlockDXT5(int x, int y, int width, byte[] blockData, int blockOff, int blockWidth, int blockHeight, Color[] pixels)
        {
            byte alpha0 = blockData[blockOff + 0];
            byte alpha1 = blockData[blockOff + 1];

            int bitOffset = blockOff + 2;
            uint alphaCode1 = BitConverter.ToUInt32(blockData, blockOff + 4);
            ushort alphaCode2 = BitConverter.ToUInt16(blockData, blockOff + 2);

            ushort color0 = BitConverter.ToUInt16(blockData, blockOff + 8);
            ushort color1 = BitConverter.ToUInt16(blockData, blockOff + 10);

            int temp;

            temp = (color0 >> 11) * 255 + 16;
            byte r0 = (byte)((temp / 32 + temp) / 32);
            temp = ((color0 & 0x07E0) >> 5) * 255 + 32;
            byte g0 = (byte)((temp / 64 + temp) / 64);
            temp = (color0 & 0x001F) * 255 + 16;
            byte b0 = (byte)((temp / 32 + temp) / 32);

            temp = (color1 >> 11) * 255 + 16;
            byte r1 = (byte)((temp / 32 + temp) / 32);
            temp = ((color1 & 0x07E0) >> 5) * 255 + 32;
            byte g1 = (byte)((temp / 64 + temp) / 64);
            temp = (color1 & 0x001F) * 255 + 16;
            byte b1 = (byte)((temp / 32 + temp) / 32);

            uint code = BitConverter.ToUInt32(blockData, blockOff + 12);

            for (int j = 0; j < 4; j++)
            {
                for (int i = 0; i < 4; i++)
                {
                    int alphaCodeIndex = 3 * (4 * j + i);
                    int alphaCode;

                    if (alphaCodeIndex <= 12)
                    {
                        alphaCode = (alphaCode2 >> alphaCodeIndex) & 0x07;
                    }
                    else if (alphaCodeIndex == 15)
                    {
                        alphaCode = (int)((alphaCode2 >> 15) | ((alphaCode1 << 1) & 0x06));
                    }
                    else
                    {
                        alphaCode = (int)((alphaCode1 >> (alphaCodeIndex - 16)) & 0x07);
                    }

                    byte finalAlpha;
                    if (alphaCode == 0)
                    {
                        finalAlpha = alpha0;
                    }
                    else if (alphaCode == 1)
                    {
                        finalAlpha = alpha1;
                    }
                    else
                    {
                        if (alpha0 > alpha1)
                        {
                            finalAlpha = (byte)(((8 - alphaCode) * alpha0 + (alphaCode - 1) * alpha1) / 7);
                        }
                        else
                        {
                            if (alphaCode == 6)
                                finalAlpha = 0;
                            else if (alphaCode == 7)
                                finalAlpha = 255;
                            else
                                finalAlpha = (byte)(((6 - alphaCode) * alpha0 + (alphaCode - 1) * alpha1) / 5);
                        }
                    }

                    byte colorCode = (byte)((code >> 2 * (4 * j + i)) & 0x03);

                    Color finalColor = new Color();
                    switch (colorCode)
                    {
                        case 0:
                            finalColor = Color.FromArgb(finalAlpha, r0, g0, b0);
                            break;
                        case 1:
                            finalColor = Color.FromArgb(finalAlpha, r1, g1, b1);
                            break;
                        case 2:
                            finalColor = Color.FromArgb(finalAlpha, (2 * r0 + r1) / 3, (2 * g0 + g1) / 3, (2 * b0 + b1) / 3);
                            break;
                        case 3:
                            finalColor = Color.FromArgb(finalAlpha, (r0 + 2 * r1) / 3, (g0 + 2 * g1) / 3, (b0 + 2 * b1) / 3);
                            break;
                    }

                    // if (x + i < width)
                        pixels[(y + j)*width + (x + i)] = finalColor;
                    //pixels[(y + j)*width + (x + i)] = finalColor;
                }
            }
        }
        #endregion

        private void Read_DDS_HEADER(DDS_HEADER h, BinaryReader r)
        {
            h.dwSize = r.ReadInt32();
            h.dwFlags = r.ReadInt32();
            h.dwHeight = r.ReadInt32();
            h.dwWidth = r.ReadInt32();
            h.dwPitchOrLinearSize = r.ReadInt32();
            h.dwDepth = r.ReadInt32();
            h.dwMipMapCount = r.ReadInt32();
            for (int i = 0; i < 11; ++i)
            {
                h.dwReserved1[i] = r.ReadInt32();
            }
            Read_DDS_PIXELFORMAT(h.ddspf, r);
            h.dwCaps = r.ReadInt32();
            h.dwCaps2 = r.ReadInt32();
            h.dwCaps3 = r.ReadInt32();
            h.dwCaps4 = r.ReadInt32();
            h.dwReserved2 = r.ReadInt32();
        }

        private void Read_DDS_PIXELFORMAT(DDS_PIXELFORMAT p, BinaryReader r)
        {
            p.dwSize = r.ReadInt32();
            p.dwFlags = r.ReadInt32();
            p.dwFourCC = r.ReadInt32();
            p.dwRGBBitCount = r.ReadInt32();
            p.dwRBitMask = r.ReadInt32();
            p.dwGBitMask = r.ReadInt32();
            p.dwBBitMask = r.ReadInt32();
            p.dwABitMask = r.ReadInt32();
        }
    }

    class DDS_HEADER
    {
        public int dwSize;
        public int dwFlags;
        /*	DDPF_ALPHAPIXELS   0x00000001 
            DDPF_ALPHA   0x00000002 
            DDPF_FOURCC   0x00000004 
            DDPF_RGB   0x00000040 
            DDPF_YUV   0x00000200 
            DDPF_LUMINANCE   0x00020000 
         */
        public int dwHeight;
        public int dwWidth;
        public int dwPitchOrLinearSize;
        public int dwDepth;
        public int dwMipMapCount;
        public int[] dwReserved1 = new int[11];
        public DDS_PIXELFORMAT ddspf = new DDS_PIXELFORMAT();
        public int dwCaps;
        public int dwCaps2;
        public int dwCaps3;
        public int dwCaps4;
        public int dwReserved2;
    }

    class DDS_HEADER_DXT10
    {
        public DXGI_FORMAT dxgiFormat;
        public D3D10_RESOURCE_DIMENSION resourceDimension;
        public uint miscFlag;
        public uint arraySize;
        public uint reserved;
    }

    class DDS_PIXELFORMAT
    {
        public int dwSize;
        public int dwFlags;
        public int dwFourCC;
        public int dwRGBBitCount;
        public int dwRBitMask;
        public int dwGBitMask;
        public int dwBBitMask;
        public int dwABitMask;

        public DDS_PIXELFORMAT()
        {
        }
    }

    enum DXGI_FORMAT : uint
    {
        DXGI_FORMAT_UNKNOWN = 0,
        DXGI_FORMAT_R32G32B32A32_TYPELESS = 1,
        DXGI_FORMAT_R32G32B32A32_FLOAT = 2,
        DXGI_FORMAT_R32G32B32A32_UINT = 3,
        DXGI_FORMAT_R32G32B32A32_SINT = 4,
        DXGI_FORMAT_R32G32B32_TYPELESS = 5,
        DXGI_FORMAT_R32G32B32_FLOAT = 6,
        DXGI_FORMAT_R32G32B32_UINT = 7,
        DXGI_FORMAT_R32G32B32_SINT = 8,
        DXGI_FORMAT_R16G16B16A16_TYPELESS = 9,
        DXGI_FORMAT_R16G16B16A16_FLOAT = 10,
        DXGI_FORMAT_R16G16B16A16_UNORM = 11,
        DXGI_FORMAT_R16G16B16A16_UINT = 12,
        DXGI_FORMAT_R16G16B16A16_SNORM = 13,
        DXGI_FORMAT_R16G16B16A16_SINT = 14,
        DXGI_FORMAT_R32G32_TYPELESS = 15,
        DXGI_FORMAT_R32G32_FLOAT = 16,
        DXGI_FORMAT_R32G32_UINT = 17,
        DXGI_FORMAT_R32G32_SINT = 18,
        DXGI_FORMAT_R32G8X24_TYPELESS = 19,
        DXGI_FORMAT_D32_FLOAT_S8X24_UINT = 20,
        DXGI_FORMAT_R32_FLOAT_X8X24_TYPELESS = 21,
        DXGI_FORMAT_X32_TYPELESS_G8X24_UINT = 22,
        DXGI_FORMAT_R10G10B10A2_TYPELESS = 23,
        DXGI_FORMAT_R10G10B10A2_UNORM = 24,
        DXGI_FORMAT_R10G10B10A2_UINT = 25,
        DXGI_FORMAT_R11G11B10_FLOAT = 26,
        DXGI_FORMAT_R8G8B8A8_TYPELESS = 27,
        DXGI_FORMAT_R8G8B8A8_UNORM = 28,
        DXGI_FORMAT_R8G8B8A8_UNORM_SRGB = 29,
        DXGI_FORMAT_R8G8B8A8_UINT = 30,
        DXGI_FORMAT_R8G8B8A8_SNORM = 31,
        DXGI_FORMAT_R8G8B8A8_SINT = 32,
        DXGI_FORMAT_R16G16_TYPELESS = 33,
        DXGI_FORMAT_R16G16_FLOAT = 34,
        DXGI_FORMAT_R16G16_UNORM = 35,
        DXGI_FORMAT_R16G16_UINT = 36,
        DXGI_FORMAT_R16G16_SNORM = 37,
        DXGI_FORMAT_R16G16_SINT = 38,
        DXGI_FORMAT_R32_TYPELESS = 39,
        DXGI_FORMAT_D32_FLOAT = 40,
        DXGI_FORMAT_R32_FLOAT = 41,
        DXGI_FORMAT_R32_UINT = 42,
        DXGI_FORMAT_R32_SINT = 43,
        DXGI_FORMAT_R24G8_TYPELESS = 44,
        DXGI_FORMAT_D24_UNORM_S8_UINT = 45,
        DXGI_FORMAT_R24_UNORM_X8_TYPELESS = 46,
        DXGI_FORMAT_X24_TYPELESS_G8_UINT = 47,
        DXGI_FORMAT_R8G8_TYPELESS = 48,
        DXGI_FORMAT_R8G8_UNORM = 49,
        DXGI_FORMAT_R8G8_UINT = 50,
        DXGI_FORMAT_R8G8_SNORM = 51,
        DXGI_FORMAT_R8G8_SINT = 52,
        DXGI_FORMAT_R16_TYPELESS = 53,
        DXGI_FORMAT_R16_FLOAT = 54,
        DXGI_FORMAT_D16_UNORM = 55,
        DXGI_FORMAT_R16_UNORM = 56,
        DXGI_FORMAT_R16_UINT = 57,
        DXGI_FORMAT_R16_SNORM = 58,
        DXGI_FORMAT_R16_SINT = 59,
        DXGI_FORMAT_R8_TYPELESS = 60,
        DXGI_FORMAT_R8_UNORM = 61,
        DXGI_FORMAT_R8_UINT = 62,
        DXGI_FORMAT_R8_SNORM = 63,
        DXGI_FORMAT_R8_SINT = 64,
        DXGI_FORMAT_A8_UNORM = 65,
        DXGI_FORMAT_R1_UNORM = 66,
        DXGI_FORMAT_R9G9B9E5_SHAREDEXP = 67,
        DXGI_FORMAT_R8G8_B8G8_UNORM = 68,
        DXGI_FORMAT_G8R8_G8B8_UNORM = 69,
        DXGI_FORMAT_BC1_TYPELESS = 70,
        DXGI_FORMAT_BC1_UNORM = 71,
        DXGI_FORMAT_BC1_UNORM_SRGB = 72,
        DXGI_FORMAT_BC2_TYPELESS = 73,
        DXGI_FORMAT_BC2_UNORM = 74,
        DXGI_FORMAT_BC2_UNORM_SRGB = 75,
        DXGI_FORMAT_BC3_TYPELESS = 76,
        DXGI_FORMAT_BC3_UNORM = 77,
        DXGI_FORMAT_BC3_UNORM_SRGB = 78,
        DXGI_FORMAT_BC4_TYPELESS = 79,
        DXGI_FORMAT_BC4_UNORM = 80,
        DXGI_FORMAT_BC4_SNORM = 81,
        DXGI_FORMAT_BC5_TYPELESS = 82,
        DXGI_FORMAT_BC5_UNORM = 83,
        DXGI_FORMAT_BC5_SNORM = 84,
        DXGI_FORMAT_B5G6R5_UNORM = 85,
        DXGI_FORMAT_B5G5R5A1_UNORM = 86,
        DXGI_FORMAT_B8G8R8A8_UNORM = 87,
        DXGI_FORMAT_B8G8R8X8_UNORM = 88,
        DXGI_FORMAT_R10G10B10_XR_BIAS_A2_UNORM = 89,
        DXGI_FORMAT_B8G8R8A8_TYPELESS = 90,
        DXGI_FORMAT_B8G8R8A8_UNORM_SRGB = 91,
        DXGI_FORMAT_B8G8R8X8_TYPELESS = 92,
        DXGI_FORMAT_B8G8R8X8_UNORM_SRGB = 93,
        DXGI_FORMAT_BC6H_TYPELESS = 94,
        DXGI_FORMAT_BC6H_UF16 = 95,
        DXGI_FORMAT_BC6H_SF16 = 96,
        DXGI_FORMAT_BC7_TYPELESS = 97,
        DXGI_FORMAT_BC7_UNORM = 98,
        DXGI_FORMAT_BC7_UNORM_SRGB = 99,
        DXGI_FORMAT_AYUV = 100,
        DXGI_FORMAT_Y410 = 101,
        DXGI_FORMAT_Y416 = 102,
        DXGI_FORMAT_NV12 = 103,
        DXGI_FORMAT_P010 = 104,
        DXGI_FORMAT_P016 = 105,
        DXGI_FORMAT_420_OPAQUE = 106,
        DXGI_FORMAT_YUY2 = 107,
        DXGI_FORMAT_Y210 = 108,
        DXGI_FORMAT_Y216 = 109,
        DXGI_FORMAT_NV11 = 110,
        DXGI_FORMAT_AI44 = 111,
        DXGI_FORMAT_IA44 = 112,
        DXGI_FORMAT_P8 = 113,
        DXGI_FORMAT_A8P8 = 114,
        DXGI_FORMAT_B4G4R4A4_UNORM = 115,
        DXGI_FORMAT_FORCE_UINT = 0xffffffff
    }

    enum D3D10_RESOURCE_DIMENSION
    {
        D3D10_RESOURCE_DIMENSION_UNKNOWN = 0,
        D3D10_RESOURCE_DIMENSION_BUFFER = 1,
        D3D10_RESOURCE_DIMENSION_TEXTURE1D = 2,
        D3D10_RESOURCE_DIMENSION_TEXTURE2D = 3,
        D3D10_RESOURCE_DIMENSION_TEXTURE3D = 4
    }

}
