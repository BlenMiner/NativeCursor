namespace Riten.Native.Cursors.Virtual
{
    public class VirtualCursorService : ICursorService
    {
        readonly CursorPack _cursorPack;
        
        public VirtualCursorService(CursorPack cursorPack)
        {
            _cursorPack = cursorPack;
        }
        
        public bool SetCursor(NTCursors ntCursor)
        {
            return _cursorPack.SetCursor(ntCursor);
        }

        public void ResetCursor()
        {
            _cursorPack.SetCursor(NTCursors.Arrow);
        }
    }
}