namespace Riten.Native.Cursors
{
    public enum NTCursors
    {
        Default,
        Arrow,
        IBeam,
        Crosshair,
        Link,
        Busy,
        Invalid,
        ResizeVertical,
        ResizeHorizontal,
        ResizeDiagonalLeft,
        ResizeDiagonalRight,
        ResizeAll,
    }
    
    public interface ICursorService
    {
        bool SetCursor(NTCursors ntCursor);
        
        void ResetCursor();
    }
}