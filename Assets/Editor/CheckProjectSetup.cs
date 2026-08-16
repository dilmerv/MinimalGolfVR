using UnityEditor;
using UnityEngine;
using System.Linq;

public static class CheckProjectSetup
{
    static CheckProjectSetup() { EditorApplication.delayCall += LogTasks; }

    [MenuItem("Meta/Tools/Log Setup Tasks")]
    public static void LogTasks()
    {
        var type = System.Type.GetType("OVRProjectSetup, Oculus.VR");
        if (type == null)
        {
            // Try find via assemblies
            foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                type = a.GetType("OVRProjectSetup");
                if (type != null) break;
            }
        }
        if (type == null) { Debug.LogError("OVRProjectSetup not found"); return; }
        var registryProp = type.GetProperty("Registry", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        var registry = registryProp.GetValue(null);
        var tasksProp = registry.GetType().GetProperty("Tasks");
        var tasks = tasksProp.GetValue(registry) as System.Collections.IEnumerable;
        int total = 0, done = 0, notDone = 0;
        foreach (var task in tasks)
        {
            total++;
            var taskType = task.GetType();
            var messageProp = taskType.GetProperty("Message");
            var isDoneProp = taskType.GetProperty("IsDone");
            var levelProp = taskType.GetProperty("Level");
            var groupProp = taskType.GetProperty("Group");
            string msg = messageProp.GetValue(task) as string;
            bool isDone = (bool)isDoneProp.GetValue(task);
            var level = levelProp.GetValue(task).ToString();
            var group = groupProp.GetValue(task).ToString();
            if (isDone) done++; else notDone++;
            Debug.Log($"[SetupTask] {(isDone?"DONE":"TODO")} [{level}/{group}] {msg}");
        }
        Debug.Log($"[SetupTask] Total {total} Done {done} TODO {notDone}");
    }
}
