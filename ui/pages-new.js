// ═══════════════════════════════════════════════════════════════════
//  聊天记录 + Token 用量（扇形图）+ API 管理
//  设计：完整照搬 WinIsland  https://winisland.cn/WinIsland/
// ═══════════════════════════════════════════════════════════════════

const DONUT_COLORS = ['#0071e3', '#5ac8fa', '#af52de', '#ff9500', '#34c759', '#ff3b30', '#ff2d55'];

/* ─────────────── 聊天记录 ─────────────── */
function pageChat() {
  return `<div class="page dynamic-page" data-dyn="chat">
  <div class="page-head">
    <div class="page-title">聊天记录</div>
    <div class="page-sub">QQ 群聊与私聊消息，来自 DSH 会话记录</div>
  </div>

  <div class="metrics">
    <div class="card"><div class="card-label">消息总数</div>
      <div class="card-value" id="cTotal">—</div><div class="card-foot">最近 500 条</div></div>
    <div class="card"><div class="card-label">群聊</div>
      <div class="card-value" id="cGroup">—</div><div class="card-foot">群里 @ 的消息</div></div>
    <div class="card"><div class="card-label">私聊</div>
      <div class="card-value" id="cC2c">—</div><div class="card-foot">一对一消息</div></div>
    <div class="card"><div class="card-label">会话数</div>
      <div class="card-value" id="cSess">—</div><div class="card-foot">历史会话文件</div></div>
  </div>

  <div class="card" style="margin-bottom:14px">
    <div class="btn-row">
      <button class="btn btn-primary" onclick="loadChat()">重新采集</button>
      <select id="chatFilter" onchange="renderChat()" style="min-width:150px">
        <option value="all">全部消息</option>
        <option value="group">只看群聊</option>
        <option value="c2c">只看私聊</option>
        <option value="user">只看用户发的</option>
        <option value="assistant">只看机器人回的</option>
      </select>
      <span id="chatTime" style="font-size:12px;color:var(--muted);margin-left:auto"></span>
    </div>
  </div>

  <div class="card">
    <div class="chat-list" id="chatList"><div class="empty">点「重新采集」加载</div></div>
  </div>
  </div>`;
}

let _chatData = null;

let _chatCache = null;

async function loadChat() {
  // ① 先显示上次的缓存（不空白）
  if (_chatCache && !_chatData) {
    _chatData = _chatCache;
    const cc = _chatCache;
    const set0 = (id, v) => { const e = document.getElementById(id); if (e) e.textContent = v; };
    const m0 = cc.messages || [];
    set0('cTotal', m0.length);
    set0('cGroup', m0.filter(x => x.kind === 'group').length);
    set0('cC2c', m0.filter(x => x.kind === 'c2c').length);
    set0('cSess', cc.sessions || 0);
    set0('chatTime', '缓存 · ' + (cc.generated || ''));
    renderChat();
  }

  const r = await invokeCSharp('api/chat');
  if (!r || r.ok === false) {
    if (!_chatCache) toast('采集失败', 'fail');
    return;
  }
  _chatCache = r;
  _chatData = r;
  const m = r.messages || [];
  const set = (id, v) => { const e = document.getElementById(id); if (e) e.textContent = v; };
  set('cTotal', m.length);
  set('cGroup', m.filter(x => x.kind === 'group').length);
  set('cC2c', m.filter(x => x.kind === 'c2c').length);
  set('cSess', r.sessions || 0);
  set('chatTime', '采集于 ' + (r.generated || ''));
  renderChat();
  toast('已加载 ' + m.length + ' 条消息', 'ok');
}

function renderChat() {
  const box = document.getElementById('chatList');
  if (!box || !_chatData) return;
  const f = (document.getElementById('chatFilter') || {}).value || 'all';
  let m = _chatData.messages || [];
  if (f === 'group') m = m.filter(x => x.kind === 'group');
  else if (f === 'c2c') m = m.filter(x => x.kind === 'c2c');
  else if (f === 'user') m = m.filter(x => x.role === 'user');
  else if (f === 'assistant') m = m.filter(x => x.role === 'assistant');

  if (!m.length) { box.innerHTML = '<div class="empty">没有匹配的消息</div>'; return; }

  box.innerHTML = m.slice().reverse().map(x => {
    const d = new Date(x.t || 0);
    const tm = isNaN(d.getTime()) ? '' :
      `${d.getMonth()+1}/${d.getDate()} ${String(d.getHours()).padStart(2,'0')}:${String(d.getMinutes()).padStart(2,'0')}`;
    const bot = x.role === 'assistant';
    const tag = bot ? '机器人' : (x.kind === 'group' ? '群聊' : '私聊');
    const cls = bot ? 'bot' : (x.kind === 'group' ? 'group' : 'c2c');
    const txt = String(x.text || '').replace(/</g,'&lt;').replace(/>/g,'&gt;');
    return `<div class="chat-item ${cls}">
      <div class="chat-meta"><span class="chat-tag ${cls}">${tag}</span>
      <span class="chat-time">${tm}</span></div>
      <div class="chat-text">${txt}</div></div>`;
  }).join('');
}


/* ─────────────── Token 用量（扇形图） ─────────────── */
function pageToken() {
  return `<div class="page dynamic-page" data-dyn="token">
  <div class="page-head">
    <div class="page-title">Token 用量</div>
    <div class="page-sub">API 调用与 Token 消耗统计</div>
  </div>

  <div class="metrics">
    <div class="card"><div class="card-label">总 Token</div>
      <div class="card-value" id="tkTotal">—</div><div class="card-foot">输入 + 输出</div></div>
    <div class="card"><div class="card-label">输入 Token</div>
      <div class="card-value" id="tkIn">—</div><div class="card-foot">prompt</div></div>
    <div class="card"><div class="card-label">输出 Token</div>
      <div class="card-value" id="tkOut">—</div><div class="card-foot">completion</div></div>
    <div class="card"><div class="card-label">API 调用</div>
      <div class="card-value" id="tkCalls">—</div><div class="card-foot" id="tkCacheSub">缓存 0</div></div>
  </div>

  <!-- 扇形图：输入 vs 输出 -->
  <div class="card" style="margin-bottom:14px">
    <div class="card-label">Token 构成</div>
    <div class="donut-wrap">
      <div class="donut" id="donutIO"></div>
      <div class="donut-legend" id="legendIO"></div>
    </div>
  </div>

  <div class="grid-2" style="margin-bottom:14px">
    <!-- 扇形图：每日分布 -->
    <div class="card">
      <div class="card-label">每日用量占比</div>
      <div class="donut-wrap" style="gap:20px">
        <div class="donut" id="donutDaily"></div>
        <div class="donut-legend" id="legendDaily"></div>
      </div>
    </div>
    <!-- 扇形图：模型分布 -->
    <div class="card">
      <div class="card-label">模型占比</div>
      <div class="donut-wrap" style="gap:20px">
        <div class="donut" id="donutModel"></div>
        <div class="donut-legend" id="legendModel"></div>
      </div>
    </div>
  </div>

  <!-- 会话明细（条形） -->
  <div class="card">
    <div class="card-label">会话明细</div>
    <div id="sessDist" style="margin-top:12px"><div class="empty">暂无数据</div></div>
  </div>

  <div class="btn-row" style="margin-top:14px">
    <button class="btn btn-primary" onclick="loadToken()">重新采集</button>
    <span id="tkTime" style="font-size:12px;color:var(--muted)"></span>
  </div>
  </div>`;
}

function fmtNum(n) {
  if (n == null) return '—';
  if (n >= 1e9) return (n / 1e9).toFixed(2) + 'B';
  if (n >= 1e6) return (n / 1e6).toFixed(2) + 'M';
  if (n >= 1e3) return (n / 1e3).toFixed(1) + 'K';
  return String(n);
}

async function loadToken() {
  const r = await invokeCSharp('api/chat-read');
  if (!r) { toast('读取失败', 'fail'); return; }
  _chatData = r;

  const t = r.total || { input: 0, output: 0, cache: 0, calls: 0 };
  const set = (id, v) => { const e = document.getElementById(id); if (e) e.textContent = v; };
  set('tkTotal', fmtNum(t.input + t.output));
  set('tkIn', fmtNum(t.input));
  set('tkOut', fmtNum(t.output));
  set('tkCalls', t.calls);
  set('tkCacheSub', '缓存 ' + fmtNum(t.cache));
  set('tkTime', '采集于 ' + (r.generated || ''));

  // 1) 输入 / 输出 扇形
  drawDonut('donutIO', 'legendIO', [
    { name: '输入 Token', value: t.input },
    { name: '输出 Token', value: t.output },
  ]);

  // 2) 每日占比扇形
  const daily = (r.daily || []).map(d => ({ name: d.day.slice(5), value: d.input + d.output }));
  drawDonut('donutDaily', 'legendDaily', daily);

  // 3) 模型占比扇形
  const models = Object.keys(r.byModel || {}).map(k => ({
    name: k,
    value: r.byModel[k].input + r.byModel[k].output,
  }));
  drawDonut('donutModel', 'legendModel', models);

  // 4) 会话明细
  renderSessDist(r.sessionStats || []);
}

/**
 * 绘制甜甜圈（扇形）图
 * @param {string} hostId  容器 id
 * @param {string} legendId 图例容器 id
 * @param {Array<{name,value}>} data
 */
function drawDonut(hostId, legendId, data) {
  const host = document.getElementById(hostId);
  const legend = document.getElementById(legendId);
  if (!host) return;

  const valid = (data || []).filter(d => d.value > 0);
  if (!valid.length) {
    host.innerHTML = '<div class="empty" style="padding:60px 10px">暂无</div>';
    if (legend) legend.innerHTML = '';
    return;
  }

  const total = valid.reduce((s, d) => s + d.value, 0);
  const R = 74;                 // 半径
  const C = 2 * Math.PI * R;    // 周长
  const cx = 100, cy = 100;

  let offset = 0;
  const segs = valid.map((d, i) => {
    const frac = d.value / total;
    const len = frac * C;
    const color = DONUT_COLORS[i % DONUT_COLORS.length];
    const seg = `<circle class="donut-seg" cx="${cx}" cy="${cy}" r="${R}"
      stroke="${color}"
      stroke-dasharray="${len} ${C - len}"
      stroke-dashoffset="${-offset}"
      stroke-linecap="butt"><title>${d.name}: ${d.value.toLocaleString()} (${(frac*100).toFixed(1)}%)</title></circle>`;
    offset += len;
    return seg;
  }).join('');

  host.innerHTML = `
    <svg width="200" height="200" viewBox="0 0 200 200">
      <circle cx="${cx}" cy="${cy}" r="${R}" fill="none"
        stroke="rgba(0,0,0,.05)" stroke-width="26"/>
      ${segs}
    </svg>
    <div class="donut-center">
      <div class="donut-total">${fmtNum(total)}</div>
      <div class="donut-label">总计</div>
    </div>`;

  if (legend) {
    legend.innerHTML = valid.map((d, i) => {
      const pct = (d.value / total) * 100;
      return `<div class="donut-item">
        <span class="sw" style="background:${DONUT_COLORS[i % DONUT_COLORS.length]}"></span>
        <span class="nm">${d.name}</span>
        <span class="vl">${fmtNum(d.value)}</span>
        <span class="pc">${pct.toFixed(1)}%</span>
      </div>`;
    }).join('');
  }
}

function renderSessDist(sessions) {
  const box = document.getElementById('sessDist');
  if (!box) return;
  if (!sessions.length) { box.innerHTML = '<div class="empty">暂无数据</div>'; return; }

  const max = Math.max(...sessions.map(s => s.input + s.output), 1);
  const sorted = sessions.slice().sort((a, b) => (b.input + b.output) - (a.input + a.output));

  box.innerHTML = sorted.map(s => {
    const sum = s.input + s.output;
    const pct = (sum / max) * 100;
    const d = s.start ? new Date(s.start) : null;
    const dt = d ? `${d.getMonth()+1}/${d.getDate()} ${String(d.getHours()).padStart(2,'0')}:${String(d.getMinutes()).padStart(2,'0')}` : '';
    return `<div class="dist-row" style="margin-bottom:14px">
      <div style="display:flex;justify-content:space-between;align-items:baseline;margin-bottom:6px">
        <span style="font-size:13px;font-weight:500;font-family:var(--font-mono,monospace)">${s.sid}</span>
        <span style="font-size:12px;color:var(--muted);font-variant-numeric:tabular-nums">${fmtNum(sum)}</span>
      </div>
      <div class="dist-bar" style="height:6px;border-radius:99px;background:rgba(0,0,0,.06);overflow:hidden">
        <i style="display:block;height:100%;width:${pct}%;border-radius:99px;
          background:linear-gradient(90deg,#0071e3,#4fa3ff);transition:width .5s var(--ease)"></i>
      </div>
      <div style="margin-top:5px;font-size:11px;color:var(--muted)">${s.msgs} 消息 · ${s.calls} 调用 · ${dt}</div>
    </div>`;
  }).join('');
}



// ═══════════════════════════════════════════════════════════════════
//  API / 模型管理 —— 参考主流 Agent 平台
// ═══════════════════════════════════════════════════════════════════

const PROVIDERS = {
  deepseek: { name: 'DeepSeek', icon: '🐳', baseUrl: 'https://api.deepseek.com',
    models: ['deepseek-v4-pro', 'deepseek-flash', 'deepseek-chat', 'deepseek-reasoner'],
    keyHint: 'sk-...', docs: 'https://platform.deepseek.com/api_keys' },
  openai: { name: 'OpenAI', icon: '⚫', baseUrl: 'https://api.openai.com',
    models: ['gpt-4o', 'gpt-4o-mini', 'gpt-4-turbo', 'o1', 'o1-mini'],
    keyHint: 'sk-...', docs: 'https://platform.openai.com/api-keys' },
  anthropic: { name: 'Anthropic', icon: '🅰', baseUrl: 'https://api.anthropic.com',
    models: ['claude-sonnet-4-20250514', 'claude-opus-4-20250514', 'claude-3-5-haiku-20241022'],
    keyHint: 'sk-ant-...', docs: 'https://console.anthropic.com/settings/keys' },
  moonshot: { name: '月之暗面 Kimi', icon: '🌙', baseUrl: 'https://api.moonshot.cn',
    models: ['moonshot-v1-8k', 'moonshot-v1-32k', 'moonshot-v1-128k', 'kimi-k2-0711-preview'],
    keyHint: 'sk-...', docs: 'https://platform.moonshot.cn/console/api-keys' },
  zhipu: { name: '智谱 GLM', icon: '🧠', baseUrl: 'https://open.bigmodel.cn/api/paas/v4',
    models: ['glm-4-plus', 'glm-4-0520', 'glm-4-air', 'glm-4-flash'],
    keyHint: 'xxxx.xxxx', docs: 'https://bigmodel.cn/usercenter/apikeys' },
  qwen: { name: '阿里通义千问', icon: '☁', baseUrl: 'https://dashscope.aliyuncs.com/compatible-mode/v1',
    models: ['qwen-max', 'qwen-plus', 'qwen-turbo', 'qwen-long'],
    keyHint: 'sk-...', docs: 'https://dashscope.console.aliyun.com/apiKey' },
  siliconflow: { name: '硅基流动', icon: '🌊', baseUrl: 'https://api.siliconflow.cn/v1',
    models: ['deepseek-ai/DeepSeek-V3', 'Qwen/Qwen2.5-72B-Instruct'],
    keyHint: 'sk-...', docs: 'https://cloud.siliconflow.cn/account/ak' },
  custom: { name: '自定义 / 本地', icon: '⚙', baseUrl: 'http://127.0.0.1:8080/v1',
    models: [], keyHint: '可留空', docs: '' }
};

let _selectedProvider = null;

function pageApi() {
  return `<div class="page dynamic-page" data-dyn="api">
  <div class="page-head">
    <div class="page-title">模型与 API</div>
    <div class="page-sub">管理 AI 厂商、密钥和模型</div>
  </div>

  <div class="card" style="margin-bottom:16px">
    <div style="display:flex;align-items:center;justify-content:space-between;margin-bottom:14px">
      <div class="card-label" style="margin:0">已配置</div>
      <button class="btn btn-primary sm" onclick="openAddProvider()">+ 添加厂商</button>
    </div>
    <div id="apiList"><div class="empty">加载中…</div></div>
  </div>

  <div class="card" id="addPanel" style="display:none">
    <div class="card-label">添加厂商</div>

    <div style="margin:14px 0 18px">
      <div style="font-size:12px;font-weight:600;color:var(--muted);margin-bottom:10px">选择厂商</div>
      <div class="provider-grid" id="providerGrid"></div>
    </div>

    <div style="max-width:640px">
      <div class="form-row"><label>显示名称</label>
        <input type="text" id="apiName" placeholder="例如：DeepSeek 官方"></div>
      <div class="form-row"><label>Base URL</label>
        <input type="text" id="apiUrl" placeholder="https://api.deepseek.com"></div>
      <div class="form-row"><label>API Key</label>
        <input type="password" id="apiKey" placeholder="sk-...">
        <button class="tb-btn" onclick="toggleKey()" title="显示/隐藏" style="flex-shrink:0">
          <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg>
        </button></div>
      <div class="form-row"><label>模型</label>
        <input type="text" id="apiModel" list="modelList" placeholder="选择或手输模型名">
        <datalist id="modelList"></datalist></div>

      <div id="noteBox" style="display:none;padding:10px 14px;margin-bottom:14px;
        border-radius:10px;background:rgba(0,113,227,.07);font-size:12.5px;color:var(--body-copy)"></div>

      <div class="btn-row" style="margin-top:18px">
        <button class="btn btn-primary" id="btnSave" onclick="saveProvider()">保存</button>
        <button class="btn btn-secondary" onclick="closeAddProvider()">取消</button>
        <button class="btn btn-secondary" id="btnTest" onclick="testNewProvider()">测试连接</button>
        <span id="testResult" style="font-size:12.5px"></span>
      </div>
    </div>
  </div>
  </div>`;
}

function openAddProvider() {
  const p = document.getElementById('addPanel');
  if (p) p.style.display = 'block';
  renderProviderGrid();
  selectProvider('deepseek');
}

function closeAddProvider() {
  const p = document.getElementById('addPanel');
  if (p) p.style.display = 'none';
  _selectedProvider = null;
}

function renderProviderGrid() {
  const g = document.getElementById('providerGrid');
  if (!g) return;
  g.innerHTML = Object.entries(PROVIDERS).map(([k, v]) =>
    `<button class="provider-item${k === 'deepseek' ? ' active' : ''}" data-provider="${k}"
      onclick="selectProvider('${k}')">
      <span class="pi-icon">${v.icon}</span>
      <span class="pi-name">${v.name}</span>
    </button>`).join('');
}

function selectProvider(key) {
  _selectedProvider = key;
  const v = PROVIDERS[key];
  if (!v) return;

  document.querySelectorAll('.provider-item').forEach(el => {
    el.classList.toggle('active', el.dataset.provider === key);
  });

  const nameEl = document.getElementById('apiName');
  const urlEl = document.getElementById('apiUrl');
  const keyEl = document.getElementById('apiKey');
  const modelEl = document.getElementById('apiModel');
  const dl = document.getElementById('modelList');
  const note = document.getElementById('noteBox');
  const testRes = document.getElementById('testResult');

  if (nameEl) nameEl.value = v.name;
  if (urlEl) urlEl.value = v.baseUrl;
  if (keyEl) { keyEl.value = ''; keyEl.placeholder = v.keyHint; }
  if (testRes) testRes.textContent = '';
  if (modelEl) { modelEl.value = v.models[0] || ''; }
  if (dl) dl.innerHTML = v.models.map(m => `<option value="${m}"></option>`).join('');

  if (note) {
    if (v.docs) {
      note.style.display = 'block';
      note.innerHTML = '获取密钥：<span style="color:var(--blue)">' + v.docs + '</span>';
    } else { note.style.display = 'none'; }
  }
}

function toggleKey() {
  const el = document.getElementById('apiKey');
  if (el) el.type = el.type === 'password' ? 'text' : 'password';
}

async function saveProvider() {
  const name = document.getElementById('apiName').value.trim();
  const url = document.getElementById('apiUrl').value.trim();
  const key = document.getElementById('apiKey').value.trim();
  const model = document.getElementById('apiModel').value.trim();

  if (!name) { toast('请填写显示名称', 'fail'); return; }
  if (!url) { toast('请填写 Base URL', 'fail'); return; }
  if (!model) { toast('请填写模型名', 'fail'); return; }

  const r = await invokeCSharp('api/api-add', { name, baseUrl: url, apiKey: key, model });
  toast(r && r.msg ? r.msg : '完成', r && r.ok ? 'ok' : 'fail');
  if (r && r.ok) { closeAddProvider(); loadApis(); }
}

async function testNewProvider() {
  const btn = document.getElementById('btnTest');
  const res = document.getElementById('testResult');
  const url = document.getElementById('apiUrl').value.trim();
  const key = document.getElementById('apiKey').value.trim();
  const model = document.getElementById('apiModel').value.trim();

  if (!url || !key) { toast('先填 Base URL 和 Key', 'fail'); return; }

  btn.disabled = true;
  btn.innerHTML = '<span class="spin"></span> 测试中';
  if (res) { res.textContent = ''; }

  const r = await invokeCSharp('api/api-add', { name: '__test__', baseUrl: url, apiKey: key, model });
  const tid = r && r.id;
  if (tid) {
    const t = await invokeCSharp('api/api-test', { id: tid });
    await invokeCSharp('api/api-delete', { id: tid });
    if (res) {
      res.textContent = (t && t.ok ? '✓ ' : '✗ ') + (t ? t.msg : '');
      res.style.color = t && t.ok ? 'var(--green)' : 'var(--red)';
    }
  } else if (res) { res.textContent = '✗ 添加失败'; res.style.color = 'var(--red)'; }

  btn.disabled = false;
  btn.textContent = '测试连接';
}

async function loadApis() {
  const r = await invokeCSharp('api/apis');
  const box = document.getElementById('apiList');
  if (!box) return;

  if (!r || !r.apis || !r.apis.length) {
    box.innerHTML = '<div class="empty">还没有配置，点右上角「+ 添加厂商」</div>';
    return;
  }

  box.innerHTML = r.apis.map(a => {
    let icon = '⚙';
    for (const k in PROVIDERS) {
      const v = PROVIDERS[k];
      if (v.baseUrl && a.baseUrl && a.baseUrl.indexOf(v.baseUrl.replace(/\/v1\/?$/, '')) === 0) { icon = v.icon; break; }
    }
    return `<div class="api-card">
      <div class="api-card-icon">${icon}</div>
      <div class="api-card-body">
        <div class="api-card-name">${a.name}${a.isDefault ? ' <span class="badge-default">默认</span>' : ''}</div>
        <div class="api-card-meta">
          <span class="mono">${a.baseUrl}</span>
          <span style="opacity:.4">·</span>
          <span class="mono">${a.model}</span>
          <span style="opacity:.4">·</span>
          <span class="mono">${a.apiKeyMasked}</span>
        </div>
      </div>
      <div class="api-card-actions">
        <button class="btn btn-secondary sm" onclick="testApi('${a.id}')">测试</button>
        ${a.isDefault ? '' : `<button class="btn btn-secondary sm" onclick="setDefaultApi('${a.id}')">设为默认</button>`}
        ${a.isDefault ? '' : `<button class="btn btn-danger sm" onclick="delApi('${a.id}','${a.name}')">删除</button>`}
      </div>
    </div>`;
  }).join('');
}

async function testApi(id) {
  toast('测试中…');
  const r = await invokeCSharp('api/api-test', { id });
  toast(r && r.msg ? r.msg : '完成', r && r.ok ? 'ok' : 'fail');
}

async function setDefaultApi(id) {
  const r = await invokeCSharp('api/api-default', { id });
  toast(r && r.msg ? r.msg : '完成', r && r.ok ? 'ok' : 'fail');
  loadApis();
}

async function delApi(id, name) {
  if (!confirm('确定删除「' + name + '」？')) return;
  const r = await invokeCSharp('api/api-delete', { id });
  toast(r && r.msg ? r.msg : '完成', r && r.ok ? 'ok' : 'fail');
  loadApis();
}
// ═══════════════════════════════════════════════════════════════════
//  动态页面（全部走 JS 渲染，静态 HTML 已移除）
// ═══════════════════════════════════════════════════════════════════
function pageOverview() {
  return `<div class="page dynamic-page" data-dyn="overview">
    <div class="page-head">
    <div class="page-title">概览</div>
    <div class="page-sub" id="ovSub">正在读取状态…</div>
    </div>
    
    <div class="metrics">
    <div class="card">
    <div class="card-label">进程 PID</div>
    <div class="card-value" id="mPid">—</div>
    <div class="card-foot">机器人进程</div>
    </div>
    <div class="card">
    <div class="card-label">运行时长</div>
    <div class="card-value sm" id="mUptime">—</div>
    <div class="card-foot">自启动以来</div>
    </div>
    <div class="card">
    <div class="card-label">CPU 占用</div>
    <div class="card-value" id="mCpu">—</div>
    <div class="bar"><i id="barCpu" style="width:0%"></i></div>
    </div>
    <div class="card">
    <div class="card-label">内存占用</div>
    <div class="card-value" id="mMem">—</div>
    <div class="card-foot" id="mMemFoot">等待数据</div>
    <div class="bar"><i id="barMem" style="width:0%"></i></div>
    </div>
    <div class="card">
    <div class="card-label">消息量</div>
    <div class="card-value" id="mMsg">—</div>
    <div class="card-foot">今日收发</div>
    </div>
    <div class="card">
    <div class="card-label">出站连接</div>
    <div class="card-value" id="mConn">—</div>
    <div class="card-foot">到腾讯 443</div>
    </div>
    </div>
    
    <div class="card" style="margin-bottom:12px">
    <div class="card-label">快捷操作</div>
    <div class="btn-row" style="margin-top:12px">
    <button class="btn btn-primary" id="btnStart" onclick="doStart()">启动机器人</button>
    <button class="btn btn-secondary" onclick="doRestart()">重启</button>
    <button class="btn btn-danger" onclick="doStop()">停止</button>
    <button class="btn btn-secondary" onclick="refreshAll()">刷新</button>
    </div>
    </div>
    
    <div class="card">
    <div class="card-label">运行详情</div>
    <div class="list-row"><span class="k">当前人格</span><span class="v" id="dPersona">—</span></div>
    <div class="list-row"><span class="k">QQ API 解析</span><span class="v" id="dDns">—</span></div>
    <div class="list-row"><span class="k">网关状态</span><span class="v" id="dGw">—</span></div>
    <div class="list-row"><span class="k">Clash TUN</span><span class="v" id="dTun">—</span></div>
    <div class="list-row"><span class="k">最后更新</span><span class="v" id="dTime">—</span></div>
    </div>
  </div>`;
}

function pageControl() {
  return `<div class="page dynamic-page" data-dyn="control">
    <div class="page-head">
    <div class="page-title">控制</div>
    <div class="page-sub">所有操作真实执行系统命令</div>
    </div>
    
    <div class="card" style="margin-bottom:12px">
    <div class="card-label">机器人</div>
    <div class="btn-row" style="margin-top:12px">
    <button class="btn btn-primary" onclick="doStart()">启动</button>
    <button class="btn btn-secondary" onclick="doRestart()">重启</button>
    <button class="btn btn-danger" onclick="doStop()">停止</button>
    </div>
    </div>
    
    <div class="card" style="margin-bottom:12px">
    <div class="card-label">诊断</div>
    <div class="btn-row" style="margin-top:12px">
    <button class="btn btn-secondary" onclick="doGateway()">检查网关</button>
    <button class="btn btn-secondary" onclick="doDns()">DNS 检测</button>
    <button class="btn btn-secondary" onclick="doTun()">关闭 TUN</button>
    </div>
    </div>
    
    <div class="grid-2">
    <div class="card">
    <div class="card-label">操作记录</div>
    <div class="log-box" id="opList" style="margin-top:10px;height:260px">还没有操作</div>
    </div>
    <div class="card">
    <div class="card-label">执行详情</div>
    <div class="log-box" id="opDetail" style="margin-top:10px;height:260px">点上方按钮执行，这里显示完整命令、结果、验证</div>
    </div>
    </div>
  </div>`;
}

function pagePersona() {
  return `<div class="page dynamic-page" data-dyn="persona">
    <div class="page-head">
    <div class="page-title">人格</div>
    <div class="page-sub">切换或编辑机器人的人格设定</div>
    </div>
    
    <div class="card" style="margin-bottom:12px">
    <div class="card-label">快速切换</div>
    <div style="margin-top:10px;font-size:12.5px;color:var(--label-3)">当前：<b id="pCur" style="color:var(--label)">—</b></div>
    <div class="btn-row" style="margin-top:12px">
    <select id="pSel" style="min-width:240px"></select>
    <button class="btn btn-primary" onclick="applyPersona()">应用并重启</button>
    </div>
    </div>
    
    <div class="card">
    <div class="card-label">直接编辑 AGENTS.md <span id="pInfo" style="font-weight:400;margin-left:6px"></span></div>
    <textarea id="pText" style="width:100%;height:300px;margin-top:10px;font-family:var(--font-mono);font-size:12px"></textarea>
    <div class="btn-row" style="margin-top:12px">
    <button class="btn btn-primary" onclick="savePersona()">保存</button>
    <button class="btn btn-secondary" onclick="loadPersona()">重新读取</button>
    </div>
    </div>
  </div>`;
}

function pageLog() {
  return `<div class="page dynamic-page" data-dyn="log">
    <div class="page-head">
    <div class="page-title">日志</div>
    <div class="page-sub">查看运行、错误、启动日志</div>
    </div>
    
    <div class="card">
    <div class="btn-row" style="margin-bottom:12px">
    <select id="logSel" onchange="loadLog()" style="min-width:200px">
    <option value="run-out.log">run-out.log（运行）</option>
    <option value="run-err.log">run-err.log（错误）</option>
    <option value="autostart.log">autostart.log（启动）</option>
    <option value="console.log">console.log（控制台）</option>
    </select>
    <button class="btn btn-secondary" onclick="loadLog()">刷新</button>
    </div>
    <div class="log-box" id="logBox" style="height:calc(100vh - 300px)">加载中…</div>
    </div>
  </div>`;
}

function pageCheck() {
  return `<div class="page dynamic-page" data-dyn="check">
    <div class="page-head">
    <div class="page-title">自检</div>
    <div class="page-sub">逐项验证后端能力，失败如实显示</div>
    </div>
    
    <div class="card">
    <div class="btn-row" style="margin-bottom:14px">
    <button class="btn btn-primary" id="btnCheck" onclick="runCheck()">开始自检</button>
    <span id="checkSum" style="font-size:12.5px;color:var(--label-3)">尚未运行</span>
    </div>
    <div id="checkList"></div>
    </div>
  </div>`;
}


// ═══════════════════════════════════════════════════════════════════
//  AI 测试（鲸鱼娘人格）
// ═══════════════════════════════════════════════════════════════════
function pageAI() {
  return `<div class="page dynamic-page" data-dyn="ai">
  <div class="page-head">
    <div class="page-title">AI 测试</div>
    <div class="page-sub">跟鲸鱼娘聊聊，测试模型和人格是否正常</div>
  </div>

  <div class="card" style="margin-bottom:14px">
    <div class="card-label">当前人格</div>
    <div style="display:flex;align-items:center;gap:12px;margin-top:10px">
      <div style="width:44px;height:44px;border-radius:12px;background:linear-gradient(135deg,#4a90e2,#9b59f0);
        display:flex;align-items:center;justify-content:center;font-size:22px">🐳</div>
      <div>
        <div style="font-size:15px;font-weight:600" id="aiPersonaName">鲸鱼娘 · 溟月</div>
        <div style="font-size:12px;color:var(--muted);margin-top:2px" id="aiPersonaDesc">深海少女鲸鱼 · 温柔 · 会冒泡泡</div>
      </div>
    </div>
  </div>

  <div class="card" style="margin-bottom:14px">
    <div class="card-label">模型</div>
    <div class="btn-row" style="margin-top:10px">
      <select id="aiModel" style="min-width:240px"></select>
      <button class="btn btn-secondary sm" onclick="loadModels()">刷新列表</button>
      <button class="btn btn-secondary sm" onclick="testAll()">测试全部</button>
      <span id="aiRes" style="font-size:12px;color:var(--muted)"></span>
    </div>
  </div>

  <div class="card" style="margin-bottom:14px">
    <div class="card-label">对话测试</div>
    <div class="chat-area" id="aiChat" style="margin-top:12px;max-height:320px;overflow-y:auto;
      padding:12px;border-radius:12px;background:rgba(255,255,255,.5);min-height:120px">
      <div class="empty" style="padding:20px">跟鲸鱼娘打个招呼吧~</div>
    </div>
    <div style="display:flex;gap:10px;margin-top:12px">
      <input type="text" id="aiPrompt" placeholder="说点什么…（回车发送）" style="flex:1"
        onkeydown="if(event.key==='Enter')doAI()">
      <button class="btn btn-primary" onclick="doAI()">发送</button>
      <button class="btn btn-secondary" onclick="clearChat()">清空</button>
    </div>
  </div>

  <div class="card">
    <div class="card-label">快捷测试</div>
    <div class="btn-row" style="margin-top:10px">
      <button class="btn btn-secondary sm" onclick="quickAsk('你好')">打招呼</button>
      <button class="btn btn-secondary sm" onclick="quickAsk('今天好累啊')">累了</button>
      <button class="btn btn-secondary sm" onclick="quickAsk('你是谁')">自我介绍</button>
      <button class="btn btn-secondary sm" onclick="quickAsk('检查一下网关状态')">检查网关</button>
    </div>
  </div>
  </div>`;
}

let _aiHistory = [];

function quickAsk(t) {
  const el = document.getElementById('aiPrompt');
  if (el) el.value = t;
  doAI();
}

function clearChat() {
  _aiHistory = [];
  const box = document.getElementById('aiChat');
  if (box) box.innerHTML = '<div class="empty" style="padding:20px">跟鲸鱼娘打个招呼吧~</div>';
}

async function doAI() {
  const input = document.getElementById('aiPrompt');
  const box = document.getElementById('aiChat');
  const sel = document.getElementById('aiModel');
  if (!input || !box) return;

  const text = input.value.trim();
  if (!text) return;
  input.value = '';

  const model = sel ? sel.value : 'deepseek-v4.1-flash';

  // 显示用户消息
  if (box.querySelector('.empty')) box.innerHTML = '';
  box.insertAdjacentHTML('beforeend',
    `<div style="text-align:right;margin-bottom:10px">
      <span style="display:inline-block;padding:9px 14px;border-radius:14px 14px 4px 14px;
        background:var(--blue);color:#fff;font-size:13px;max-width:80%;text-align:left">${esc(text)}</span>
    </div>`);
  box.scrollTop = box.scrollHeight;

  // 显示"正在输入"
  const tid = 'typing_' + Date.now();
  box.insertAdjacentHTML('beforeend',
    `<div id="${tid}" style="margin-bottom:10px">
      <span style="display:inline-block;padding:9px 14px;border-radius:14px 14px 14px 4px;
        background:rgba(255,255,255,.9);font-size:13px;color:var(--muted)">鲸鱼娘正在思考…</span>
    </div>`);
  box.scrollTop = box.scrollHeight;

  const r = await invokeCSharp('api/assist', { q: text });

  const t = document.getElementById(tid);
  if (t) t.remove();

  if (r && r.ok) {
    const reply = r.reply || r.text || '';
    box.insertAdjacentHTML('beforeend',
      `<div style="margin-bottom:10px">
        <span style="display:inline-block;padding:9px 14px;border-radius:14px 14px 14px 4px;
          background:rgba(255,255,255,.92);font-size:13px;max-width:80%;line-height:1.6;
          white-space:pre-wrap">${esc(reply)}</span>
        <span style="font-size:11px;color:var(--muted);margin-left:8px">${r.ms||''}ms</span>
      </div>`);
  } else {
    box.insertAdjacentHTML('beforeend',
      `<div style="margin-bottom:10px">
        <span style="display:inline-block;padding:9px 14px;border-radius:14px;
          background:rgba(255,59,48,.1);color:var(--red);font-size:13px">
          ✗ ${esc((r && r.msg) || '调用失败')}</span>
      </div>`);
  }
  box.scrollTop = box.scrollHeight;
}

function esc(s) {
  return String(s || '').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
}
// ═══════════════════════════════════════════════════════════════════
//  设置
// ═══════════════════════════════════════════════════════════════════
function pageSetting() {
  return `<div class="page dynamic-page" data-dyn="setting">
  <div class="page-head">
    <div class="page-title">设置</div>
    <div class="page-sub">改动写入 patch.yml，重启机器人后生效</div>
  </div>

  <!-- 机器人 -->
  <div class="card" style="margin-bottom:14px">
    <div class="card-label">机器人</div>
    <div style="margin-top:12px">
      <div class="list-row">
        <span class="k">模型<small>机器人使用的 AI 模型</small></span>
        <span class="v"><select id="cfgModel" style="min-width:220px"></select></span>
      </div>
      <div class="list-row">
        <span class="k">群里必须 @ 才回复<small>关闭后群里任何消息都会回</small></span>
        <label class="switch"><input type="checkbox" id="cfgMention"><span></span></label>
      </div>
      <div class="list-row">
        <span class="k">流式输出<small>边想边发，建议关闭</small></span>
        <label class="switch"><input type="checkbox" id="cfgStream"><span></span></label>
      </div>
      <div class="list-row">
        <span class="k">显示工具结果<small>在聊天里显示工具调用过程</small></span>
        <label class="switch"><input type="checkbox" id="cfgTools"><span></span></label>
      </div>
      <div class="list-row">
        <span class="k">调试模式<small>输出详细日志</small></span>
        <label class="switch"><input type="checkbox" id="cfgDebug"><span></span></label>
      </div>
    </div>
  </div>

  <!-- 网关检测 -->
  <div class="card" style="margin-bottom:14px">
    <div class="card-label">网关检测</div>
    <div style="margin-top:12px">
      <div class="list-row">
        <span class="k">网关地址<small>AI 网关 的 HTTP 端点</small></span>
        <span class="v"><input type="text" id="cfgGateway" value="http://127.0.0.1:3065"
          style="width:240px;text-align:right;font-family:monospace"></span>
      </div>
      <div class="list-row">
        <span class="k">自动检测间隔<small>多久检查一次网关和机器人</small></span>
        <span class="v"><select id="cfgInterval" style="min-width:140px">
          <option value="3">3 秒</option>
          <option value="5">5 秒</option>
          <option value="10">10 秒</option>
          <option value="30">30 秒</option>
        </select></span>
      </div>
      <div class="list-row">
        <span class="k">掉线自动重启<small>检测到机器人挂了自动拉起</small></span>
        <label class="switch"><input type="checkbox" id="cfgAutoRestart" checked><span></span></label>
      </div>
      <div class="list-row">
        <span class="k">自动修 DNS 劫持<small>检测到 Clash 劫持自动关闭 TUN</small></span>
        <label class="switch"><input type="checkbox" id="cfgAutoDns"><span></span></label>
      </div>
      <div class="list-row">
        <span class="k">健康检查<small>立即检测网关连通性</small></span>
        <span class="v">
          <button class="btn btn-secondary sm" onclick="checkGatewayNow()">立即检测</button>
          <span id="gwResult" style="margin-left:10px;font-size:12px"></span>
        </span>
      </div>
    </div>
  </div>

  <!-- 外观 -->
  <div class="card" style="margin-bottom:14px">
    <div class="card-label">外观</div>
    <div style="margin-top:12px">
      <div class="list-row">
        <span class="k">窗口效果<small>液态玻璃模糊强度</small></span>
        <span class="v"><select id="cfgBlur" style="min-width:140px">
          <option value="20">轻</option>
          <option value="42" selected>标准</option>
          <option value="64">强</option>
        </select></span>
      </div>
      <div class="list-row">
        <span class="k">最小化到托盘<small>关闭窗口时隐藏到系统托盘</small></span>
        <label class="switch"><input type="checkbox" id="cfgTray" checked><span></span></label>
      </div>
    </div>
  </div>

  <!-- 保存 -->
  <div class="btn-row">
    <button class="btn btn-primary" onclick="saveSettings()">保存设置</button>
    <button class="btn btn-secondary" onclick="loadConfig()">重新读取</button>
    <button class="btn btn-secondary" onclick="restartBot()">重启机器人</button>
    <span id="cfgMsg" style="font-size:12px;color:var(--muted)"></span>
  </div>
  </div>`;
}

async function checkGatewayNow() {
  const el = document.getElementById('gwResult');
  if (el) { el.textContent = '检测中…'; el.style.color = 'var(--muted)'; }
  const r = await invokeCSharp('api/gateway');
  if (el) {
    el.textContent = r && r.ok ? '✓ 网关正常' : '✗ 网关离线';
    el.style.color = r && r.ok ? 'var(--green)' : 'var(--red)';
  }
}

async function saveSettings() {
  const msg = document.getElementById('cfgMsg');
  const model = document.getElementById('cfgModel').value;
  const mention = document.getElementById('cfgMention').checked;
  const stream = document.getElementById('cfgStream').checked;
  const tools = document.getElementById('cfgTools').checked;
  const debug = document.getElementById('cfgDebug').checked;

  if (msg) msg.textContent = '保存中…';

  let n = 0;
  const set = async (k, v) => {
    const r = await invokeCSharp('api/config-set', { key: k, value: String(v) });
    if (r && r.ok) n++;
  };
  await set('model', model);
  await set('requireMention', mention);
  await set('streaming', stream);
  await set('showToolResults', tools);
  await set('debug', debug);

  if (msg) {
    msg.textContent = n > 0 ? `✓ 已保存 ${n} 项，重启后生效` : '✗ 保存失败';
    msg.style.color = n > 0 ? 'var(--green)' : 'var(--red)';
  }
}

async function restartBot() {
  toast('正在重启机器人…');
  const r = await invokeCSharp('api/restart');
  toast(r && r.ok ? '重启成功' : '重启失败', r && r.ok ? 'ok' : 'fail');
}
// ═══════════════════════════════════════════════════════════════════
//  自动化
// ═══════════════════════════════════════════════════════════════════
function pageAuto() {
  return `<div class="page dynamic-page" data-dyn="auto">
  <div class="page-head">
    <div class="page-title">自动化</div>
    <div class="page-sub">让控制台自动看护你的机器人</div>
  </div>

  <div class="card" style="margin-bottom:14px">
    <div class="card-label">自动看护</div>
    <div style="margin-top:12px">
      <div class="list-row">
        <span class="k">掉线自动重启<small>检测到机器人挂了，自动拉起来</small></span>
        <label class="switch"><input type="checkbox" id="autoRestart" checked onchange="saveAuto()"><span></span></label>
      </div>
      <div class="list-row">
        <span class="k">网关掉线自动重启<small>网关不在就重启网关进程</small></span>
        <label class="switch"><input type="checkbox" id="autoGw" onchange="saveAuto()"><span></span></label>
      </div>
      <div class="list-row">
        <span class="k">DNS 劫持自动修复<small>检测到 198.18.x.x 自动关 TUN</small></span>
        <label class="switch"><input type="checkbox" id="autoDns" onchange="saveAuto()"><span></span></label>
      </div>
      <div class="list-row">
        <span class="k">模型限流自动切换<small>429 时自动换模型</small></span>
        <label class="switch"><input type="checkbox" id="autoFallback" checked onchange="saveAuto()"><span></span></label>
      </div>
    </div>
  </div>

  <div class="card" style="margin-bottom:14px">
    <div class="card-label">定时巡检</div>
    <div style="margin-top:12px">
      <div class="list-row">
        <span class="k">巡检间隔<small>多久做一次完整检查</small></span>
        <span class="v"><select id="autoInterval" style="min-width:140px" onchange="saveAuto()">
          <option value="0">关闭</option>
          <option value="5">5 分钟</option>
          <option value="15">15 分钟</option>
          <option value="30" selected>30 分钟</option>
          <option value="60">1 小时</option>
        </select></span>
      </div>
      <div class="list-row">
        <span class="k">开机自启<small>Windows 启动时自动打开控制台</small></span>
        <label class="switch"><input type="checkbox" id="autoBoot" onchange="toggleBoot()"><span></span></label>
      </div>
    </div>
  </div>

  <div class="card">
    <div class="card-label">执行记录</div>
    <div id="autoLog" style="margin-top:12px;max-height:260px;overflow-y:auto">
      <div class="empty">还没有自动化记录</div>
    </div>
    <div class="btn-row" style="margin-top:14px">
      <button class="btn btn-secondary sm" onclick="loadAuto()">刷新记录</button>
      <button class="btn btn-secondary sm" onclick="runCheckNow()">立即巡检一次</button>
      <span id="autoMsg" style="font-size:12px;color:var(--muted)"></span>
    </div>
  </div>
  </div>`;
}

async function loadAuto() {
  // 读设置
  const r = await invokeCSharp('api/auto');
  if (r) {
    const set = (id, v) => { const e = document.getElementById(id); if (e) e.checked = !!v; };
    set('autoRestart', r.autoRestart);
    set('autoGw', r.autoGw);
    set('autoDns', r.autoDns);
    set('autoFallback', r.autoFallback);
    set('autoBoot', r.autoBoot);
    const iv = document.getElementById('autoInterval');
    if (iv && r.interval != null) iv.value = String(r.interval);
  }

  // 读执行记录
  const log = await invokeCSharp('api/ops');
  const box = document.getElementById('autoLog');
  if (!box) return;
  const items = (log && log.items) || [];
  if (!items.length) {
    box.innerHTML = '<div class="empty">还没有自动化记录</div>';
    return;
  }
  box.innerHTML = items.slice(0, 30).map(it => `
    <div class="list-row">
      <span class="k">${it.action || it.name || '操作'}<small>${it.detail || ''}</small></span>
      <span class="v">${it.time || ''}</span>
    </div>`).join('');
}

async function saveAuto() {
  const g = id => { const e = document.getElementById(id); return e ? e.checked : false; };
  const iv = document.getElementById('autoInterval');
  const payload = {
    autoRestart: g('autoRestart'),
    autoGw: g('autoGw'),
    autoDns: g('autoDns'),
    autoFallback: g('autoFallback'),
    interval: iv ? iv.value : '30'
  };
  const r = await invokeCSharp('api/auto-set', payload);
  const msg = document.getElementById('autoMsg');
  if (msg) {
    msg.textContent = r && r.ok ? '✓ 已保存' : '✗ 保存失败';
    msg.style.color = r && r.ok ? 'var(--green)' : 'var(--red)';
  }
}

async function toggleBoot() {
  const on = document.getElementById('autoBoot').checked;
  const r = await invokeCSharp('api/auto-boot', { on: on ? '1' : '0' });
  toast(r && r.ok ? (on ? '已设置开机自启' : '已取消开机自启') : '设置失败', r && r.ok ? 'ok' : 'fail');
}

async function runCheckNow() {
  toast('正在巡检…');
  const r = await invokeCSharp('api/check');
  toast(r && r.ok ? '巡检完成' : '巡检出错', r && r.ok ? 'ok' : 'fail');
  loadAuto();
}
// ═══════════════════════════════════════════════════════════════════
//  日志（历史缓存：先显示旧内容，再异步更新）
// ═══════════════════════════════════════════════════════════════════
const _logCache = {};

async function loadLog() {
  const sel = document.getElementById('logSel');
  const box = document.getElementById('logBox');
  if (!box) return;

  const file = sel ? sel.value : 'run-out.log';

  // ① 先显示缓存（立刻有东西看，不空白）
  if (_logCache[file]) {
    box.textContent = _logCache[file];
    box.scrollTop = box.scrollHeight;
  } else {
    // 没缓存 → 显示占位提示，避免纯白
    box.innerHTML = '<span style="color:var(--muted)">正在读取 ' + file + ' …</span>';
  }

  // ② 异步拉最新
  try {
    const r = await invokeCSharp('api/log', { file: file });
    let txt = '';
    if (r && typeof r === 'string') txt = r;
    else if (r && r.text) txt = r.text;
    else if (r && r.content) txt = r.content;

    if (!txt) txt = '（空文件）';

    // ③ 覆盖缓存 + 更新界面
    _logCache[file] = txt;
    box.textContent = txt;
    box.scrollTop = box.scrollHeight;
  } catch (e) {
    if (!_logCache[file]) box.textContent = '读取失败：' + e.message;
  }
}

// ═══════════════════════════════════════════════════════════════════
//  模型列表
// ═══════════════════════════════════════════════════════════════════
async function loadModels() {
  const r = await invokeCSharp('api/models');
  const sel = document.getElementById('aiModel');
  const cfg = document.getElementById('cfgModel');
  let list = [];
  if (r && r.models) list = r.models;
  else if (Array.isArray(r)) list = r;

  const opts = list.map(m => {
    const id = typeof m === 'string' ? m : (m.id || m.name || '');
    return `<option value="${id}">${id}</option>`;
  }).join('');

  if (sel) sel.innerHTML = opts || '<option value="deepseek-v4.1-flash">deepseek-v4.1-flash</option>';
  if (cfg) cfg.innerHTML = opts || '<option value="deepseek-v4.1-flash">deepseek-v4.1-flash</option>';
}

// ═══════════════════════════════════════════════════════════════════
//  控制页
// ═══════════════════════════════════════════════════════════════════
async function loadOps() {
  const r = await invokeCSharp('api/ops');
  const box = document.getElementById('opsList');
  if (!box) return;
  const items = (r && r.items) || [];
  if (!items.length) {
    box.innerHTML = '<div class="empty">还没有操作记录</div>';
    return;
  }
  box.innerHTML = items.slice(0, 20).map(it => `
    <div class="list-row">
      <span class="k">${it.action || it.name || '操作'}<small>${it.detail || ''}</small></span>
      <span class="v">${it.time || ''}</span>
    </div>`).join('');
}