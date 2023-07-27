using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
public class VTubeStudioMessage
{
  public long Timestamp {get; set;}
  public int Hotkey {get; set;}
  public bool FaceFound {get; set;}
  public Vector3 Rotation {get; set;}
  public Vector3 Position {get; set;}
  public Vector3 EyeLeft {get; set;}
  public Vector3 EyeRight {get; set;}
  public List<VTubeStudioBlendshape> Blendshapes {get; set;}
  public Dictionary<string, float> BlendshapeDictionary {
    get {
      return this.Blendshapes.ToDictionary(x => Char.ToLower(x.k[0]) + x.k[1..], x => x.v);
    }
  }
}

public struct VTubeStudioBlendshape {
    public string k;
    public float v;
}
