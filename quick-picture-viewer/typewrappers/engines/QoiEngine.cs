using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace quick_picture_viewer
{
    public static class QoiEngine
    {
        private static int QOI_MAGIC_BYTES = 0x716f6966;
        private static int QOI_HEADER_SIZE = 14;
        private static byte[] END_MARKER = { 0, 0, 0, 0, 0, 0, 0, 1 };
        private static int QOI_END_MARKER_SIZE = END_MARKER.Length;
        private const int QOI_OP_INDEX = 0x00;
        private const int QOI_OP_DIFF = 0x40;
        private const int QOI_OP_LUMA = 0x80;
        private const int QOI_OP_RUN = 0xc0;
        private const int QOI_OP_RGB = 0xfe;
        private const int QOI_OP_RGBA = 0xff;
        private const int QOI_MASK_2 = 0xc0;
        private const int QOI_RUN_LENGTH = 0x3f;
        private const int QOI_INDEX = 0x3f;
        private const int QOI_DIFF_RED = 0x30;
        private const int QOI_DIFF_GREEN = 0x0c;
        private const int QOI_DIFF_BLUE = 0x03;
        private const int QOI_LUMA_GREEN = 0x3f;
        private const int QOI_LUMA_DRDG = 0xf0;
        private const int QOI_LUMA_DBDG = 0x0f;

        private static void Write32(byte[] buffer, ref int index, int value)
        {
            buffer[index++] = (byte)((value & 0xff000000) >> 24);
            buffer[index++] = (byte)((value & 0x00ff0000) >> 16);
            buffer[index++] = (byte)((value & 0x0000ff00) >> 8);
            buffer[index++] = (byte)((value & 0x000000ff) >> 0);
        }

        private static int Read32(byte[] buffer, ref int index)
        {
            return buffer[index++] << 24 | buffer[index++] << 16 | buffer[index++] << 8 | buffer[index++] << 0;
        }

        private static void WriteColor(byte[] buffer, ref int index, Color color)
        {
            buffer[index++] = (byte)color.b;
            buffer[index++] = (byte)color.g;
            buffer[index++] = (byte)color.r;
            buffer[index++] = (byte)color.a;
        }

        private static void InsertIntoSeenColors(Color[] seenColors, Color color)
        {
            seenColors[color.GetHashCode() % 64] = color;
        }

        public static Bitmap Decode(byte[] rawQoi)
        {
            Color previousColor = new Color()
            {
                r = 0,
                g = 0,
                b = 0,
                a = 255
            };

            Color[] seenColors = new Color[64];

            for (int i = 0; i < seenColors.Length; i++)
            {
                seenColors[i] = new Color();
            }

            int readIndex = 0;
            int writeIndex = 0;

            if (rawQoi.Length < QOI_HEADER_SIZE + QOI_END_MARKER_SIZE)
            {
                throw new Exception();
            }

            if (Read32(rawQoi, ref readIndex) != QOI_MAGIC_BYTES)
            {
                throw new Exception();
            }

            int width = Read32(rawQoi, ref readIndex);
            int height = Read32(rawQoi, ref readIndex);
            byte channels = rawQoi[readIndex++];
            byte colorSpace = rawQoi[readIndex++];

            int pixelBufferSize = width * height * 4;

            byte[] bytes = new byte[pixelBufferSize];

            while (readIndex < rawQoi.Length - QOI_END_MARKER_SIZE)
            {
                byte currentByte = rawQoi[readIndex++];

                if (currentByte == QOI_OP_RGB)
                {
                    previousColor.r = rawQoi[readIndex++];
                    previousColor.g = rawQoi[readIndex++];
                    previousColor.b = rawQoi[readIndex++];

                    WriteColor(bytes, ref writeIndex, previousColor);
                    InsertIntoSeenColors(seenColors, previousColor);

                    continue;
                }

                if (currentByte == QOI_OP_RGBA)
                {
                    previousColor.r = rawQoi[readIndex++];
                    previousColor.g = rawQoi[readIndex++];
                    previousColor.b = rawQoi[readIndex++];
                    previousColor.a = rawQoi[readIndex++];

                    WriteColor(bytes, ref writeIndex, previousColor);
                    InsertIntoSeenColors(seenColors, previousColor);

                    continue;
                }

                if ((currentByte & QOI_MASK_2) == QOI_OP_RUN)
                {
                    int runLength = currentByte & QOI_RUN_LENGTH;

                    for (int i = 0; i < runLength + 1; i++)
                    {
                        WriteColor(bytes, ref writeIndex, previousColor);
                    }

                    continue;
                }

                if ((currentByte & QOI_MASK_2) == QOI_OP_INDEX)
                {
                    int index = currentByte & QOI_INDEX;

                    Color color = seenColors[index];
                    WriteColor(bytes, ref writeIndex, color);
                    previousColor = color;
                    continue;
                }

                if ((currentByte & QOI_MASK_2) == QOI_OP_DIFF)
                {
                    int dr = ((currentByte & QOI_DIFF_RED) >> 4) - 2;
                    int dg = ((currentByte & QOI_DIFF_GREEN) >> 2) - 2;
                    int db = ((currentByte & QOI_DIFF_BLUE)) - 2;

                    previousColor.r = (previousColor.r + dr) & 0xff;
                    previousColor.g = (previousColor.g + dg) & 0xff;
                    previousColor.b = (previousColor.b + db) & 0xff;

                    WriteColor(bytes, ref writeIndex, previousColor);
                    InsertIntoSeenColors(seenColors, previousColor);

                    continue;
                }

                if ((currentByte & QOI_MASK_2) == QOI_OP_LUMA)
                {
                    int dg = (currentByte & QOI_LUMA_GREEN) - 32;
                    byte nextByte = rawQoi[readIndex++];
                    int drdg = ((nextByte & QOI_LUMA_DRDG) >> 4) - 8;
                    int dbdg = (nextByte & QOI_LUMA_DBDG) - 8;

                    int dr = drdg + dg;
                    int db = dbdg + dg;

                    previousColor.r = (previousColor.r + dr) & 0xff;
                    previousColor.g = (previousColor.g + dg) & 0xff;
                    previousColor.b = (previousColor.b + db) & 0xff;

                    WriteColor(bytes, ref writeIndex, previousColor);
                    InsertIntoSeenColors(seenColors, previousColor);

                    continue;
                }
            }

            if(writeIndex < pixelBufferSize)
            {
                throw new Exception();
            }

            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            BitmapData data=bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, bitmap.PixelFormat);

            Marshal.Copy(bytes, 0, data.Scan0, pixelBufferSize);

            bitmap.UnlockBits(data);

            return bitmap;
        }

        public static bool Save(Bitmap inputBitmap, Stream output)
        {
            if (inputBitmap == null)
                return false;

            Color previousColor = new Color()
            {
                r = 0,
                g = 0,
                b = 0,
                a = 255
            };

            Color[] seenColors = new Color[64];

            for (int i = 0; i < seenColors.Length; i++)
            {
                seenColors[i] = new Color();
            }

            BitmapData data = inputBitmap.LockBits(new Rectangle(0, 0, inputBitmap.Width, inputBitmap.Height), ImageLockMode.ReadOnly, inputBitmap.PixelFormat);

            unsafe
            {
                byte* buffer = (byte*)data.Scan0;

                int channels = (inputBitmap.PixelFormat == PixelFormat.Format24bppRgb ? 3 : 4);

                int imageSize = data.Stride * inputBitmap.Height;
                int lastPixel = imageSize - channels;

                int maxSize = inputBitmap.Width * inputBitmap.Height * (channels + 1) + QOI_HEADER_SIZE + QOI_END_MARKER_SIZE;

                byte[] bytes = new byte[maxSize];

                int index = 0;
                int run = 0;

                Write32(bytes, ref index, QOI_MAGIC_BYTES);
                Write32(bytes, ref index, inputBitmap.Width);
                Write32(bytes, ref index, inputBitmap.Height);
                bytes[index++] = (byte)channels;
                bytes[index++] = 0;

                for (int i = 0; i < imageSize; i+=channels)
                {
                    Color color = new Color()
                    {
                        r = buffer[i + 2],
                        g = buffer[i + 1],
                        b = buffer[i + 0],
                        a = channels == 4 ? buffer[i + 3] : previousColor.a,
                    };

                    if (previousColor.Equals(color))
                    {
                        run++;

                        if (run == 62 || i == lastPixel)
                        {
                            bytes[index++] = (byte)(QOI_OP_RUN | (run - 1));
                            run = 0;
                        }
                    }
                    else
                    {
                        if (run > 0)
                        {
                            bytes[index++] = (byte)(QOI_OP_RUN | (run - 1));
                            run = 0;
                        }

                        int seenColorIndex = color.GetHashCode() % 64;
                        if (color.Equals(seenColors[seenColorIndex]))
                        {
                            bytes[index++] = (byte)(QOI_OP_INDEX | seenColorIndex);
                        }
                        else
                        {
                            InsertIntoSeenColors(seenColors, color);

                            Color diff = color - previousColor;
                            diff.r = (sbyte)diff.r;
                            diff.g = (sbyte)diff.g;
                            diff.b = (sbyte)diff.b;
                            diff.a = (sbyte)diff.a;

                            sbyte drdg = (sbyte)(diff.r - diff.g);
                            sbyte dbdg = (sbyte)(diff.b - diff.g);

                            if (diff.a == 0)
                            {
                                if ((diff.r >= -2 && diff.r <= 1) && (diff.g >= -2 && diff.g <= 1) && (diff.b >= -2 && diff.b <= 1))
                                {
                                    bytes[index++] = (byte)(QOI_OP_DIFF | ((diff.r + 2) << 4) | ((diff.g + 2) << 2) | ((diff.b + 2)));
                                }
                                else if ((drdg >= -8 && drdg <= 7) && (diff.g >= -32 && diff.g <= 31) && (dbdg >= -8 && dbdg <= 7))
                                {
                                    bytes[index++] = (byte)(QOI_OP_LUMA | (diff.g + 32));
                                    bytes[index++] = (byte)(((drdg + 8) << 4) | (dbdg + 8));
                                }
                                else
                                {
                                    bytes[index++] = QOI_OP_RGB;
                                    bytes[index++] = (byte)color.r;
                                    bytes[index++] = (byte)color.g;
                                    bytes[index++] = (byte)color.b;
                                }
                            }
                            else
                            {
                                bytes[index++] = QOI_OP_RGBA;
                                bytes[index++] = (byte)color.r;
                                bytes[index++] = (byte)color.g;
                                bytes[index++] = (byte)color.b;
                                bytes[index++] = (byte)color.a;
                            }
                        }
                    }

                    previousColor = color;
                }

                foreach (var item in END_MARKER)
                {
                    bytes[index++] = item;
                }

                bytes = bytes.Take(index).ToArray();

                output.Write(bytes, 0, bytes.Length);
            }

            inputBitmap.UnlockBits(data);

            return true;
        }

        internal struct Color
        {
            public int r;
            public int g;
            public int b;
            public int a;

            public override bool Equals(object obj)
            {
                if (obj is Color c)
                {
                    return c.r == r && c.g == g && c.b == b && c.a == a;
                }
                return false;
            }

            public static Color operator -(Color a, Color b) => new Color()
            {
                r = a.r - b.r,
                g = a.g - b.g,
                b = a.b - b.b,
                a = a.a - b.a
            };

            public override int GetHashCode()
            {
                return r * 3 + g * 5 + b * 7 + a * 11;
            }
        }
    }
}
