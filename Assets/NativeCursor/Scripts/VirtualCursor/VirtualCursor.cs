using UnityEngine;

namespace Riten.Native.Cursors
{
    [CreateAssetMenu(fileName = "VirtualCursor", menuName = "Native Cursor/Virtual Cursor", order = 1)]
    public class VirtualCursor : ScriptableObject
    {
        public Texture2D texture;
        
        [Tooltip("Hotspot of the cursor in uv coordinates [0-1]")]
        public Vector2 hotspot;
    }
}

