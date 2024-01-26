namespace Riten.Native.Cursors
{
    public static class NativeCursor
    {
        static ICursorService _instance;
        
        public static string ServiceName => _instance == null ? "NULL" : _instance.GetType().Name;
        
        public static void SetService(ICursorService service)
        {
            _instance = service;
        }
        
        public static bool SetCursor(NTCursors ntCursor)
        {
            return _instance != null && _instance.SetCursor(ntCursor);
        }
        
        public static void ResetCursor()
        {
            _instance?.ResetCursor();
        }
    }
}
