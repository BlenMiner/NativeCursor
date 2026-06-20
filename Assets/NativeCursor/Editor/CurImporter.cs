using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Riten.Native.Cursors.Editor.Importers
{
    public struct CURSOR_RESULT
    {
        public Texture2D texture;
        public Vector2 hotspot;

        public bool isMask;
        public Color32 backgroundColor;
        public Color32 foregroundColor;
    }

    public struct CURSOR
    {
        public byte width;
        public byte height;
        public byte colorCount;
        public byte reserved;
        public ushort xhotspot;
        public ushort yhotspot;
        public uint sizeInBytes;
        public uint fileOffset;

        public int Width => width == 0 ? 256 : width;
        public int Height => height == 0 ? 256 : height;
    }

    [ScriptedImporter(3, "cur")]
    public class CurImporter : ScriptedImporter
    {
        private const ushort CursorResourceType = 2;
        private const uint PngSignatureFirstBytes = 0x474E5089;
        private const uint BiRgb = 0;
        private const uint BiBitFields = 3;
        private const byte InvertedMaskAlpha = 1;
        private const int MaxCursorDimension = 4096;

        [SerializeField] bool _invertXHotspot;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var cursors = LoadCursorFromFile(ctx.assetPath, _invertXHotspot);

            if (cursors is not { Count: > 0 })
            {
                Debug.LogError($"Failed to load cursor from file: {ctx.assetPath}");
                return;
            }

            CursorImportAssets.AddStaticCursors(ctx, cursors);
        }

        static List<CURSOR_RESULT> LoadCursorFromFile(string file, bool invertXHotspot = false)
        {
            if (!File.Exists(file))
                return null;

            using var br = new BinaryReader(File.OpenRead(file));

            return LoadCursorFromBinary(br, out var res, invertXHotspot) ? res : null;
        }

        public static bool LoadCursorFromBinary(BinaryReader br, out List<CURSOR_RESULT> result,
            bool invertXHotspot = false)
        {
            result = new List<CURSOR_RESULT>();
            var begin = br.BaseStream.Position;

            try
            {
                if (!CanRead(br, 6))
                    return false;

                if (br.ReadUInt16() != 0)
                    return false;

                if (br.ReadUInt16() != CursorResourceType)
                    return false;

                ushort count = br.ReadUInt16();

                if (count == 0 || !CanRead(br, count * 16L))
                    return false;

                var cursors = new List<CURSOR>(count);

                for (int i = 0; i < count; ++i)
                {
                    cursors.Add(new CURSOR
                    {
                        width = br.ReadByte(),
                        height = br.ReadByte(),
                        colorCount = br.ReadByte(),
                        reserved = br.ReadByte(),
                        xhotspot = br.ReadUInt16(),
                        yhotspot = br.ReadUInt16(),
                        sizeInBytes = br.ReadUInt32(),
                        fileOffset = br.ReadUInt32()
                    });
                }

                var results = new List<CURSOR_RESULT>(count);

                for (int i = 0; i < cursors.Count; ++i)
                {
                    var cursor = cursors[i];
                    var imageStart = begin + cursor.fileOffset;
                    var imageEnd = imageStart + cursor.sizeInBytes;

                    if (cursor.sizeInBytes == 0 || imageStart < begin || imageEnd > br.BaseStream.Length)
                        return false;

                    br.BaseStream.Seek(imageStart, SeekOrigin.Begin);

                    if (IsPngImage(br, imageStart, imageEnd))
                    {
                        if (!TryLoadPngCursor(br, cursor, invertXHotspot, out var pngCursor))
                            return false;

                        results.Add(pngCursor);
                    }
                    else
                    {
                        if (!TryLoadDibCursor(br, cursor, imageEnd, invertXHotspot, out var dibCursor))
                            return false;

                        results.Add(dibCursor);
                    }

                    br.BaseStream.Seek(imageEnd, SeekOrigin.Begin);
                }

                result = results;
                return true;
            }
            catch (EndOfStreamException)
            {
                result.Clear();
                return false;
            }
            catch (IOException)
            {
                result.Clear();
                return false;
            }
            catch (ArgumentException)
            {
                result.Clear();
                return false;
            }
            catch (NotSupportedException)
            {
                result.Clear();
                return false;
            }
        }

        private static bool TryLoadPngCursor(BinaryReader br, CURSOR cursor, bool invertXHotspot,
            out CURSOR_RESULT result)
        {
            result = default;

            if (cursor.sizeInBytes > int.MaxValue)
                return false;

            var rawData = br.ReadBytes((int)cursor.sizeInBytes);

            if (rawData.Length != cursor.sizeInBytes)
                return false;

            var rawTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                alphaIsTransparency = true
            };

            if (!rawTexture.LoadImage(rawData))
            {
                UnityEngine.Object.DestroyImmediate(rawTexture);
                return false;
            }

            var finalTexture = CreateTexture(rawTexture.width, rawTexture.height, rawTexture.GetPixels32());
            UnityEngine.Object.DestroyImmediate(rawTexture);

            result = new CURSOR_RESULT
            {
                texture = finalTexture,
                isMask = false,
                backgroundColor = default,
                foregroundColor = default,
                hotspot = NormalizeHotspot(cursor.xhotspot, cursor.yhotspot, finalTexture.width, finalTexture.height,
                    invertXHotspot)
            };

            return true;
        }

        private static bool TryLoadDibCursor(BinaryReader br, CURSOR cursor, long imageEnd, bool invertXHotspot,
            out CURSOR_RESULT result)
        {
            result = default;
            var headerStart = br.BaseStream.Position;

            if (!CanRead(br, 40))
                return false;

            var headerSize = br.ReadUInt32();

            if (headerSize < 40 || headerStart + headerSize > imageEnd)
                return false;

            var bitmapWidth = br.ReadInt32();
            var bitmapHeight = br.ReadInt32();
            br.ReadUInt16(); // planes
            var bitPerPixel = br.ReadUInt16();
            var compression = br.ReadUInt32();
            br.ReadUInt32(); // image size; often zero for BI_RGB
            br.ReadInt32(); // x pixels per meter
            br.ReadInt32(); // y pixels per meter
            var colorsUsed = br.ReadUInt32();
            br.ReadUInt32(); // important colors

            if (compression != BiRgb && compression != BiBitFields)
                return false;

            if (!IsSupportedBitDepth(bitPerPixel))
                return false;

            var redMask = 0u;
            var greenMask = 0u;
            var blueMask = 0u;
            var alphaMask = 0u;
            var pixelDataStart = headerStart + headerSize;

            if (compression == BiBitFields)
            {
                if (headerSize >= 52)
                {
                    redMask = br.ReadUInt32();
                    greenMask = br.ReadUInt32();
                    blueMask = br.ReadUInt32();

                    if (headerSize >= 56)
                        alphaMask = br.ReadUInt32();
                }
                else
                {
                    br.BaseStream.Seek(headerStart + headerSize, SeekOrigin.Begin);

                    if (!CanRead(br, 12))
                        return false;

                    redMask = br.ReadUInt32();
                    greenMask = br.ReadUInt32();
                    blueMask = br.ReadUInt32();
                    pixelDataStart = br.BaseStream.Position;
                }
            }

            br.BaseStream.Seek(pixelDataStart, SeekOrigin.Begin);

            var width = bitmapWidth == 0 ? cursor.Width : Math.Abs(bitmapWidth);
            var storedHeight = Math.Abs(bitmapHeight);
            var height = storedHeight > 1 ? storedHeight / 2 : cursor.Height;

            if (!IsValidDimension(width) || !IsValidDimension(height))
                return false;

            if (!TryReadPalette(br, bitPerPixel, colorsUsed, imageEnd, out var palette))
                return false;

            var rowStride = GetBitmapStride(width, bitPerPixel);
            var maskStride = GetBitmapStride(width, 1);
            var pixelBytes = (long)rowStride * height;
            var maskBytes = (long)maskStride * height;

            if (pixelBytes < 0 || br.BaseStream.Position + pixelBytes > imageEnd)
                return false;

            var pixels = new Color32[width * height];
            var topDown = bitmapHeight < 0;
            var hasNonZeroAlpha = false;

            for (int rowIndex = 0; rowIndex < height; ++rowIndex)
            {
                var row = br.ReadBytes(rowStride);

                if (row.Length != rowStride)
                    return false;

                var targetY = topDown ? height - rowIndex - 1 : rowIndex;

                for (int x = 0; x < width; ++x)
                {
                    var pixel = DecodePixel(row, x, bitPerPixel, compression, palette, redMask, greenMask, blueMask,
                        alphaMask);

                    if (bitPerPixel == 32 && pixel.a != 0)
                        hasNonZeroAlpha = true;

                    pixels[targetY * width + x] = pixel;
                }
            }

            if (bitPerPixel == 32 && !hasNonZeroAlpha)
            {
                for (int i = 0; i < pixels.Length; ++i)
                {
                    var pixel = pixels[i];
                    pixel.a = 255;
                    pixels[i] = pixel;
                }
            }

            var hasInvertedMaskPixels = false;
            var preserveInvertedMaskPixels = bitPerPixel < 32 || !hasNonZeroAlpha;

            if (br.BaseStream.Position + maskBytes <= imageEnd)
                hasInvertedMaskPixels = ApplyAndMask(br, pixels, width, height, maskStride, topDown,
                    preserveInvertedMaskPixels);

            var texture = CreateTexture(width, height, pixels);

            result = new CURSOR_RESULT
            {
                texture = texture,
                isMask = hasInvertedMaskPixels,
                backgroundColor = palette is { Length: > 0 } ? palette[0] : default,
                foregroundColor = palette is { Length: > 1 } ? palette[1] : default,
                hotspot = NormalizeHotspot(cursor.xhotspot, cursor.yhotspot, width, height, invertXHotspot)
            };

            return true;
        }

        private static bool TryReadPalette(BinaryReader br, ushort bitPerPixel, uint colorsUsed, long imageEnd,
            out Color32[] palette)
        {
            palette = null;

            if (bitPerPixel > 8)
                return true;

            var paletteLength = colorsUsed == 0 ? 1 << bitPerPixel : (int)colorsUsed;

            if (paletteLength <= 0 || paletteLength > 256 || br.BaseStream.Position + paletteLength * 4L > imageEnd)
                return false;

            palette = new Color32[paletteLength];

            for (int i = 0; i < paletteLength; ++i)
            {
                var b = br.ReadByte();
                var g = br.ReadByte();
                var r = br.ReadByte();
                br.ReadByte();
                palette[i] = new Color32(r, g, b, 255);
            }

            return true;
        }

        private static Color32 DecodePixel(byte[] row, int x, ushort bitPerPixel, uint compression, Color32[] palette,
            uint redMask, uint greenMask, uint blueMask, uint alphaMask)
        {
            switch (bitPerPixel)
            {
                case 32:
                {
                    var offset = x * 4;

                    if (compression == BiBitFields && redMask != 0 && greenMask != 0 && blueMask != 0)
                    {
                        var value = BitConverter.ToUInt32(row, offset);
                        return new Color32(
                            ExtractMaskedChannel(value, redMask),
                            ExtractMaskedChannel(value, greenMask),
                            ExtractMaskedChannel(value, blueMask),
                            alphaMask == 0 ? (byte)255 : ExtractMaskedChannel(value, alphaMask)
                        );
                    }

                    return new Color32(row[offset + 2], row[offset + 1], row[offset], row[offset + 3]);
                }
                case 24:
                {
                    var offset = x * 3;
                    return new Color32(row[offset + 2], row[offset + 1], row[offset], 255);
                }
                case 16:
                {
                    var offset = x * 2;
                    var value = BitConverter.ToUInt16(row, offset);

                    if (compression == BiBitFields && redMask != 0 && greenMask != 0 && blueMask != 0)
                    {
                        return new Color32(
                            ExtractMaskedChannel(value, redMask),
                            ExtractMaskedChannel(value, greenMask),
                            ExtractMaskedChannel(value, blueMask),
                            alphaMask == 0 ? (byte)255 : ExtractMaskedChannel(value, alphaMask)
                        );
                    }

                    return new Color32(
                        (byte)(((value >> 10) & 0x1F) * 255 / 31),
                        (byte)(((value >> 5) & 0x1F) * 255 / 31),
                        (byte)((value & 0x1F) * 255 / 31),
                        255
                    );
                }
                case 8:
                    return ReadPaletteColor(palette, row[x]);
                case 4:
                {
                    var value = row[x / 2];
                    var index = (x & 1) == 0 ? value >> 4 : value & 0x0F;
                    return ReadPaletteColor(palette, index);
                }
                case 1:
                {
                    var value = row[x / 8];
                    var index = (value >> (7 - x % 8)) & 1;
                    return ReadPaletteColor(palette, index);
                }
                default:
                    throw new NotSupportedException($"Bit per pixel {bitPerPixel} is not supported.");
            }
        }

        private static Color32 ReadPaletteColor(IReadOnlyList<Color32> palette, int index)
        {
            if (palette == null || index < 0 || index >= palette.Count)
                return new Color32(0, 0, 0, 255);

            return palette[index];
        }

        private static bool ApplyAndMask(BinaryReader br, IList<Color32> pixels, int width, int height, int maskStride,
            bool topDown, bool preserveInvertedPixels)
        {
            var hasInvertedPixels = false;

            for (int rowIndex = 0; rowIndex < height; ++rowIndex)
            {
                var row = br.ReadBytes(maskStride);
                var targetY = topDown ? height - rowIndex - 1 : rowIndex;

                for (int x = 0; x < width; ++x)
                {
                    var value = row[x / 8];
                    var transparent = ((value >> (7 - x % 8)) & 1) != 0;

                    if (!transparent)
                        continue;

                    var pixelIndex = targetY * width + x;
                    var pixel = pixels[pixelIndex];

                    if (preserveInvertedPixels && HasXorColor(pixel))
                    {
                        pixel.a = InvertedMaskAlpha;
                        hasInvertedPixels = true;
                    }
                    else
                    {
                        pixel.a = 0;
                    }

                    pixels[pixelIndex] = pixel;
                }
            }

            return hasInvertedPixels;
        }

        private static bool HasXorColor(Color32 pixel)
        {
            return pixel.r != 0 || pixel.g != 0 || pixel.b != 0;
        }

        private static Texture2D CreateTexture(int width, int height, Color32[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                alphaIsTransparency = true,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private static Vector2 NormalizeHotspot(ushort xhotspot, ushort yhotspot, int width, int height,
            bool invertXHotspot)
        {
            var x = invertXHotspot ? width - xhotspot : xhotspot;
            var y = yhotspot;

            return new Vector2(
                Mathf.Clamp(x / (float)width, 0f, 1f),
                Mathf.Clamp(y / (float)height, 0f, 1f)
            );
        }

        private static byte ExtractMaskedChannel(uint value, uint mask)
        {
            if (mask == 0)
                return 0;

            var shift = 0;
            var shiftedMask = mask;

            while ((shiftedMask & 1) == 0)
            {
                shiftedMask >>= 1;
                shift++;
            }

            var bitCount = 0;
            var bits = shiftedMask;

            while ((bits & 1) == 1)
            {
                bitCount++;
                bits >>= 1;
            }

            if (bitCount == 0)
                return 0;

            var channel = (value & mask) >> shift;
            var max = (1u << bitCount) - 1u;
            return (byte)(channel * 255u / max);
        }

        private static int GetBitmapStride(int width, int bitPerPixel)
        {
            return ((width * bitPerPixel + 31) / 32) * 4;
        }

        private static bool IsPngImage(BinaryReader br, long imageStart, long imageEnd)
        {
            if (imageEnd - imageStart < 8)
                return false;

            var position = br.BaseStream.Position;
            br.BaseStream.Seek(imageStart, SeekOrigin.Begin);
            var signature = br.ReadUInt32();
            br.BaseStream.Seek(position, SeekOrigin.Begin);
            return signature == PngSignatureFirstBytes;
        }

        private static bool CanRead(BinaryReader br, long bytes)
        {
            return bytes >= 0 && br.BaseStream.Position + bytes <= br.BaseStream.Length;
        }

        private static bool IsValidDimension(int value)
        {
            return value > 0 && value <= MaxCursorDimension;
        }

        private static bool IsSupportedBitDepth(ushort bitPerPixel)
        {
            return bitPerPixel is 1 or 4 or 8 or 16 or 24 or 32;
        }
    }

    internal static class CursorImportAssets
    {
        public static void AddStaticCursors(AssetImportContext ctx, IList<CURSOR_RESULT> cursors)
        {
            VirtualCursor firstCursor = null;

            for (int i = cursors.Count - 1; i >= 0; --i)
            {
                var cursorData = cursors[i];

                if (!cursorData.texture)
                    continue;

                var cursor = ScriptableObject.CreateInstance<VirtualCursor>();
                var idx = cursors.Count - i;
                cursor.name = $"cursor_{idx}";
                cursor.texture = cursorData.texture;
                cursor.texture.name = $"cursor_{idx}_texture";
                cursor.hotspot = cursorData.hotspot;
                cursor.isMask = cursorData.isMask;
                cursor.backgroundColor = cursorData.backgroundColor;
                cursor.foregroundColor = cursorData.foregroundColor;
                ctx.AddObjectToAsset($"cursor_{idx}_texture", cursorData.texture, cursor.texture);
                ctx.AddObjectToAsset($"cursor_{idx}", cursor, cursor.texture);

                var newCur = UnityEngine.Object.Instantiate(cursor);
                newCur.name = $"cursor_{idx}_cursor";
                ctx.AddObjectToAsset($"cursor_{idx}_cursor", newCur, cursor.texture);

                firstCursor ??= cursor;
            }

            if (firstCursor)
                ctx.SetMainObject(firstCursor);
        }

        public static void AddAnimatedCursor(AssetImportContext ctx, string name, IList<CURSOR_RESULT> frames, int fps)
        {
            if (frames.Count == 0)
                return;

            var animatedCursor = ScriptableObject.CreateInstance<AnimatedVirtualCursor>();
            animatedCursor.name = name;
            animatedCursor.frames = new VirtualCursor[frames.Count];

            for (int i = 0; i < frames.Count; ++i)
            {
                var frame = frames[i];
                var cursor = ScriptableObject.CreateInstance<VirtualCursor>();
                cursor.name = $"cursor_{i}";
                cursor.texture = frame.texture;
                cursor.texture.name = $"cursor_{i}_texture";
                cursor.isMask = frame.isMask;
                cursor.hotspot = frame.hotspot;
                cursor.backgroundColor = frame.backgroundColor;
                cursor.foregroundColor = frame.foregroundColor;

                ctx.AddObjectToAsset($"cursor_{i}_texture", cursor.texture, cursor.texture);
                ctx.AddObjectToAsset($"cursor_{i}", cursor, cursor.texture);
                animatedCursor.frames[i] = cursor;
            }

            animatedCursor.fps = Mathf.Max(1, fps);
            ctx.AddObjectToAsset("animated_cursor", animatedCursor, animatedCursor.frames[0].texture);
            ctx.SetMainObject(animatedCursor);
        }
    }
}
