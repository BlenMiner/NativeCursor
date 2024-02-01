using UnityEngine;

namespace Riten.Native.Cursors.Virtual
{
    public class AutoSetCursorPack : MonoBehaviour
    {
        [SerializeField] CursorPack _cursorPack;

        private void Start()
        {
            if (_cursorPack == null)
                return;
            
            NativeCursor.SetCursorPack(_cursorPack);
            NativeCursor.SetCursor(NTCursors.Arrow);
        }
    }
}