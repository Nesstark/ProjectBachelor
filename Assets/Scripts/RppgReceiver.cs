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
    public float arousalScore = 0f;
    public float stressLevel => arousalScore;
    public string arousalLabel = "Low";
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
    private string previousArousalLabel = "";
    public event Action<string, string> OnArousalLevelChanged;

    // UDP
    private UdpClient udpClient;
    private Thread receiveThread;
    private RppgPayload latestPayload;
    private bool newData = false;
    private readonly object dataLock = new object();

    void Start()
    {
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
        // Always tick the baseline timer regardless of data
        if (isCollectingBaseline)
        {
            baselineTimer += Time.deltaTime;
            Debug.Log($"[Baseline] {baselineTimer:F0}s / {baselineDuration:F0}s — samples: {baselineSamples}");

            if (baselineTimer >= baselineDuration)
                FinalizeBaseline();
        }

        // Get latest UDP data if available
        RppgPayload payload = null;
        lock (dataLock)
        {
            if (newData) { payload = latestPayload; newData = false; }
        }

        if (payload == null) return;

        // Update live values
        heartRate     = payload.hr;
        signalQuality = payload.sqi;

        if (payload.hrv != null)
        {
            hrv_rmssd     = payload.hrv.rmssd;
            hrv_ibi       = payload.hrv.ibi;
            hrv_lfhf      = payload.hrv.lf_hf;
            breathingRate = payload.hrv.breathingrate;
        }

        // Accumulate baseline samples
        if (isCollectingBaseline && payload.hrv != null && payload.hrv.ibi > 0)
        {
            baselineIBISum   += payload.hrv.ibi;
            baselineRMSSDSum += payload.hrv.rmssd;
            baselineLFHFSum  += payload.hrv.lf_hf;
            baselineSamples++;
        }

        // Calculate arousal once baseline is ready
        if (baselineReady && payload.hrv != null)
        {
            signalValid = payload.sqi > 0.3f && payload.hrv.ibi > 300f;

            if (signalValid)
            {
                arousalScore = CalculateArousal(payload.hrv);
                arousalLabel = GetArousalLabel(arousalScore);

                if (arousalLabel != previousArousalLabel && previousArousalLabel != "")
                    OnArousalLevelChanged?.Invoke(previousArousalLabel, arousalLabel);

                previousArousalLabel = arousalLabel;
            }
            else if (payload.hrv.ibi < 300f && heartRate > 0f)
            {
                // Fallback to HR-based arousal when IBI is unavailable
                float hrBaseline = 60000f / baselineIBI;
                arousalScore = Mathf.Clamp01((heartRate - hrBaseline) / hrBaseline);
                arousalLabel = GetArousalLabel(arousalScore);

                if (arousalLabel != previousArousalLabel && previousArousalLabel != "")
                    OnArousalLevelChanged?.Invoke(previousArousalLabel, arousalLabel);

                previousArousalLabel = arousalLabel;
                Debug.LogWarning("[RppgReceiver] Signal too weak — using HR fallback.");
            }
            else
            {
                Debug.LogWarning("[RppgReceiver] Signal too weak — arousal paused. Get back on screen.");
            }
        }
    }

    private void FinalizeBaseline()
    {
        baselineIBI   = baselineSamples > 0 ? baselineIBISum   / baselineSamples : 800f;
        baselineRMSSD = baselineSamples > 0 ? baselineRMSSDSum / baselineSamples : 60f;
        baselineLFHF  = baselineSamples > 0 ? baselineLFHFSum  / baselineSamples : 1f;

        isCollectingBaseline = false;
        baselineReady = true;
        Debug.Log($"[RppgReceiver] Baseline complete — IBI: {baselineIBI:F1}ms  RMSSD: {baselineRMSSD:F1}ms  LF/HF: {baselineLFHF:F2}");
    }

    private float CalculateArousal(HrvData hrv)
    {
        // If IBI is missing fall back to HR
        if (hrv.ibi < 300f)
        {
            float hrBaseline = 60000f / baselineIBI;
            return Mathf.Clamp01((heartRate - hrBaseline) / hrBaseline);
        }

        // IBI — reaches max score at 30% drop from baseline
        float ibiScore   = Mathf.Clamp01((baselineIBI - hrv.ibi) / (baselineIBI * 0.3f));

        // RMSSD — lower than baseline = more aroused
        float rmssdScore = baselineRMSSD > 0 ? Mathf.Clamp01(1f - (hrv.rmssd / baselineRMSSD)) : 0f;

        // LF/HF — reaches max at 1.5x baseline, heavily weighted for phasic detection
        float lfhfScore  = baselineLFHF  > 0 ? Mathf.Clamp01(hrv.lf_hf / (baselineLFHF * 1.5f)) : 0f;

        // LF/HF now weighted equally with IBI for better phasic event detection
        return (ibiScore * 0.4f) + (rmssdScore * 0.2f) + (lfhfScore * 0.4f);
    }

    private string GetArousalLabel(float score)
    {
        if (score < 0.33f) return "Low";
        if (score < 0.66f) return "Medium";
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