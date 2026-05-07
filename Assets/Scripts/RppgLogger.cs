using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class RppgLogger : MonoBehaviour
{
    public RppgReceiver receiver;

    [Header("Logging")]
    public bool  isLogging   = false;
    public float logInterval = 1f;

    private float         timer              = 0f;
    private List<string>  logLines           = new List<string>();
    private string        filePath;
    private float         sessionStartTime;
    private DateTime      sessionStartDateTime;
    private int           sessionNumber;

    private string previousLabel       = "";
    private bool   baselineStartLogged = false;
    private bool   baselineEndLogged   = false;
    private bool   isInBossRoom        = false;
    private int    dataEntryCount      = 0;

    // ── Singleton ─────────────────────────────────
    private static RppgLogger instance;

    // Column widths — must match the session file layout exactly
    private const int COL_TIME      = 11;
    private const int COL_EVENT     = 19;
    private const int COL_LOAD   = 11;
    private const int COL_SCORE     = 9;
    private const int COL_HR        = 9;
    private const int COL_IBI       = 9;
    private const int COL_RMSSD     = 9;
    private const int COL_LFHF      = 9;
    private const int COL_BREATHING = 13;

    // ─────────────────────────────────────────────

    void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        string username = System.Environment.UserName;
        string folder   = Path.Combine(
            "C:\\Users", username,
            "Documents", "GitHub", "ProjectBachelor", "Assets", "Logged");

        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        string[] existing = Directory.GetFiles(folder, "Session_*.txt");
        sessionNumber = existing.Length + 1;

        string dateStr  = DateTime.Now.ToString("yyyy-MM-dd");
        string fileName = $"Session_{sessionNumber:D2}_{dateStr}.txt";
        filePath = Path.Combine(folder, fileName);

        StartLogging();
    }

    void Update()
    {
        if (!isLogging || receiver == null) return;

        // BASELINE START — logged once as soon as baseline begins
        if (receiver.isCollectingBaseline && !baselineStartLogged)
        {
            logLines.Add(Row(GetElapsed(), "BASELINE START", "", "", "", "", "", "", "", ""));
            baselineStartLogged = true;
        }

        // BASELINE END — logged once as soon as baseline completes
        if (!receiver.isCollectingBaseline && receiver.baselineReady && !baselineEndLogged)
        {
            logLines.Add(Row(
                GetElapsed(), "BASELINE END",
                "–", "–",
                $"{receiver.baseline_HR:F1}",
                $"{receiver.baseline_IBI:F1}",
                $"{receiver.baseline_RMSSD:F1}",
                $"{receiver.baseline_LFHF:F2}",
                $"{receiver.baseline_Breathing:F3}",
                $"{receiver.baseline_SQI:F3}"));
            baselineEndLogged = true;
        }

        // Only log live data once baseline is done and signal is valid
        if (!receiver.baselineReady || !receiver.signalValid) return;

        bool bossRoom = RoomManager.Instance?.CurrentRoom?.isBossRoom == true;
        if (bossRoom && !isInBossRoom)
        {
            logLines.Add(Row(GetElapsed(), "BOSS ROOM ENTER", "", "", "", "", "", "", "", ""));
        }
        else if (!bossRoom && isInBossRoom)
        {
            logLines.Add(Row(GetElapsed(), "BOSS ROOM EXIT", "", "", "", "", "", "", "", ""));
        }
        isInBossRoom = bossRoom;

        timer += Time.deltaTime;
        if (timer >= logInterval)
        {
            timer = 0f;
            LogData();
        }
    }

    // ─────────────────────────────────────────────

    private void LogData()
    {
        string t       = GetElapsed();
        string load    = receiver.cognitiveLoadLabel;
        string score   = $"{receiver.cognitiveLoadScore:F3}";
        string hr      = $"{receiver.heartRate:F1}";
        string ibi     = $"{receiver.hrv_ibi:F1}";
        string rmssd   = $"{receiver.hrv_rmssd:F1}";
        string lfhf    = $"{receiver.hrv_lf_hf:F2}";
        string breath  = $"{receiver.breathingRate:F3}";
        string sqi     = $"{receiver.signalQuality:F3}";
        bool bossRoom  = RoomManager.Instance?.CurrentRoom?.isBossRoom == true;

        if (previousLabel != "" && previousLabel != load)
        {
            string evt = $"LEVEL {previousLabel}>{load}";
            logLines.Add(Row(t, evt, load, score, hr, ibi, rmssd, lfhf, breath, sqi));
        }

        string eventName = bossRoom ? "BOSS DATA" : "DATA";
        logLines.Add(Row(t, eventName, load, score, hr, ibi, rmssd, lfhf, breath, sqi));
        previousLabel = load;
        dataEntryCount++;
    }

    private string Row(string time, string evt,
        string load, string score,
        string hr,      string ibi,
        string rmssd,   string lfhf,
        string breath,  string sqi)
    {
        return time.PadRight(COL_TIME)
             + evt.PadRight(COL_EVENT)
             + load.PadRight(COL_LOAD)
             + score.PadRight(COL_SCORE)
             + hr.PadRight(COL_HR)
             + ibi.PadRight(COL_IBI)
             + rmssd.PadRight(COL_RMSSD)
             + lfhf.PadRight(COL_LFHF)
             + breath.PadRight(COL_BREATHING)
             + sqi;
    }

    private string GetElapsed()
    {
        float e = Time.time - sessionStartTime;
        int h = (int)(e / 3600);
        int m = (int)((e % 3600) / 60);
        int s = (int)(e % 60);
        return $"{h:D2}:{m:D2}:{s:D2}";
    }

    // ─────────────────────────────────────────────

    public void StartLogging()
    {
        logLines.Clear();
        sessionStartTime     = Time.time;
        sessionStartDateTime = DateTime.Now;
        baselineStartLogged  = false;
        baselineEndLogged    = false;
        previousLabel        = "";
        dataEntryCount       = 0;
        timer                = 0f;
        isLogging            = true;

        string eqLine = new string('=', 46);
        logLines.Add(eqLine);
        logLines.Add($"  Session: Session {sessionNumber:D2}");
        logLines.Add($"  Started: {sessionStartDateTime:yyyy-MM-dd HH:mm:ss}");
        logLines.Add(eqLine);
        logLines.Add("");

        logLines.Add(
            "Time".PadRight(COL_TIME)      +
            "Event".PadRight(COL_EVENT)    +
            "Load".PadRight(COL_LOAD)      +
            "Score".PadRight(COL_SCORE)    +
            "HR".PadRight(COL_HR)          +
            "IBI".PadRight(COL_IBI)        +
            "RMSSD".PadRight(COL_RMSSD)    +
            "LF/HF".PadRight(COL_LFHF)    +
            "Breathing".PadRight(COL_BREATHING) +
            "SQI");
        logLines.Add(new string('-', 100));

        Debug.Log($"[RppgLogger] Session {sessionNumber:D2} started → {filePath}");
    }

    public void StopLogging()
    {
        isLogging = false;

        logLines.Add(new string('-', 100));
        logLines.Add($"Session ended: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        logLines.Add($"Total entries logged: {dataEntryCount}");

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

    void OnApplicationQuit()
    {
        if (isLogging) StopLogging();
    }
}