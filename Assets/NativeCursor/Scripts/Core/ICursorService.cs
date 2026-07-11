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
        OpenHand,
        ClosedHand
    }
    
    public interface ICursorService
    {
        bool SetCursor(NTCursors ntCursor);
        
        void ResetCursor();
    }

    /// <summary>
    /// Optional lifecycle for cursor services that continuously enforce or animate a cursor.
    /// NativeCursor invokes these callbacks whenever the active service changes.
    /// </summary>
    public interface ICursorServiceLifecycle
    {
        void OnActivated();

        void OnDeactivated();
    }
}
