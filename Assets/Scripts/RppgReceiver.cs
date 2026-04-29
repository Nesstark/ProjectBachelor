using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System;
using System.Collections.Generic;
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

        List<float> rrList = new List<float>();

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

        if (payload != null) {
            UpdateVariables(payload);
        }

        // TODO: Evaluate signalValid variable. Introducing rolling window to smooth out data and circumvent loss of signal. Investigate why CognitiveLoadScore returns 0.00 in periods despite SQI > .5.

        if (baselineReady && payload.hrv != null) {
            signalValid = payload.sqi > 0.3f;

            if (signalValid) {
                cognitiveLoadScore = CalculateCognitiveLoad(payload.hrv);
                cognitiveLoadLabel = GetCognitiveLoadLabel(cognitiveLoadScore);

                if (cognitiveLoadLabel != previousCognitiveLoadLabel && previousCognitiveLoadLabel != "")
                    OnCognitiveLoadLevelChanged?.Invoke(previousCognitiveLoadLabel, cognitiveLoadLabel);

                previousCognitiveLoadLabel = cognitiveLoadLabel;
            }
            else
            {
                Debug.LogWarning("[RppgReceiver] Signal too weak — cognitive load monitoring paused. Get back on screen.");
            }
        }
    }

    private void UpdateVariables(RppgPayload payload)
    {
        heartRate     = payload.hr;
        signalQuality = payload.sqi;

        if (payload.hrv != null) {
            hrv_rmssd     = payload.hrv.rmssd;
            hrv_ibi       = payload.hrv.ibi;
            hrv_lfhf      = payload.hrv.lf_hf;
            breathingRate = payload.hrv.breathingrate;
        }

        if (isCollectingBaseline && payload.hrv != null && payload.hrv.ibi > 0) {
            baselineIBISum   += payload.hrv.ibi;
            baselineRMSSDSum += payload.hrv.rmssd;
            baselineLFHFSum  += payload.hrv.lf_hf;
            baselineSamples++;

            rrList.Add(payload.hrv.ibi);
        }
    }

    private void FinalizeBaseline()
    {
        if (baselineSamples < 10) {
            Debug.LogWarning($"[RppgReceiver] Not enough baseline samples ({baselineSamples}) — restarting baseline collection. Check camera and lighting.");
            StartBaseline();
            return;
        }

        baselineIBI   = baselineIBISum   / baselineSamples;
        baselineLFHF  = baselineLFHFSum  / baselineSamples;
        // baselineRMSSD = baselineRMSSDSum / baselineSamples;

        CalculateRMSSDBaseline(rrList.ToArray());

        isCollectingBaseline = false;
        baselineReady = true;
        Debug.Log($"[RppgReceiver] Baseline complete — IBI: {baselineIBI:F1}ms  RMSSD: {baselineRMSSD:F1}ms  LF/HF: {baselineLFHF:F2}  (from {baselineSamples} samples)");
    }

    private float CalculateRMSSDBaseline(float[] rrArray)
    {
        int n = rrArray.Length;
        if (n < 2) throw new ArgumentException("At least 2 RR intervals are required to calculate RMSSD baseline.");

        float sumSqDiffs = 0.0;
        for (int i = 1; i < n; i++) {
            float diff = rrArray[i] - rrArray[i - 1];
            sumSqDiffs += diff * diff;
        }

        float meanSqDiffs = sumSqDiffs / (n - 1);
        baselineRMSSD = Mathf.Sqrt(meanSqDiffs);
        return Mathf.Log(baselineRMSSD+1e-6f);
    }

    private float CalculateCognitiveLoad(HrvData hrv)
    {
        float logRMSSD = Math.Log(hrv.rmssd + 1e-6f);
        float deviation = logRMSSD - baselineRMSSD;
        float normalized = 0.5f*(1f +(float)Math.Tanh(deviation*2f));
        float rmssdScore = 1f - normalized; // invert scoring

        return rmssdScore;
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