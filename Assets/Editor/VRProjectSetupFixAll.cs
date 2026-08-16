using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class VRProjectSetupFixAll
{
    static VRProjectSetupFixAll()
    {
        EditorApplication.delayCall += TryFix;
    }

    private static void TryFix()
    {
        if (SessionState.GetBool("VRProjectSetupFixAll_Done", false)) return;
        SessionState.SetBool("VRProjectSetupFixAll_Done", true);
        Debug.Log("[VRProjectSetupFixAll] Attempting to fix all Project Setup Tool tasks for Android and Standalone...");

        try
        {
            // Use reflection to call OVRProjectSetup.FixTasks without requiring compile-time reference if SDK not present
            var type = System.Type.GetType("OVRProjectSetup, Oculus.VR");
            if (type == null)
            {
                var asm = System.AppDomain.CurrentDomain.GetAssemblies();
                foreach (var a in asm)
                {
                    type = a.GetType("OVRProjectSetup");
                    if (type != null) break;
                }
            }
            if (type != null)
            {
                var fixTasks = type.GetMethod("FixTasks", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                if (fixTasks != null)
                {
                    // Fix Android
                    Debug.Log("[VRProjectSetupFixAll] Fixing Android...");
                    // Need to pass BuildTargetGroup.Android
                    // Signature: FixTasks(BuildTargetGroup, Func<..., List<...>> filter = null, ...)
                    fixTasks.Invoke(null, new object[] { BuildTargetGroup.Android, null, 0, true, null });
                    Debug.Log("[VRProjectSetupFixAll] Fixing Standalone...");
                    fixTasks.Invoke(null, new object[] { BuildTargetGroup.Standalone, null, 0, true, null });
                    Debug.Log("[VRProjectSetupFixAll] FixAll invoked for both platforms.");
                }
                else
                {
                    Debug.LogWarning("[VRProjectSetupFixAll] FixTasks method not found via reflection.");
                }
            }
            else
            {
                Debug.LogWarning("[VRProjectSetupFixAll] OVRProjectSetup type not found - Meta SDK may not be installed.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[VRProjectSetupFixAll] Exception: " + e);
        }
    }

    [MenuItem("Meta/Tools/Fix All Project Setup Issues (VR)")]
    public static void ManualFix()
    {
        SessionState.SetBool("VRProjectSetupFixAll_Done", false);
        TryFix();
    }
}
