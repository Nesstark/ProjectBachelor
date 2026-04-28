using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System;
using UnityEngine;

public class RppgReceiver : MonoBehaviour
{
    [Header("Live Data")]
    public float heartRate = 0f;
    public float signalQuality = 0f;
    public float hrv_rmssd = 0f;
    public float hrv_ibi = 0f;
    public float hrv_lfhf = 0f;
    public float breathingRate = 0f;

    [Header("Arousal")]
    public float cognitiveLoadScore = 0f;
    public float stressLevel => cognitiveLoadScore;
    public string cognitiveLoadLabel = "Low";
    public bool signalValid = false;

    [Header("Baseline")]
    public float baselineDuration = 120f;
    public bool isCollectingBaseline = false;
    public bool baselineReady = false;

    // Baseline internals
    private float baselineIBI, baselineRMSSD, baselineLFHF;
    private float baselineTimer, baselineIBISum, baselineRMSSDSum, baselineLFHFSum;
    private int baselineSamples;

    // Level change event
    private string previousCognitiveLoadLabel = "";
    public event Action<string, string> OnCognitiveLoadLevelChanged;

    // UDP
    private UdpClient udpClient;
    private Thread receiveThread;
    private RppgPayload latestPayload;
    private bool newData = false;
    private readonly object dataLock = new object();

    void Start()
    {
        // ── Persist across scene loads — only one instance ever exists ──
        RppgReceiver[] existing = FindObjectsByType<RppgReceiver>(FindObjectsSortMode.None);
        if (existing.Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);

        udpClient = new UdpClient(5005);
        receiveThread = new Thread(Receive) { IsBackground = true };
        receiveThread.Start();

        StartBaseline();
    }

    public void StartBaseline()
    {
        isCollectingBaseline = true;
        baselineReady = false;
        baselineTimer = baselineIBISum = baselineRMSSDSum = baselineLFHFSum = 0f;
        baselineSamples = 0;
        Debug.Log("Baseline started — sit still for 2 minutes.");
    }

    void Update()
    {
        if (isCollectingBaseline)
        {
            baselineTimer += Time.deltaTime;
            Debug.Log($"[Baseline] {baselineTimer:F0}s / {baselineDuration:F0}s — samples: {baselineSamples}");

            if (baselineTimer >= baselineDuration)
                FinalizeBaseline();
        }

        RppgPayload payload = null;
        lock (dataLock)
        {
            if (newData) { payload = latestPayload; newData = false; }
        }

        if (payload == null) return;

        heartRate     = payload.hr;
        signalQuality = payload.sqi;

        if (payload.hrv != null)
        {
            hrv_rmssd     = payload.hrv.rmssd;
            hrv_ibi       = payload.hrv.ibi;
            hrv_lfhf      = payload.hrv.lf_hf;
            breathingRate = payload.hrv.breathingrate;
        }

        if (isCollectingBaseline && payload.hrv != null && payload.hrv.ibi > 0)
        {
            baselineIBISum   += payload.hrv.ibi;
            baselineRMSSDSum += payload.hrv.rmssd;
            baselineLFHFSum  += payload.hrv.lf_hf;
            baselineSamples++;
        }

        if (baselineReady && payload.hrv != null)
        {
            signalValid = payload.sqi > 0.3f && payload.hrv.ibi > 300f;

            if (signalValid)
            {
                cognitiveLoadScore = CalculateCognitiveLoad(payload.hrv);                    // FIX: was referencing non-existent arousalScore
                cognitiveLoadLabel = GetCognitiveLoadLabel(cognitiveLoadScore);              // FIX: was referencing non-existent arousalScore

                if (cognitiveLoadLabel != previousCognitiveLoadLabel && previousCognitiveLoadLabel != "")
                    OnCognitiveLoadLevelChanged?.Invoke(previousCognitiveLoadLabel, cognitiveLoadLabel);  // FIX: was calling non-existent OnArousalLevelChanged

                previousCognitiveLoadLabel = cognitiveLoadLabel;
            }
            else
            {
                Debug.LogWarning("[RppgReceiver] Signal too weak — cognitive load monitoring paused. Get back on screen.");
            }
        }
    }

    private void FinalizeBaseline()
    {
        if (baselineSamples < 10)
        {
            Debug.LogWarning($"[RppgReceiver] Not enough baseline samples ({baselineSamples}) — restarting baseline collection. Check camera and lighting.");
            StartBaseline();
            return;
        }

        baselineIBI   = baselineIBISum   / baselineSamples;
        baselineRMSSD = baselineRMSSDSum / baselineSamples;
        baselineLFHF  = baselineLFHFSum  / baselineSamples;

        isCollectingBaseline = false;
        baselineReady = true;
        Debug.Log($"[RppgReceiver] Baseline complete — IBI: {baselineIBI:F1}ms  RMSSD: {baselineRMSSD:F1}ms  LF/HF: {baselineLFHF:F2}  (from {baselineSamples} samples)");
    }

    private float CalculateCognitiveLoad(HrvData hrv)
    {
        float ibiScore   = Mathf.Clamp01((baselineIBI - hrv.ibi) / baselineIBI);
        float rmssdScore = baselineRMSSD > 0 ? Mathf.Clamp01(1f - (hrv.rmssd / baselineRMSSD)) : 0f;
        float lfhfScore  = baselineLFHF  > 0 ? Mathf.Clamp01(hrv.lf_hf / (baselineLFHF * 3f))  : 0f;

        return (ibiScore * 0.5f) + (rmssdScore * 0.3f) + (lfhfScore * 0.2f);
    }

    private string GetCognitiveLoadLabel(float score)
    {
        if (score < 0.15f) return "Low";
        if (score < 0.4f)  return "Medium";
        return "High";
    }

    void Receive()
    {
        IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);
        while (true)
        {
            try
            {
                byte[] data = udpClient.Receive(ref ep);
                string json = Encoding.UTF8.GetString(data);
                var payload = JsonUtility.FromJson<RppgPayload>(json);
                lock (dataLock) { latestPayload = payload; newData = true; }
            }
            catch (Exception e) { Debug.LogWarning("UDP error: " + e.Message); }
        }
    }

    void OnDestroy()
    {
        receiveThread?.Abort();
        udpClient?.Close();
    }
}

[System.Serializable]
public class HrvData
{
    public float rmssd;
    public float sdnn;
    public float ibi;
    public float lf_hf;
    public float breathingrate;
}

[System.Serializable]
public class RppgPayload
{
    public float hr;
    public float sqi;
    public HrvData hrv;
}