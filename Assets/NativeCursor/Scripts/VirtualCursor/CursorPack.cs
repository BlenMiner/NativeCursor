using UnityEngine;

namespace Riten.Native.Cursors
{
    [CreateAssetMenu(fileName = "CursorPack", menuName = "Native Cursor/Cursor Pack")]
    public class CursorPack : ScriptableObject
    {
        public VirtualCursor @default;
        public VirtualCursor pointer;
        public VirtualCursor ibeam;
        public VirtualCursor wait;
        public VirtualCursor cross;
        [Space]
        public VirtualCursor grab;
        public VirtualCursor grabbing;
        public VirtualCursor denied;
        [Space]
        public VirtualCursor move;
        public VirtualCursor resizeHorizontal;
        public VirtualCursor resizeVertical;
        public VirtualCursor resizeDiagonal1;
        public VirtualCursor resizeDiagonal2;

        public bool SetCursor(NTCursors ntCursor)
        {
            var cursor = ntCursor switch
            {
                NTCursors.Default => null,
                NTCursors.Arrow => @default,
                NTCursors.Link => pointer,
                NTCursors.IBeam => ibeam,
                NTCursors.Busy => wait,
                NTCursors.Crosshair => cross,
                NTCursors.OpenHand => grab,
                NTCursors.ClosedHand => grabbing,
                NTCursors.Invalid => denied,
                NTCursors.ResizeAll => move,
                NTCursors.ResizeHorizontal => resizeHorizontal,
                NTCursors.ResizeVertical => resizeVertical,
                NTCursors.ResizeDiagonalLeft => resizeDiagonal1,
                NTCursors.ResizeDiagonalRight => resizeDiagonal2,
                _ => null
            };

            if (cursor == null)
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                return true;
            }

            Cursor.SetCursor(cursor.texture, cursor.hotspot * new Vector2(cursor.texture.width, cursor.texture.height), CursorMode.Auto);
            return true;
        }
    }
}