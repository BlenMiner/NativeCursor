using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Riten.Native.Cursors.Examples
{
    public class DragDropExample : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Image _graphic;

        DroppableContainer _current;
        DroppableContainer _lastHovered;

        private int _pushedCursor;
        
        private void Awake()
        {
            _current = GetComponentInParent<DroppableContainer>();
            _lastHovered = _current;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _graphic.raycastTarget = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (DroppableContainer.current != _lastHovered)
            {
                _lastHovered = DroppableContainer.current;

                if (_pushedCursor != 0)
                {
                    CursorStack.Pop(_pushedCursor);
                    _pushedCursor = 0;
                }

                if (_lastHovered != null && !_lastHovered.canDrop)
                    _pushedCursor = CursorStack.Push(NTCursors.Invalid);
            }
            transform.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            var trs = transform;

            if (DroppableContainer.current != null &&
                DroppableContainer.current.canDrop)
            {
                trs.SetParent(DroppableContainer.current.transform);
            }
            
            if (_pushedCursor != 0)
            {
                CursorStack.Pop(_pushedCursor);
                _pushedCursor = 0;
            }
            
            trs.localPosition = Vector3.zero;

            _graphic.raycastTarget = true;
        }
    }
}