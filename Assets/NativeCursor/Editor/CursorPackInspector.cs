using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Riten.Native.Cursors.Editor
{
    [CustomEditor(typeof(CursorPack))]
    public class CursorPackInspector : UnityEditor.Editor
    {
        private const float PreviewSize = 64f;
        private const float TileHeight = 112f;
        private const float MinTileWidth = 108f;
        private const byte InvertedMaskAlpha = 1;

        private static Texture2D _checkerTexture;
        private static readonly Dictionary<int, Texture2D> _maskPreviewTextures = new();
        private CursorPack _pack;

        private readonly CursorPreview[] _previews =
        {
            new("Default", pack => pack.@default),
            new("Pointer", pack => pack.pointer),
            new("IBeam", pack => pack.ibeam),
            new("Wait", pack => pack.wait),
            new("Cross", pack => pack.cross),
            new("Grab", pack => pack.grab),
            new("Grabbing", pack => pack.grabbing),
            new("Denied", pack => pack.denied),
            new("Move", pack => pack.move),
            new("Resize H", pack => pack.resizeHorizontal),
            new("Resize V", pack => pack.resizeVertical),
            new("Resize NWSE", pack => pack.resizeDiagonal1),
            new("Resize NESW", pack => pack.resizeDiagonal2)
        };

        private void OnEnable()
        {
            _pack = (CursorPack)target;
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (!_pack)
                return;

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            DrawPreviewGrid();

            if (HasAnimatedCursor())
                Repaint();
        }

        private void DrawPreviewGrid()
        {
            var inspectorWidth = EditorGUIUtility.currentViewWidth - 32f;
            var columns = Mathf.Max(1, Mathf.FloorToInt(inspectorWidth / MinTileWidth));
            var tileWidth = inspectorWidth / columns;

            for (var rowStart = 0; rowStart < _previews.Length; rowStart += columns)
            {
                EditorGUILayout.BeginHorizontal();

                for (var column = 0; column < columns; ++column)
                {
                    var index = rowStart + column;
                    var rect = GUILayoutUtility.GetRect(tileWidth, TileHeight, GUILayout.Width(tileWidth));

                    if (index < _previews.Length)
                        DrawPreviewTile(rect, _previews[index]);
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawPreviewTile(Rect rect, CursorPreview preview)
        {
            var cursor = preview.GetCursor(_pack);
            var texture = GetPreviewTexture(cursor, out var hotspot, out var isAnimated, out var isMask,
                out var foregroundColor);
            var labelRect = new Rect(rect.x + 4f, rect.y + 2f, rect.width - 8f, 18f);
            var previewRect = new Rect(
                rect.x + (rect.width - PreviewSize) * 0.5f,
                rect.y + 24f,
                PreviewSize,
                PreviewSize
            );
            var infoRect = new Rect(rect.x + 4f, rect.yMax - 20f, rect.width - 8f, 18f);

            EditorGUI.LabelField(labelRect, preview.label, EditorStyles.miniBoldLabel);
            EditorGUI.DrawRect(previewRect, new Color(0.16f, 0.16f, 0.16f, 1f));
            GUI.DrawTextureWithTexCoords(previewRect, CheckerTexture,
                new Rect(0f, 0f, previewRect.width / 8f, previewRect.height / 8f));

            if (texture)
            {
                var previewTexture = isMask ? GetMaskPreviewTexture(texture, foregroundColor) : texture;
                var imageRect = FitRect(previewTexture, previewRect);
                GUI.DrawTexture(imageRect, previewTexture, ScaleMode.ScaleToFit, true);
                DrawHotspot(imageRect, hotspot);
                EditorGUI.LabelField(infoRect, isAnimated ? "Animated" : $"{texture.width}x{texture.height}",
                    EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                EditorGUI.LabelField(previewRect, "None", EditorStyles.centeredGreyMiniLabel);
            }
        }

        private static Texture2D GetPreviewTexture(VirtualCursorBase cursor, out Vector2 hotspot, out bool isAnimated,
            out bool isMask, out Color32 foregroundColor)
        {
            hotspot = default;
            isAnimated = false;
            isMask = false;
            foregroundColor = default;

            if (!cursor)
                return null;

            if (cursor.isAnimated)
            {
                isAnimated = true;

                if (cursor.frames == null || cursor.frames.Length == 0)
                    return null;

                var framesPerSecond = Mathf.Max(1, cursor.framesPerSecond);
                var frame = Mathf.FloorToInt(Time.realtimeSinceStartup * framesPerSecond) % cursor.frames.Length;
                var virtualCursor = cursor.frames[frame];

                if (!virtualCursor)
                    return null;

                hotspot = virtualCursor.hotspot;
                isMask = virtualCursor.isMask;
                foregroundColor = virtualCursor.foregroundColor;
                return virtualCursor.texture;
            }

            hotspot = cursor.hotspot;
            isMask = cursor.isMask;
            foregroundColor = cursor.foregroundColor;
            return cursor.texture;
        }

        private static Texture2D GetMaskPreviewTexture(Texture2D source, Color32 foregroundColor)
        {
            if (!source)
                return null;

            var id = source.GetInstanceID();

            if (!_maskPreviewTextures.TryGetValue(id, out var preview) || !preview ||
                preview.width != source.width || preview.height != source.height)
            {
                preview = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    alphaIsTransparency = true,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                _maskPreviewTextures[id] = preview;
            }

            try
            {
                var sourcePixels = source.GetPixels32();
                var previewPixels = new Color32[sourcePixels.Length];
                var hasExplicitInvertedPixels = HasExplicitInvertedMaskPixels(sourcePixels);
                var invertedFallback = new Color32(0, 0, 0, 255);
                var foreground = EnsureOpaque(foregroundColor, new Color32(255, 255, 255, 255));

                for (var i = 0; i < sourcePixels.Length; ++i)
                {
                    var pixel = sourcePixels[i];

                    if (pixel.a == 0)
                        continue;

                    if (IsInvertedMaskPixel(pixel))
                    {
                        previewPixels[i] = invertedFallback;
                    }
                    else if (!hasExplicitInvertedPixels)
                    {
                        previewPixels[i] = foreground;
                    }
                    else
                    {
                        pixel.a = 255;
                        previewPixels[i] = pixel;
                    }
                }

                preview.SetPixels32(previewPixels);
                preview.Apply();
                return preview;
            }
            catch (UnityException)
            {
                return source;
            }
        }

        private static Rect FitRect(Texture texture, Rect bounds)
        {
            var textureWidth = Mathf.Max(1f, texture.width);
            var textureHeight = Mathf.Max(1f, texture.height);
            var scale = Mathf.Min(bounds.width / textureWidth, bounds.height / textureHeight);
            var width = textureWidth * scale;
            var height = textureHeight * scale;

            return new Rect(
                bounds.x + (bounds.width - width) * 0.5f,
                bounds.y + (bounds.height - height) * 0.5f,
                width,
                height
            );
        }

        private static void DrawHotspot(Rect imageRect, Vector2 hotspot)
        {
            var position = new Vector2(
                imageRect.x + imageRect.width * hotspot.x,
                imageRect.y + imageRect.height * hotspot.y
            );

            Handles.BeginGUI();
            Handles.color = Color.red;
            Handles.DrawLine(new Vector3(position.x - 4f, position.y), new Vector3(position.x + 4f, position.y));
            Handles.DrawLine(new Vector3(position.x, position.y - 4f), new Vector3(position.x, position.y + 4f));
            Handles.EndGUI();
        }

        private static Color32 EnsureOpaque(Color32 color, Color32 fallback)
        {
            if (color.a == 0)
                color = fallback;

            color.a = 255;
            return color;
        }

        private static bool HasExplicitInvertedMaskPixels(Color32[] pixels)
        {
            for (var i = 0; i < pixels.Length; ++i)
            {
                if (IsInvertedMaskPixel(pixels[i]))
                    return true;
            }

            return false;
        }

        private static bool IsInvertedMaskPixel(Color32 pixel)
        {
            return pixel.a == InvertedMaskAlpha;
        }

        private bool HasAnimatedCursor()
        {
            foreach (var preview in _previews)
            {
                var cursor = preview.GetCursor(_pack);

                if (cursor && cursor.isAnimated)
                    return true;
            }

            return false;
        }

        private static Texture2D CheckerTexture
        {
            get
            {
                if (_checkerTexture)
                    return _checkerTexture;

                _checkerTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Repeat
                };
                _checkerTexture.SetPixels32(new[]
                {
                    new Color32(74, 74, 74, 255),
                    new Color32(96, 96, 96, 255),
                    new Color32(96, 96, 96, 255),
                    new Color32(74, 74, 74, 255)
                });
                _checkerTexture.Apply();
                return _checkerTexture;
            }
        }

        private readonly struct CursorPreview
        {
            public readonly string label;
            private readonly System.Func<CursorPack, VirtualCursorBase> _getCursor;

            public CursorPreview(string label, System.Func<CursorPack, VirtualCursorBase> getCursor)
            {
                this.label = label;
                _getCursor = getCursor;
            }

            public VirtualCursorBase GetCursor(CursorPack pack)
            {
                return _getCursor(pack);
            }
        }
    }
}
