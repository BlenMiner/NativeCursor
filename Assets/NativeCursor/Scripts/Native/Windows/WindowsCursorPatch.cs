#if !UNITY_EDITOR && UNITY_STANDALONE_WIN

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace Riten.Native.Cursors
{
    /// <summary>
    /// Windows player cursor service.
    ///
    /// Unity's player handles WM_SETCURSOR itself and calls SetCursor with its own cursor, so the
    /// class cursor (GCLP_HCURSOR) cannot be used. Instead every Unity player window on the main
    /// thread is subclassed with SetWindowSubclass. The subclass answers client-area WM_SETCURSOR
    /// with the requested system cursor and reapplies it after Unity finishes processing mouse movement.
    ///
    /// Unity hides the cursor with ShowCursor's display counter, so Cursor.visible and
    /// CursorLockMode.Locked keep working while this service is active.
    /// </summary>
    internal class WindowsCursorPatch : MonoBehaviour, ICursorService, ICursorServiceLifecycle
    {
        private const int IDC_ARROW = 32512;        // Normal select
        private const int IDC_IBEAM = 32513;        // Text select
        private const int IDC_WAIT = 32514;         // Busy
        private const int IDC_CROSS = 32515;        // Precision select
        private const int IDC_SIZENWSE = 32642;     // Diagonal resize 1
        private const int IDC_SIZENESW = 32643;     // Diagonal resize 2
        private const int IDC_SIZEWE = 32644;       // Horizontal resize
        private const int IDC_SIZENS = 32645;       // Vertical resize
        private const int IDC_SIZEALL = 32646;      // Move
        private const int IDC_NO = 32648;           // Unavailable
        private const int IDC_HAND = 32649;         // Link select

        private const int HTCLIENT = 1;
        private const uint WM_SETCURSOR = 0x0020;
        private const uint WM_NCDESTROY = 0x0082;
        private const uint WM_MOUSEMOVE = 0x0200;

        private const string UnityWindowClassName = "UnityWndClass";
        private const int ClassNameBufferLength = 64;
        private const float WindowScanInterval = 0.5f;

        private static readonly Dictionary<NTCursors, IntPtr> Cursors = new();
        private static readonly SubclassProcDelegate SubclassProc = WindowSubclassProc;
        private static readonly EnumThreadWindowsDelegate EnumWindowsProc = CollectUnityWindow;
        private static readonly UIntPtr SubclassId = new(0x4E435552u); // "NCUR"
        private static readonly List<IntPtr> HookedWindows = new();
        private static readonly List<IntPtr> ScannedWindows = new();
        private static readonly StringBuilder ClassNameBuffer = new(ClassNameBufferLength);

        private static IntPtr _cursorHandle;
        private static bool _cursorOverrideActive;

        private bool _hasFocus;
        private bool _serviceActive;
        private float _nextWindowScanTime;

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

        [DllImport("user32.dll", EntryPoint = "LoadCursorW")]
        private static extern IntPtr LoadCursor(IntPtr hInstance, IntPtr lpCursorName);

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetFocus();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumThreadWindows(uint dwThreadId, EnumThreadWindowsDelegate lpfn, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

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

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private delegate bool EnumThreadWindowsDelegate(IntPtr hWnd, IntPtr lParam);

        /// <summary>
        /// True while NativeCursor has selected this service and the component is enabled.
        /// </summary>
        private bool OverrideEnabled => _serviceActive && isActiveAndEnabled;

        private void Awake()
        {
            _hasFocus = Application.isFocused;
            _cursorHandle = GetCursorHandle(NTCursors.Default);
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
            Deactivate();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            _hasFocus = hasFocus;

            if (!hasFocus || !OverrideEnabled)
                return;

            HookUnityWindows();
            ApplyCursor();
        }

        private void Update()
        {
            if (!OverrideEnabled)
                return;

            // The player window may not exist yet during BeforeSceneLoad, and Display.Activate can
            // create additional windows later. Scan every frame until something is hooked, then
            // at a low rate to pick up new windows.
            if (HookedWindows.Count > 0 && Time.unscaledTime < _nextWindowScanTime)
                return;

            _nextWindowScanTime = Time.unscaledTime + WindowScanInterval;

            if (HookUnityWindows())
                ApplyCursor();
        }

        private void Activate()
        {
            _cursorOverrideActive = true;
            HookUnityWindows();
            ApplyCursor();
        }

        private void Deactivate()
        {
            _cursorOverrideActive = false;
            UnhookAllWindows();
        }

        private void ApplyCursor()
        {
            // SetCursor only sticks for the thread that owns the window under the pointer.
            // When unfocused the subclass still answers WM_SETCURSOR for our own windows.
            if (_hasFocus && _cursorHandle != IntPtr.Zero)
                SetCursor(_cursorHandle);
        }

        /// <summary>
        /// Subclasses every Unity player window owned by the main thread that is not hooked yet.
        /// Returns true when at least one new window was hooked.
        /// </summary>
        private static bool HookUnityWindows()
        {
            PruneDestroyedWindows();
            FindUnityWindows();

            var hookedAny = false;

            foreach (var window in ScannedWindows)
            {
                if (HookedWindows.Contains(window))
                    continue;

                if (SetWindowSubclass(window, SubclassProc, SubclassId, UIntPtr.Zero))
                {
                    HookedWindows.Add(window);
                    hookedAny = true;
                }
            }

            ScannedWindows.Clear();
            return hookedAny;
        }

        private static void UnhookAllWindows()
        {
            foreach (var window in HookedWindows)
            {
                if (IsWindow(window))
                    RemoveWindowSubclass(window, SubclassProc, SubclassId);
            }

            HookedWindows.Clear();
        }

        private static void PruneDestroyedWindows()
        {
            for (var i = HookedWindows.Count - 1; i >= 0; i--)
            {
                if (!IsWindow(HookedWindows[i]))
                    HookedWindows.RemoveAt(i);
            }
        }

        /// <summary>
        /// Collects Unity player windows into <see cref="ScannedWindows"/>.
        /// SetWindowSubclass cannot cross threads, so only windows owned by the calling (main) thread qualify.
        /// Matching on the window class avoids hooking message boxes or native dialogs that happen to be active.
        /// </summary>
        private static void FindUnityWindows()
        {
            ScannedWindows.Clear();

            // Top-level player windows, including secondary displays.
            EnumThreadWindows(GetCurrentThreadId(), EnumWindowsProc, IntPtr.Zero);

            // When embedded with -parentHWND the player window is a child and is not enumerated above.
            AddIfUnityWindow(GetActiveWindow());
            AddIfUnityWindow(GetFocus());
        }

        [AOT.MonoPInvokeCallback(typeof(EnumThreadWindowsDelegate))]
        private static bool CollectUnityWindow(IntPtr hWnd, IntPtr lParam)
        {
            AddIfUnityWindow(hWnd);
            return true;
        }

        private static void AddIfUnityWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero || ScannedWindows.Contains(hWnd) || !IsUnityWindow(hWnd))
                return;

            ScannedWindows.Add(hWnd);
        }

        private static bool IsUnityWindow(IntPtr hWnd)
        {
            ClassNameBuffer.Clear();

            var length = GetClassName(hWnd, ClassNameBuffer, ClassNameBufferLength);

            if (length != UnityWindowClassName.Length)
                return false;

            for (var i = 0; i < length; i++)
            {
                if (ClassNameBuffer[i] != UnityWindowClassName[i])
                    return false;
            }

            return true;
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
            if (message == WM_NCDESTROY)
            {
                // Documented pattern: remove ourselves here, then still forward to the chain.
                RemoveWindowSubclass(hWnd, SubclassProc, SubclassId);
                HookedWindows.Remove(hWnd);
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

            var resourceId = nativeCursorName switch
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
            };

            // System cursors loaded with a null module are shared and must not be destroyed.
            cursor = LoadCursor(IntPtr.Zero, new IntPtr(resourceId));

            // Only cache successful loads so a transient failure can be retried.
            if (cursor != IntPtr.Zero)
                Cursors.Add(nativeCursorName, cursor);

            return cursor;
        }

        public bool SetCursor(NTCursors nativeCursorName)
        {
            var cursor = GetCursorHandle(nativeCursorName);

            if (cursor == IntPtr.Zero)
                return false;

            _cursorHandle = cursor;

            if (!OverrideEnabled)
                return true;

            HookUnityWindows();
            ApplyCursor();
            return true;
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
