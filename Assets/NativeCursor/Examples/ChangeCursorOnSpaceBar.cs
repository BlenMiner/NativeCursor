using System.Collections.Generic;
using UnityEngine;

namespace Riten.Native.Cursors.Examples
{
    public class ChangeCursorOnSpaceBar : MonoBehaviour
    {
        List<string> _logs = new ();
        
        void OnEnable()
        {
            Application.logMessageReceived += OnLogMessageReceived;
        }
        
        void OnDisable()
        {
            Application.logMessageReceived -= OnLogMessageReceived;
        }

        private void OnLogMessageReceived(string condition, string stacktrace, LogType type)
        {
            _logs.Add(condition);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                NativeCursor.SetCursor(NTCursors.Link);
            }
            else if (Input.GetKeyUp(KeyCode.Space))
            {
                NativeCursor.ResetCursor();
            }

            if (Input.GetKeyUp(KeyCode.F))
            {
                NativeCursor.ResetCursor();
            }
            else if (Input.GetKey(KeyCode.F))
            {
                NativeCursor.SetCursor(NTCursors.Link);
            }
        }
        
        void OnGUI()
        {
            GUILayout.Label(NativeCursor.ServiceName);
            
            for (var i = 0; i < _logs.Count; i++)
                GUILayout.Label(_logs[i]);
        }
    }
}