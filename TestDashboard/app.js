const HISTORY = 'history.json';
const THUMBS = 'thumbnails/';
let data = [];
let selected = 0;

const runList = document.getElementById('runList');
const runCount = document.getElementById('runCount');
const detail = document.getElementById('detail');
const detailEmpty = document.getElementById('detailEmpty');
const refreshBtn = document.getElementById('refreshBtn');

async function load(){
  try{
    const r = await fetch(HISTORY + '?t=' + Date.now(), {cache:'no-store'});
    if(!r.ok) throw new Error(r.status);
    const j = await r.json();
    data = Array.isArray(j) ? j : (j.items || []);
  }catch(e){
    // fallback to last run snapshot
    try{
      const r2 = await fetch('../Temp/dashboard_last_run.json?t='+Date.now(), {cache:'no-store'});
      if(r2.ok){ const j2 = await r2.json(); data = [j2]; }
    }catch{}
  }
  renderList();
  if(data.length) select(0);
  runCount.textContent = data.length + ' runs';
}

function thumbSrc(name){
  if(!name) return '';
  // Prefer dashboard thumbnail, fallback to Temp
  return THUMBS + name;
}

async function deleteRun(runId, e){
  if(e) e.stopPropagation();
  if(!confirm('Delete this run?\n' + runId)) return;
  const url = '/api/runs/' + encodeURIComponent(runId);
  try{
    const res = await fetch(url, {method:'DELETE'});
    if(res.ok){
      const j = await res.json().catch(()=>({}));
      console.log('delete ok', j);
      data = data.filter(r=>r.runId!==runId);
      if(selected >= data.length) selected = Math.max(0, data.length-1);
      renderList();
      if(data.length) select(selected); else { detail.classList.add('hidden'); detailEmpty.classList.remove('hidden'); }
      runCount.textContent = data.length + ' runs';
      return;
    } else {
      const txt = await res.text().catch(()=>res.statusText);
      alert('Delete failed (' + res.status + '): ' + txt + '\n\nMake sure you started the dashboard via:\n  python3 TestDashboard/server.py --port 8080\n(not python -m http.server)');
      return;
    }
  }catch(err){
    console.error('delete fetch failed', err);
    alert('Delete requires TestDashboard/server.py.\nYou are likely using `python -m http.server` which cannot save deletions.\n\nRestart via:\n  python3 TestDashboard/server.py --port 8080\n\nError: ' + (err.message||err));
    return;
  }
}

function renderList(){
  runList.innerHTML = '';
  data.forEach((run,i)=>{
    const s = run.summary || {};
    const ok = s.failed===0 && s.total>0;
    const div = document.createElement('div');
    div.className = 'runItem' + (i===selected ? ' active':'');
    div.onclick = ()=> select(i);
    div.innerHTML = `
      <div class="runTop">
        <span class="when">${run.timestamp||run.runId}</span>
        <span style="display:flex;gap:6px;align-items:center">
          <span class="badge ${ok?'ok':'bad'}">${ok?'✓ PASS':'✗ FAIL'} ${s.passed||0}/${s.total||0}</span>
          <button class="runDel" onclick="deleteRun('${String(run.runId).replace(/'/g,"\\'")}',event)" title="Delete this run">🗑 Delete</button>
        </span>
      </div>
      <div class="runTitle">${s.total} tests · ${Number(run.duration||0).toFixed(1)}s</div>
      <div class="runMeta">${(run.tests||[]).map(t=>t.name).join(' · ').slice(0,120)}</div>
      <div class="thumbRow">${(run.thumbnails||[]).slice(0,6).map(n=>`<img src="${thumbSrc(n)}" onerror="this.style.display='none'" loading="lazy">`).join('')}</div>
    `;
    runList.appendChild(div);
  });
  if(!data.length){
    runList.innerHTML = '<div class="runItem">No runs yet — run <code>unity command run_tests</code> or press Test Runner ▶️</div>';
  }
}

function select(i){
  selected = i;
  renderList();
  const run = data[i];
  if(!run){ detail.classList.add('hidden'); detailEmpty.classList.remove('hidden'); return; }
  detailEmpty.classList.add('hidden'); detail.classList.remove('hidden');
  const s = run.summary||{};
  const ok = s.failed===0;
  detail.innerHTML = `
    <div style="display:flex;justify-content:space-between;align-items:center;gap:8px">
      <div style="min-width:0;overflow:hidden">
        <div class="when" style="overflow:hidden;text-overflow:ellipsis;white-space:nowrap">${run.timestamp} · <span class="mono">${run.runId}</span></div>
        <h2 style="margin:6px 0 4px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap">${ok?'✓':'✗'} Run ${s.passed}/${s.total} · ${Number(run.duration||0).toFixed(2)}s</h2>
        <div style="font-size:12px;color:var(--muted)">${ok?'All green':'Has failures'} — click thumbnail to enlarge</div>
      </div>
      <span style="display:flex;gap:8px;align-items:center">
        <span class="badge ${ok?'ok':'bad'}" style="font-size:13px;padding:6px 10px;flex-shrink:0">${ok?'✓ PASS':'✗ FAIL'}</span>
        <button class="delBtn" onclick="deleteRun('${String(run.runId).replace(/'/g,"\\'")}',event)" title="Delete this run">🗑 Delete run</button>
      </span>
    </div>
    <div class="kpi">
      <div><span>Total</span><b>${s.total||0}</b></div>
      <div><span>Passed ✓</span><b style="color:var(--ok)">${s.passed||0}</b></div>
      <div><span>Failed ✗</span><b style="color:var(--bad)">${s.failed||0}</b></div>
      <div><span>Skipped</span><b>${s.skipped||0}</b></div>
      <div><span>Duration</span><b>${Number(run.duration||0).toFixed(2)}s</b></div>
    </div>
    <div class="tests">
      ${(run.tests||[]).map(t=>{
        const isOk = t.status==='Passed' || t.status==='Success';
        return `<div class="trow">
          <div class="icon ${isOk?'ok':'bad'}">${isOk?'✓':'✗'}</div>
          <div>
            <div class="tname">${t.name}</div>
            <div class="tdesc">${t.description||''}</div>
            ${t.message ? `<pre>${escapeHtml(t.message).slice(0,800)}</pre>` : ''}
          </div>
          <div class="dur2">${Number(t.duration||0).toFixed(2)}s</div>
          <img class="tthumb" src="${thumbSrc(t.thumbnail)}" onerror="this.style.display='none'" onclick="window.open(this.src,'_blank')" title="${t.thumbnail||''}">
        </div>`;
      }).join('')}
    </div>
    <div style="margin-top:10px" class="mono" >Thumbnails: ${(run.thumbnails||[]).join(', ')||'none'} — stored in TestDashboard/thumbnails/</div>
  `;
}

function escapeHtml(s){ return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;'); }

refreshBtn.onclick = load;

// Poll every 3s — recording is automatic via TestHistoryRecorder / capture_history.py
setInterval(load, 3000);
load();
