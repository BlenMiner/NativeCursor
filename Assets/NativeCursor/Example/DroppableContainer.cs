using UnityEngine;
using UnityEngine.EventSystems;

namespace Riten.Native.Cursors.Examples
{
    public class DroppableContainer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private static DroppableContainer _current;
        
        public static DroppableContainer current => _current;
        
        [SerializeField] bool _canDrop = true;

        public bool canDrop => _canDrop;
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            _current = this;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_current == this)
                _current = null;
        }
    }
}