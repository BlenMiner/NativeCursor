#if !UNITY_EDITOR && UNITY_STANDALONE_WIN

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Riten.Native.Cursors
{
    internal class WindowsCursorPatch : MonoBehaviour, ICursorService, ICursorServiceLifecycle
    {
        private const uint IDC_ARROW = 32512;        // Normal select
        private const uint IDC_IBEAM = 32513;        // Text select
        private const uint IDC_WAIT = 32514;         // Busy
        private const uint IDC_CROSS = 32515;        // Precision select
        private const uint IDC_SIZENWSE = 32642;     // Diagonal resize 1
        private const uint IDC_SIZENESW = 32643;     // Diagonal resize 2
        private const uint IDC_SIZEWE = 32644;       // Horizontal resize
        private const uint IDC_SIZENS = 32645;       // Vertical resize
        private const uint IDC_SIZEALL = 32646;      // Move
        private const uint IDC_NO = 32648;           // Unavailable
        private const uint IDC_HAND = 32649;         // Link select

        private const int HTCLIENT = 1;
        private const uint WM_SETCURSOR = 0x0020;
        private const uint WM_NCDESTROY = 0x0082;
        private const uint WM_MOUSEMOVE = 0x0200;
        private static readonly Dictionary<NTCursors, IntPtr> Cursors = new();
        private static readonly SubclassProcDelegate SubclassProc = WindowSubclassProc;
        private static readonly UIntPtr SubclassId = new(0x4E435552u); // "NCUR"

        private static IntPtr _hookedWindow;
        private static IntPtr _cursorHandle;
        private static bool _cursorOverrideActive;

        private bool _hasFocus = true;
        private bool _serviceActive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Setup()
        {
            var go = new GameObject("NativeCursor#WindowsCursorService")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            DontDestroyOnLoad(go);

            var service = go.AddComponent<WindowsCursorPatch>();
            NativeCursor.SetFallbackService(service);
            NativeCursor.SetService(service);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SetCursor(IntPtr hCursor);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr LoadCursor(IntPtr hInstance, uint lpCursorName);

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("comctl32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowSubclass(
            IntPtr hWnd,
            SubclassProcDelegate pfnSubclass,
            UIntPtr uIdSubclass,
            UIntPtr dwRefData);

        [DllImport("comctl32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RemoveWindowSubclass(
            IntPtr hWnd,
            SubclassProcDelegate pfnSubclass,
            UIntPtr uIdSubclass);

        [DllImport("comctl32.dll")]
        private static extern IntPtr DefSubclassProc(
            IntPtr hWnd,
            uint uMsg,
            IntPtr wParam,
            IntPtr lParam);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate IntPtr SubclassProcDelegate(
            IntPtr hWnd,
            uint uMsg,
            IntPtr wParam,
            IntPtr lParam,
            UIntPtr uIdSubclass,
            UIntPtr dwRefData);

        private void Awake()
        {
            _cursorHandle = GetCursorHandle(NTCursors.Default);
        }

        private void OnEnable()
        {
            if (!_serviceActive)
                return;

            _cursorOverrideActive = true;
            InstallHook();

            if (_hasFocus && _cursorHandle != IntPtr.Zero)
                SetCursor(_cursorHandle);
        }

        private void OnDisable()
        {
            _cursorOverrideActive = false;
            RestoreHook();
        }

        private void OnDestroy()
        {
            _serviceActive = false;
            _cursorOverrideActive = false;
            RestoreHook();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            _hasFocus = hasFocus;

            if (!_serviceActive || !hasFocus || _cursorHandle == IntPtr.Zero)
                return;

            InstallHook();
            SetCursor(_cursorHandle);
        }

        private void Update()
        {
            if (!_serviceActive || !_hasFocus || _cursorHandle == IntPtr.Zero || _hookedWindow != IntPtr.Zero)
                return;

            // The player window is not guaranteed to be active during BeforeSceneLoad.
            // Retry once it becomes available, and keep the requested cursor visible meanwhile.
            InstallHook();
            SetCursor(_cursorHandle);
        }

        private static void InstallHook()
        {
            var window = GetUnityWindow();

            if (window == IntPtr.Zero || window == _hookedWindow)
                return;

            RestoreHook();

            if (SetWindowSubclass(window, SubclassProc, SubclassId, UIntPtr.Zero))
                _hookedWindow = window;
        }

        private static void RestoreHook()
        {
            if (_hookedWindow == IntPtr.Zero)
                return;

            RemoveWindowSubclass(_hookedWindow, SubclassProc, SubclassId);
            _hookedWindow = IntPtr.Zero;
        }

        private static IntPtr GetUnityWindow()
        {
            // SetWindowSubclass cannot cross threads. GetActiveWindow only returns a window
            // owned by the calling thread, so a missing window is retried on the next focus/cursor update.
            return GetActiveWindow();
        }

        [AOT.MonoPInvokeCallback(typeof(SubclassProcDelegate))]
        private static IntPtr WindowSubclassProc(
            IntPtr hWnd,
            uint message,
            IntPtr wParam,
            IntPtr lParam,
            UIntPtr subclassId,
            UIntPtr referenceData)
        {
            if (message == WM_NCDESTROY && hWnd == _hookedWindow)
            {
                RemoveWindowSubclass(hWnd, SubclassProc, SubclassId);
                _hookedWindow = IntPtr.Zero;
            }

            if (_cursorOverrideActive && message == WM_SETCURSOR &&
                IsClientCursorMessage(lParam) && _cursorHandle != IntPtr.Zero)
            {
                SetCursor(_cursorHandle);
                return new IntPtr(1);
            }

            var result = DefSubclassProc(hWnd, message, wParam, lParam);

            // Unity can call SetCursor while processing mouse movement. Apply ours after it finishes.
            if (_cursorOverrideActive && message == WM_MOUSEMOVE && _cursorHandle != IntPtr.Zero)
                SetCursor(_cursorHandle);

            return result;
        }

        private static bool IsClientCursorMessage(IntPtr lParam)
        {
            return ((long)lParam & 0xffff) == HTCLIENT;
        }

        private static IntPtr GetCursorHandle(NTCursors nativeCursorName)
        {
            if (Cursors.TryGetValue(nativeCursorName, out var cursor))
                return cursor;

            cursor = LoadCursor(IntPtr.Zero, nativeCursorName switch
            {
                NTCursors.Default => IDC_ARROW,
                NTCursors.Arrow => IDC_ARROW,
                NTCursors.IBeam => IDC_IBEAM,
                NTCursors.Crosshair => IDC_CROSS,
                NTCursors.Link => IDC_HAND,
                NTCursors.ResizeVertical => IDC_SIZENS,
                NTCursors.ResizeHorizontal => IDC_SIZEWE,
                NTCursors.ResizeDiagonalLeft => IDC_SIZENWSE,
                NTCursors.ResizeDiagonalRight => IDC_SIZENESW,
                NTCursors.ResizeAll => IDC_SIZEALL,
                NTCursors.Busy => IDC_WAIT,
                NTCursors.Invalid => IDC_NO,
                NTCursors.OpenHand => IDC_HAND,
                NTCursors.ClosedHand => IDC_HAND,
                _ => throw new ArgumentOutOfRangeException(nameof(nativeCursorName), nativeCursorName, null)
            });

            Cursors.Add(nativeCursorName, cursor);
            return cursor;
        }

        public bool SetCursor(NTCursors nativeCursorName)
        {
            var cursor = GetCursorHandle(nativeCursorName);

            if (cursor == IntPtr.Zero)
                return false;

            _cursorHandle = cursor;

            if (!_serviceActive)
                return true;

            InstallHook();

            if (_hasFocus)
                SetCursor(cursor);

            return true;
        }

        public void ResetCursor()
        {
            SetCursor(NTCursors.Default);
        }

        public void OnActivated()
        {
            _serviceActive = true;
            _cursorOverrideActive = true;

            if (!isActiveAndEnabled)
                return;

            InstallHook();

            if (_hasFocus && _cursorHandle != IntPtr.Zero)
                SetCursor(_cursorHandle);
        }

        public void OnDeactivated()
        {
            _serviceActive = false;
            _cursorOverrideActive = false;
            RestoreHook();
        }
    }
}

#endif
