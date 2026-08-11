#!/usr/bin/env python3
"""Simple dashboard server: serves TestDashboard/ and exposes /api/record to capture pipeline status."""
import http.server, pathlib, json, urllib.parse
ROOT = pathlib.Path(__file__).parent
HISTORY = ROOT / "history.json"

class Handler(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *a, **kw): super().__init__(*a, directory=str(ROOT), **kw)
    def _cors(self):
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, DELETE, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type")
    def do_OPTIONS(self):
        self.send_response(200); self._cors(); self.end_headers()
    def do_POST(self):
        if self.path.startswith("/api/record"):
            import subprocess, sys
            subprocess.run([sys.executable, str(ROOT.parent/"Tools/capture_history.py")])
            self.send_response(200); self.send_header("Content-Type","application/json"); self._cors(); self.end_headers()
            self.wfile.write(b'{"ok":true}')
        else: self.send_error(404)
    def do_DELETE(self):
        parsed = urllib.parse.urlparse(self.path)
        if parsed.path.startswith("/api/runs/"):
            run_id = urllib.parse.unquote(parsed.path[len("/api/runs/"):])
            try:
                if HISTORY.exists():
                    raw = HISTORY.read_text()
                    try: data = json.loads(raw)
                    except: data = []
                    if isinstance(data, dict) and "items" in data: data = data["items"]
                    orig = len(data) if isinstance(data, list) else 0
                    data = [r for r in (data if isinstance(data, list) else []) if r.get("runId") != run_id]
                    HISTORY.write_text(json.dumps(data, indent=2))
                    self.send_response(200); self.send_header("Content-Type","application/json"); self._cors(); self.end_headers()
                    self.wfile.write(json.dumps({"ok": True, "deleted": run_id, "before": orig, "after": len(data)}).encode())
                    return
            except Exception as e:
                self.send_response(500); self.send_header("Content-Type","application/json"); self._cors(); self.end_headers()
                self.wfile.write(json.dumps({"ok": False, "error": str(e)}).encode()); return
        self.send_error(404)
    def do_GET(self):
        # Allow fetching Temp files for thumbnails fallback
        if self.path.startswith("/Temp/"):
            p = pathlib.Path(__file__).parent.parent / self.path.lstrip("/")
            if p.exists():
                self.send_response(200)
                self.send_header("Content-Type","image/png" if p.suffix==".png" else "application/json")
                self.end_headers(); self.wfile.write(p.read_bytes()); return
            self.send_error(404); return
        return super().do_GET()

if __name__=="__main__":
    import argparse; p=argparse.ArgumentParser(); p.add_argument("--port", type=int, default=8080); a=p.parse_args()
    print(f"Serving {ROOT} at http://localhost:{a.port}/  (Ctrl+C to stop)")
    print(f"History: {HISTORY}  Thumbnails: {ROOT/'thumbnails'}  Pipeline: {ROOT.parent/'Temp/pipeline_test_status.json'}")
    http.server.ThreadingHTTPServer(("127.0.0.1", a.port), Handler).serve_forever()
