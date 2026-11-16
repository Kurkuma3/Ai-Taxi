using System.IO;
using UnityEngine;

namespace Taxi
{
    /// <summary>
    /// Tiny debug panel for playtests, only in development builds and the editor (Debug.isDebugBuild).
    /// A small "DBG" button in the top-left corner opens it: the objective log path and a "Copy log" button that puts
    /// the CSV on the clipboard. IMGUI keeps it out of the game HUD; it costs nothing in release builds (disabled).
    /// </summary>
    public class DebugPanel : MonoBehaviour
    {
        bool open;
        string status = "";
        GUIStyle label, button;

        void Awake()
        {
            if (!Debug.isDebugBuild) enabled = false;
        }

        void OnGUI()
        {
            float u = Mathf.Max(1f, Screen.height / 1920f);   // scale for phone screens
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { wordWrap = true };
                button = new GUIStyle(GUI.skin.button);
            }
            label.fontSize = Mathf.RoundToInt(26 * u);
            button.fontSize = Mathf.RoundToInt(28 * u);

            if (GUI.Button(new Rect(16 * u, 16 * u, 110 * u, 64 * u), open ? "×" : "DBG", button)) open = !open;
            if (!open) return;

            var area = new Rect(16 * u, 90 * u, Screen.width - 32 * u, 330 * u);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(area.x + 12 * u, area.y + 12 * u, area.width - 24 * u, area.height - 24 * u));
            GUILayout.Label("Objective log:\n" + ObjectiveLog.FilePath, label);
            if (GUILayout.Button("Copy log", button, GUILayout.Height(80 * u)))
            {
                try
                {
                    string text = File.Exists(ObjectiveLog.FilePath) ? File.ReadAllText(ObjectiveLog.FilePath) : "";
                    GUIUtility.systemCopyBuffer = text;
                    status = text.Length > 0 ? "Copied " + text.Split('\n').Length + " lines" : "Log is empty";
                }
                catch (System.Exception e) { status = "Copy failed: " + e.Message; }
            }
            GUILayout.Label(status, label);
            GUILayout.EndArea();
        }
    }
}
