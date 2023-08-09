using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using Warudo.Core.Utils;

public class VTubeStudioMocapClient
{
  private CancellationTokenSource _cts;
  private CancellationTokenSource _heartbeatCts;
  private string _prevMessage = "";
  private readonly object _rawMessageLock = new object();
  private string _rawMessage = "";
  private readonly Dictionary<string, float> _blendShapes = new Dictionary<string, float>();
  private Vector3 _headPosition = Vector3.zero;
  private Quaternion _headRotation = Quaternion.identity;
  private Quaternion _leftEyeRotation = Quaternion.identity;
  private Quaternion _rightEyeRotation = Quaternion.identity;
  private int _sendPort;
  private int _receivePort;
  private string _ip;
  private bool _failedToStart;
  private volatile float _lastReceivedTime;
  private volatile float _lastSentTime;
  private UdpClient client;
  private float _sendTime;

  public Dictionary<string, float> BlendShapes => this._blendShapes;

  public Vector3 HeadPosition => this._headPosition;

  public Quaternion HeadRotation => this._headRotation;

  public Quaternion LeftEyeRotation => this._leftEyeRotation;

  public Quaternion RightEyeRotation => this._rightEyeRotation;

  public float LastReceivedTime => this._lastReceivedTime;

  public bool FailedToStart => this._failedToStart;

  private static List<string> vtsBlendshapes = new List<string>()
  {
    "EyeBlinkLeft",
    "EyeLookUpLeft",
    "EyeLookDownLeft",
    "EyeLookInLeft",
    "EyeLookOutLeft",
    "EyeWideLeft",
    "EyeSquintLeft",
    "EyeBlinkRight",
    "EyeLookUpRight",
    "EyeLookDownRight",
    "EyeLookInRight",
    "EyeLookOutRight",
    "EyeWideRight",
    "EyeSquintRight",
    "MouthLeft",
    "MouthSmileLeft",
    "MouthFrownLeft",
    "MouthPressLeft",
    "MouthUpperUpLeft",
    "MouthLowerDownLeft",
    "MouthStretchLeft",
    "MouthDimpleLeft",
    "MouthRight",
    "MouthSmileRight",
    "MouthFrownRight",
    "MouthPressRight",
    "MouthUpperUpRight",
    "MouthLowerDownRight",
    "MouthStretchRight",
    "MouthDimpleRight",
    "MouthClose",
    "MouthFunnel",
    "MouthPucker",
    "MouthShrugUpper",
    "MouthShrugLower",
    "MouthRollUpper",
    "MouthRollLower",
    "JawOpen",
    "JawForward",
    "JawLeft",
    "JawRight",
    "NoseSneerLeft",
    "noseSneer_R",
    "NoseSneerRight",
    "CheekPuff",
    "CheekSquintLeft",
    "CheekSquintRight",
    "TongueOut",
    "BrowDownLeft",
    "BrowOuterUpLeft",
    "BrowDownRight",
    "BrowOuterUpRight",
    "BrowInnerUp"
  };

  private string RawMessage
  {
    get
    {
      lock (this._rawMessageLock)
        return this._rawMessage;
    }
    set
    {
      lock (this._rawMessageLock)
        this._rawMessage = value;
    }
  }

  public VTubeStudioMocapClient(string ip, int sendPort, int receivePort, float sendTime)
  {
    this._sendPort = sendPort;
    this._receivePort = receivePort;
    this._ip = ip;
    this._cts = new CancellationTokenSource();
    this._heartbeatCts = new CancellationTokenSource();
    this._sendTime = sendTime;
    new Thread((ThreadStart) (() => {
      this.ThreadMethod(this._cts.Token);
    })).Start();
  }

  public void Update()
  {
    if (this._cts == null || this._heartbeatCts == null){
      return;
    }
    string rawMessage = this.RawMessage;
    if (string.IsNullOrEmpty(rawMessage))
      return;
    this.RawMessage = "";
    if (this._prevMessage == rawMessage)
      return;
    this.DeserializeMessage(rawMessage);
    this._prevMessage = rawMessage;
  }

  private async void UpdateLastReceivedTime()
  {
    await UniTask.SwitchToMainThread();
    this._lastReceivedTime = Time.realtimeSinceStartup;
  }

  private async void UpdateLastSentTime()
  {
    await UniTask.SwitchToMainThread();
    this._lastSentTime = Time.realtimeSinceStartup;
  }

  public void UpdateSendTime(float sendTime)
  {
    this._sendTime = sendTime;
  }

  public void Destroy()
  {
    this._cts?.Cancel();
    try
    {
      this.client?.Close();
    }
    catch (Exception ex)
    {
    }
    Debug.Log((object) ("[VTubeStudioClient] Stopped client on port " + this._sendPort.ToString()));
    this._cts = (CancellationTokenSource) null;
    this._heartbeatCts = (CancellationTokenSource) null;
    this.RawMessage = "";
    this._prevMessage = "";
  }

  private void ThreadMethod(CancellationToken token)
  {
    for (int index = 2; index >= 0; --index)
    {
      try
      {
        this.client = new UdpClient(_receivePort, AddressFamily.InterNetwork);
        this.client.Client.ReceiveTimeout = 500;
        byte[] data = Encoding.ASCII.GetBytes($"{{\"messageType\":\"iOSTrackingDataRequest\",\"sentBy\":\"Warudo\",\"sendForSeconds\":10,\"ports\":[{_receivePort}]}}");
        this.client.Send(data, data.Length, _ip, _sendPort);
        this.UpdateLastSentTime();
        break;
      }
      catch (Exception ex)
      {
        if (index == 0)
        {
          Log.UserError("[VTubeStudio] Failed to start client on port " + this._sendPort.ToString(), ex);
          this._failedToStart = true;
          return;
        }
        Thread.Sleep(250);
      }
    }
    if (this.client == null)
    {
      this._failedToStart = true;
    }
    else
    {
      while (!token.IsCancellationRequested)
      {
        try
        {
          IPEndPoint remoteEP = (IPEndPoint)null;
          this.RawMessage = Encoding.ASCII.GetString(this.client.Receive(ref remoteEP));
          this.UpdateLastReceivedTime();
          break;
        }
        catch (Exception ex)
        {
        }
      }
      if (token.IsCancellationRequested)
        return;
      Debug.Log((object) string.Format("[VTubeStudio] Connected to VTubeStudio. Port: {0}", (object) this._sendPort));
      while (!token.IsCancellationRequested)
      {
        try
        {
          if (Time.realtimeSinceStartup > _lastSentTime + _sendTime){
            IPEndPoint sendEndpoint = new IPEndPoint(IPAddress.Parse(_ip), _sendPort);
            byte[] data = Encoding.ASCII.GetBytes($"{{\"messageType\":\"iOSTrackingDataRequest\",\"sentBy\":\"Warudo\",\"sendForSeconds\":10,\"ports\":[{_receivePort}]}}");
            this.client.Send(data, data.Length, _ip, _sendPort);
            this.UpdateLastSentTime();
          }
          IPEndPoint remoteEP = (IPEndPoint)null;
          this.RawMessage = Encoding.ASCII.GetString(this.client.Receive(ref remoteEP));
          this.UpdateLastReceivedTime();
        }
        catch (Exception ex)
        {
        }
      }
    }
  }

  private void DeserializeMessage(string msg){
    VTubeStudioMessage parsedMessage =  JsonConvert.DeserializeObject<VTubeStudioMessage>(msg);
    foreach (KeyValuePair<string, float> bs in parsedMessage.BlendshapeDictionary){
      this._blendShapes[bs.Key] = bs.Value;
    }
    this._headRotation = Quaternion.Euler(parsedMessage.Rotation.y, parsedMessage.Rotation.x, parsedMessage.Rotation.z);
    this._headPosition = parsedMessage.VNyanPos != null ? (Vector3)parsedMessage.VNyanPos : parsedMessage.Position * 0.04f;
    this._leftEyeRotation = Quaternion.Euler(parsedMessage.EyeLeft);
    this._rightEyeRotation = Quaternion.Euler(parsedMessage.EyeRight);
  }
}
