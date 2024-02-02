using UnityEngine;

namespace Riten.Native.Cursors
{
    [CreateAssetMenu(fileName = "AnimatedVirtualCursor", menuName = "Native Cursor/Animated Virtual Cursor")]
    public class AnimatedVirtualCursor : ScriptableObject
    {
        public VirtualCursor[] frames;
        
        [Tooltip("Frames per second")]
        public int fps = 30;
    }
}