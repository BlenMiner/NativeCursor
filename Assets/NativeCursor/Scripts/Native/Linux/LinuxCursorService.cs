#if !UNITY_EDITOR && UNITY_STANDALONE_LINUX

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Riten.Native.Cursors
{
    /// <summary>
    /// Linux (X11) cursor service.
    ///
    /// Opens its own X connection and defines the requested cursor on every top-level window owned by
    /// this process. Windows are found through the window manager's _NET_CLIENT_LIST and the
    /// _NET_WM_PID property the player sets on its windows, so the cursor is never defined on another
    /// application's window. XFixes cursor notifications reapply the cursor after Unity or SDL replace
    /// it; without XFixes a low-frequency reapply is used while focused.
    ///
    /// X errors raised by the service's own requests (for example a window destroyed between a scan and
    /// a define) are swallowed by a temporary error handler instead of terminating the process.
    ///
    /// Under native Wayland there is no X11 window owned by the process and the service stays idle.
    /// </summary>
    internal class LinuxCursorService : MonoBehaviour, ICursorService, ICursorServiceLifecycle
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Setup()
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
        private static extern IntPtr XOpenDisplay(string display);

        [DllImport("libX11")]
        private static extern int XCloseDisplay(IntPtr display);

        [DllImport("libX11")]
        private static extern IntPtr XDefaultRootWindow(IntPtr display);

        [DllImport("libX11")]
        private static extern IntPtr XInternAtom(IntPtr display, string atomName, [MarshalAs(UnmanagedType.Bool)] bool onlyIfExists);

        [DllImport("libX11")]
        private static extern int XGetWindowProperty(
            IntPtr display,
            IntPtr window,
            IntPtr property,
            long offset,
            long length,
            [MarshalAs(UnmanagedType.Bool)] bool delete,
            IntPtr requestedType,
            out IntPtr actualType,
            out int actualFormat,
            out UIntPtr itemCount,
            out UIntPtr bytesAfter,
            out IntPtr data);

        [DllImport("libX11")]
        private static extern int XFree(IntPtr data);

        [DllImport("libX11")]
        private static extern int XGetInputFocus(IntPtr display, out IntPtr focusReturn, out int revertToReturn);

        [DllImport("libX11")]
        private static extern IntPtr XCreateFontCursor(IntPtr display, uint shape);

        [DllImport("libX11")]
        private static extern int XDefineCursor(IntPtr display, IntPtr window, IntPtr cursor);

        [DllImport("libX11")]
        private static extern int XUndefineCursor(IntPtr display, IntPtr window);

        [DllImport("libX11")]
        private static extern int XFlush(IntPtr display);

        [DllImport("libX11")]
        private static extern int XSync(IntPtr display, [MarshalAs(UnmanagedType.Bool)] bool discard);

        [DllImport("libX11")]
        private static extern int XFreeCursor(IntPtr display, IntPtr cursor);

        [DllImport("libX11")]
        private static extern int XPending(IntPtr display);

        [DllImport("libX11")]
        private static extern int XNextEvent(IntPtr display, out XEvent xevent);

        [DllImport("libX11", EntryPoint = "XSetErrorHandler")]
        private static extern IntPtr XSetErrorHandlerDelegate(XErrorHandlerDelegate handler);

        [DllImport("libX11", EntryPoint = "XSetErrorHandler")]
        private static extern IntPtr XSetErrorHandlerPointer(IntPtr handler);

        [DllImport("libXfixes")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool XFixesQueryExtension(IntPtr display, out int eventBase, out int errorBase);

        [DllImport("libXfixes")]
        private static extern void XFixesSelectCursorInput(IntPtr display, IntPtr window, ulong eventMask);

        [DllImport("libc")]
        private static extern int getpid();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int XErrorHandlerDelegate(IntPtr display, IntPtr errorEvent);

        private const uint XC_X_cursor = 0;                 // Invalid
        private const uint XC_arrow = 2;                    // Arrow
        private const uint XC_bottom_left_corner = 12;      // NE-SW diagonal
        private const uint XC_bottom_right_corner = 14;     // NW-SE diagonal
        private const uint XC_crosshair = 34;               // Crosshair
        private const uint XC_fleur = 52;                   // ResizeAll
        private const uint XC_hand1 = 58;                   // Grab hand
        private const uint XC_hand2 = 60;                   // Link
        private const uint XC_sb_h_double_arrow = 108;      // ResizeHorizontal
        private const uint XC_sb_v_double_arrow = 116;      // ResizeVertical
        private const uint XC_watch = 150;                  // Busy
        private const uint XC_xterm = 152;                  // IBeam

        private const int Success = 0;
        private const int XFixesCursorNotify = 1;
        private const ulong XFixesDisplayCursorNotifyMask = 1;
        private static readonly IntPtr XA_CARDINAL = new(6);
        private static readonly IntPtr XA_WINDOW = new(33);
        private static readonly IntPtr AnyPropertyType = IntPtr.Zero;
        private static readonly IntPtr PointerRoot = new(1);

        private const float WindowScanInterval = 0.25f;
        private const float FallbackReapplyInterval = 0.5f;
        private const float OwnCursorNotifyGracePeriod = 0.1f;
        private const float MissingWindowWarningDelay = 5f;

        // Kept alive for the lifetime of the process: the native thunk must outlive every XSetErrorHandler call.
        private static readonly XErrorHandlerDelegate IgnoreErrorHandler = IgnoreXError;

        [StructLayout(LayoutKind.Sequential, Size = 192)]
        private struct XEvent
        {
            public int type;
        }

        private readonly Dictionary<NTCursors, IntPtr> _cursors = new();
        private readonly List<IntPtr> _windows = new();
        private readonly List<IntPtr> _scannedWindows = new();

        private IntPtr _display;
        private IntPtr _rootWindow;
        private IntPtr _netClientListAtom;
        private IntPtr _netWmPidAtom;
        private long _processId;

        private NTCursors _activeCursor = NTCursors.Default;
        private bool _hasFocus;
        private bool _serviceActive;
        private bool _hasCursorNotifications;
        private bool _ignoreNextCursorNotify;
        private float _ignoreNextCursorNotifyUntil;
        private int _xfixesCursorNotifyEvent;
        private float _nextWindowScanTime;
        private float _nextFallbackReapplyTime;
        private float _missingWindowWarningTime;
        private bool _missingWindowWarned;

        private bool IsReady => _display != IntPtr.Zero;

        private bool OverrideEnabled => IsReady && _serviceActive && isActiveAndEnabled;

        private void Awake()
        {
            _hasFocus = Application.isFocused;
            _processId = getpid();

            try
            {
                _display = XOpenDisplay(null);
            }
            catch (DllNotFoundException)
            {
                Debug.LogError("Native Cursor could not load libX11. Linux native cursor support is disabled.");
                enabled = false;
                return;
            }
            catch (EntryPointNotFoundException)
            {
                Debug.LogError("Native Cursor could not find the required libX11 entry points. Linux native cursor support is disabled.");
                enabled = false;
                return;
            }

            if (_display == IntPtr.Zero)
            {
                Debug.LogWarning("Native Cursor could not open an X11 display. Linux native cursor support is disabled.");
                enabled = false;
                return;
            }

            _rootWindow = XDefaultRootWindow(_display);
            _netClientListAtom = XInternAtom(_display, "_NET_CLIENT_LIST", true);
            _netWmPidAtom = XInternAtom(_display, "_NET_WM_PID", true);
            _missingWindowWarningTime = Time.unscaledTime + MissingWindowWarningDelay;

            TryInitializeCursorNotifications();
        }

        private void OnEnable()
        {
            if (_serviceActive)
                Activate();
        }

        private void OnDisable()
        {
            Deactivate();
        }

        private void OnDestroy()
        {
            _serviceActive = false;

            if (!IsReady)
                return;

            Deactivate();

            foreach (var cursor in _cursors.Values)
            {
                if (cursor != IntPtr.Zero)
                    XFreeCursor(_display, cursor);
            }

            _cursors.Clear();
            XCloseDisplay(_display);
            _display = IntPtr.Zero;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            _hasFocus = hasFocus;

            if (!hasFocus || !OverrideEnabled)
                return;

            RefreshWindows();
            ApplyCursor();
        }

        private void Update()
        {
            if (!IsReady)
                return;

            // Always drain our connection so the event queue cannot grow while unfocused or inactive.
            var cursorChangedElsewhere = ProcessCursorEvents();

            if (!OverrideEnabled)
                return;

            if (Time.unscaledTime >= _nextWindowScanTime)
            {
                _nextWindowScanTime = Time.unscaledTime + WindowScanInterval;

                if (RefreshWindows())
                    ApplyCursor();
            }

            if (!_hasFocus)
                return;

            if (_hasCursorNotifications)
            {
                if (cursorChangedElsewhere)
                    ApplyCursor();

                return;
            }

            if (Time.unscaledTime >= _nextFallbackReapplyTime)
            {
                _nextFallbackReapplyTime = Time.unscaledTime + FallbackReapplyInterval;
                ApplyCursor();
            }
        }

        private void Activate()
        {
            if (!IsReady)
                return;

            RefreshWindows();
            ApplyCursor();
        }

        private void Deactivate()
        {
            _ignoreNextCursorNotify = false;

            if (!IsReady || _windows.Count == 0)
                return;

            // Hand the window back to Unity/SDL so their own cursor shows again.
            var previous = BeginIgnoringErrors();

            foreach (var window in _windows)
                XUndefineCursor(_display, window);

            XSync(_display, false);
            EndIgnoringErrors(previous);
            _windows.Clear();
        }

        // ---- Window discovery -------------------------------------------------------------------

        /// <summary>
        /// Rebuilds the list of top-level X windows owned by this process.
        /// Returns true when the set of windows changed.
        /// </summary>
        private bool RefreshWindows()
        {
            if (!IsReady)
                return false;

            _scannedWindows.Clear();

            var previous = BeginIgnoringErrors();
            CollectOwnWindows(_scannedWindows);
            EndIgnoringErrors(previous);

            var changed = _scannedWindows.Count != _windows.Count;

            if (!changed)
            {
                foreach (var window in _scannedWindows)
                {
                    if (_windows.Contains(window))
                        continue;

                    changed = true;
                    break;
                }
            }

            if (changed)
            {
                _windows.Clear();
                _windows.AddRange(_scannedWindows);
            }

            if (_windows.Count == 0)
            {
                if (!_missingWindowWarned && Time.unscaledTime >= _missingWindowWarningTime)
                {
                    _missingWindowWarned = true;
                    Debug.LogWarning("Native Cursor could not find an X11 window owned by this process. " +
                                     "Native cursor override is inactive; this is expected under native Wayland.");
                }
            }
            else
            {
                _missingWindowWarned = false;
                _missingWindowWarningTime = Time.unscaledTime + MissingWindowWarningDelay;
            }

            return changed;
        }

        private void CollectOwnWindows(List<IntPtr> result)
        {
            if (_netClientListAtom != IntPtr.Zero && _netWmPidAtom != IntPtr.Zero)
            {
                if (TryReadWindowList(_rootWindow, _netClientListAtom, result))
                {
                    for (var i = result.Count - 1; i >= 0; i--)
                    {
                        if (!IsOwnedByThisProcess(result[i]))
                            result.RemoveAt(i);
                    }

                    return;
                }
            }

            // No EWMH window manager: fall back to the focus window, but only while Unity reports focus
            // and the window either carries our pid or cannot be identified at all.
            if (!_hasFocus)
                return;

            XGetInputFocus(_display, out var focused, out _);

            if (focused == IntPtr.Zero || focused == PointerRoot || focused == _rootWindow)
                return;

            if (_netWmPidAtom == IntPtr.Zero || IsOwnedByThisProcess(focused))
                result.Add(focused);
        }

        private bool IsOwnedByThisProcess(IntPtr window)
        {
            return TryReadCardinal(window, _netWmPidAtom, out var pid) && pid == _processId;
        }

        private bool TryReadWindowList(IntPtr window, IntPtr property, List<IntPtr> result)
        {
            var status = XGetWindowProperty(
                _display, window, property, 0, long.MaxValue / 4, false, XA_WINDOW,
                out var actualType, out var actualFormat, out var itemCount, out _, out var data);

            if (status != Success || data == IntPtr.Zero)
                return false;

            try
            {
                if (actualType != XA_WINDOW || actualFormat != 32)
                    return false;

                // Xlib returns 32-bit format properties as native longs (8 bytes on 64-bit).
                var count = (int)Math.Min((ulong)itemCount, int.MaxValue);

                for (var i = 0; i < count; i++)
                    result.Add(Marshal.ReadIntPtr(data, i * IntPtr.Size));

                return true;
            }
            finally
            {
                XFree(data);
            }
        }

        private bool TryReadCardinal(IntPtr window, IntPtr property, out long value)
        {
            value = 0;

            var status = XGetWindowProperty(
                _display, window, property, 0, 1, false, XA_CARDINAL,
                out var actualType, out var actualFormat, out var itemCount, out _, out var data);

            if (status != Success || data == IntPtr.Zero)
                return false;

            try
            {
                if (actualType != XA_CARDINAL || actualFormat != 32 || (ulong)itemCount < 1)
                    return false;

                value = (long)Marshal.ReadIntPtr(data);
                return true;
            }
            finally
            {
                XFree(data);
            }
        }

        // ---- Error handling ---------------------------------------------------------------------

        [AOT.MonoPInvokeCallback(typeof(XErrorHandlerDelegate))]
        private static int IgnoreXError(IntPtr display, IntPtr errorEvent)
        {
            return 0;
        }

        /// <summary>
        /// Installs a no-op X error handler for the duration of our own requests.
        /// Callers must XSync before <see cref="EndIgnoringErrors"/> so pending errors are delivered here.
        /// </summary>
        private static IntPtr BeginIgnoringErrors()
        {
            return XSetErrorHandlerDelegate(IgnoreErrorHandler);
        }

        private static void EndIgnoringErrors(IntPtr previousHandler)
        {
            XSetErrorHandlerPointer(previousHandler);
        }

        // ---- Cursor notifications ---------------------------------------------------------------

        private void TryInitializeCursorNotifications()
        {
            try
            {
                if (!XFixesQueryExtension(_display, out var eventBase, out _))
                    return;

                _xfixesCursorNotifyEvent = eventBase + XFixesCursorNotify;
                XFixesSelectCursorInput(_display, _rootWindow, XFixesDisplayCursorNotifyMask);
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

        private bool ProcessCursorEvents()
        {
            if (!_hasCursorNotifications)
                return false;

            var changedElsewhere = false;

            while (XPending(_display) > 0)
            {
                XNextEvent(_display, out var xevent);

                if (xevent.type != _xfixesCursorNotifyEvent)
                    continue;

                if (_ignoreNextCursorNotify && Time.unscaledTime > _ignoreNextCursorNotifyUntil)
                    _ignoreNextCursorNotify = false;

                if (_ignoreNextCursorNotify)
                {
                    // Notification caused by our own XDefineCursor.
                    _ignoreNextCursorNotify = false;
                    continue;
                }

                changedElsewhere = true;
            }

            return changedElsewhere;
        }

        // ---- Cursor application -----------------------------------------------------------------

        private IntPtr LoadCursor(NTCursors nativeCursor)
        {
            if (_cursors.TryGetValue(nativeCursor, out var cursor))
                return cursor;

            var shape = nativeCursor switch
            {
                NTCursors.Default => XC_arrow,
                NTCursors.Arrow => XC_arrow,
                NTCursors.IBeam => XC_xterm,
                NTCursors.Crosshair => XC_crosshair,
                NTCursors.Link => XC_hand2,
                NTCursors.Busy => XC_watch,
                NTCursors.Invalid => XC_X_cursor,
                NTCursors.ResizeVertical => XC_sb_v_double_arrow,
                NTCursors.ResizeHorizontal => XC_sb_h_double_arrow,
                // DiagonalLeft is the NW-SE shape on every platform; DiagonalRight is NE-SW.
                NTCursors.ResizeDiagonalLeft => XC_bottom_right_corner,
                NTCursors.ResizeDiagonalRight => XC_bottom_left_corner,
                NTCursors.ResizeAll => XC_fleur,
                NTCursors.OpenHand => XC_hand1,
                NTCursors.ClosedHand => XC_hand1,
                _ => throw new ArgumentOutOfRangeException(nameof(nativeCursor), nativeCursor, null)
            };

            cursor = XCreateFontCursor(_display, shape);

            if (cursor != IntPtr.Zero)
                _cursors.Add(nativeCursor, cursor);

            return cursor;
        }

        private bool ApplyCursor()
        {
            if (!OverrideEnabled || _windows.Count == 0)
                return false;

            var cursor = LoadCursor(_activeCursor);

            if (cursor == IntPtr.Zero)
                return false;

            var previous = BeginIgnoringErrors();

            foreach (var window in _windows)
                XDefineCursor(_display, window, cursor);

            XSync(_display, false);
            EndIgnoringErrors(previous);

            _ignoreNextCursorNotify = true;
            _ignoreNextCursorNotifyUntil = Time.unscaledTime + OwnCursorNotifyGracePeriod;
            return true;
        }

        public bool SetCursor(NTCursors nativeCursorName)
        {
            _activeCursor = nativeCursorName;

            if (!OverrideEnabled)
                return IsReady;

            if (_windows.Count == 0)
                RefreshWindows();

            return ApplyCursor();
        }

        public void ResetCursor()
        {
            SetCursor(NTCursors.Default);
        }

        public void OnActivated()
        {
            _serviceActive = true;

            if (isActiveAndEnabled)
                Activate();
        }

        public void OnDeactivated()
        {
            _serviceActive = false;
            Deactivate();
        }
    }
}

#endif
