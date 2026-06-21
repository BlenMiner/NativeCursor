using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Riten.Native.Cursors.Editor
{
    public class CursorStackDebugWindow : EditorWindow
    {
        private readonly List<CursorStackItem> _items = new();
        private Vector2 _scroll;

        [MenuItem("Window/Native Cursor/Cursor Stack Debugger")]
        public static void Open()
        {
            GetWindow<CursorStackDebugWindow>("Cursor Stack");
        }

        private void OnEnable()
        {
            CursorStack.Changed += Repaint;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            CursorStack.Changed -= Repaint;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void Update()
        {
            if (Application.isPlaying && hasFocus)
                Repaint();
        }

        private void OnGUI()
        {
            DrawSummary();
            EditorGUILayout.Space(6f);
            DrawControls();
            EditorGUILayout.Space(6f);
            DrawStack();
        }

        private void DrawSummary()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Service", NativeCursor.ServiceName);
                EditorGUILayout.LabelField("Stack Count", CursorStack.Count.ToString());

                var top = CursorStack.Peek();
                EditorGUILayout.LabelField("Active Cursor", top.id == 0 ? "Default" : $"{top.cursor} (ID {top.id})");
            }
        }

        private void DrawControls()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Controls", EditorStyles.boldLabel);

                EditorGUI.BeginChangeCheck();
                var paused = EditorGUILayout.Toggle("Pause Rendering", CursorStack.IsRenderingPaused);

                if (EditorGUI.EndChangeCheck())
                    CursorStack.PauseRendering(paused);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Reapply"))
                        CursorStack.ReApply();

                    using (new EditorGUI.DisabledScope(CursorStack.Count == 0))
                    {
                        if (GUILayout.Button("Pop Active"))
                            CursorStack.Pop();

                        if (GUILayout.Button("Clear"))
                            CursorStack.Clear();
                    }
                }
            }
        }

        private void DrawStack()
        {
            CursorStack.CopyItemsTo(_items);

            EditorGUILayout.LabelField("Stack", EditorStyles.boldLabel);

            if (_items.Count == 0)
            {
                EditorGUILayout.HelpBox("No cursor entries are currently pushed.", MessageType.Info);
                return;
            }

            var activeId = CursorStack.Peek().id;
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            for (var i = _items.Count - 1; i >= 0; --i)
                DrawStackItem(_items[i], _items[i].id == activeId);

            EditorGUILayout.EndScrollView();
        }

        private static void DrawStackItem(CursorStackItem item, bool isActive)
        {
            var originalBackgroundColor = GUI.backgroundColor;

            if (isActive)
                GUI.backgroundColor = new Color(0.72f, 1f, 0.72f, 1f);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUI.backgroundColor = originalBackgroundColor;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        isActive ? $"ID {item.id} - Active" : $"ID {item.id}",
                        EditorStyles.boldLabel
                    );

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Pop", GUILayout.Width(56f)))
                        CursorStack.Pop(item.id);
                }

                EditorGUILayout.LabelField("Cursor", item.cursor.ToString());
                EditorGUILayout.LabelField("Priority", item.priority.ToString());
                EditorGUILayout.LabelField("Secondary Priority", item.secondaryPriority.ToString());
            }
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            Repaint();
        }
    }
}
