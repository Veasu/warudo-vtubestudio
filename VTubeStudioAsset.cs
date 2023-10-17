using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using UMod;
using UnityEngine;
using Warudo.Core;
using Warudo.Core.Attributes;
using Warudo.Core.Graphs;
using Warudo.Core.Localization;
using Warudo.Core.Server;
using Warudo.Core.Utils;
using Warudo.Plugins.Core;
using Warudo.Plugins.Core.Assets.Character;
using Warudo.Plugins.Core.Assets.MotionCapture;
using Warudo.Plugins.Core.Utils;

namespace veasu.vtubestudio {
  [AssetType(Id = "com.veasu.vtubestudioasset", Title = "VTubeStudio Receiver", Category = "CATEGORY_MOTION_CAPTURE")]
  public class VTubeStudioReceiverAsset : GenericTrackerAsset, IQuickCalibratable
  {
    public new VTubeStudioTrackingPlugin Plugin => base.Plugin as VTubeStudioTrackingPlugin;
    [Markdown(-1001, false, false)]
    public string Status = "RECEIVER_NOT_STARTED".Localized();
    [DataInput(-999)]
    [Label("VTubeStudio IP")]
    public string Ip = "";
    [DataInput(-998)]
    [Label("PORT")]
    public int Port = 21412;

    [Trigger(-997)]
    [HiddenIf(nameof(showReconnectButton))]
    [Label("Retry Connection")]
    public void reconnect() {
      ResetReceiver();
    }

    [Markdown(-996, false, false)]
    public string HeartbeatString = "Heartbeat Timer is the time between messages that are sent to VTubeStudio to request data. This can be raised to lower the amount of messages being sent, doing so may help less powerful phone / tablets, but if raised too high may cause dropouts.";
    [DataInput(-995)]
    [FloatSlider(1.0f, 5.0f)]
    [Label("Heartbeat Timer")]
    public float sendTime = 1.0f;

    [Trigger(-994)]
    [Label("Create Blueprint")]
    public void createBlueprint() {
      Graph currentGraph = Context.OpenedScene.GetGraph(vtubeStudioBlueprintID);
      if (currentGraph != null) {
        if (graph != currentGraph) {
          graph = currentGraph;
        }
        Context.Service?.Toast(ToastSeverity.Warning, "Blueprint Already Found!", "VTubeStudio blueprint already imported. Please delete it if you would like to reimport the default.");
        return;
      }
      Context.Service.ImportGraph(Plugin.DashboardJSON);
      Context.Service.BroadcastOpenedScene();
      currentGraph = Context.OpenedScene.GetGraph(vtubeStudioBlueprintID);
      GetVTubeStudioDataNode receiverNode = (GetVTubeStudioDataNode)currentGraph.GetNode(Guid.Parse("c668c6f3-37ae-42a7-bead-177172a133d8"));
      receiverNode.Receiver = this;
      receiverNode.Broadcast();
      if (shouldersFollow) {
        Node node = currentGraph.GetNode(Guid.Parse("70801ad1-b33b-4f8a-b9c7-53412a8fdcff"));
        node.SetDataInput("Scale", followAmount, true);
      }
      graph = currentGraph;
      Context.Service?.Toast(ToastSeverity.Success, "SUCCESS", "Succesfully created blueprint!");
    }

    [DataInput]
    [HiddenIf(nameof(showShouldersFollow))]
    [Label("Shoulders Follow Head")]
    public bool shouldersFollow = false;

    [DataInput]
    [HiddenIf(nameof(showFollowIntensity))]
    [FloatSlider(0.0f, 1.0f)]
    [Label("Shoulder Follow Intensity")]
    public float followAmount = 0.0f;

    [DataInput(-899)]
    [Label("Mirror Blendshapes")]
    public bool mirrorBlendshapes = false;

    [DataInput(9999)]
    [Label("CALCULATE_EYE_BONE_ROTATIONS_FROM_BLENDSHAPES")]
    [Description("Had reports of eye rotations being a bit weird sometimes. Going to steal this from IFacialMocap tracking and see if it helps out. Only use if you're seeing odd issues with eye rotations.")]
    public bool CalculateEyeBoneRotationsFromBlendShapes;

    private Guid vtubeStudioBlueprintID = new Guid("e4b0bb0f-52df-4dd3-8362-46ebc96c0b84");
    private bool isFirstTimeCalibrated;
    private VTubeStudioClient client;

    public string TrackerName => "VTUBESTUDIO";

    public void QuickCalibrate()
    {
      if (!this.IsStarted)
        return;
      this.Calibrate();
    }

    protected override bool UseHeadIK => true;

    protected override bool UseCharacterDaemon => true;

    protected override bool IsInputHeadTransformMirrored => true;

    protected override Vector2 EyeMovementIntensityBaseMultiplier => new Vector2(0.3f, 0.3f);

    public override List<string> InputBlendShapes => ((IEnumerable<string>) BlendShapes.ARKitBlendShapeNames).ToList<string>();

    private Graph graph;

    public bool showReconnectButton() => this.client != null && !this.client.FailedToStart;

    public bool showShouldersFollow() {
      if (graph != null) return false;
      return true;
    }

    public bool showFollowIntensity() {
      if (graph != null && shouldersFollow) return false;
      return true;
    }

    protected override void OnCreate()
    {
      base.OnCreate();
      Watch<float>(nameof(sendTime), (from, to) => {
        if (client != null) {
          client.UpdateSendTime(to);
        }
      });

      this.Watch("Port", new Action(this.ResetReceiver));
      this.Watch("Ip", new Action(this.ResetReceiver));
      Watch<bool>(nameof(shouldersFollow), (from, to) => {
        if (graph != null) {
          Node node = graph.GetNode(Guid.Parse("70801ad1-b33b-4f8a-b9c7-53412a8fdcff"));
          if (node != null) {
            node.SetDataInput("Scale", to ? followAmount : 0.0f, true);
          }
        }
      });
      Watch<bool>(nameof(mirrorBlendshapes), (from, to) => {
        if (client != null) {
          client.MirrorBlendshapes = to;
        }
      });
      Watch<float>(nameof(followAmount), (from, to) => {
        if(shouldersFollow && graph != null){
          Node node = graph.GetNode(Guid.Parse("70801ad1-b33b-4f8a-b9c7-53412a8fdcff"));
          if (node != null) {
            node.SetDataInput("Scale", followAmount, true);
          }
        }
      });
      this.ResetReceiver();
    }

    public override void OnUpdate()
    {
      base.OnUpdate();
      graph = Context.OpenedScene.GetGraph(vtubeStudioBlueprintID);
    }

    protected override void OnDestroy()
    {
      base.OnDestroy();
      this.client?.Destroy();
    }

    public async void ResetReceiver()
    {
      VTubeStudioReceiverAsset vTubeStudioAsset = this;
      vTubeStudioAsset.IsStarted = false;
      vTubeStudioAsset.SetActive(false);
      if (vTubeStudioAsset.client != null) {
        vTubeStudioAsset.client.Destroy();
      }
      await Context.PluginManager.GetPlugin<CorePlugin>().BeforeListenToPort();
      vTubeStudioAsset.client = new VTubeStudioClient(vTubeStudioAsset.Ip, vTubeStudioAsset.Port, 0, sendTime)
      {
        MirrorBlendshapes = mirrorBlendshapes
      };
      vTubeStudioAsset.Status = "RECEIVER_STARTED_ON_PORT".Localized((object) vTubeStudioAsset.Port, (object) string.Join(", ", Networking.GetLocalIPAddresses().Select<IPAddress, string>((Func<IPAddress, string>) (it => it.ToString()))));
      vTubeStudioAsset.IsStarted = true;
      vTubeStudioAsset.isFirstTimeCalibrated = false;
      vTubeStudioAsset.SetActive(true);
      await UniTask.Delay(TimeSpan.FromSeconds(1.5));
      if (vTubeStudioAsset.client.FailedToStart) {
        Log.UserError("Failed to start VTubeStudio receiver on port " + vTubeStudioAsset.Port.ToString());
        vTubeStudioAsset.Status = "FAILED_TO_START_RECEIVER_ANOTHER_PROCESS_IS_ALREADY_LISTENING_ON_THIS_PORT".Localized();
        vTubeStudioAsset.IsStarted = false;
        vTubeStudioAsset.SetActive(false);
      }
      Context.PluginManager.GetPlugin<CorePlugin>().AfterListenToPort();
      vTubeStudioAsset.BroadcastDataInput("Status");
    }

    protected override bool UpdateRawData()
    {
      this.client.Update();
      if ((double) Time.realtimeSinceStartup - (double) this.client.LastReceivedTime > 0.5)
        return false;
      if (!this.client.IsTracked) {
        return false;
      } 
      this.RawBlendShapes.Clear();
      foreach (KeyValuePair<string, float> blendShape in this.client.BlendShapes)
      {
        this.RawBlendShapes[blendShape.Key] = blendShape.Value;
      }

      this.RawHeadPosition = this.client.HeadPosition;   
      this.RawBoneRotations[10] = this.client.HeadRotation;
      if (this.CalculateEyeBoneRotationsFromBlendShapes)
      {
        float num1 = this.RawBlendShapes["eyeLookInLeft"] - this.RawBlendShapes["eyeLookOutLeft"];
        float num2 = this.RawBlendShapes["eyeLookUpLeft"] - this.RawBlendShapes["eyeLookDownLeft"];
        float num3 = this.RawBlendShapes["eyeLookOutRight"] - this.RawBlendShapes["eyeLookInRight"];
        float num4 = this.RawBlendShapes["eyeLookUpRight"] - this.RawBlendShapes["eyeLookDownRight"];
        Vector3 euler1 = new Vector3((float) (-(double) this.EyeMovementIntensity.y * 25.0) * num2, (float) (-(double) this.EyeMovementIntensity.x * 25.0) * num1, 0.0f);
        Vector3 euler2 = new Vector3((float) (-(double) this.EyeMovementIntensity.y * 25.0) * num4, (float) (-(double) this.EyeMovementIntensity.x * 25.0) * num3, 0.0f);
        this.RawBoneRotations[21] = Quaternion.Euler(euler1);
        this.RawBoneRotations[22] = Quaternion.Euler(euler2);
      }
      else
      {
        this.RawBoneRotations[21] = this.client.LeftEyeRotation;
        this.RawBoneRotations[22] = this.client.RightEyeRotation;
      }
    
      if (!this.isFirstTimeCalibrated && (double) this.client.LastReceivedTime >= 0.0)
      {
        this.Calibrate();
        this.isFirstTimeCalibrated = true;
      }
      return true;
    }
  }

}
