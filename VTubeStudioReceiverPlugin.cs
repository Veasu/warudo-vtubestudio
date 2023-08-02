using UnityEngine;
using Warudo.Core.Attributes;
using Warudo.Core.Plugins;

[PluginType(Id = "com.veasu.vtubestudiotracking", 
Name = "VTubeStudio Tracking", 
Description = "Adds a VTubeStudio tracking asset. Any issues feel free to reach out on discord or steam.", 
Author = "Veasu", 
Version = "0.1.1", 
Icon = @"<svg version=""1.0"" xmlns=""http://www.w3.org/2000/svg""
 width=""50.000000pt"" height=""50.000000pt"" viewBox=""0 0 50.000000 50.000000""
 preserveAspectRatio=""xMidYMid meet""><g transform=""translate(0.000000,50.000000) scale(0.100000,-0.100000)""
fill=""currentColor"">
<path d=""M123 250 c57 -166 76 -210 89 -210 10 0 18 3 18 7 0 4 -30 99 -67
210 -61 183 -70 203 -90 203 -22 0 -20 -8 50 -210z""/>
<path d=""M184 250 c67 -198 72 -210 96 -210 24 0 29 12 96 210 67 199 70 210
50 210 -19 0 -29 -21 -81 -180 -33 -99 -62 -180 -65 -180 -3 0 -32 81 -65 180
-52 159 -62 180 -81 180 -20 0 -17 -11 50 -210z""/>
<path d=""M303 334 c-35 -105 -40 -130 -31 -152 9 -25 14 -15 59 118 27 79 49
148 49 152 0 5 -8 8 -18 8 -14 0 -27 -28 -59 -126z""/>
</g></svg>",
AssetTypes = new System.Type[] {typeof (VTubeStudioReceiverAsset)}, NodeTypes = new System.Type[] {typeof (GetVTubeStudioDataNode)})]
public class VTubeStudioTrackingPlugin : Plugin
{
  public string DashboardJSON;
  protected override void OnCreate() {
    DashboardJSON = ModHost.Assets.Load<TextAsset>("Assets/VTubeStudioNode/Dashboard.json").text;
  }
}
