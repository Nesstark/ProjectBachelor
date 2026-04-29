using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class RppgLogger : MonoBehaviour
{
    public RppgReceiver receiver;

    [Header("Logging")]
    public bool isLogging = false;
    public float logInterval = 1f;

    private float timer = 0f;
    private List<string> logLines = new List<string>();
    private string filePath;

    void Start()
    {
        filePath = Path.Combine(Application.persistentDataPath, "rppg_log.csv");

        // CSV header (updated to match receiver)
        logLines.Add("Time,HeartRate,RMSSD,BreathingRate,Score,Label,SignalQuality");
    }

    void Update()
    {
        if (!isLogging || receiver == null) return;

        timer += Time.deltaTime;

        if (timer >= logInterval)
        {
            timer = 0f;
            LogData();
        }
    }

    private void LogData()
    {
        string line = string.Format(
            "{0:F2},{1:F1},{2:F2},{3:F2},{4:F2},{5},{6:F2}",
            Time.time,
            receiver.heartRate,
            receiver.hrv_rmssd,
            receiver.breathingRate,
            receiver.cognitiveLoadScore,
            receiver.cognitiveLoadLabel,
            receiver.signalQuality
        );

        logLines.Add(line);
    }

    public void StartLogging()
    {
        logLines.Clear();
        logLines.Add("Time,HeartRate,RMSSD,BreathingRate,Score,Label,SignalQuality");

        isLogging = true;
        timer = 0f;

        Debug.Log("[RppgLogger] Logging started");
    }

    public void StopLogging()
    {
        isLogging = false;
        SaveToFile();

        Debug.Log("[RppgLogger] Logging stopped and saved");
    }

    private void SaveToFile()
    {
        try
        {
            File.WriteAllLines(filePath, logLines);
            Debug.Log("[RppgLogger] Saved to: " + filePath);
        }
        catch (IOException e)
        {
            Debug.LogError("[RppgLogger] File write failed: " + e.Message);
        }
    }
}