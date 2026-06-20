using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Riten.Native.Cursors.Editor.Importers
{
    internal abstract class XCursorImporterBase : ScriptedImporter
    {
        [SerializeField] bool _useTargetSize;
        [SerializeField] Vector2Int _targetSize = new(32, 32);

        public override void OnImportAsset(AssetImportContext ctx)
        {
            if (!XCursorParser.TryLoadFile(ctx.assetPath, out var images) || images.Count == 0)
            {
                Debug.LogError($"Failed to load Xcursor file: {ctx.assetPath}");
                return;
            }

            var selectedImages = XCursorParser.SelectImages(images, _useTargetSize, _targetSize);

            if (selectedImages.Count == 0)
            {
                Debug.LogError($"No usable Xcursor images found: {ctx.assetPath}");
                return;
            }

            if (XCursorParser.IsAnimatedGroup(selectedImages))
            {
                CursorImportAssets.AddAnimatedCursor(
                    ctx,
                    Path.GetFileNameWithoutExtension(ctx.assetPath),
                    XCursorParser.ToCursorResults(selectedImages),
                    XCursorParser.CalculateFramesPerSecond(selectedImages)
                );
                return;
            }

            CursorImportAssets.AddStaticCursors(
                ctx,
                XCursorParser.ToCursorResults(_useTargetSize ? selectedImages : images)
            );
        }
    }

    internal static class XCursorParser
    {
        private const uint XCursorMagic = 0x72756358;
        private const uint XCursorImageType = 0xfffd0002;
        private const int MinImageHeaderSize = 36;
        private const int MaxCursorDimension = 4096;

        public static bool TryLoadFile(string path, out List<XCursorImage> images)
        {
            images = new List<XCursorImage>();

            if (!File.Exists(path))
                return false;

            using var stream = File.OpenRead(path);
            return TryLoadStream(stream, out images);
        }

        public static bool TryLoadStream(Stream stream, out List<XCursorImage> images)
        {
            images = new List<XCursorImage>();

            if (!stream.CanSeek)
            {
                using var copy = new MemoryStream();
                stream.CopyTo(copy);
                copy.Position = 0;
                return TryLoadSeekableStream(copy, out images);
            }

            return TryLoadSeekableStream(stream, out images);
        }

        public static List<XCursorImage> SelectImages(IReadOnlyList<XCursorImage> images, bool useTargetSize,
            Vector2Int targetSize)
        {
            if (!useTargetSize)
            {
                var animatedGroup = FindFirstAnimatedGroup(images);

                if (animatedGroup.Count > 0)
                    return animatedGroup;

                return new List<XCursorImage>(images);
            }

            var target = Mathf.Max(targetSize.x, targetSize.y);
            var bestDistance = int.MaxValue;
            var bestNominalSize = 0u;

            foreach (var image in images)
            {
                var distance = Mathf.Abs((int)image.nominalSize - target);

                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                bestNominalSize = image.nominalSize;
            }

            var selected = new List<XCursorImage>();

            foreach (var image in images)
            {
                if (image.nominalSize == bestNominalSize)
                    selected.Add(image);
            }

            return selected;
        }

        public static List<CURSOR_RESULT> ToCursorResults(IReadOnlyList<XCursorImage> images)
        {
            var results = new List<CURSOR_RESULT>(images.Count);

            foreach (var image in images)
            {
                var texture = new Texture2D(image.width, image.height, TextureFormat.RGBA32, false)
                {
                    alphaIsTransparency = true,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };

                texture.SetPixels32(image.pixels);
                texture.Apply();

                results.Add(new CURSOR_RESULT
                {
                    texture = texture,
                    hotspot = new Vector2(
                        Mathf.Clamp(image.xhot / (float)image.width, 0f, 1f),
                        Mathf.Clamp(image.yhot / (float)image.height, 0f, 1f)
                    ),
                    isMask = false,
                    backgroundColor = default,
                    foregroundColor = default
                });
            }

            return results;
        }

        public static bool IsAnimatedGroup(IReadOnlyList<XCursorImage> images)
        {
            if (images.Count == 0)
                return false;

            var nominalSize = images[0].nominalSize;

            for (var i = 1; i < images.Count; ++i)
            {
                if (images[i].nominalSize != nominalSize)
                    return false;
            }

            if (images.Count > 1)
                return true;

            return images[0].delayMilliseconds > 0;
        }

        public static int CalculateFramesPerSecond(IReadOnlyList<XCursorImage> images)
        {
            var totalDelay = 0f;

            foreach (var image in images)
                totalDelay += image.delayMilliseconds == 0 ? 100f : image.delayMilliseconds;

            var averageDelay = totalDelay / images.Count;
            return Mathf.Max(1, Mathf.RoundToInt(1000f / averageDelay));
        }

        private static bool TryLoadSeekableStream(Stream stream, out List<XCursorImage> images)
        {
            images = new List<XCursorImage>();

            try
            {
                stream.Position = 0;
                using var br = new BinaryReader(stream);

                if (!CanRead(br, 16) || br.ReadUInt32() != XCursorMagic)
                    return false;

                var headerSize = br.ReadUInt32();
                br.ReadUInt32(); // version
                var tocCount = br.ReadUInt32();

                if (headerSize < 16 || tocCount > 4096 || headerSize > br.BaseStream.Length)
                    return false;

                br.BaseStream.Seek(headerSize, SeekOrigin.Begin);

                if (!CanRead(br, tocCount * 12L))
                    return false;

                var tableOfContents = new List<XCursorTocEntry>((int)tocCount);

                for (var i = 0; i < tocCount; ++i)
                {
                    tableOfContents.Add(new XCursorTocEntry
                    {
                        type = br.ReadUInt32(),
                        subtype = br.ReadUInt32(),
                        position = br.ReadUInt32()
                    });
                }

                foreach (var entry in tableOfContents)
                {
                    if (entry.type != XCursorImageType)
                        continue;

                    if (TryReadImage(br, entry, out var image))
                        images.Add(image);
                }

                return images.Count > 0;
            }
            catch (EndOfStreamException)
            {
                images.Clear();
                return false;
            }
            catch (IOException)
            {
                images.Clear();
                return false;
            }
            catch (ArgumentException)
            {
                images.Clear();
                return false;
            }
        }

        private static bool TryReadImage(BinaryReader br, XCursorTocEntry entry, out XCursorImage image)
        {
            image = default;

            if (entry.position > br.BaseStream.Length || br.BaseStream.Length - entry.position < MinImageHeaderSize)
                return false;

            br.BaseStream.Seek(entry.position, SeekOrigin.Begin);

            var headerSize = br.ReadUInt32();
            var chunkType = br.ReadUInt32();
            var chunkSubtype = br.ReadUInt32();
            br.ReadUInt32(); // version

            if (headerSize < MinImageHeaderSize || chunkType != entry.type || chunkSubtype != entry.subtype)
                return false;

            if (entry.position + headerSize > br.BaseStream.Length)
                return false;

            var width = br.ReadUInt32();
            var height = br.ReadUInt32();
            var xhot = br.ReadUInt32();
            var yhot = br.ReadUInt32();
            var delayMilliseconds = br.ReadUInt32();

            if (!IsValidDimension(width) || !IsValidDimension(height))
                return false;

            var pixelCount = (long)width * height;
            var pixelBytes = pixelCount * 4L;
            var pixelStart = entry.position + headerSize;

            if (pixelBytes > int.MaxValue || pixelStart + pixelBytes > br.BaseStream.Length)
                return false;

            br.BaseStream.Seek(pixelStart, SeekOrigin.Begin);

            var pixels = new Color32[pixelCount];
            var w = (int)width;
            var h = (int)height;

            for (var sourceY = 0; sourceY < h; ++sourceY)
            {
                var targetY = h - sourceY - 1;

                for (var x = 0; x < w; ++x)
                {
                    pixels[targetY * w + x] = DecodePremultipliedArgb(br.ReadUInt32());
                }
            }

            image = new XCursorImage
            {
                nominalSize = entry.subtype,
                width = w,
                height = h,
                xhot = xhot,
                yhot = yhot,
                delayMilliseconds = delayMilliseconds,
                pixels = pixels
            };

            return true;
        }

        private static List<XCursorImage> FindFirstAnimatedGroup(IReadOnlyList<XCursorImage> images)
        {
            var groups = new Dictionary<uint, List<XCursorImage>>();

            foreach (var image in images)
            {
                if (!groups.TryGetValue(image.nominalSize, out var group))
                {
                    group = new List<XCursorImage>();
                    groups.Add(image.nominalSize, group);
                }

                group.Add(image);
            }

            List<XCursorImage> bestGroup = null;

            foreach (var group in groups.Values)
            {
                if (!IsAnimatedGroup(group))
                    continue;

                if (bestGroup == null || group[0].nominalSize < bestGroup[0].nominalSize)
                    bestGroup = group;
            }

            return bestGroup ?? new List<XCursorImage>();
        }

        private static Color32 DecodePremultipliedArgb(uint argb)
        {
            var a = (byte)(argb >> 24);
            var r = (byte)(argb >> 16);
            var g = (byte)(argb >> 8);
            var b = (byte)argb;

            if (a is > 0 and < 255)
            {
                r = Unpremultiply(r, a);
                g = Unpremultiply(g, a);
                b = Unpremultiply(b, a);
            }

            return new Color32(r, g, b, a);
        }

        private static byte Unpremultiply(byte color, byte alpha)
        {
            return (byte)Mathf.Min(255, Mathf.RoundToInt(color * 255f / alpha));
        }

        private static bool CanRead(BinaryReader br, long bytes)
        {
            return bytes >= 0 && br.BaseStream.Position + bytes <= br.BaseStream.Length;
        }

        private static bool IsValidDimension(uint value)
        {
            return value > 0 && value <= MaxCursorDimension;
        }

        private struct XCursorTocEntry
        {
            public uint type;
            public uint subtype;
            public uint position;
        }
    }

    internal struct XCursorImage
    {
        public uint nominalSize;
        public int width;
        public int height;
        public uint xhot;
        public uint yhot;
        public uint delayMilliseconds;
        public Color32[] pixels;
    }

    [ScriptedImporter(1, "xcursor")]
    internal sealed class XCursorImporter : XCursorImporterBase
    {
    }

    [ScriptedImporter(1, "xcur")]
    internal sealed class XCurImporter : XCursorImporterBase
    {
    }
}
