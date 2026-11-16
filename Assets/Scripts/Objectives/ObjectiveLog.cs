using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Taxi
{
    /// <summary>
    /// Measures the intro and the objectives. Each run has its own file in Application.persistentDataPath,
    /// objective_log_yyyy-MM-dd_HH-mm.csv (number, title, start, finish, duration_s, attempts). A line is appended (and the
    /// file closed) the moment each step finishes, so a crash or restart loses nothing. Old files are never overwritten
    /// or deleted, resetProgressOnLaunch included. A run that starts from the intro gets a new file; a resumed run
    /// (resetProgressOnLaunch off) continues its file, remembered in PlayerPrefs.
    /// Console: "[OBJ] 6 Long haul — 54.2 s"; after the last objective "[OBJ] Total — 12:34" (sum of objectives 1..N).
    /// </summary>
    public class ObjectiveLog : MonoBehaviour
    {
        public const string FilePrefix = "objective_log";
        public const string FileKey = "Taxi.ObjectiveLogFile";
        const string Header = "number,title,start,finish,duration_s,attempts";

        [SerializeField] ObjectiveTracker tracker;

        public static string Folder => Application.persistentDataPath;
        /// <summary>File of the current run (null until Start).</summary>
        public static string FilePath { get; private set; }

        void Start()
        {
            bool fresh = PlayerPrefs.GetInt(ObjectiveTracker.SaveKey, 0) == 0;
            string remembered = PlayerPrefs.GetString(FileKey, "");
            string resumePath = remembered.Length > 0 ? Path.Combine(Folder, remembered) : null;
            if (!fresh && resumePath != null && File.Exists(resumePath)) FilePath = resumePath;
            else
            {
                FilePath = NewRunPath();
                try { File.WriteAllText(FilePath, Header + "\n"); }   // a new name, never an existing file
                catch (Exception e) { Debug.LogWarning("[OBJ] Cannot write " + FilePath + ": " + e.Message); }
                PlayerPrefs.SetString(FileKey, Path.GetFileName(FilePath));
                PlayerPrefs.Save();
            }
            Debug.Log("[OBJ] Log: " + FilePath);
            tracker.Completed += OnCompleted;
            tracker.AllCompleted += OnAllCompleted;
        }

        /// <summary>objective_log_2026-11-16_16-07.csv; _2, _3 ... if that minute already has a file.</summary>
        static string NewRunPath()
        {
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm", CultureInfo.InvariantCulture);
            string path = Path.Combine(Folder, FilePrefix + "_" + stamp + ".csv");
            for (int n = 2; File.Exists(path); n++) path = Path.Combine(Folder, FilePrefix + "_" + stamp + "_" + n + ".csv");
            return path;
        }

        void OnDestroy()
        {
            if (tracker == null) return;
            tracker.Completed -= OnCompleted;
            tracker.AllCompleted -= OnAllCompleted;
        }

        void OnCompleted(int number, string title, float start, float finish, int attempts)
        {
            float duration = finish - start;
            Debug.Log("[OBJ] " + number + " " + title + " — " + duration.ToString("0.0", CultureInfo.InvariantCulture) + " s");
            string line = number + "," + Csv(title) + "," + Stamp(start) + "," + Stamp(finish) + ","
                          + duration.ToString("0.0", CultureInfo.InvariantCulture) + "," + attempts;
            Append(line);
        }

        void OnAllCompleted(float totalSeconds)
        {
            int t = Mathf.RoundToInt(totalSeconds);
            string mmss = t / 60 + ":" + (t % 60).ToString("00");
            Debug.Log("[OBJ] Total — " + mmss);
            Append("total,,,," + totalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + ",");
        }

        static void Append(string line)
        {
            try { File.AppendAllText(FilePath, line + "\n"); }
            catch (Exception e) { Debug.LogWarning("[OBJ] Cannot write " + FilePath + ": " + e.Message); }
        }

        /// <summary>Wall-clock time of a moment given in unscaled seconds since launch.</summary>
        static string Stamp(float unscaled)
        {
            var t = DateTime.Now.AddSeconds(unscaled - Time.unscaledTime);
            return t.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        static string Csv(string s) => s.IndexOfAny(new[] { ',', '"' }) < 0 ? s : "\"" + s.Replace("\"", "\"\"") + "\"";
    }
}
