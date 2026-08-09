using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace F1AR.Telemetry
{
    /// <summary>
    /// UDP Receiver script to capture SimTools telemetry packets over UDP socket.
    /// Supports both:
    /// 1. SimTools Binary 8Bit packets: !8<6 bytes># (0..255 mapped to -128..+127)
    /// 2. SimTools CSV Decimal packets: <Axis1>,<Axis2>,<Axis3>,<Axis4>,<Axis5>,<Axis6>
    /// </summary>
    public class SimToolsUDPReceiver : MonoBehaviour
    {
        [Header("UDP Socket Configuration")]
        [Tooltip("Port configured in SimTools Interface 2 (e.g. 4124 or 4123).")]
        [SerializeField] private int listenPort = 4124;

        [Header("Live 6DOF Telemetry Data")]
        [SerializeField] private float sway = 0f;    // Axis1: X Translation
        [SerializeField] private float surge = 0f;   // Axis2: Z Translation
        [SerializeField] private float heave = 0f;   // Axis3: Y Translation
        [SerializeField] private float pitch = 0f;   // Axis4: X Rotation
        [SerializeField] private float roll = 0f;    // Axis5: Z Rotation
        [SerializeField] private float yaw = 0f;     // Axis6: Y Rotation

        public float Sway => sway;
        public float Surge => surge;
        public float Heave => heave;
        public float Pitch => pitch;
        public float Roll => roll;
        public float Yaw => yaw;

        private UdpClient _udpClient;
        private Thread _receiveThread;
        private bool _isRunning = false;
        private readonly object _lock = new object();

        private float _swayBuffer, _surgeBuffer, _heaveBuffer, _pitchBuffer, _rollBuffer, _yawBuffer;

        private void OnEnable()
        {
            StartReceiver();
        }

        private void OnDisable()
        {
            StopReceiver();
        }

        private void OnApplicationQuit()
        {
            StopReceiver();
        }

        private void StartReceiver()
        {
            if (_isRunning) return;

            int targetPort = listenPort;
            bool boundSuccessfully = false;

            for (int attempt = 0; attempt < 6; attempt++)
            {
                try
                {
                    Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    socket.ExclusiveAddressUse = false;

                    IPAddress bindIp = (attempt % 2 == 0) ? IPAddress.Loopback : IPAddress.Any;
                    IPEndPoint endPoint = new IPEndPoint(bindIp, targetPort);

                    socket.Bind(endPoint);

                    _udpClient = new UdpClient();
                    _udpClient.Client = socket;

                    listenPort = targetPort;
                    boundSuccessfully = true;

                    Debug.Log($"[SimToolsUDPReceiver] Listening for SimTools Interface 2 telemetry on UDP port {listenPort} ({bindIp})...");
                    break;
                }
                catch (Exception)
                {
                    if (attempt % 2 == 1)
                    {
                        targetPort++;
                    }
                }
            }

            if (!boundSuccessfully)
            {
                Debug.LogWarning($"[SimToolsUDPReceiver] Could not bind UDP port {listenPort}. Ensure Interface 2 in SimTools is set to UDP port {listenPort}.");
                return;
            }

            _isRunning = true;
            _receiveThread = new Thread(ReceiveThreadLoop)
            {
                IsBackground = true
            };
            _receiveThread.Start();
        }

        private void StopReceiver()
        {
            _isRunning = false;
            if (_udpClient != null)
            {
                try { _udpClient.Close(); } catch { }
                _udpClient = null;
            }
            if (_receiveThread != null && _receiveThread.IsAlive)
            {
                try { _receiveThread.Abort(); } catch { }
                _receiveThread = null;
            }
        }

        private void ReceiveThreadLoop()
        {
            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

            while (_isRunning && _udpClient != null)
            {
                try
                {
                    byte[] data = _udpClient.Receive(ref remoteEndPoint);
                    if (data == null || data.Length == 0) continue;

                    // 1. SimTools Binary 8Bit format: !8<6 bytes># (Total 8 bytes)
                    if (data.Length >= 8 && data[0] == (byte)'!' && data[1] == (byte)'8')
                    {
                        // 6 byte values (0..255) mapped to normalized (-128..+127) range
                        float s  = (data[2] - 128f);
                        float su = (data[3] - 128f);
                        float h  = (data[4] - 128f);
                        float p  = (data[5] - 128f);
                        float r  = (data[6] - 128f);
                        float y  = (data[7] - 128f);

                        lock (_lock)
                        {
                            _swayBuffer = s;
                            _surgeBuffer = su;
                            _heaveBuffer = h;
                            _pitchBuffer = p;
                            _rollBuffer = r;
                            _yawBuffer = y;
                        }
                    }
                    // 2. SimTools CSV String format: <Axis1>,<Axis2>,<Axis3>,<Axis4>,<Axis5>,<Axis6>
                    else
                    {
                        string text = Encoding.UTF8.GetString(data).Trim();
                        string cleanText = text.Replace("<", "").Replace(">", "").Replace("!", "").Replace("#", "");
                        string[] tokens = cleanText.Split(new char[] { ',', ';', ' ', ':' }, StringSplitOptions.RemoveEmptyEntries);

                        if (tokens.Length >= 6)
                        {
                            int offset = (tokens[0] == "A" || tokens[0] == "F" || tokens[0] == "8") ? 1 : 0;

                            float.TryParse(tokens[offset + 0], out float s);
                            float.TryParse(tokens[offset + 1], out float su);
                            float.TryParse(tokens[offset + 2], out float h);
                            float.TryParse(tokens[offset + 3], out float p);
                            float.TryParse(tokens[offset + 4], out float r);
                            float.TryParse(tokens[offset + 5], out float y);

                            lock (_lock)
                            {
                                _swayBuffer = s;
                                _surgeBuffer = su;
                                _heaveBuffer = h;
                                _pitchBuffer = p;
                                _rollBuffer = r;
                                _yawBuffer = y;
                            }
                        }
                    }
                }
                catch (SocketException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SimToolsUDPReceiver] Telemetry parse error: {ex.Message}");
                }
            }
        }

        private void Update()
        {
            lock (_lock)
            {
                sway = _swayBuffer;
                surge = _surgeBuffer;
                heave = _heaveBuffer;
                pitch = _pitchBuffer;
                roll = _rollBuffer;
                yaw = _yawBuffer;
            }
        }

        public void SetManualValues(float s, float su, float h, float p, float r, float y)
        {
            sway = s;
            surge = su;
            heave = h;
            pitch = p;
            roll = r;
            yaw = y;
        }
    }
}
