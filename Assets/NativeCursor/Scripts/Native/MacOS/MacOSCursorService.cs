#if !UNITY_EDITOR && UNITY_STANDALONE_OSX
using System.Runtime.InteropServices;
using UnityEngine;

namespace Riten.Native.Cursors
{
    public class MacOSCursorService : MonoBehaviour, ICursorService, ICursorServiceLifecycle
    {
        private bool _serviceActive;
        private NTCursors _activeCursor = NTCursors.Default;

        [DllImport("CursorWrapper")]
        private static extern void SetCursorToArrow();

        [DllImport("CursorWrapper")]
        private static extern void SetCursorToIBeam();

        [DllImport("CursorWrapper")]
        private static extern void SetCursorToCrosshair();

        [DllImport("CursorWrapper")]
        private static extern void SetCursorToOpenHand();
        
        [DllImport("CursorWrapper")]
        private static extern void SetCursorToClosedHand();

        [DllImport("CursorWrapper")]
        private static extern void SetCursorToResizeLeftRight();

        [DllImport("CursorWrapper")]
        private static extern void SetCursorToResizeUp();

        [DllImport("CursorWrapper")]
        private static extern void SetCursorToResizeDown();

        [DllImport("CursorWrapper")]
        private static extern void SetCursorToResizeUpDown();

        [DllImport("CursorWrapper")]
        private static extern void SetCursorToOperationNotAllowed();

        [DllImport("CursorWrapper")]
        private static extern void SetCursorToPointingHand();
        
        [DllImport("CursorWrapper")]
        private static extern void SetCursorToBusy();

        [DllImport("CursorWrapper")]
        private static extern void ReapplyNativeCursor();

        [DllImport("CursorWrapper")]
        private static extern void DisableNativeCursorOverride();
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Setup()
        {
            var go = new GameObject("NativeCursor#MacOSCursorService")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            DontDestroyOnLoad(go);

            var service = go.AddComponent<MacOSCursorService>();
            NativeCursor.SetFallbackService(service);
            NativeCursor.SetService(service);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (_serviceActive && hasFocus)
                ReapplyNativeCursor();
        }

        private void OnEnable()
        {
            if (_serviceActive)
                SetCursor(_activeCursor);
        }

        private void OnDisable()
        {
            DisableNativeCursorOverride();
        }

        private void OnDestroy()
        {
            _serviceActive = false;
            DisableNativeCursorOverride();
        }

        public bool SetCursor(NTCursors cursor)
        {
            _activeCursor = cursor;

            switch (cursor)
            {
                case NTCursors.Default:
                case NTCursors.Arrow: SetCursorToArrow(); return true;
                
                case NTCursors.IBeam: SetCursorToIBeam(); return true;
                case NTCursors.Crosshair: SetCursorToCrosshair(); return true;
                case NTCursors.Link: SetCursorToPointingHand(); return true;
                case NTCursors.Busy: SetCursorToBusy(); return true;
                case NTCursors.Invalid: SetCursorToOperationNotAllowed(); return true;
                case NTCursors.ResizeVertical: SetCursorToResizeUpDown(); return true;
                case NTCursors.ResizeHorizontal: SetCursorToResizeLeftRight(); return true;
                case NTCursors.ResizeDiagonalLeft: SetCursorToResizeUp(); return true;
                case NTCursors.ResizeDiagonalRight: SetCursorToResizeDown(); return true;
                case NTCursors.ResizeAll: SetCursorToArrow(); return true;
                case NTCursors.OpenHand: SetCursorToOpenHand(); return true;
                case NTCursors.ClosedHand: SetCursorToClosedHand(); return true;
                default: return false;
            }
        }

        public void ResetCursor()
        {
            SetCursor(NTCursors.Default);
        }

        public void OnActivated()
        {
            _serviceActive = true;
        }

        public void OnDeactivated()
        {
            _serviceActive = false;
            DisableNativeCursorOverride();
        }
    }
}

#endif
