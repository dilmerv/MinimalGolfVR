using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

#if UNITY_INCLUDE_TESTS
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

/// <summary>
/// Records every Test Runner run (both Pipeline and in-Editor Test Runner) to
/// TestDashboard/history.json and copies thumbnails for the local dashboard.
/// </summary>
public class TestHistoryRecorder : ICallbacks
{
    private const string HistoryPath = "TestDashboard/history.json";
    private const string ThumbSourceGlob = "Temp/test_*.png";

    public void RunStarted(ITestAdaptor testsToRun) { }

    public void RunFinished(ITestResultAdaptor result)
    {
        try
        {
            var timestamp = DateTime.UtcNow;
            var run = new RunRecord
            {
                runId = timestamp.ToString("o"),
                timestamp = timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                duration = result.Duration,
                summary = new Summary { total = Count(result, TestStatus.Passed) + Count(result, TestStatus.Failed) + Count(result, TestStatus.Skipped) + Count(result, TestStatus.Inconclusive), passed = Count(result, TestStatus.Passed), failed = Count(result, TestStatus.Failed), skipped = Count(result, TestStatus.Skipped), inconclusive = Count(result, TestStatus.Inconclusive) },
                tests = Flatten(result),
                thumbnails = CopyThumbnails(timestamp)
            };
            // Fallback summary from result if available
            if (run.summary.total == 0)
            {
                run.summary.total = result.Test.TestCaseCount;
                run.summary.passed = result.PassCount;
                run.summary.failed = result.FailCount;
                run.summary.skipped = result.SkipCount;
                run.summary.inconclusive = result.InconclusiveCount;
            }
            AppendHistory(run);
            Debug.Log($"[TestHistory] Recorded run {run.runId} {run.summary.passed}/{run.summary.total} to {HistoryPath}");
        }
        catch (Exception e) { Debug.LogError($"[TestHistory] Failed to record: {e}"); }
    }

    public void TestStarted(ITestAdaptor test) { }
    public void TestFinished(ITestResultAdaptor result) { }

    private int Count(ITestResultAdaptor r, TestStatus s)
    {
        int c = 0;
        void Recurse(ITestResultAdaptor n)
        {
            if (n.Test.IsSuite == false && n.TestStatus == s) c++;
            foreach (var ch in n.Children) Recurse(ch);
        }
        foreach (var ch in r.Children) Recurse(ch);
        return c;
    }

    private List<TestRecord> Flatten(ITestResultAdaptor root)
    {
        var list = new List<TestRecord>();
        void Recurse(ITestResultAdaptor n)
        {
            if (!n.Test.IsSuite)
            {
                list.Add(new TestRecord
                {
                    name = n.Test.Name,
                    fullName = n.Test.FullName,
                    status = n.TestStatus.ToString(),
                    duration = n.Duration,
                    message = n.Message,
                    stackTrace = n.StackTrace,
                    description = Describe(n.Test.FullName),
                    thumbnail = GuessThumb(n.Test.Name)
                });
            }
            foreach (var ch in n.Children) Recurse(ch);
        }
        foreach (var ch in root.Children) Recurse(ch);
        return list;
    }

    private string Describe(string fullName)
    {
        if (fullName.Contains("T1_Pos")) return "Level 1 (THE WARM UP) — positive hole-in-one, expects completion & UI progress";
        if (fullName.Contains("T2_Neg")) return "Level 1 (THE WARM UP) — negative miss into rail, expects NOT complete but strokes +1";
        if (fullName.Contains("T3_Pos")) return "Level 2 (THE GARDEN) — positive through gates, expects completion & PAR UI";
        if (fullName.Contains("T4_Neg")) return "Level 3 (WINDMILL WAY) — negative weak shot blocked by windmill, expects NOT complete";
        return "";
    }

    private string GuessThumb(string testName)
    {
        if (testName.Contains("T1_")) return "test_T1_final.png";
        if (testName.Contains("T2_")) return "test_T2_after.png";
        if (testName.Contains("T3_")) return "test_T3_final.png";
        if (testName.Contains("T4_")) return "test_T4_after.png";
        return "";
    }

    private List<string> CopyThumbnails(DateTime ts)
    {
        var copied = new List<string>();
        try
        {
            var files = Directory.GetFiles("Temp", "test_*.png");
            foreach (var src in files)
            {
                var name = Path.GetFileName(src);
                var dst = Path.Combine("TestDashboard/thumbnails", $"{ts:yyyyMMdd-HHmmss}_{name}");
                Directory.CreateDirectory("TestDashboard/thumbnails");
                File.Copy(src, dst, true);
                // Also keep latest thumb for dashboard immediate view
                var latest = Path.Combine("TestDashboard/thumbnails", name);
                File.Copy(src, latest, true);
                copied.Add(name);
            }
        }
        catch { }
        return copied;
    }

    private void AppendHistory(RunRecord run)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(HistoryPath));
        List<RunRecord> history = new List<RunRecord>();
        if (File.Exists(HistoryPath))
        {
            try { history = JsonUtilityList<RunRecord>.FromJsonArray(File.ReadAllText(HistoryPath)) ?? new List<RunRecord>(); } catch { }
            // Fallback manual parse if JsonUtility fails
            if (history.Count == 0)
            {
                try { history = NewtonsoftFallback(File.ReadAllText(HistoryPath)); } catch { }
            }
        }
        history.Insert(0, run); // newest first
        // Keep last 50 runs
        if (history.Count > 50) history = history.GetRange(0, 50);
        File.WriteAllText(HistoryPath, JsonUtilityList<RunRecord>.ToJsonArray(history, true));
        // Also update pipeline snapshot for website polling
        File.WriteAllText("Temp/dashboard_last_run.json", JsonUtility.ToJson(run, true));
    }

    private List<RunRecord> NewtonsoftFallback(string json)
    {
        // Minimal fallback - if history.json was hand-edited as [] we already handled.
        return new List<RunRecord>();
    }

    [Serializable] public class RunRecord { public string runId; public string timestamp; public double duration; public Summary summary; public List<TestRecord> tests; public List<string> thumbnails; }
    [Serializable] public class Summary { public int total; public int passed; public int failed; public int skipped; public int inconclusive; }
    [Serializable] public class TestRecord { public string name; public string fullName; public string status; public double duration; public string message; public string stackTrace; public string description; public string thumbnail; }

    // Tiny helper to (de)serialize List<T> as JSON array with JsonUtility
    private static class JsonUtilityList<T>
    {
        [Serializable] private class Wrapper { public List<T> items; }
        public static string ToJsonArray(List<T> list, bool pretty) { return JsonUtility.ToJson(new Wrapper { items = list }, pretty); }
        public static List<T> FromJsonArray(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Trim() == "[]") return new List<T>();
            // Wrap array into object if needed
            if (json.TrimStart().StartsWith("[")) json = "{\"items\":" + json + "}";
            var w = JsonUtility.FromJson<Wrapper>(json);
            return w?.items ?? new List<T>();
        }
    }
}
#endif
