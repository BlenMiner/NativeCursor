using UnityEngine;

namespace Riten.Native.Cursors
{
    public static class NativeCursor
    {
        static ICursorService _instance;
        
        public static string ServiceName => _instance == null ? "NULL" : _instance.GetType().Name;
        
        /// <summary>
        /// Set custom cursor service.
        /// You should not need to call this method.
        /// But you can!
        /// </summary>
        public static void SetService(ICursorService service)
        {
            _instance?.SetCursor(NTCursors.Default);
            _instance = service;
        }
        
        public static bool SetCursor(NTCursors ntCursor)
        {
            return _instance != null && _instance.SetCursor(ntCursor);
        }
        
        /// <summary>
        /// This method uses a virtual cursor pack to set the cursor.
        /// </summary>
        /// <param name="cursorPack"></param>
        public static void SetCursorPack(CursorPack cursorPack)
        {
            if (cursorPack == null)
            {
                Debug.LogError("CursorPack is null, ignoring call.");
                return;
            }
            
            SetService(new Virtual.VirtualCursorService(cursorPack));
        }
        
        /// <summary>
        /// Reset cursor to default.
        /// This is safer than setting the cursor to NTCursors.Arrow.
        /// </summary>
        public static void ResetCursor()
        {
            _instance?.ResetCursor();
        }
    }
}
