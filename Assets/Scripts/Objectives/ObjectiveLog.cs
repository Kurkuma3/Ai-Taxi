using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Taxi
{
    /// <summary>
    /// Measures the intro and the objectives. Every finished step is appended to
    /// Application.persistentDataPath/objective_log.csv (number, title, start, finish, duration_s, attempts) and printed
    /// as "[OBJ] 6 Long haul — 54.2 s"; after the last objective "[OBJ] Total — 12:34" (sum of objectives 1..N).
    /// A run that starts from the intro (fresh progress) starts a new file; a resumed run appends.
    /// </summary>
    public class ObjectiveLog : MonoBehaviour
    {
        public const string FileName = "objective_log.csv";
        const string Header = "number,title,start,finish,duration_s,attempts";

        [SerializeField] ObjectiveTracker tracker;

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        void Start()
        {
            bool fresh = PlayerPrefs.GetInt(ObjectiveTracker.SaveKey, 0) == 0;
            try
            {
                if (fresh || !File.Exists(FilePath)) File.WriteAllText(FilePath, Header + "\n");
            }
            catch (Exception e) { Debug.LogWarning("[OBJ] Cannot write " + FilePath + ": " + e.Message); }
            tracker.Completed += OnCompleted;
            tracker.AllCompleted += OnAllCompleted;
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
