using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class RppgReceiver : MonoBehaviour
{
    public float heartRate = 0f;
    public float signalQuality = 0f;
    public float hrv_rmssd = 0f;

    // Derived stress estimate (0=calm, 1=stressed)
    public float stressLevel => Mathf.Clamp01(1f - (hrv_rmssd / 80f));

    private UdpClient udpClient;
    private Thread receiveThread;

    void Start()
    {
        udpClient = new UdpClient(5005);
        receiveThread = new Thread(Receive) { IsBackground = true };
        receiveThread.Start();
    }

    void Receive()
    {
        while (true)
        {
            IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = udpClient.Receive(ref ep);
            string json = Encoding.UTF8.GetString(data);
            var payload = JsonUtility.FromJson<RppgPayload>(json);
            heartRate = payload.hr;
            signalQuality = payload.sqi;
            if (payload.hrv != null) hrv_rmssd = payload.hrv.rmssd;
        }
    }

    void OnDestroy() => udpClient?.Close();
}

[System.Serializable]
public class HrvData { public float rmssd; public float sdnn; }

[System.Serializable]
public class RppgPayload { public float hr; public float sqi; public HrvData hrv; }