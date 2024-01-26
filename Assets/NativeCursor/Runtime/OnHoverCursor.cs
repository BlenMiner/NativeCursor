using Riten.Native.Cursors;
using UnityEngine;
using UnityEngine.EventSystems;

public class OnHoverCursor : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Editor Preview May Not Match Runtime")]
    [SerializeField] private NTCursors ntCursor;

    private bool _isHovering;
    
    public NTCursors Cursor
    {
        get => ntCursor;
        set
        {
            if (_isHovering)
                NativeCursor.SetCursor(value);
            ntCursor = value;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovering = true;
        NativeCursor.SetCursor(ntCursor);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovering = false;
        NativeCursor.ResetCursor();
    }
}
