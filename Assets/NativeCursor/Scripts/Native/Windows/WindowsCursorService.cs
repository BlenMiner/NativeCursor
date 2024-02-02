#if !UNITY_EDITOR && UNITY_STANDALONE_WIN

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Riten.Native.Cursors
{
    internal class WindowsCursorService : MonoBehaviour, ICursorService
    {
        private const uint IDC_ARROW = 32512;        // Normal select
        private const uint IDC_IBEAM = 32513;        // Text select
        private const uint IDC_WAIT = 32650;         // Busy
        private const uint IDC_CROSS = 32515;        // Precision select
        private const uint IDC_SIZENWSE = 32642;     // Diagonal resize 1
        private const uint IDC_SIZENESW = 32643;     // Diagonal resize 2
        private const uint IDC_SIZEWE = 32644;       // Horizontal resize
        private const uint IDC_SIZENS = 32645;       // Vertical resize
        private const uint IDC_SIZEALL = 32646;      // Move
        private const uint IDC_NO = 32648;           // Unavailable
        private const uint IDC_HAND = 32649;         // Link select

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadCursor(IntPtr hInstance, uint lpCursorName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetCursor(IntPtr hCursor);

        readonly Dictionary<NTCursors, IntPtr> _cursors = new ();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Setup()
        {
            var go = new GameObject("NativeCursor#WindowsCursorService")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            DontDestroyOnLoad(go);

            var service = go.AddComponent<WindowsCursorService>();

            NativeCursor.SetFallbackService(service);
            NativeCursor.SetService(service);
        }
        
        NTCursors? _currentCursor;

        private void Update()
        {
            if (_currentCursor.HasValue)
                SetCursor(_currentCursor.Value);
        }

        IntPtr GetCursor(NTCursors nativeCursorName)
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
                
                NTCursors.OpenHand => 32516,
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
            {
                _currentCursor = null;
                return false;
            }

            SetCursor(arrowCursorHandle);
            _currentCursor = nativeCursorName == NTCursors.Default ? null : nativeCursorName;
            return true;
        }

        public void ResetCursor()
        {
            SetCursor(NTCursors.Default);
        }
    }
}

#endif