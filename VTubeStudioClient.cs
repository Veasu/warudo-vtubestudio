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

namespace veasu.vtubestudio {
  public class VTubeStudioClient
  {
    private CancellationTokenSource _cts;
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
    private bool _isTracked = false;
    private bool _mirrorBlendshapes = false;

    public bool MirrorBlendshapes {
      get => _mirrorBlendshapes;
      set => _mirrorBlendshapes = value;
    }

    public Dictionary<string, float> BlendShapes => this._blendShapes;

    public Vector3 HeadPosition => this._headPosition;

    public Quaternion HeadRotation => this._headRotation;

    public Quaternion LeftEyeRotation => this._leftEyeRotation;

    public Quaternion RightEyeRotation => this._rightEyeRotation;

    public float LastReceivedTime => this._lastReceivedTime;

    public bool FailedToStart => this._failedToStart;

    public bool IsTracked => this._isTracked;

    private static Dictionary<string, string> vtsBlendshapes = new Dictionary<string, string>()
    {
      {
        "EyeBlinkLeft",
        "eyeBlinkLeft"
      },
      {
        "EyeLookUpLeft",
        "eyeLookUpLeft"
      },
      {
        "EyeLookDownLeft",
        "eyeLookDownLeft"
      },
      {
        "EyeLookInLeft",
        "eyeLookInLeft"
      },
      {
        "EyeLookOutLeft",
        "eyeLookOutLeft"
      },
      {
        "EyeWideLeft",
        "eyeWideLeft"
      },
      {
        "EyeSquintLeft",
        "eyeSquintLeft"
      },
      {
        "EyeBlinkRight",
        "eyeBlinkRight"
      },
      {
        "EyeLookUpRight",
        "eyeLookUpRight"
      },
      {
        "EyeLookDownRight",
        "eyeLookDownRight"
      },
      {
        "EyeLookInRight",
        "eyeLookInRight"
      },
      {
        "EyeLookOutRight",
        "eyeLookOutRight"
      },
      {
        "EyeWideRight",
        "eyeWideRight"
      },
      {
        "EyeSquintRight",
        "eyeSquintRight"
      },
      {
        "MouthLeft",
        "mouthLeft"
      },
      {
        "MouthSmileLeft",
        "mouthSmileLeft"
      },
      {
        "MouthFrownLeft",
        "mouthFrownLeft"
      },
      {
        "MouthPressLeft",
        "mouthPressLeft"
      },
      {
        "MouthUpperUpLeft",
        "mouthUpperUpLeft"
      },
      {
        "MouthLowerDownLeft",
        "mouthLowerDownLeft"
      },
      {
        "MouthStretchLeft",
        "mouthStretchLeft"
      },
      {
        "MouthDimpleLeft",
        "mouthDimpleLeft"
      },
      {
        "MouthRight",
        "mouthRight"
      },
      {
        "MouthSmileRight",
        "mouthSmileRight"
      },
      {
        "MouthFrownRight",
        "mouthFrownRight"
      },
      {
        "MouthPressRight",
        "mouthPressRight"
      },
      {
        "MouthUpperUpRight",
        "mouthUpperUpRight"
      },
      {
        "MouthLowerDownRight",
        "mouthLowerDownRight"
      },
      {
        "MouthStretchRight",
        "mouthStretchRight"
      },
      {
        "MouthDimpleRight",
        "mouthDimpleRight"
      },
      {
        "MouthClose",
        "mouthClose"
      },
      {
        "MouthFunnel",
        "mouthFunnel"
      },
      {
        "MouthPucker",
        "mouthPucker"
      },
      {
        "MouthShrugUpper",
        "mouthShrugUpper"
      },
      {
        "MouthShrugLower",
        "mouthShrugLower"
      },
      {
        "MouthRollUpper",
        "mouthRollUpper"
      },
      {
        "MouthRollLower",
        "mouthRollLower"
      },
      {
        "JawOpen",
        "jawOpen"
      },
      {
        "JawForward",
        "jawForward"
      },
      {
        "JawLeft",
        "jawLeft"
      },
      {
        "JawRight",
        "jawRight"
      },
      {
        "NoseSneerLeft",
        "noseSneerLeft"
      },
      {
        "NoseSneerRight",
        "noseSneerRight"
      },
      {
        "CheekPuff",
        "cheekPuff"
      },
      {
        "CheekSquintLeft",
        "cheekSquintLeft"
      },
      {
        "CheekSquintRight",
        "cheekSquintRight"
      },
      {
        "TongueOut",
        "tongueOut"
      },
      {
        "BrowDownLeft",
        "browDownLeft"
      },
      {
        "BrowOuterUpLeft",
        "browOuterUpLeft"
      },
      {
        "BrowDownRight",
        "browDownRight"
      },
      {
        "BrowOuterUpRight",
        "browOuterUpRight"
      },
      {
        "BrowInnerUp",
        "browInnerUp"
      },
    };

    private static Dictionary<string, string> vtsBlendshapesMirrored = new Dictionary<string, string>()
    {
      {
        "EyeBlinkLeft",
        "eyeBlinkRight"
      },
      {
        "EyeLookUpLeft",
        "eyeLookUpRight"
      },
      {
        "EyeLookDownLeft",
        "eyeLookDownRight"
      },
      {
        "EyeLookInLeft",
        "eyeLookInRight"
      },
      {
        "EyeLookOutLeft",
        "eyeLookOutRight"
      },
      {
        "EyeWideLeft",
        "eyeWideRight"
      },
      {
        "EyeSquintLeft",
        "eyeSquintRight"
      },
      {
        "EyeBlinkRight",
        "eyeBlinkLeft"
      },
      {
        "EyeLookUpRight",
        "eyeLookUpLeft"
      },
      {
        "EyeLookDownRight",
        "eyeLookDownLeft"
      },
      {
        "EyeLookInRight",
        "eyeLookInLeft"
      },
      {
        "EyeLookOutRight",
        "eyeLookOutLeft"
      },
      {
        "EyeWideRight",
        "eyeWideLeft"
      },
      {
        "EyeSquintRight",
        "eyeSquintLeft"
      },
      {
        "MouthLeft",
        "mouthRight"
      },
      {
        "MouthSmileLeft",
        "mouthSmileRight"
      },
      {
        "MouthFrownLeft",
        "mouthFrownRight"
      },
      {
        "MouthPressLeft",
        "mouthPressRight"
      },
      {
        "MouthUpperUpLeft",
        "mouthUpperUpRight"
      },
      {
        "MouthLowerDownLeft",
        "mouthLowerDownRight"
      },
      {
        "MouthStretchLeft",
        "mouthStretchRight"
      },
      {
        "MouthDimpleLeft",
        "mouthDimpleRight"
      },
      {
        "MouthRight",
        "mouthLeft"
      },
      {
        "MouthSmileRight",
        "mouthSmileLeft"
      },
      {
        "MouthFrownRight",
        "mouthFrownLeft"
      },
      {
        "MouthPressRight",
        "mouthPressLeft"
      },
      {
        "MouthUpperUpRight",
        "mouthUpperUpLeft"
      },
      {
        "MouthLowerDownRight",
        "mouthLowerDownLeft"
      },
      {
        "MouthStretchRight",
        "mouthStretchLeft"
      },
      {
        "MouthDimpleRight",
        "mouthDimpleLeft"
      },
      {
        "MouthClose",
        "mouthClose"
      },
      {
        "MouthFunnel",
        "mouthFunnel"
      },
      {
        "MouthPucker",
        "mouthPucker"
      },
      {
        "MouthShrugUpper",
        "mouthShrugUpper"
      },
      {
        "MouthShrugLower",
        "mouthShrugLower"
      },
      {
        "MouthRollUpper",
        "mouthRollUpper"
      },
      {
        "MouthRollLower",
        "mouthRollLower"
      },
      {
        "JawOpen",
        "jawOpen"
      },
      {
        "JawForward",
        "jawForward"
      },
      {
        "JawLeft",
        "jawRight"
      },
      {
        "JawRight",
        "jawLeft"
      },
      {
        "NoseSneerLeft",
        "noseSneerRight"
      },
      {
        "NoseSneerRight",
        "noseSneerLeft"
      },
      {
        "CheekPuff",
        "cheekPuff"
      },
      {
        "CheekSquintLeft",
        "cheekSquintRight"
      },
      {
        "CheekSquintRight",
        "cheekSquintLeft"
      },
      {
        "TongueOut",
        "tongueOut"
      },
      {
        "BrowDownLeft",
        "browDownRight"
      },
      {
        "BrowOuterUpLeft",
        "browOuterUpRight"
      },
      {
        "BrowDownRight",
        "browDownLeft"
      },
      {
        "BrowOuterUpRight",
        "browOuterUpLeft"
      },
      {
        "BrowInnerUp",
        "browInnerUp"
      },
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

    public VTubeStudioClient(string ip, int sendPort, int receivePort, float sendTime)
    {
      this._sendPort = sendPort;
      this._receivePort = receivePort;
      this._ip = ip;
      this._cts = new CancellationTokenSource();
      this._sendTime = sendTime;
      this._failedToStart = false;
      new Thread((ThreadStart) (() => {
        this.ThreadMethod(this._cts.Token);
      })).Start();
    }

    public void Update()
    {
      if (this._cts == null){
        return;
      }
      string rawMessage = this.RawMessage;
      if (string.IsNullOrEmpty(rawMessage)) {
        return;
      }
        
      this.RawMessage = "";
      if (this._prevMessage == rawMessage) {
        return;
      }
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
        Debug.Log(ex);
      }
      Debug.Log((object) ("[VTubeStudioClient] Stopped client on port " + this._sendPort.ToString()));
      this._cts = (CancellationTokenSource) null;
      this.RawMessage = "";
      this._prevMessage = "";
    }

    private void ThreadMethod(CancellationToken token)
    {
      for (int index = 2; index >= 0; --index)
      {
        try
        {
          this.client = new UdpClient(0);
          this._receivePort = ((IPEndPoint)client.Client.LocalEndPoint).Port;
          Debug.Log($"Port: {_receivePort}");
          this.client.Client.SendTimeout = 500;
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
          catch (SocketException ex)
          {
            if (ex.SocketErrorCode == SocketError.TimedOut)
            {
              Log.UserError("[VTubeStudio] Failed to receive data on port " + this._receivePort.ToString() + " from IP " + this._ip, ex);
              this._failedToStart = true;
              return;
            }
            Thread.Sleep(250);
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
        if (_mirrorBlendshapes) {
          vtsBlendshapesMirrored.TryGetValue(bs.Key, out string bsKey);
          if (bsKey != null) {
            this._blendShapes[bsKey] = bs.Value;
          }
        } else {
          vtsBlendshapes.TryGetValue(bs.Key, out string bsKey);
          if (bsKey != null) {
            this._blendShapes[bsKey] = bs.Value;
          }
        }
      }
      this._headRotation = Quaternion.Euler(parsedMessage.Rotation.y, parsedMessage.Rotation.x, parsedMessage.Rotation.z);
      if (parsedMessage.VNyanPos is Vector3 VNyanPosition) {
        this._headPosition = new Vector3(-VNyanPosition.x, VNyanPosition.y, -VNyanPosition.z);
      } else {
        this._headPosition = new Vector3(-parsedMessage.Position.x, parsedMessage.Position.y, -parsedMessage.Position.z) * 0.04f;
      }
      this._leftEyeRotation = Quaternion.Euler(parsedMessage.EyeLeft);
      this._rightEyeRotation = Quaternion.Euler(parsedMessage.EyeRight);
      this._isTracked = parsedMessage.FaceFound;
    }
  }
}
