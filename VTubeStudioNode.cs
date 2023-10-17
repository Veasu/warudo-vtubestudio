using System.Collections.Generic;
using UnityEngine;
using Warudo.Core.Attributes;

namespace veasu.vtubestudio {
  [NodeType(Id = "com.veasu.vtubestudionode", Title = "VTubeStudio Receiver", Category = "CATEGORY_MOTION_CAPTURE")]
  public class GetVTubeStudioDataNode : Warudo.Core.Graphs.Node
  {
    [DataInput(11)]
    [Label("RECEIVER")]
    public VTubeStudioReceiverAsset Receiver;

    [DataOutput(15)]
    [Label("IS_TRACKED")]
    public bool IsTracked()
    {
      VTubeStudioReceiverAsset receiver = this.Receiver;
      return receiver != null && receiver.IsTracked;
    }

    [DataOutput(19)]
    [Label("ROOT_POSITION")]
    public Vector3 RootPosition()
    {
      VTubeStudioReceiverAsset receiver = this.Receiver;
      return receiver == null ? Vector3.zero : receiver.LatestRootPosition;
    }

    [DataOutput(23)]
    [Label("BONE_ROTATIONS")]
    public Quaternion[] BoneRotations() => this.Receiver?.LatestBoneRotations;

    [DataOutput(27)]
    [Label("BLENDSHAPES")]
    public Dictionary<string, float> BlendShapes() => this.Receiver?.LatestBlendShapes;

    [DataOutput(31)]
    [Label("HEAD_POSITION")]
    public Vector3 HeadPosition()
    {
      VTubeStudioReceiverAsset receiver = this.Receiver;
      return receiver == null ? Vector3.zero : receiver.LatestHeadPosition;
    }
  }
}
