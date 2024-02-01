using System.Collections.Generic;
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;
// ReSharper disable UnusedVariable

namespace Riten.Native.Cursors.Editor
{
    public struct CURSOR_RESULT
    {
        public Texture2D texture;
        public Vector2 hotspot;
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
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var cursors = LoadCursorFromFile(ctx.assetPath);
            
            VirtualCursor firstCursor = null;

            for (int i = 0; i < cursors.Count; ++i)
            {
                var cursor = ScriptableObject.CreateInstance<VirtualCursor>();
                cursor.texture = cursors[i].texture;
                cursor.hotspot = cursors[i].hotspot;
                ctx.AddObjectToAsset($"cursor_{i}_texture", cursors[i].texture);
                ctx.AddObjectToAsset($"cursor_{i}", cursor, cursors[i].texture);
                
                firstCursor ??= cursor;
            }
            
            ctx.SetMainObject(firstCursor);
        }

        List<CURSOR_RESULT> LoadCursorFromFile(string file)
        {
            if (!File.Exists(file))
                return null;
            
            using var br = new BinaryReader(File.OpenRead(file));
            
            br.BaseStream.Seek(2, SeekOrigin.Begin);

            if (br.ReadUInt16() != 2)
            {
                Debug.LogError("Invalid/unsupported cursor format!");
                return null;
            }
            
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
                
                br.BaseStream.Seek(cursors[i].fileOffset, SeekOrigin.Begin);
                
                var sizeOfStruct = br.ReadUInt32();
                
                if (sizeOfStruct != 40) Debug.LogError($"BitmapInfoHeader size expected to be 40, was {sizeOfStruct}!");

                var cursorWidth = br.ReadUInt32();
                var cursorHeight = br.ReadUInt32();
                
                var planes = br.ReadUInt16();
                var bitPetPixel = br.ReadUInt16();
                var compression = br.ReadUInt32();
                var sizeImage = br.ReadUInt32();
                var xPelsPerMeter = br.ReadUInt32();
                var yPelsPerMeter = br.ReadUInt32();
                var colorsUsed = br.ReadUInt32();
                var colorsImportant = br.ReadUInt32();
                
                var pixelCount = cursor.width * cursor.height;
                var pixels = new Color32[pixelCount];
                
                if (bitPetPixel == 32)
                {
                    for (int j = 0; j < pixelCount; ++j)
                    {
                        var b = br.ReadByte();
                        var g = br.ReadByte();
                        var r = br.ReadByte();
                        var a = br.ReadByte();

                        pixels[j] = new Color32(r, g, b, a);
                    }
                }
                
                var texture = new Texture2D(cursor.width, cursor.height, TextureFormat.RGBA32, false);
                texture.name = $"cursor_{i}_texture";
                texture.SetPixels32(pixels);
                texture.filterMode = FilterMode.Bilinear;
                texture.Apply();
                
                results.Add(new CURSOR_RESULT
                {
                    texture = texture,
                    hotspot = new Vector2(
                        cursor.xhotspot / (float)cursor.width, 
                        cursor.yhotspot / (float)cursor.height
                    )
                });
            }

            return results;
        }
    }
}