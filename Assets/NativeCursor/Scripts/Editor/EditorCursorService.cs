using System.Collections.Generic;
using HarmonyLib;
using UnityEditor;
using UnityEngine;

namespace Riten.Native.Cursors.Editor
{
    [HarmonyPatch(typeof(EditorGUIUtility), "Internal_AddCursorRect", typeof(Rect), typeof(MouseCursor), typeof(int))]
    internal static class EditorCursorPatch
    {
        public static MouseCursor? targetCursor;
        
        public static void Prefix(Rect r, ref MouseCursor m, int controlID)
        {
            if (m == MouseCursor.CustomCursor && targetCursor != null)
                m = targetCursor.Value;
        }
    }
    
    public class EditorCursorService : ICursorService
    {
        Harmony _harmony;
        private bool _patched;

        private void OnEnable()
        {
            _harmony = new Harmony("com.riten.nativecursor");

            try
            {
                _harmony.PatchAll();
                _patched = true;
            }
            catch
            {
                _patched = false;
                SetCursor(NTCursors.Default);
            }
        }

        private void OnDisable()
        {
            _harmony?.UnpatchAll();
            _harmony = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Setup()
        {
            var service = new EditorCursorService();
            service.OnEnable();
            NativeCursor.SetService(service);

            EditorApplication.playModeStateChanged += service.OnPlayModeStateChanged;
        }
        
        void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            
            if (state == PlayModeStateChange.ExitingPlayMode)
                OnDisable();
        }

        readonly Dictionary<string, Sprite> _loadedCursors = new ();

        void LoadCursor(string text)
        {
            if (_loadedCursors.TryGetValue(text, out var cursor))
            {
                Cursor.SetCursor(cursor.texture, cursor.pivot, CursorMode.Auto);
                return;
            }
            
            var sprite = Resources.Load<Sprite>("MacOS/" + text);
            var texture = sprite.texture;
            _loadedCursors.Add(text, sprite);

            Cursor.SetCursor(texture, sprite.pivot, CursorMode.Auto);
        }

        private bool SetCustomCursor(NTCursors cursor)
        {
            switch (cursor)
            {
                case NTCursors.Default:
                case NTCursors.Arrow: LoadCursor("default"); break;
                case NTCursors.Link: LoadCursor("pointer"); break;
                case NTCursors.IBeam: LoadCursor("textcursor"); break;
                case NTCursors.Crosshair: LoadCursor("cross"); break;
                case NTCursors.ResizeVertical: LoadCursor("resizenorthsouth"); break;
                case NTCursors.ResizeHorizontal: LoadCursor("resizewesteast"); break;
                case NTCursors.Busy: LoadCursor("beachball"); break;
                case NTCursors.Invalid: LoadCursor("notallowed"); break;
                case NTCursors.ResizeDiagonalLeft: LoadCursor("resizenorthwestsoutheast"); break;
                case NTCursors.ResizeDiagonalRight: LoadCursor("resizenortheastsouthwest"); break;
                case NTCursors.ResizeAll: LoadCursor("move"); break;
                case NTCursors.OpenHand: LoadCursor("handopen"); break;
                case NTCursors.ClosedHand: LoadCursor("handgrabbing"); break;
                
                default:
                    LoadCursor("default");
                    break;
            }

            return true;
        }
        
        public bool SetCursor(NTCursors ntCursorName)
        {
            if (!_patched)
            {
                return SetCustomCursor(ntCursorName);
            }
            
            EditorCursorPatch.targetCursor = ntCursorName switch
            {
                NTCursors.Default => null,
                NTCursors.Arrow => MouseCursor.Arrow,
                NTCursors.IBeam => MouseCursor.Text,
                NTCursors.Crosshair => MouseCursor.ArrowPlus,
                NTCursors.Link => MouseCursor.Link,
                NTCursors.ResizeVertical => MouseCursor.ResizeVertical,
                NTCursors.ResizeHorizontal => MouseCursor.ResizeHorizontal,
                NTCursors.Busy => MouseCursor.RotateArrow,
                NTCursors.Invalid => MouseCursor.ArrowMinus,
                NTCursors.ResizeDiagonalLeft => MouseCursor.ResizeUpLeft,
                NTCursors.ResizeDiagonalRight => MouseCursor.ResizeUpRight,
                NTCursors.ResizeAll => MouseCursor.ScaleArrow,
                NTCursors.OpenHand => MouseCursor.Pan,
                NTCursors.ClosedHand => MouseCursor.Pan,
                _ => null
            };
            
            return true;
        }

        public void ResetCursor()
        {
            if (!_patched)
            {
                SetCustomCursor(NTCursors.Default);
                return;
            }
            
            EditorCursorPatch.targetCursor = null;
        }
    }
}
