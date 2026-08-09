// Enable System.IO.Ports only if API Compatibility Level is set to .NET Framework in Unity Player Settings
#if (NET_4_6 || NET_FRAMEWORK) && !NET_STANDARD && !NET_STANDARD_2_0
#define HAS_SERIAL_PORT
#endif

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

#if HAS_SERIAL_PORT
using System.IO.Ports;
#endif

[System.Serializable]
public class PlatformController : MonoBehaviour
{
    [SerializeField] Slider[] sliders; // list of references to the sliders
    [SerializeField] bool useSliders = false;

    public enum PlatformModes { Mode_Basic, Mode_8Bit, Mode_Float32 };
    [SerializeField] PlatformModes mode = PlatformModes.Mode_Float32;

#if HAS_SERIAL_PORT
    SerialPort serialPort;
#endif
    public string comPort = "COM3";
    public int baudRate = 115200;

    bool initialized = false; // a bool to check if this controller has been initialized

    // 6 DOF Axis Order for Simviz Stewart Platform: [Sway, Surge, Heave, Pitch, Roll, Yaw]
    public byte[] byteValues; // six byte values to be sent to the platform (8Bit Mode)
    public float[] floatValues; // six 32bit float values (Float32 mode)

#if HAS_SERIAL_PORT
    private string startFrame = "!"; // '!' startFrame character (33) (to indicate the start of a message)
    private string endFrame = "#"; // '#' endFrame character (35) (to indicate the end of a message)
#endif

    private float nextSendTimestamp = 0; // timestamp to control the rate of sending
    [SerializeField] private float nextSendDelay = 0.02f; // delay between sends in seconds (float)

    private void Start()
    {
        if (!initialized) { Init(comPort, baudRate); }
    }

    public bool Init(string _com, int _baud)
    {
        if (initialized) return false;

        initialized = true;

        comPort = _com;
        baudRate = _baud;
        byteValues = new byte[] { 128, 128, 128, 128, 128, 128 };
        floatValues = new float[] { 0, 0, 0, 0, 0, 0 };

#if HAS_SERIAL_PORT
        try
        {
            if (serialPort == null && !string.IsNullOrEmpty(comPort))
            {
                serialPort = new SerialPort(@"\\.\" + comPort);          
                serialPort.BaudRate = baudRate;
                serialPort.Parity = Parity.None;
                serialPort.DataBits = 8;
                serialPort.ReadTimeout = 20;
                serialPort.Open();
                Debug.Log("[PlatformController] Serial Port Opened: " + comPort);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[PlatformController] Serial Port Open Warning: " + ex.Message);
        }
#endif

        if (useSliders) { ReinitializeSliders(); }
        HomePlatform();

        return true;
    }

    void Update()
    {
        if (useSliders == true) { UpdateValuesFromSliders(); }

        if (Time.time > nextSendTimestamp)
        {
            SendSerial(); 
            nextSendTimestamp = Time.time + nextSendDelay; 
        }
    }

    public void SendSerial()
    {
#if HAS_SERIAL_PORT
        if (serialPort == null || !serialPort.IsOpen) return;

        serialPort.Write(startFrame);

        if (mode == PlatformModes.Mode_Basic)
        {
            serialPort.Write(byteValues, 0, byteValues.Length);
        }
        else if (mode == PlatformModes.Mode_8Bit)
        {
            serialPort.Write("8");
            serialPort.Write(byteValues, 0, byteValues.Length);
        }
        else if (mode == PlatformModes.Mode_Float32)
        {
            serialPort.Write(startFrame);
            serialPort.Write("F");
            byte[] byteArray = new byte[24];

            for (int i = 0; i < floatValues.Length; i++)
            {
                byte[] myBytes = System.BitConverter.GetBytes(floatValues[i]);
                for (int b = 0; b < myBytes.Length; b++)
                {
                    byteArray[0 + i * 4 + b] = myBytes[b];
                }
            }
            serialPort.Write(byteArray, 0, byteArray.Length);
        }

        serialPort.Write(endFrame);
#endif
    }

    public void SendASCII(string cmd)
    {
#if HAS_SERIAL_PORT
        if (serialPort != null && serialPort.IsOpen)
        {
            serialPort.Write(startFrame);
            serialPort.Write("A" + cmd + "\n");
        }
#endif
    }

    public void HomePlatform()
    {
        if (useSliders)
        {
            ResetSliders();

            if (mode == PlatformModes.Mode_8Bit || mode == PlatformModes.Mode_Basic)
            {
                for (int i = 0; i < byteValues.Length; i++) byteValues[i] = 128;
            }
            else if (mode == PlatformModes.Mode_Float32)
            {
                for (int i = 0; i < floatValues.Length; i++) floatValues[i] = 0;
            }

            SendSerial();
        }
    }

    void OnApplicationQuit()
    {
#if HAS_SERIAL_PORT
        if (serialPort != null && serialPort.IsOpen)
        {
            HomePlatform();
            serialPort.Close();
        }
#endif
    }

    #region Slider Code
    void ReinitializeSliders()
    {
        if (sliders == null) return;
        for (int i = 0; i < sliders.Length; i++)
        {
            if (sliders[i] == null) continue;
            if (mode == PlatformModes.Mode_8Bit || mode == PlatformModes.Mode_Basic)
            {
                sliders[i].wholeNumbers = true;
                sliders[i].minValue = 0;
                sliders[i].maxValue = 255;
                sliders[i].value = mode == PlatformModes.Mode_8Bit ? 128 : 0;
            }
            else if (mode == PlatformModes.Mode_Float32)
            {
                sliders[i].wholeNumbers = false;
                sliders[i].minValue = -30;
                sliders[i].maxValue = 30;
                sliders[i].value = 0;
            }
        }
    }

    public void UpdateValuesFromSliders()
    {
        if (sliders == null) return;
        for (int i = 0; i < sliders.Length && i < floatValues.Length; i++)
        {
            if (sliders[i] == null) continue;
            if (mode == PlatformModes.Mode_Float32) { floatValues[i] = sliders[i].value; }
            else if (mode == PlatformModes.Mode_8Bit || mode == PlatformModes.Mode_Basic) { byteValues[i] = (byte)sliders[i].value; }
        }
    }

    public void ResetSliders()
    {
        ReinitializeSliders();
        if (sliders == null) return;
        for (int i = 0; i < sliders.Length; i++)
        {
            if (sliders[i] == null) continue;
            sliders[i].value = mode == PlatformModes.Mode_Float32 ? 0 : 128;
        }
    }
    #endregion

    #region Getters
    public float Sway
    {
        get { return (floatValues != null && floatValues.Length > 0) ? floatValues[0] : 0f; }
        set { if (floatValues != null && floatValues.Length > 0) floatValues[0] = value; }
    }
    public float Surge
    {
        get { return (floatValues != null && floatValues.Length > 1) ? floatValues[1] : 0f; }
        set { if (floatValues != null && floatValues.Length > 1) floatValues[1] = value; }
    }
    public float Heave
    {
        get { return (floatValues != null && floatValues.Length > 2) ? floatValues[2] : 0f; }
        set { if (floatValues != null && floatValues.Length > 2) floatValues[2] = value; }
    }
    public float Pitch
    {
        get { return (floatValues != null && floatValues.Length > 3) ? floatValues[3] : 0f; }
        set { if (floatValues != null && floatValues.Length > 3) floatValues[3] = value; }
    }
    public float Roll
    {
        get { return (floatValues != null && floatValues.Length > 4) ? floatValues[4] : 0f; }
        set { if (floatValues != null && floatValues.Length > 4) floatValues[4] = value; }
    }
    public float Yaw
    {
        get { return (floatValues != null && floatValues.Length > 5) ? floatValues[5] : 0f; }
        set { if (floatValues != null && floatValues.Length > 5) floatValues[5] = value; }
    }
    #endregion

    #region PlatformController Singleton
    private static PlatformController _singleton;
    public static PlatformController singleton
    {
        get
        {
            if (_singleton == null)
            {
                GameObject go = new GameObject("PlatformController");
                DontDestroyOnLoad(go);
                _singleton = go.AddComponent<PlatformController>();
            }
            return _singleton;
        }
    }
    #endregion
}
