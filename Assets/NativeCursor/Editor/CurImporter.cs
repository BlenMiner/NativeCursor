using System.Collections.Generic;
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;
// ReSharper disable UnusedVariable

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
    }
    
    [ScriptedImporter(1, "cur")]
    public class CurImporter : ScriptedImporter
    {
        [SerializeField] private int offset;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var cursors = LoadCursorFromFile(ctx.assetPath);
            
            VirtualCursor firstCursor = null;
            
            for (int i = cursors.Count - 1; i >= 0; --i)
            {
                var cursor = ScriptableObject.CreateInstance<VirtualCursor>();
                var idx = cursors.Count - i;
                cursor.name = $"cursor_{idx}";
                cursor.texture = cursors[i].texture;
                cursor.texture.name = $"cursor_{idx}_texture";
                cursor.hotspot = cursors[i].hotspot;
                cursor.isMask = cursors[i].isMask;
                cursor.backgroundColor = cursors[i].backgroundColor;
                cursor.foregroundColor = cursors[i].foregroundColor;
                ctx.AddObjectToAsset($"cursor_{idx}_texture", cursors[i].texture, cursor.texture);
                ctx.AddObjectToAsset($"cursor_{idx}", cursor, cursor.texture);

                firstCursor ??= cursor;
            }
            
            ctx.SetMainObject(firstCursor);
        }

        static List<CURSOR_RESULT> LoadCursorFromFile(string file)
        {
            if (!File.Exists(file))
                return null;
            
            using var br = new BinaryReader(File.OpenRead(file));
            
            LoadCursorFromBinary(br, out var res);
            return res;
        }

        public static bool LoadCursorFromBinary(BinaryReader br, out List<CURSOR_RESULT> result)
        {
            result = new List<CURSOR_RESULT>();
            
            var begin = br.BaseStream.Position;
            
            if (br.ReadUInt16() != 0)
                return false;

            if (br.ReadUInt16() != 2)
                return false;
            
            ushort count = br.ReadUInt16();
            var cursors = new List<CURSOR>();
            var results = new List<CURSOR_RESULT>();

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

            for (int i = 0; i < cursors.Count; ++i)
            {
                var cursor = cursors[i];
                
                br.BaseStream.Seek(begin + cursors[i].fileOffset, SeekOrigin.Begin);
                
                var sizeOfStruct = br.ReadUInt32();
                
                if (sizeOfStruct != 40) Debug.LogError($"BitmapInfoHeader size expected to be 40, was {sizeOfStruct}!");

                var cursorWidth = br.ReadUInt32();
                var cursorHeight = br.ReadUInt32();
                
                var planes = br.ReadUInt16();
                var bitPetPixel = br.ReadUInt16();
                var compression = br.ReadUInt32();
                var imageSizeInBytes = br.ReadUInt32();
                var xPelsPerMeter = br.ReadUInt32();
                var yPelsPerMeter = br.ReadUInt32();
                var colorsUsed = br.ReadUInt32();
                var colorsImportant = br.ReadUInt32();
                
                var pixelCount = cursor.width * cursor.height;
                var pixels = new Color32[pixelCount];
                var isMask = false;
                
                Color32 backgroundColor = default;
                Color32 foregroundColor = default;

                switch (bitPetPixel)
                {
                    case 32:
                    {
                        for (int j = 0; j < pixelCount; ++j)
                        {
                            var b = br.ReadByte();
                            var g = br.ReadByte();
                            var r = br.ReadByte();
                            var a = br.ReadByte();

                            pixels[j] = new Color32(r, g, b, a);
                        }

                        break;
                    }
                    case 1:
                    {
                        isMask = true;

                        backgroundColor = new Color32(br.ReadByte(), br.ReadByte(), br.ReadByte(), 255);
                        br.ReadByte();
                        foregroundColor = new Color32(br.ReadByte(), br.ReadByte(), br.ReadByte(), 255);
                        br.ReadByte();

                        var padding = (32 - cursor.width % 32) % 32;
                        var values = br.ReadBytes((int)imageSizeInBytes);

                        int pixelIdx = 0;

                        for (int j = 0; j < imageSizeInBytes * 8; ++j)
                        {
                            int x = j % (cursor.width + padding);

                            var idx = j / 8;
                            var bit = 7 - j % 8;
                            var val = (values[idx] >> bit) & 1;

                            if (x < cursor.width)
                            {
                                pixels[pixelIdx++] = new Color32(255, 255, 255, val == 1 ? (byte)255 : (byte)0);
                            }
                        }

                        break;
                    }
                    default: throw new System.NotImplementedException($"Bit per pixel {bitPetPixel} not implemented!");
                }

                // Skip mask
                br.BaseStream.Seek(begin + cursors[i].fileOffset + cursor.sizeInBytes, SeekOrigin.Begin);
                
                var texture = new Texture2D(cursor.width, cursor.height, TextureFormat.RGBA32, false)
                {
                    alphaIsTransparency = true
                };
                
                texture.SetPixels32(pixels);
                texture.filterMode = FilterMode.Bilinear;
                texture.Apply();
                
                results.Add(new CURSOR_RESULT
                {
                    texture = texture,
                    isMask = isMask,
                    backgroundColor = backgroundColor,
                    foregroundColor = foregroundColor,
                    hotspot = new Vector2(
                        cursor.xhotspot / (float)cursor.width, 
                        cursor.yhotspot / (float)cursor.height
                    )
                });
            }
            
            result = results;
            return true;
        }
    }
}