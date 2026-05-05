using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RppgReceiver : MonoBehaviour
{
    [Header("Live Data")]
    public float heartRate    = 0f;
    public float signalQuality = 0f;
    public float hrv_rmssd   = 0f;
    public float hrv_ibi     = 0f;
    public float hrv_lf_hf   = 0f;
    public float breathingRate = 0f;

    [Header("Arousal")]
    public float cognitiveLoadScore = 0f;
    public string cognitiveLoadLabel = "Low";
    public bool signalValid = false;

    [Header("Baseline")]
    public float baselineDuration    = 120f;
    public bool  isCollectingBaseline = false;
    public bool  baselineReady        = false;
    public int   baselineSamples      = 0;

    [Header("Baseline Averages (for logging)")]
    public float baseline_HR        = 0f;
    public float baseline_IBI       = 0f;
    public float baseline_RMSSD     = 0f;
    public float baseline_LFHF      = 0f;
    public float baseline_Breathing = 0f;
    public float baseline_SQI       = 0f;

    [Header("Smoothing")]
    public int smoothingWindow = 5;

    private const float SubsampleInterval = 10f;
    private float baselineTimer   = 0f;
    private float subsampleTimer  = 0f;

    private List<float> baselineRMSSDSamples    = new List<float>();
    private List<float> baselineHRSamples        = new List<float>();
    private List<float> baselineIBISamples       = new List<float>();
    private List<float> baselineLFHFSamples      = new List<float>();
    private List<float> baselineBreathingSamples = new List<float>();
    private List<float> baselineSQISamples       = new List<float>();

    private float baselineRMSSD_mean, baselineRMSSD_std;
    private Queue<float> scoreHistory = new Queue<float>();

    private UdpClient  udpClient;
    private Thread     receiveThread;
    private RppgPayload latestPayload;
    private bool        newData = false;
    private readonly object dataLock = new object();

    void Start()
    {
        DontDestroyOnLoad(gameObject);
        udpClient     = new UdpClient(5005);
        receiveThread = new Thread(Receive) { IsBackground = true };
        receiveThread.Start();
        StartBaseline();
    }

    public void StartBaseline()
    {
        isCollectingBaseline = true;
        baselineReady        = false;
        baselineTimer        = 0f;
        subsampleTimer       = 0f;
        baselineSamples      = 0;

        baselineRMSSDSamples.Clear();
        baselineHRSamples.Clear();
        baselineIBISamples.Clear();
        baselineLFHFSamples.Clear();
        baselineBreathingSamples.Clear();
        baselineSQISamples.Clear();
        scoreHistory.Clear();

        Debug.Log("[RppgReceiver] Baseline started");
    }

    void Update()
    {
        RppgPayload payload = null;
        lock (dataLock)
        {
            if (newData) { payload = latestPayload; newData = false; }
        }

        if (isCollectingBaseline)
        {
            baselineTimer  += Time.deltaTime;
            subsampleTimer += Time.deltaTime;

            Debug.Log($"[Baseline] {baselineTimer:F0}s / {baselineDuration:F0}s — samples: {baselineSamples}");

            if (payload != null &&
                payload.hrv != null &&
                payload.hrv.rmssd > 0f &&
                subsampleTimer >= SubsampleInterval)
            {
                baselineRMSSDSamples.Add(payload.hrv.rmssd);
                baselineHRSamples.Add(payload.hr);
                baselineIBISamples.Add(payload.hrv.ibi);
                baselineLFHFSamples.Add(payload.hrv.lf_hf);
                baselineBreathingSamples.Add(payload.hrv.breathingrate);
                baselineSQISamples.Add(payload.sqi);
                baselineSamples++;
                subsampleTimer = 0f;
            }

            if (baselineTimer >= baselineDuration)
                FinalizeBaseline();

            if (payload != null) UpdateLive(payload);
            return;
        }

        if (payload == null || !baselineReady) return;

        UpdateLive(payload);

        signalValid = payload.sqi > 0.3f;
        if (!signalValid) return;

        float rawScore = CalculateCognitiveLoad(payload.hrv);

        scoreHistory.Enqueue(rawScore);
        if (scoreHistory.Count > smoothingWindow)
            scoreHistory.Dequeue();

        cognitiveLoadScore = scoreHistory.Average();
        cognitiveLoadLabel = GetLabel(cognitiveLoadScore);
    }

    private void UpdateLive(RppgPayload p)
    {
        heartRate     = p.hr;
        signalQuality = p.sqi;

        if (p.hrv != null)
        {
            hrv_rmssd   = p.hrv.rmssd;
            hrv_ibi     = p.hrv.ibi;
            hrv_lf_hf   = p.hrv.lf_hf;
            breathingRate = p.hrv.breathingrate;
        }
    }

    private void FinalizeBaseline()
    {
        if (baselineRMSSDSamples.Count < 6)
        {
            Debug.LogWarning("[RppgReceiver] Not enough baseline samples, restarting...");
            StartBaseline();
            return;
        }

        baselineRMSSD_mean = baselineRMSSDSamples.Average();

        float variance = baselineRMSSDSamples
            .Select(v => (v - baselineRMSSD_mean) * (v - baselineRMSSD_mean))
            .Average();

        baselineRMSSD_std = Mathf.Sqrt(variance);

        // Store averages so RppgLogger can write BASELINE END row
        baseline_HR        = baselineHRSamples.Average();
        baseline_IBI       = baselineIBISamples.Average();
        baseline_RMSSD     = baselineRMSSD_mean;
        baseline_LFHF      = baselineLFHFSamples.Average();
        baseline_Breathing = baselineBreathingSamples.Average();
        baseline_SQI       = baselineSQISamples.Average();

        baselineReady        = true;
        isCollectingBaseline = false;

        Debug.Log($"[RppgReceiver] Baseline RMSSD: {baselineRMSSD_mean:F2} ± {baselineRMSSD_std:F2} ({baselineSamples} samples)");
    }

    private float CalculateCognitiveLoad(HrvData hrv)
    {
        float delta = baselineRMSSD_mean - hrv.rmssd;
        float std   = Mathf.Max(baselineRMSSD_std, baselineRMSSD_mean * 0.05f);
        float z     = delta / std;
        return Mathf.Clamp01(Mathf.Max(0f, z) / 2f);
    }

    private string GetLabel(float score)
    {
        if (score < 0.25f) return "Low";
        if (score < 0.5f)  return "Medium";
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
                lock (dataLock)
                {
                    latestPayload = payload;
                    newData       = true;
                }
            }
            catch { }
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
    public float   hr;
    public float   sqi;
    public HrvData hrv;
}