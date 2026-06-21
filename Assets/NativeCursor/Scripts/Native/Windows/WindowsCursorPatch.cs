#if !UNITY_EDITOR && UNITY_STANDALONE_WIN

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Riten.Native.Cursors
{
    internal class WindowsCursorPatch : MonoBehaviour, ICursorService
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
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Setup()
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
        
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SetCursor(IntPtr hCursor);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr GetCursor();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr LoadCursor(IntPtr hInstance, uint lpCursorName);

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint wMsg, IntPtr wParam,
            IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(HandleRef hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(HandleRef hWnd, int nIndex, IntPtr dwNewLong);

        private static HandleRef hMainWindow;
        private static IntPtr unityWndProcHandler, customWndProcHandler;

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private static WndProcDelegate procDelegate;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        private const int GWLP_WNDPROC = -4;
        private const int HTCLIENT = 1;
        private const uint WM_SETCURSOR = 0x0020;

        private static IntPtr cursorHandle;
        private bool _hasFocus = true;

        static readonly Dictionary<NTCursors, IntPtr> _cursors = new ();

        void Awake()
        {
            cursorHandle = GetCursor(NTCursors.Default);
            InstallHook();
        }

        void Update()
        {
            if (!_hasFocus || cursorHandle == IntPtr.Zero)
                return;

            if (customWndProcHandler == IntPtr.Zero)
                InstallHook();

            if (IsCursorInsideClientArea() && GetCursor() != cursorHandle)
                SetCursor(cursorHandle);
        }

        void OnApplicationFocus(bool hasFocus)
        {
            _hasFocus = hasFocus;

            if (hasFocus && cursorHandle != IntPtr.Zero)
            {
                InstallHook();
                SetCursor(cursorHandle);
            }
        }

        private static void InstallHook()
        {
            if (customWndProcHandler != IntPtr.Zero)
                return;

            var window = GetActiveWindow();

            if (window == IntPtr.Zero)
                return;

            hMainWindow = new HandleRef(null, window);
            procDelegate = WndProc;
            customWndProcHandler = Marshal.GetFunctionPointerForDelegate(procDelegate);
            unityWndProcHandler = SetWindowLongPtr(hMainWindow, GWLP_WNDPROC, customWndProcHandler);

            if (unityWndProcHandler != IntPtr.Zero)
                return;

            hMainWindow = new HandleRef(null, IntPtr.Zero);
            customWndProcHandler = IntPtr.Zero;
            procDelegate = null;
        }

        void OnDestroy()
        {
            RestoreHook();
        }

        private static void RestoreHook()
        {
            if (hMainWindow.Handle != IntPtr.Zero && unityWndProcHandler != IntPtr.Zero)
                SetWindowLongPtr(hMainWindow, GWLP_WNDPROC, unityWndProcHandler);

            hMainWindow = new HandleRef(null, IntPtr.Zero);
            unityWndProcHandler = IntPtr.Zero;
            customWndProcHandler = IntPtr.Zero;
            procDelegate = null;
        }

        [AOT.MonoPInvokeCallback(typeof(WndProcDelegate))]
        private static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_SETCURSOR && IsClientCursorMessage(lParam) && cursorHandle != IntPtr.Zero)
            {
                SetCursor(cursorHandle);
                return new IntPtr(1);
            }

            return CallWindowProc(unityWndProcHandler, hWnd, msg, wParam, lParam);
        }

        private static bool IsClientCursorMessage(IntPtr lParam)
        {
            return ((int)lParam & 0xffff) == HTCLIENT;
        }

        private static bool IsCursorInsideClientArea()
        {
            if (hMainWindow.Handle == IntPtr.Zero)
                return true;

            if (!GetCursorPos(out var point))
                return true;

            if (!ScreenToClient(hMainWindow.Handle, ref point))
                return true;

            if (!GetClientRect(hMainWindow.Handle, out var clientRect))
                return true;

            return point.x >= clientRect.left
                   && point.x < clientRect.right
                   && point.y >= clientRect.top
                   && point.y < clientRect.bottom;
        }

        private static IntPtr SetWindowLongPtr(HandleRef hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8) return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        static IntPtr GetCursor(NTCursors nativeCursorName)
        {
            if (_cursors.TryGetValue(nativeCursorName, out var cursor))
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
            
            _cursors.Add(nativeCursorName, cursor);
            return cursor;
        }

        public bool SetCursor(NTCursors nativeCursorName)
        {
            var arrowCursorHandle = GetCursor(nativeCursorName);

            if (arrowCursorHandle == IntPtr.Zero)
                return false;

            cursorHandle = arrowCursorHandle;
            InstallHook();
            SetCursor(arrowCursorHandle);
            return true;
        }

        public void ResetCursor()
        {
            SetCursor(NTCursors.Default);
        }
    }
}

#endif
