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
    public float heartRate = 0f;
    public float signalQuality = 0f;
    public float hrv_rmssd = 0f;
    public float breathingRate = 0f;

    [Header("Arousal")]
    public float cognitiveLoadScore = 0f;
    public string cognitiveLoadLabel = "Low";
    public bool signalValid = false;

    [Header("Baseline")]
    public float baselineDuration = 120f;
    public bool isCollectingBaseline = false;
    public bool baselineReady = false;

    public int baselineSamples = 0; // FIXED: int instead of float

    [Header("Smoothing")]
    public int smoothingWindow = 5;

    private const float SubsampleInterval = 10f;
    private float baselineTimer = 0f;
    private float subsampleTimer = 0f;

    private List<float> baselineRMSSDSamples = new List<float>();
    private float baselineRMSSD_mean, baselineRMSSD_std;

    private Queue<float> scoreHistory = new Queue<float>();

    private UdpClient udpClient;
    private Thread receiveThread;
    private RppgPayload latestPayload;
    private bool newData = false;
    private readonly object dataLock = new object();

    void Start()
    {
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
        baselineTimer = 0f;
        subsampleTimer = 0f;

        baselineRMSSDSamples.Clear();
        scoreHistory.Clear();
        baselineSamples = 0; // FIXED: reset counter

        Debug.Log("Baseline started");
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
            baselineTimer += Time.deltaTime;
            subsampleTimer += Time.deltaTime;

            Debug.Log($"[Baseline] {baselineTimer:F0}s / {baselineDuration:F0}s — independent samples: {baselineSamples}");

            if (payload != null &&
                payload.hrv != null &&
                subsampleTimer >= SubsampleInterval)
            {
                baselineRMSSDSamples.Add(payload.hrv.rmssd);
                baselineSamples++; // FIXED: increment sample count
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
        heartRate = p.hr;
        signalQuality = p.sqi;

        if (p.hrv != null)
        {
            hrv_rmssd = p.hrv.rmssd;
            breathingRate = p.hrv.breathingrate;
        }
    }

    private void FinalizeBaseline()
    {
        if (baselineRMSSDSamples.Count < 6)
        {
            Debug.LogWarning("Not enough baseline samples, restarting...");
            StartBaseline();
            return;
        }

        baselineRMSSD_mean = baselineRMSSDSamples.Average();

        float variance = baselineRMSSDSamples
            .Select(v => (v - baselineRMSSD_mean) * (v - baselineRMSSD_mean))
            .Average();

        baselineRMSSD_std = Mathf.Sqrt(variance);

        baselineReady = true;
        isCollectingBaseline = false;

        Debug.Log($"Baseline RMSSD: {baselineRMSSD_mean} ± {baselineRMSSD_std} (Samples: {baselineSamples})");
    }

    private float CalculateCognitiveLoad(HrvData hrv)
    {
        float delta = baselineRMSSD_mean - hrv.rmssd;
        float std = Mathf.Max(baselineRMSSD_std, baselineRMSSD_mean * 0.05f);

        float z = delta / std;
        float score = Mathf.Clamp01(Mathf.Max(0f, z) / 2f);

        return score;
    }

    private string GetLabel(float score)
    {
        if (score < 0.25f) return "Low";
        if (score < 0.5f) return "Medium";
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
                    newData = true;
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
    public float breathingrate;
}

[System.Serializable]
public class RppgPayload
{
    public float hr;
    public float sqi;
    public HrvData hrv;
}