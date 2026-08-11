#!/usr/bin/env python3
"""
Capture current pipeline_test_status.json + screenshots into TestDashboard/history.json
Use after `unity command run_tests` or any Test Runner run.
Also usable as watcher: python3 Tools/capture_history.py --watch
"""
import json, pathlib, datetime, shutil, sys, time
ROOT = pathlib.Path(__file__).resolve().parents[1]
HISTORY = ROOT / "TestDashboard/history.json"
PIPE = ROOT / "Temp/pipeline_test_status.json"
LAST = ROOT / "Temp/dashboard_last_run.json"
DESC = {
    "T1_Pos": "Level 1 (THE WARM UP) — positive hole-in-one, expects completion & UI progress",
    "T2_Neg": "Level 1 (THE WARM UP) — negative miss into rail, expects NOT complete but strokes +1",
    "T3_Pos": "Level 2 (THE GARDEN) — positive through gates, expects completion & PAR UI",
    "T4_Neg": "Level 3 (WINDMILL WAY) — negative weak shot blocked by windmill, expects NOT complete",
}
THUMB_MAP = {"T1_":"test_T1_final.png","T2_":"test_T2_after.png","T3_":"test_T3_final.png","T4_":"test_T4_after.png"}

def load_history():
    if not HISTORY.exists(): return []
    try:
        data=json.loads(HISTORY.read_text())
        if isinstance(data, dict) and "items" in data: return data["items"]
        return data if isinstance(data, list) else []
    except: return []

def save_history(h): HISTORY.write_text(json.dumps(h, indent=2))

def describe(full):
    for k,v in DESC.items():
        if k in full: return v
    return ""

def guess_thumb(name):
    for k,v in THUMB_MAP.items():
        if k in name: return v
    return ""

def capture():
    if not PIPE.exists():
        print(f"No {PIPE} — run tests first (unity command run_tests)")
        return None
    raw=json.loads(PIPE.read_text())
    # Handle both direct summary and nested pipeline format
    summary=raw.get("summary") or raw
    results=raw.get("results") or []
    ts=datetime.datetime.now()
    runId=ts.isoformat()
    stamp=ts.astimezone().strftime("%Y-%m-%d %H:%M:%S")
    tests=[]
    for r in results:
        tests.append({
            "name": r.get("FullName","").split(".")[-1],
            "fullName": r.get("FullName",""),
            "status": r.get("Status",""),
            "duration": r.get("Duration",0),
            "message": r.get("Message"),
            "stackTrace": r.get("StackTrace"),
            "description": describe(r.get("FullName","")),
            "thumbnail": guess_thumb(r.get("FullName",""))
        })
    # Copy thumbnails
    thumbs=[]
    for src in (ROOT/"Temp").glob("test_*.png"):
        dst_dir=ROOT/"TestDashboard/thumbnails"
        dst_dir.mkdir(parents=True, exist_ok=True)
        dst=dst_dir/f"{ts.strftime('%Y%m%d-%H%M%S')}_{src.name}"
        latest=dst_dir/src.name
        try:
            shutil.copy(src, dst)
            shutil.copy(src, latest)
            thumbs.append(src.name)
        except: pass
    run={"runId":runId,"timestamp":stamp,"duration":raw.get("duration",0) or summary.get("duration",0) or 0,"summary":{"total":summary.get("total",len(tests)),"passed":summary.get("passed",0),"failed":summary.get("failed",0),"skipped":summary.get("skipped",0),"inconclusive":summary.get("inconclusive",0)},"tests":tests,"thumbnails":thumbs}
    hist=load_history()
    # Avoid duplicate runId
    if not any(h.get("runId")==runId for h in hist):
        hist.insert(0, run)
        hist=hist[:50]
        save_history(hist)
        (ROOT/"Temp/dashboard_last_run.json").write_text(json.dumps(run, indent=2))
        print(f"Recorded run {stamp} {run['summary']['passed']}/{run['summary']['total']} -> {HISTORY}")
    else: print("Already recorded")
    return run

if __name__=="__main__":
    if "--watch" in sys.argv:
        print("Watching Temp/pipeline_test_status.json ... Ctrl+C to stop")
        last=0
        while True:
            try:
                mtime=PIPE.stat().st_mtime if PIPE.exists() else 0
                if mtime!=last and mtime!=0:
                    time.sleep(1); capture(); last=mtime
                time.sleep(2)
            except KeyboardInterrupt: break
    else:
        capture()
