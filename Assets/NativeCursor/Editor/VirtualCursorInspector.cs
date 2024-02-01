using UnityEditor;
using UnityEngine;

namespace Riten.Native.Cursors.Editor
{
    [CustomEditor(typeof(VirtualCursor))]
    public class VirtualCursorInspector : UnityEditor.Editor
    {
        VirtualCursor _target;
        Texture2D _originalTexture;
        Texture2D _adaptedTexture;
        
        bool _dragging;

        private void RefreshTexture()
        {
            if (_originalTexture == _target.texture) return;
            
            _originalTexture = _target.texture;

            if (!_originalTexture)
            {
                _adaptedTexture = null;
                return;
            }
            
            _adaptedTexture = new Texture2D(_originalTexture.width, _originalTexture.height, TextureFormat.RGBA32, false);
            _adaptedTexture.SetPixels(_originalTexture.GetPixels());
            _adaptedTexture.filterMode = FilterMode.Point;
            _adaptedTexture.Apply();
        }
        
        private void OnEnable()
        {
            _target = (VirtualCursor)target;
            RefreshTexture();
        }

        void DragHotspot(Rect texture)
        {
            var uv = new Vector2(
                (Event.current.mousePosition.x - texture.x) / texture.width,
                (Event.current.mousePosition.y - texture.y - 105) / texture.height
            );
            
            uv.x = Mathf.Clamp(uv.x, 0f, 1f);
            uv.y = Mathf.Clamp(uv.y, 0f, 1f);
            
            int pixelX = Mathf.FloorToInt(uv.x * _adaptedTexture.width);
            int pixelY = Mathf.FloorToInt(uv.y * _adaptedTexture.height);
            
            pixelX = Mathf.Clamp(pixelX, 0, _adaptedTexture.width - 1);
            pixelY = Mathf.Clamp(pixelY, 0, _adaptedTexture.height - 1);
            
            uv.x = pixelX / (float)_adaptedTexture.width;
            uv.y = pixelY / (float)_adaptedTexture.height;
            
            _target.hotspot = uv;
        }

        public override void OnInspectorGUI()
        {
            if (!_target) return;

            var width = Screen.width;

            GUILayout.Label("Preview", EditorStyles.boldLabel);

            _target.texture = (Texture2D)EditorGUILayout.ObjectField("Texture", _target.texture, typeof(Texture2D), false);
            _target.hotspot = EditorGUILayout.Vector2Field("Hotspot", _target.hotspot);
            
            RefreshTexture();

            if (!_target.texture)
            {
                GUILayout.Label("No texture selected");
                return;
            }
            
            var textureRect = GUILayoutUtility.GetRect(width, width);
            textureRect.x = 0;
            textureRect.width = width;
            textureRect.height = width;

            GUI.DrawTexture(textureRect, _adaptedTexture, ScaleMode.StretchToFill);
            GUI.DrawTexture(new Rect(
                width * 0.5f - _adaptedTexture.width * 0.5f, 
                width + textureRect.y,
                _adaptedTexture.width, 
                _adaptedTexture.height), 
                _adaptedTexture
            );

            float onePixel = width / (float)_adaptedTexture.width;
            
            var arcRect = new Rect(
                width * _target.hotspot.x, 
                width * _target.hotspot.y + textureRect.y, 
                onePixel, 
                onePixel);
            
            float arcRadius = arcRect.width * 0.5f;
            
            if (arcRect.Contains(Event.current.mousePosition))
            {
                if (!_dragging)
                {
                    Handles.color = Color.green;
                    Handles.DrawSolidArc(
                        new Vector3(arcRect.x + arcRadius, arcRect.y + arcRadius),
                        Vector3.forward,
                        Vector3.up,
                        360,
                        arcRadius * 2f
                    );
                }

                if (!_dragging && Event.current.type == EventType.MouseDown)
                    _dragging = true;
            }

            if (_dragging && Event.current.type == EventType.MouseUp)
                _dragging = false;

            Handles.color = Color.red;
            Handles.DrawSolidArc(
                new Vector3(arcRect.x + arcRadius, arcRect.y + arcRadius), 
                Vector3.forward, 
                Vector3.up, 
                360, 
                arcRadius * .5f
            );

            if (_dragging)
                DragHotspot(textureRect);

            Repaint();
        }
    }
}