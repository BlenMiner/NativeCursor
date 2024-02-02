using System.Collections.Generic;
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;
// ReSharper disable UnusedVariable

namespace Riten.Native.Cursors.Editor.Importers
{
    public struct ANIM_FRAME
    {
        public List<CURSOR_RESULT> cursors;
    }
    
    public struct ANIM_CURSOR
    {
        public int fps;
        public List<ANIM_FRAME> frames;
    }
    
    [ScriptedImporter(1, "ani")]
    public class AniImporter : ScriptedImporter
    {
        [SerializeField] float _animSpeedMultiplier = 5f;
        
        public override void OnImportAsset(AssetImportContext ctx)
        {
            if (!LoadCursorFromFile(ctx.assetPath, out var data))
            {
                Debug.LogError("Failed to load cursor from file");
                return;
            }
            
            if (data.frames.Count == 0) 
                return;

            var animatedCursor = ScriptableObject.CreateInstance<AnimatedVirtualCursor>();
            
            animatedCursor.name = Path.GetFileNameWithoutExtension(ctx.assetPath);
            
            animatedCursor.frames = new VirtualCursor[data.frames.Count];
            
            for (int i = 0; i < data.frames.Count; ++i)
            {
                var frame = data.frames[i];
                var cursor = ScriptableObject.CreateInstance<VirtualCursor>();
                
                int smallestWidth = 0;
                int smallestValue = frame.cursors[0].texture.width;
                
                for (int j = 1; j < frame.cursors.Count; ++j)
                {
                    if (frame.cursors[j].texture.width < smallestValue)
                    {
                        smallestValue = frame.cursors[j].texture.width;
                        smallestWidth = j;
                    }
                }
                
                cursor.name = $"cursor_{i}";
                cursor.texture = frame.cursors[smallestWidth].texture;
                cursor.texture.name = $"cursor_{i}_texture";
                cursor.isMask = frame.cursors[smallestWidth].isMask;
                cursor.hotspot = frame.cursors[smallestWidth].hotspot;
                
                ctx.AddObjectToAsset($"cursor_{i}_texture", cursor.texture, cursor.texture);
                ctx.AddObjectToAsset($"cursor_{i}", cursor, cursor.texture);
                animatedCursor.frames[i] = cursor;
            }
            
            animatedCursor.fps = Mathf.RoundToInt(data.fps * _animSpeedMultiplier);
            ctx.AddObjectToAsset("animated_cursor", animatedCursor, animatedCursor.frames[0].texture);
            ctx.SetMainObject(animatedCursor);
        }
        
        private static bool LoadCursorFromFile(string path, out ANIM_CURSOR result)
        {
            result = default;
            
            if (!File.Exists(path))
                return false;
            
            using var br = new BinaryReader(File.OpenRead(path));
            
            var riff = br.ReadInt32();
            
            if (riff != 0x46464952)
            {
                Debug.LogError("Invalid RIFF header");
                return false;
            }

            int riffSize = br.ReadInt32();
            var riffType = br.ReadInt32();

            if (riffType != 0x4E4F4341)
            {
                Debug.LogError($"Invalid RIFF type 0x{riffType:X}");
                return false;
            }

            var anihIdentifier = br.ReadInt32();
            
            if (anihIdentifier != 0x68696E61)
            {
                Debug.LogError("Invalid anih type");
                return false;
            }
            
            br.ReadInt32();
            
            var headerSize = br.ReadInt32();
            var numFrames = br.ReadInt32();
            var numSteps = br.ReadInt32();
            var width = br.ReadInt32();
            var height = br.ReadInt32();
            var bitCount = br.ReadInt32();
            var numPlanes = br.ReadInt32();
            var displayRate = br.ReadInt32();
            var flags = br.ReadInt32();
            
            var listIdentifier = br.ReadInt32();
            
            if (listIdentifier != 0x5453494C)
            {
                Debug.LogError("Invalid LIST identifier");
                return false;
            }
            
            var listSize = br.ReadInt32();
            
            var frameIdentifier = br.ReadInt32();
            
            if (frameIdentifier != 0x6D617266)
            {
                Debug.LogError("Invalid fram identifier");
                return false;
            }

            var cursor = new ANIM_CURSOR
            {
                fps = displayRate,
                frames = new List<ANIM_FRAME>()
            };
            
            for (int i = 0; i < numFrames; ++i)
            {
                if (!ReadIcon(br, out var cursors))
                {
                    Debug.LogError($"Failed to read icon {i}");
                    return false;
                }
                
                cursor.frames.Add(new ANIM_FRAME
                {
                    cursors = cursors
                });
            }
            
            var end = br.BaseStream.Position;
            var bytesLeft = br.BaseStream.Length - br.BaseStream.Position;

            if (bytesLeft == 0)
            {
                result = cursor;
                return true;
            }
            
            var seqIdentifier = br.ReadInt32();
            
            if (seqIdentifier != 0x20716573)
            {
                Debug.LogError("Invalid seq identifier");
                return false;
            }
            
            throw new System.NotImplementedException();
        }

        private static bool ReadIcon(BinaryReader br, out List<CURSOR_RESULT> result)
        {
            result = null;
            
            var iconIdentifier = br.ReadInt32();
            
            if (iconIdentifier != 0x6E6F6369)
            {
                br.BaseStream.Seek(-4, SeekOrigin.Current);
                return false;
            }
            
            var iconSize = br.ReadInt32();

            if (!CurImporter.LoadCursorFromBinary(br, out result))
            {
                Debug.LogError("Failed to load cursor from binary");
                return false;
            }
            
            return true;
        }
    }
}