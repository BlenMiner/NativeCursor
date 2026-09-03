#if !UNITY_EDITOR && UNITY_WEBGL

using System.Runtime.InteropServices;
using UnityEngine;

namespace Riten.Native.Cursors
{
    /// <summary>
    /// WebGL cursor service.
    ///
    /// Unity's WebGL runtime implements Cursor.visible by writing "none" or "default" to the canvas
    /// cursor style, which is the same property this service writes. The service therefore tracks
    /// visibility itself: a cursor change while hidden keeps the cursor hidden, and showing the cursor
    /// again restores the requested shape instead of the browser default.
    /// </summary>
    public class WebGlCursorService : MonoBehaviour, ICursorService, ICursorServiceLifecycle
    {
        [DllImport("__Internal")]
        private static extern void SetCursorStyle(string cursor);

        private NTCursors _activeCursor = NTCursors.Default;
        private bool _serviceActive;
        private bool _lastVisible;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Setup()
        {
            var go = new GameObject("NativeCursor#WebGlCursorService")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            DontDestroyOnLoad(go);

            var service = go.AddComponent<WebGlCursorService>();
            NativeCursor.SetFallbackService(service);
            NativeCursor.SetService(service);
        }

        // LateUpdate runs after user scripts have toggled Cursor.visible in the same frame, so Unity's
        // "default" write is overridden before the frame is presented.
        private void LateUpdate()
        {
            if (!_serviceActive)
                return;

            var visible = Cursor.visible;

            if (visible == _lastVisible)
                return;

            _lastVisible = visible;
            Apply();
        }

        private void Apply()
        {
            SetCursorStyle(_lastVisible ? ToCssCursor(_activeCursor) : "none");
        }

        private static string ToCssCursor(NTCursors cursor)
        {
            return cursor switch
            {
                NTCursors.Default => "default",
                NTCursors.Arrow => "default",
                NTCursors.IBeam => "text",
                NTCursors.Crosshair => "crosshair",
                NTCursors.Link => "pointer",
                NTCursors.Busy => "wait",
                NTCursors.Invalid => "not-allowed",
                NTCursors.ResizeVertical => "ns-resize",
                NTCursors.ResizeHorizontal => "ew-resize",
                NTCursors.ResizeDiagonalLeft => "nwse-resize",
                NTCursors.ResizeDiagonalRight => "nesw-resize",
                NTCursors.ResizeAll => "move",
                NTCursors.OpenHand => "grab",
                NTCursors.ClosedHand => "grabbing",
                _ => "default"
            };
        }

        public bool SetCursor(NTCursors cursor)
        {
            _activeCursor = cursor;

            if (!_serviceActive)
                return true;

            _lastVisible = Cursor.visible;
            Apply();
            return true;
        }

        public void ResetCursor()
        {
            SetCursor(NTCursors.Default);
        }

        public void OnActivated()
        {
            _serviceActive = true;
            _lastVisible = Cursor.visible;
            Apply();
        }

        public void OnDeactivated()
        {
            _serviceActive = false;
        }
    }
}

#endif
