#nullable enable
using System;
using System.IO;
using SkiaSharp;

namespace XboxLib.Graphics
{
    public sealed class XprImage
    {
        private XprHeader _header;
        private byte[] _image;

        public int Width => (int)Math.Pow(2.0, _header.TextureRes2);
        public int Height => (int)Math.Pow(2.0, _header.TextureRes2);
        
        public XprFormat Format => (XprFormat)_header.TextureFormat;

        public XprImage(byte[] data)
        {
            using var ms = new MemoryStream(data);
            var br = new BinaryReader(ms);
            _header = new XprHeader(br);
            ms.Position = _header.HeaderSize;
            var count = (int)(_header.FileSize - _header.HeaderSize);
            _image = new byte[count];
            _image = br.ReadBytes(count);
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
                    var yMask = (uint)(y % width);
                    for (var bit = 0; bit < 16; bit++)
                        sy |= ((yMask >> bit) & 1) << (2 * bit);
                    sy <<= 1; // y counts twice
                    sy += (uint)((y / width) * width * width);
                }
                var d = y * width * depth;
                for (uint x = 0; x < width; x++)
                {
                    uint sx = 0;
                    if (x < height * 2)
                    {
                        for (var bit = 0; bit < 16; bit++)
                            sx |= ((x >> bit) & 1) << (2 * bit);
                    }
                    else
                    {
                        var xMask = (uint)(x % (2 * height));
                        for (var bit = 0; bit < 16; bit++)
                            sx |= ((xMask >> bit) & 1) << (2 * bit);
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
                case XprFormat.X_D3DFMT_A8R8G8B8:
                {
                    var data = new byte[_image.Length];
                    Unswizzle(_image, data, Width, Height, 4);
                    return DdsImage.ReadImage(data, Width, Height, DdsImage.DecodeFormat.Argb);
                }

                case XprFormat.X_D3DFMT_DXT1:
                    return DdsImage.ReadImage(_image, Width, Height, DdsImage.DecodeFormat.Dxt1);
                
                case XprFormat.X_D3DFMT_DXT2:
                    return DdsImage.ReadImage(_image, Width, Height, DdsImage.DecodeFormat.Dxt2);

                case XprFormat.UNKNOWN_ARGB:
                {
                    var data = new byte[_image.Length];
                    Unswizzle(_image, data, Width, Height, 4);
                    for (var i = 0; i < data.Length; i += 4)
                    {
                        var tmp = data[i + 0];
                        data[i + 0] = data[i + 1];
                        data[i + 1] = data[i + 2];
                        data[i + 2] = data[i + 3];
                        data[i + 3] = tmp;
                    }

                    return DdsImage.ReadImage(data, Width, Height, DdsImage.DecodeFormat.Argb);
                }
                case XprFormat.X_D3DFMT_X8R8G8B8:
                {
                    var data = new byte[_image.Length];
                    Unswizzle(_image, data, Width, Height, 4);
                    for (var i = 0; i < data.Length; i += 4)
                    {
                        data[i + 3] = 255;
                    }
                    return DdsImage.ReadImage(data, Width, Height, DdsImage.DecodeFormat.Argb);
                }
                default:
                    throw new Exception("Unhandled XPR format");
            }
        }
    }

}