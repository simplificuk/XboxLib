using SkiaSharp;
using XboxIsoLib.Graphics;

namespace XboxIsoLib
{
    using System;
    using System.IO;

    public sealed class XPR
    {
        public XPRHeader Header;
        public byte[] Image;

        public XPR()
        {
        }

        public XPR(byte[] data)
        {
            var ms = new MemoryStream(data);
            init(new BinaryReader(ms), ms);
        }

        private void Unswizzle(byte[] src, byte[] dest, int width, int height, int depth)
        {
            for (uint y = 0; y < height; y++)
            {
                uint sy = 0;
                if (y < width)
                {
                    for (var bit = 0; bit < 16; bit++)
                        sy |= ((y >> bit) & 1) << (2 * bit);
                    sy <<= 1; // y counts twice
                }
                else
                {
                    uint y_mask = (uint)(y % width);
                    for (int bit = 0; bit < 16; bit++)
                        sy |= ((y_mask >> bit) & 1) << (2 * bit);
                    sy <<= 1; // y counts twice
                    sy += (uint)((y / width) * width * width);
                }
                var d = y * width * depth;
                for (uint x = 0; x < width; x++)
                {
                    uint sx = 0;
                    if (x < height * 2)
                    {
                        for (int bit = 0; bit < 16; bit++)
                            sx |= ((x >> bit) & 1) << (2 * bit);
                    }
                    else
                    {
                        uint x_mask = (uint)(x % (2 * height));
                        for (int bit = 0; bit < 16; bit++)
                            sx |= ((x_mask >> bit) & 1) << (2 * bit);
                        sx += (uint)((x / (2 * height)) * 2 * height * height);
                    }
                    var s = (sx + sy) * depth;
                    for (uint i = 0; i < depth; ++i)
                        dest[d++] = src[s++];
                }
            }
        }

        public SKImage AsImage()
        {
            switch (Format)
            {
                case XPRFormat.X_D3DFMT_A8R8G8B8:
                {
                    var data = new byte[Image.Length];
                    Unswizzle(Image, data, Width, Height, 4);
                    // SKSwizzle.SwapRedBlue(data, Image, Image.Length);
                    return DDSImage.ReadImage(data, Width, Height, DDSImage.DecodeFormat.ARGB);
                }

                case XPRFormat.X_D3DFMT_DXT1:
                    return DDSImage.ReadImage(Image, Width, Height, DDSImage.DecodeFormat.DXT1);
                
                case XPRFormat.X_D3DFMT_DXT2:
                    return DDSImage.ReadImage(Image, Width, Height, DDSImage.DecodeFormat.DXT2);

                case XPRFormat.UNKNOWN_ARGB:
                {
                    var data = new byte[Image.Length];
                    Unswizzle(Image, data, Width, Height, 4);
                    for (var i = 0; i < data.Length; i += 4)
                    {
                        var tmp = data[i + 0];
                        data[i + 0] = data[i + 1];
                        data[i + 1] = data[i + 2];
                        data[i + 2] = data[i + 3];
                        data[i + 3] = tmp;
                    }

                    // SKSwizzle.SwapRedBlue(data, Image, Image.Length);
                    return DDSImage.ReadImage(data, Width, Height, DDSImage.DecodeFormat.ARGB);
                }
                case XPRFormat.X_D3DFMT_X8R8G8B8:
                {
                    var data = new byte[Image.Length];
                    Unswizzle(Image, data, Width, Height, 4);
                    for (var i = 0; i < data.Length; i += 4)
                    {
                        data[i + 3] = 255;
                    }
                    return DDSImage.ReadImage(data, Width, Height, DDSImage.DecodeFormat.ARGB);
                }
                default:
                    throw new Exception("Unhandled XPR format");
            }
        }

        private void init(BinaryReader br, MemoryStream ms)
        {
            ms.Seek(0L, SeekOrigin.Begin);
            Header = new XPRHeader(br);
            readImageData(br, ms);
        }

        private void readImageData(BinaryReader br, MemoryStream ms)
        {
            ms.Seek(Header.HeaderSize, SeekOrigin.Begin);
            var count = (int)(Header.FileSize - Header.HeaderSize);
            Image = new byte[count];
            Image = br.ReadBytes(count);
        }

        public XPRFormat Format
        {
            get
            {
                if (Header == null)
                {
                    return 0;
                }
                return (XPRFormat)Header.TextureFormat;
            }
        }

        public int Height
        {
            get
            {
                if (Header == null)
                {
                    return -1;
                }
                return (int)Math.Pow(2.0, Header.TextureRes2);
            }
        }

        public int Width
        {
            get
            {
                if (Header == null)
                {
                    return -1;
                }
                return (int)Math.Pow(2.0, Header.TextureRes2);
            }
        }
    }

}