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
        static extern int XFlush(IntPtr display);

        [DllImport("libX11")]
        static extern int XFreeCursor(IntPtr display, IntPtr cursor);

        [DllImport("libX11")]
        static extern int XCloseDisplay(IntPtr display);

        [DllImport("libX11")]
        static extern int XPending(IntPtr display);

        [DllImport("libX11")]
        static extern int XNextEvent(IntPtr display, out XEvent xevent);

        [DllImport("libXfixes")]
        static extern bool XFixesQueryExtension(IntPtr display, out int eventBase, out int errorBase);

        [DllImport("libXfixes")]
        static extern void XFixesSelectCursorInput(IntPtr display, IntPtr window, long eventMask);
        
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
        private const int XFixesCursorNotify = 1;
        private const long XFixesDisplayCursorNotifyMask = 1;

        readonly Dictionary<NTCursors, IntPtr> _cursors = new ();

        private IntPtr _display;
        private IntPtr _window;
        private NTCursors _activeCursor = NTCursors.Default;
        private bool _hasFocus = true;
        private bool _hasCursorNotifications;
        private bool _ignoreNextCursorNotify;
        private float _ignoreNextCursorNotifyUntil;
        private int _xfixesCursorNotifyEvent;
        private float _nextWindowRefreshTime;
        private float _nextFallbackReapplyTime;

        [StructLayout(LayoutKind.Sequential, Size = 192)]
        private struct XEvent
        {
            public int type;
        }
        
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
            
            TryInitializeCursorNotifications();
            RefreshTargetWindow();
            
            if (_window == IntPtr.Zero)
            {
                Debug.LogError("Failed to get cursor target window");
                return;
            }
            
            Debug.Log($"Display: {_display}; CursorWindow: {_window}");
        }

        private void Update()
        {
            if (_display == IntPtr.Zero || !_hasFocus)
                return;

            if (Time.unscaledTime >= _nextWindowRefreshTime)
            {
                _nextWindowRefreshTime = Time.unscaledTime + 0.25f;
                RefreshTargetWindow();
            }

            if (_hasCursorNotifications)
            {
                if (ProcessCursorEvents())
                    ApplyCursor(_activeCursor);

                return;
            }

            if (Time.unscaledTime >= _nextFallbackReapplyTime)
            {
                _nextFallbackReapplyTime = Time.unscaledTime + 0.5f;
                ApplyCursor(_activeCursor);
            }
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
            _hasFocus = hasFocus;

            if (!hasFocus)
                return;

            RefreshTargetWindow();
            ApplyCursor(_activeCursor);
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

        private void TryInitializeCursorNotifications()
        {
            try
            {
                if (!XFixesQueryExtension(_display, out var eventBase, out _))
                    return;

                _xfixesCursorNotifyEvent = eventBase + XFixesCursorNotify;
                _hasCursorNotifications = true;
            }
            catch (DllNotFoundException)
            {
                _hasCursorNotifications = false;
            }
            catch (EntryPointNotFoundException)
            {
                _hasCursorNotifications = false;
            }
        }

        private void RefreshTargetWindow()
        {
            var window = GetTargetWindow();

            if (window == IntPtr.Zero || window == _window)
                return;

            _window = window;
            SelectCursorNotifications();
            ApplyCursor(_activeCursor);
        }

        private void SelectCursorNotifications()
        {
            if (!_hasCursorNotifications || _window == IntPtr.Zero)
                return;

            XFixesSelectCursorInput(_display, _window, XFixesDisplayCursorNotifyMask);
        }

        private bool ProcessCursorEvents()
        {
            if (!_hasCursorNotifications)
                return false;

            var shouldReapply = false;

            while (XPending(_display) > 0)
            {
                XNextEvent(_display, out var xevent);

                if (xevent.type != _xfixesCursorNotifyEvent)
                    continue;

                if (_ignoreNextCursorNotify && Time.unscaledTime > _ignoreNextCursorNotifyUntil)
                    _ignoreNextCursorNotify = false;

                if (_ignoreNextCursorNotify)
                {
                    _ignoreNextCursorNotify = false;
                    continue;
                }

                shouldReapply = true;
            }

            return shouldReapply;
        }

        public bool SetCursor(NTCursors nativeCursorName)
        {
            _activeCursor = nativeCursorName;
            return ApplyCursor(nativeCursorName);
        }

        private bool ApplyCursor(NTCursors nativeCursorName)
        {
            if (_display == IntPtr.Zero)
                return false;

            RefreshTargetWindow();

            if (_window == IntPtr.Zero)
                return false;

            var cursor = LoadCursor(nativeCursorName);

            if (cursor == IntPtr.Zero)
            {
                XFlush(_display);
                return false;
            }

            XDefineCursor(_display, _window, cursor);
            XFlush(_display);
            _ignoreNextCursorNotify = true;
            _ignoreNextCursorNotifyUntil = Time.unscaledTime + 0.1f;
            return true;
        }

        public void ResetCursor()
        {
            SetCursor(NTCursors.Default);
        }
    }
}

#endif
