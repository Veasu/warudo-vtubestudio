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

  [Trigger]
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

  private Guid vtubeStudioBlueprintID = new Guid("e4b0bb0f-52df-4dd3-8362-46ebc96c0b84");
  private bool isFirstTimeCalibrated;
  private VTubeStudioMocapClient client;

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
    this.Watch("Port", new Action(this.ResetReceiver));
    this.Watch("Ip", new Action(this.ResetReceiver));
    Watch<bool>(nameof(shouldersFollow), (from, to) => {
      Node node = graph.GetNode(Guid.Parse("70801ad1-b33b-4f8a-b9c7-53412a8fdcff"));
      if (node != null) {
         node.SetDataInput("Scale", to ? followAmount : 0.0f, true);
      }
    });
    Watch<float>(nameof(followAmount), (from, to) => {
      if(shouldersFollow){
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
    this.client.Destroy();
  }

  public async void ResetReceiver()
  {
    VTubeStudioReceiverAsset mocapReceiverAsset = this;
    mocapReceiverAsset.IsStarted = false;
    mocapReceiverAsset.SetActive(false);
    if (mocapReceiverAsset.client != null) {
      mocapReceiverAsset.client.Destroy();
    }
    await Context.PluginManager.GetPlugin<CorePlugin>().BeforeListenToPort();
    mocapReceiverAsset.client = new VTubeStudioMocapClient(mocapReceiverAsset.Ip, mocapReceiverAsset.Port, 50619);
    mocapReceiverAsset.Status = "RECEIVER_STARTED_ON_PORT".Localized((object) mocapReceiverAsset.Port, (object) string.Join(", ", Networking.GetLocalIPAddresses().Select<IPAddress, string>((Func<IPAddress, string>) (it => it.ToString()))));
    mocapReceiverAsset.IsStarted = true;
    mocapReceiverAsset.isFirstTimeCalibrated = false;
    mocapReceiverAsset.SetActive(true);
    await UniTask.Delay(TimeSpan.FromSeconds(1.0));
    Context.PluginManager.GetPlugin<CorePlugin>().AfterListenToPort();
    if (mocapReceiverAsset.client.FailedToStart)
    {
      Log.UserError("Failed to start VTubeStudio receiver on port " + mocapReceiverAsset.Port.ToString());
      mocapReceiverAsset.Status = "FAILED_TO_START_RECEIVER_ANOTHER_PROCESS_IS_ALREADY_LISTENING_ON_THIS_PORT".Localized();
      mocapReceiverAsset.IsStarted = false;
      mocapReceiverAsset.SetActive(false);
    }
    mocapReceiverAsset.BroadcastDataInput("Status");
  }

  protected override bool UpdateRawData()
  {
    this.client.Update();
    if ((double) Time.realtimeSinceStartup - (double) this.client.LastReceivedTime > 0.5)
      return false;
    this.RawBlendShapes.Clear();
    foreach (KeyValuePair<string, float> blendShape in this.client.BlendShapes)
    {
      this.RawBlendShapes[char.ToLower(blendShape.Key[0]) + blendShape.Key[1..]] = blendShape.Value;
    }

    this.RawHeadPosition = this.client.HeadPosition;   
    this.RawBoneRotations[10] = this.client.HeadRotation;
    this.RawBoneRotations[21] = this.client.LeftEyeRotation;
    this.RawBoneRotations[22] = this.client.RightEyeRotation;
  
    if (!this.isFirstTimeCalibrated && (double) this.client.LastReceivedTime >= 0.0)
    {
      this.Calibrate();
      this.isFirstTimeCalibrated = true;
    }
    return true;
  }
}
