using UnityEngine;
using System;
using System.IO;
using System.Text;

public class RppgLogger : MonoBehaviour
{
    [Header("Log Settings")]
    public bool logLevelChangesOnly = false;

    private RppgReceiver receiver;
    private StreamWriter writer;
    private string filePath;
    private string sessionName;
    private float logInterval = 1f;
    private float logTimer = 0f;
    private int entryCount = 0;
    private bool baselineLogged = false;

    void Start()
    {
        // ── Persist across scene loads — only one instance ever exists ──
        RppgLogger[] existing = FindObjectsByType<RppgLogger>(FindObjectsSortMode.None);
        if (existing.Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);

        receiver = FindFirstObjectByType<RppgReceiver>();

        // Auto-increment session number
        string logFolder = Path.Combine(Application.dataPath, "logged");
        Directory.CreateDirectory(logFolder);

        int sessionNumber = 1;
        while (File.Exists(Path.Combine(logFolder, $"Session_{sessionNumber:D2}_{DateTime.Now:yyyy-MM-dd}.txt")))
        {
            sessionNumber++;
        }

        string fileName = $"Session_{sessionNumber:D2}_{DateTime.Now:yyyy-MM-dd}.txt";
        filePath    = Path.Combine(logFolder, fileName);
        sessionName = $"Session {sessionNumber:D2}";

        writer = new StreamWriter(filePath, false, Encoding.UTF8);
        WriteHeader();

        // Log baseline start
        writer.WriteLine($"{"00:00:00",-10} {"BASELINE START",-18}");
        writer.Flush();

        // Subscribe to level change event
        receiver.OnArousalLevelChanged += OnLevelChanged;

        Debug.Log($"[RppgLogger] Session {sessionNumber:D2} started — logging to: {filePath}");
    }

    private void WriteHeader()
    {
        writer.WriteLine("==============================================");
        writer.WriteLine($"  Session: {sessionName}");
        writer.WriteLine($"  Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        writer.WriteLine("==============================================");
        writer.WriteLine();
        writer.WriteLine(
            $"{"Time",-10} {"Event",-18} {"Arousal",-10} {"Score",-8} " +
            $"{"HR",-8} {"IBI",-8} {"RMSSD",-8} {"LF/HF",-8} " +
            $"{"Breathing",-12} {"SQI",-6}"
        );
        writer.WriteLine(new string('-', 100));
        writer.Flush();
    }

    void Update()
    {
        if (receiver == null) return;

        // Log baseline end once
        if (receiver.baselineReady && !baselineLogged)
        {
            string timeStr = TimeSpan.FromSeconds(Time.time).ToString(@"hh\:mm\:ss");
            writer.WriteLine(
                $"{timeStr,-10} {"BASELINE END",-18} {"–",-10} {"–",-8} " +
                $"{receiver.heartRate,-8:F1} {receiver.hrv_ibi,-8:F1} " +
                $"{receiver.hrv_rmssd,-8:F1} {receiver.hrv_lfhf,-8:F2} " +
                $"{receiver.breathingRate,-12:F3} {receiver.signalQuality,-6:F3}"
            );
            writer.Flush();
            baselineLogged = true;
            Debug.Log($"[RppgLogger] Baseline complete — HR: {receiver.heartRate:F1} BPM  IBI: {receiver.hrv_ibi:F1}ms  RMSSD: {receiver.hrv_rmssd:F1}ms  Arousal tracking started.");
        }

        if (!receiver.baselineReady) return;
        if (logLevelChangesOnly) return;

        logTimer += Time.deltaTime;
        if (logTimer >= logInterval)
        {
            LogEntry("DATA");
            logTimer = 0f;
        }
    }

    private void OnLevelChanged(string oldLevel, string newLevel)
    {
        LogEntry($"LEVEL {oldLevel}>{newLevel}");
    }

    private void LogEntry(string eventType)
    {
        string timeStr = TimeSpan.FromSeconds(Time.time).ToString(@"hh\:mm\:ss");

        string line = string.Format(
            "{0,-10} {1,-18} {2,-10} {3,-8} {4,-8} {5,-8} {6,-8} {7,-8} {8,-12} {9,-6}",
            timeStr,
            eventType,
            receiver.arousalLabel,
            receiver.arousalScore.ToString("F3"),
            receiver.heartRate.ToString("F1"),
            receiver.hrv_ibi.ToString("F1"),
            receiver.hrv_rmssd.ToString("F1"),
            receiver.hrv_lfhf.ToString("F2"),
            receiver.breathingRate.ToString("F3"),
            receiver.signalQuality.ToString("F3")
        );

        writer.WriteLine(line);
        writer.Flush();
        entryCount++;
    }

    public void EndSession()
    {
        if (writer == null) return;
        writer.WriteLine();
        writer.WriteLine(new string('-', 100));
        writer.WriteLine($"Session ended: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        writer.WriteLine($"Total entries logged: {entryCount}");
        writer.Close();
        writer = null;
        Debug.Log($"[RppgLogger] Session ended — {entryCount} entries saved to: {filePath}");
    }

    void OnApplicationQuit()
    {
        EndSession();
    }
}