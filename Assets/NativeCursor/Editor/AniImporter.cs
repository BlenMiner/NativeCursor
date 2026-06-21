using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

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
        [SerializeField, Min(0.01f)] float _animSpeedMultiplier = 10f;
        [Space]
        [SerializeField] bool _useTargetSize;
        [SerializeField] Vector2Int _targetSize = new(32, 32);
        
        static Texture2D Resize(Texture2D texture2D, int targetX, int targetY)
        {
            var previous = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(targetX, targetY, 0, RenderTextureFormat.ARGB32);

            try
            {
                rt.filterMode = FilterMode.Bilinear;
                Graphics.Blit(texture2D, rt);
                RenderTexture.active = rt;

                var result = new Texture2D(targetX, targetY, TextureFormat.RGBA32, false)
                {
                    alphaIsTransparency = true,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };

                result.ReadPixels(new Rect(0, 0, targetX, targetY), 0, 0);
                result.Apply();
                return result;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
        }
        
        public override void OnImportAsset(AssetImportContext ctx)
        {
            if (!LoadCursorFromFile(ctx.assetPath, out var data))
            {
                Debug.LogError("Failed to load cursor from file");
                return;
            }
            
            if (data.frames.Count == 0) 
                return;

            if (_useTargetSize && (_targetSize.x <= 0 || _targetSize.y <= 0))
            {
                Debug.LogError("Target size must be greater than zero.");
                return;
            }

            var animatedCursor = ScriptableObject.CreateInstance<AnimatedVirtualCursor>();
            
            animatedCursor.name = Path.GetFileNameWithoutExtension(ctx.assetPath);
            
            animatedCursor.frames = new VirtualCursor[data.frames.Count];
            
            for (int i = 0; i < data.frames.Count; ++i)
            {
                var frame = data.frames[i];
                var cursor = ScriptableObject.CreateInstance<VirtualCursor>();

                var selectedIndex = SelectCursorIndex(frame.cursors);

                if (selectedIndex < 0)
                {
                    Debug.LogError($"Frame {i} does not contain a usable cursor image.");
                    return;
                }

                var selectedFrame = frame.cursors[selectedIndex];

                if (_useTargetSize &&
                    (selectedFrame.texture.width != _targetSize.x || selectedFrame.texture.height != _targetSize.y))
                {
                    cursor.texture = Resize(selectedFrame.texture, _targetSize.x, _targetSize.y);
                }
                else
                {
                    cursor.texture = selectedFrame.texture;
                }
                
                cursor.name = $"cursor_{i}";
                cursor.texture.name = $"cursor_{i}_texture";
                cursor.isMask = selectedFrame.isMask;
                cursor.hotspot = selectedFrame.hotspot;
                cursor.backgroundColor = selectedFrame.backgroundColor;
                cursor.foregroundColor = selectedFrame.foregroundColor;
                
                ctx.AddObjectToAsset($"cursor_{i}_texture", cursor.texture, cursor.texture);
                ctx.AddObjectToAsset($"cursor_{i}", cursor, cursor.texture);
                animatedCursor.frames[i] = cursor;
            }
            
            animatedCursor.fps = Mathf.Max(1, Mathf.RoundToInt(data.fps * _animSpeedMultiplier));
            ctx.AddObjectToAsset("animated_cursor", animatedCursor, animatedCursor.frames[0].texture);
            ctx.SetMainObject(animatedCursor);
        }

        private int SelectCursorIndex(IReadOnlyList<CURSOR_RESULT> cursors)
        {
            if (cursors == null || cursors.Count == 0)
                return -1;

            var bestIndex = -1;
            var bestScore = int.MaxValue;

            for (var i = 0; i < cursors.Count; ++i)
            {
                var texture = cursors[i].texture;

                if (!texture)
                    continue;

                var score = _useTargetSize
                    ? Math.Abs(texture.width - _targetSize.x) + Math.Abs(texture.height - _targetSize.y)
                    : texture.width * texture.height;

                if (score >= bestScore)
                    continue;

                bestScore = score;
                bestIndex = i;
            }

            return bestIndex;
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

            var riffSize = br.ReadInt32();
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
            
            br.ReadInt32();
            var numFrames = br.ReadInt32();
            br.ReadInt32();
            br.ReadInt32();
            br.ReadInt32();
            br.ReadInt32();
            br.ReadInt32();
            var displayRate = br.ReadInt32();
            br.ReadInt32();
            
            var listIdentifier = br.ReadInt32();
            
            if (listIdentifier != 0x5453494C)
            {
                Debug.LogError("Invalid LIST identifier");
                return false;
            }
            
            br.ReadInt32();
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
            
            result = cursor;
            return true;
        }

        private static bool ReadIcon(BinaryReader br, out List<CURSOR_RESULT> result)
        {
            result = null;
            
            var iconIdentifier = br.ReadInt32();

            if (iconIdentifier != 0x6E6F6369)
            {
                br.BaseStream.Seek(-4, SeekOrigin.Current);
                Debug.LogError($"Invalid icon identifier, {br.BaseStream.Position}");
                return false;
            }
            
            var chunkSize = br.ReadUInt32();
            var chunkEnd = br.BaseStream.Position + chunkSize;

            if (!CurImporter.LoadCursorFromBinary(br, out result))
            {
                Debug.LogError("Failed to load cursor from binary");
                return false;
            }

            br.BaseStream.Seek(chunkEnd + (chunkSize & 1), SeekOrigin.Begin);
            
            return true;
        }
    }
}
