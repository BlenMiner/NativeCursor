#if !UNITY_EDITOR && UNITY_STANDALONE_LINUX

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Riten.Native.Cursors
{
    internal class LinuxCursorService : MonoBehaviour, ICursorService
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Setup()
        {
            var go = new GameObject("NativeCursor#LinuxCursorService")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            DontDestroyOnLoad(go);

            var service = go.AddComponent<LinuxCursorService>();
            NativeCursor.SetFallbackService(service);
            NativeCursor.SetService(service);
        }
        
        [DllImport("libX11")]
        static extern IntPtr XOpenDisplay(string display);

        [DllImport("libX11")]
        static extern IntPtr XRootWindow(IntPtr display, int screen);

        [DllImport("libX11")]
        static extern int XGetInputFocus(IntPtr display, out IntPtr focusReturn, out int revertToReturn);
        
        [DllImport("libX11")]
        static extern IntPtr XCreateFontCursor(IntPtr display, uint shape);
        
        [DllImport("libX11")]
        static extern int XDefineCursor(IntPtr display, IntPtr window, IntPtr cursor);

        [DllImport("libX11")]
        static extern int XUndefineCursor(IntPtr display, IntPtr window);

        [DllImport("libX11")]
        static extern int XFlush(IntPtr display);

        [DllImport("libX11")]
        static extern int XFreeCursor(IntPtr display, IntPtr cursor);

        [DllImport("libX11")]
        static extern int XCloseDisplay(IntPtr display);
        
        private const uint XC_arrow = 2;                    // Arrow
        private const uint XC_xterm = 152;                  // IBeam
        private const uint XC_crosshair = 34;               // Crosshair
        private const uint XC_hand1 = 58;                   // Link
        private const uint XC_watch = 150;                  // Busy
        private const uint XC_X_cursor = 0;                 // Invalid
        private const uint XC_sb_v_double_arrow = 116;      // ResizeVertical
        private const uint XC_sb_h_double_arrow = 108;      // ResizeHorizontal
        private const uint XC_bottom_left_corner = 12;      // ResizeDiagonalLeft
        private const uint XC_bottom_right_corner = 14;     // ResizeDiagonalRight
        private const uint XC_fleur = 52;                   // ResizeAll
        private const uint XC_hand2 = 60;                   // DragDrop

        readonly Dictionary<NTCursors, IntPtr> _cursors = new ();

        private IntPtr _display;
        private IntPtr _window;
        private NTCursors _activeCursor = NTCursors.Default;
        
        IntPtr Load(uint cursor)
        {
            if (_display == IntPtr.Zero)
                return IntPtr.Zero;

            return XCreateFontCursor(_display, cursor);
        }
        
        IntPtr LoadCursor(NTCursors nativeCursor)
        {
            if (_cursors.TryGetValue(nativeCursor, out var cursor))
                return cursor;
            
            cursor = nativeCursor switch
            {
                NTCursors.Default => Load(XC_arrow),
                NTCursors.Arrow => Load(XC_arrow),
                NTCursors.IBeam => Load(XC_xterm),
                NTCursors.Crosshair => Load(XC_crosshair),
                NTCursors.Link => Load(XC_hand2),
                NTCursors.Busy => Load(XC_watch),
                NTCursors.Invalid => Load(XC_X_cursor),
                NTCursors.ResizeVertical => Load(XC_sb_v_double_arrow),
                NTCursors.ResizeHorizontal => Load(XC_sb_h_double_arrow),
                NTCursors.ResizeDiagonalLeft => Load(XC_bottom_left_corner),
                NTCursors.ResizeDiagonalRight => Load(XC_bottom_right_corner),
                NTCursors.ResizeAll => Load(XC_fleur),
                NTCursors.OpenHand => Load(XC_hand1),
                NTCursors.ClosedHand => Load(XC_hand1),
                _ => throw new ArgumentOutOfRangeException(nameof(nativeCursor), nativeCursor, null)
            };
            
            _cursors.Add(nativeCursor, cursor);
            return cursor;
        }

        private void Awake()
        {
            _display = XOpenDisplay(null);
            
            if (_display == IntPtr.Zero)
            {
                Debug.LogError("Failed to open display");
                return;
            }
            
            _window = GetTargetWindow();
            
            if (_window == IntPtr.Zero)
            {
                Debug.LogError("Failed to get cursor target window");
                return;
            }
            
            Debug.Log($"Display: {_display}; CursorWindow: {_window}");
        }

        private void OnDestroy()
        {
            if (_display == IntPtr.Zero)
                return;

            foreach (var cursor in _cursors.Values)
            {
                if (cursor != IntPtr.Zero)
                    XFreeCursor(_display, cursor);
            }

            _cursors.Clear();
            XCloseDisplay(_display);
            _display = IntPtr.Zero;
            _window = IntPtr.Zero;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
                SetCursor(_activeCursor);
        }

        private IntPtr GetTargetWindow()
        {
            if (_display == IntPtr.Zero)
                return IntPtr.Zero;

            XGetInputFocus(_display, out var focusedWindow, out _);

            if (focusedWindow == IntPtr.Zero || focusedWindow == new IntPtr(1))
                focusedWindow = XRootWindow(_display, 0);

            return focusedWindow;
        }

        public bool SetCursor(NTCursors nativeCursorName)
        {
            _activeCursor = nativeCursorName;

            if (_display == IntPtr.Zero)
                return false;

            _window = GetTargetWindow();

            if (_window == IntPtr.Zero)
                return false;

            if (nativeCursorName == NTCursors.Default)
            {
                XUndefineCursor(_display, _window);
                XFlush(_display);
                return true;
            }

            var cursor = LoadCursor(nativeCursorName);

            if (cursor == IntPtr.Zero)
            {
                XFlush(_display);
                return false;
            }

            XDefineCursor(_display, _window, cursor);
            XFlush(_display);
            return true;
        }

        public void ResetCursor()
        {
            SetCursor(NTCursors.Default);
        }
    }
}

#endif
