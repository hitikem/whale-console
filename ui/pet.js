/* ═══════════════════════════════════════════════════════════════════
   Whale Pet · 鲸鱼娘桌宠（可换形象 / 可自由摆位 / 可养多只）
   ------------------------------------------------------------------
   独立模块，只往 .window 注入浮动层，不改动任何现有结构与样式。
   互动：单击说话 · 双击摸头 · 拖拽摆到任意位置 · 右键菜单
   右键菜单：换形象 / 再养一只 / 喂食 / 摸头 / 回右下角 / 藏起来
   自定义形象存在 localStorage（图片会先缩到 256px，避免超配额）
   ═══════════════════════════════════════════════════════════════════ */
(() => {
  if (window.__whalePet) return;
  window.__whalePet = true;

  const DEFAULT_ART = '../assets/icons/whale/whale-girl-256.png';
  const KEY = 'whale.pet.v2';
  const TIERS = [0, 12, 30, 56, 90];
  const SIZE = 122;

  /* ── 存档 ── */
  const load = () => {
    try {
      const raw = JSON.parse(localStorage.getItem(KEY) || 'null');
      if (raw && Array.isArray(raw.pets) && raw.pets.length) return raw;
      // 迁移 v1 存档
      const old = JSON.parse(localStorage.getItem('whale.pet.v1') || 'null');
      if (old) {
        return {
          pets: [{
            id: 'p1', art: null, x: old.x, y: old.y,
            aff: old.aff || 0, lastPat: 0, lastFeed: 0
          }]
        };
      }
    } catch (e) { }
    return { pets: [{ id: 'p1', art: null, x: null, y: null, aff: 0, lastPat: 0, lastFeed: 0 }] };
  };
  const db = load();
  const save = () => { try { localStorage.setItem(KEY, JSON.stringify(db)); } catch (e) { } };

  const level = (aff) => {
    let lv = 1;
    TIERS.forEach((t, i) => { if (aff >= t) lv = i + 1; });
    return Math.min(lv, 5);
  };

  const LINES = {
    idle: ['咕噜……', '在的哦。', '要不要看看日志？', '我在这儿守着呢。', '水面今天很安静。',
      '摸摸头？', '有点想喝汽水。', '别一直盯着指标看啦。', '后台很稳，我盯着。'],
    pat: ['诶嘿……', '再摸一下嘛。', '头发要乱啦！', '唔，舒服。', '你手好暖。', '……才没有很开心。'],
    feed: ['是鱼！谢谢！', '啊呜——好吃。', '还有吗还有吗？', '这个我最喜欢了！'],
    drag: ['放我下来啦——', '要飞起来了！', '这里视野不错。'],
    botOff: ['机器人好像睡着了……', '要不要我帮你叫醒它？', '有点冷清呢。'],
    botOn: ['机器人在好好跑着，放心。', '连接很稳，我盯着呢。'],
    lv: { 1: '刚认识，请多指教。', 2: '你好像越来越常来了。', 3: '嗯……我记住你的声音了。',
          4: '今天也想早点见到你。', 5: '你不在的时候，海面好空。' }
  };
  const pick = (a) => a[Math.floor(Math.random() * a.length)];
  const botAlive = () => {
    const d = document.getElementById('dotBot');
    return !!(d && d.className.indexOf('on') >= 0);
  };

  // 位置变化后刷新「不可拖动区域」，避免拖宠物变成拖窗口
  let nodragT = null;
  const syncNoDrag = () => {
    clearTimeout(nodragT);
    nodragT = setTimeout(() => {
      if (typeof window.reportNoDrag === 'function') window.reportNoDrag();
    }, 120);
  };

  /* ── 样式 ── */
  const css = document.createElement('style');
  css.textContent = `
  #wp-stage{position:fixed;inset:0;z-index:70;pointer-events:none}
  .wp-p{position:absolute;width:${SIZE}px;pointer-events:none}
  .wp-body{position:relative;width:${SIZE}px;height:${SIZE}px;pointer-events:auto;
    cursor:grab;user-select:none;-webkit-user-select:none;touch-action:none;
    filter:drop-shadow(0 6px 12px rgba(10,60,140,.24));
    animation:wp-bob 3.6s ease-in-out infinite;
    transition:transform .22s cubic-bezier(.32,.72,0,1),filter .22s}
  .wp-body:hover{transform:scale(1.07) translateY(-2px);
    filter:drop-shadow(0 10px 18px rgba(10,60,140,.3))}
  .wp-body.drag{cursor:grabbing;animation:none;transform:scale(1.04)}
  .wp-body img{width:100%;height:100%;object-fit:contain;display:block;
    -webkit-user-drag:none;pointer-events:none}
  .wp-body.sad img{filter:grayscale(.5) brightness(.96)}
  .wp-body.hop{animation:wp-hop .55s ease-out}
  @keyframes wp-bob{0%,100%{transform:translateY(0)}50%{transform:translateY(-5px)}}
  @keyframes wp-hop{0%{transform:translateY(0) scale(1)}
    35%{transform:translateY(-14px) scale(1.06)}
    70%{transform:translateY(0) scale(.97)}100%{transform:translateY(0) scale(1)}}

  .wp-bubble{position:absolute;right:${SIZE + 12}px;top:6px;max-width:210px;width:max-content;
    padding:8px 12px;border-radius:13px;font-size:12.5px;line-height:1.45;color:#1d1d1f;
    background:rgba(255,255,255,.93);border:.5px solid rgba(0,0,0,.06);
    box-shadow:0 1px 2px rgba(0,0,0,.07),0 10px 26px -8px rgba(0,0,0,.22),
      inset 0 1px 0 rgba(255,255,255,.9);backdrop-filter:blur(14px);
    opacity:0;transform:translateX(6px) scale(.96);transform-origin:right center;
    transition:opacity .2s,transform .2s cubic-bezier(.32,.72,0,1);
    pointer-events:none;white-space:pre-wrap}
  .wp-bubble.on{opacity:1;transform:translateX(0) scale(1)}
  .wp-bubble::after{content:'';position:absolute;right:-5px;top:16px;width:10px;height:10px;
    background:rgba(255,255,255,.93);transform:rotate(45deg);
    border-right:.5px solid rgba(0,0,0,.06);border-top:.5px solid rgba(0,0,0,.06)}
  .wp-bubble.left{right:auto;left:${SIZE + 12}px;transform-origin:left center}
  .wp-bubble.left::after{right:auto;left:-5px;border:none;
    border-left:.5px solid rgba(0,0,0,.06);border-bottom:.5px solid rgba(0,0,0,.06)}

  .wp-hud{position:absolute;right:${SIZE + 6}px;bottom:4px;display:flex;align-items:center;gap:5px;
    padding:3px 8px;border-radius:99px;background:rgba(255,255,255,.82);
    box-shadow:0 1px 3px rgba(0,0,0,.1);font-size:10.5px;color:#6b6b70;
    opacity:0;transform:translateY(3px);transition:.2s;pointer-events:none;
    white-space:nowrap;backdrop-filter:blur(10px)}
  .wp-p:hover .wp-hud{opacity:1;transform:translateY(0)}
  .wp-hud b{color:#0071e3;font-weight:600}
  .wp-hud .chips{letter-spacing:-1px;color:#ff5d8f}

  .wp-fx{position:absolute;pointer-events:none;font-size:14px;
    animation:wp-fly 1s ease-out forwards}
  @keyframes wp-fly{0%{opacity:0;transform:translate(0,0) scale(.6)}
    22%{opacity:1}100%{opacity:0;transform:translate(var(--dx),var(--dy)) scale(1.15)}}

  #wp-menu{position:fixed;z-index:80;min-width:142px;padding:5px;border-radius:12px;
    background:rgba(250,250,252,.95);backdrop-filter:blur(20px);
    box-shadow:0 1px 2px rgba(0,0,0,.08),0 14px 34px -10px rgba(0,0,0,.3),
      inset 0 0 0 .5px rgba(0,0,0,.06);
    opacity:0;transform:scale(.94);transform-origin:top left;transition:.16s cubic-bezier(.32,.72,0,1);
    pointer-events:none}
  #wp-menu.on{opacity:1;transform:scale(1);pointer-events:auto}
  #wp-menu button{display:flex;width:100%;align-items:center;gap:8px;padding:7px 9px;
    border:none;background:transparent;border-radius:8px;font-family:inherit;
    font-size:12.5px;color:#1d1d1f;cursor:pointer;text-align:left}
  #wp-menu button:hover{background:rgba(0,0,0,.055)}
  #wp-menu button:disabled{opacity:.38;cursor:default;background:transparent}
  #wp-menu i{font-style:normal;width:15px;text-align:center;font-size:12.5px}
  #wp-menu .sep{height:1px;margin:4px 6px;background:rgba(0,0,0,.07)}
  `;
  document.head.appendChild(css);

  /* ── 容器 ── */
  const stage = document.createElement('div');
  stage.id = 'wp-stage';
  const menu = document.createElement('div');
  menu.id = 'wp-menu';
  menu.innerHTML = `
    <button data-act="pat"><i>🤍</i>摸摸头</button>
    <button data-act="feed"><i>🐟</i>喂一条鱼</button>
    <button data-act="talk"><i>💬</i>说句话</button>
    <div class="sep"></div>
    <button data-act="art"><i>🖼</i>换形象…</button>
    <button data-act="add"><i>➕</i>再养一只</button>
    <button data-act="reset"><i>↩︎</i>回到右下角</button>
    <button data-act="remove"><i>✕</i>不要这只了</button>
    <div class="sep"></div>
    <button data-act="hide"><i>👁</i>全部藏起来</button>`;

  const filePick = document.createElement('input');
  filePick.type = 'file';
  filePick.accept = 'image/png,image/jpeg,image/webp,image/gif';
  filePick.style.display = 'none';

  /* ── 单只宠物 ── */
  let seq = db.pets.length + 1;
  const instances = [];

  function createPet(cfg) {
    const el = document.createElement('div');
    el.className = 'wp-p';
    el.innerHTML = `
      <div class="wp-body"><img alt=""></div>
      <div class="wp-bubble"></div>
      <div class="wp-hud">好感 <b>Lv.1</b> <span class="chips"></span> <span class="n"></span></div>`;
    const body = el.querySelector('.wp-body');
    const img = el.querySelector('img');
    const bubble = el.querySelector('.wp-bubble');
    const hud = el.querySelector('.wp-hud');
    img.src = cfg.art || DEFAULT_ART;
    stage.appendChild(el);

    const self = {
      cfg, el, body, img, bubble, hud,
      say, place, render, destroy
    };

    function place(x, y) {
      const W = window.innerWidth, H = window.innerHeight, pad = 6;
      if (x == null || y == null) { x = W - SIZE - 26; y = H - SIZE - 26; }
      cfg.x = Math.max(pad, Math.min(x, W - SIZE - pad));
      cfg.y = Math.max(56, Math.min(y, H - SIZE - pad));
      el.style.left = cfg.x + 'px';
      el.style.top = cfg.y + 'px';
      // 靠右时气泡翻到左边，避免出界
      const flip = cfg.x + SIZE / 2 > W / 2;
      bubble.classList.toggle('left', !flip);
      syncNoDrag();
    }

    let bt = null;
    function say(text, ms = 3200) {
      bubble.textContent = text;
      bubble.classList.add('on');
      clearTimeout(bt);
      bt = setTimeout(() => bubble.classList.remove('on'), ms);
    }

    function fx(char, dx, dy) {
      const d = document.createElement('div');
      d.className = 'wp-fx';
      d.textContent = char;
      d.style.left = '34px'; d.style.top = '12px';
      d.style.setProperty('--dx', dx + 'px');
      d.style.setProperty('--dy', dy + 'px');
      body.appendChild(d);
      setTimeout(() => d.remove(), 1050);
    }
    const burst = (chars, n = 4) => {
      for (let i = 0; i < n; i++) {
        setTimeout(() => fx(pick(chars), -26 + Math.random() * 52, -46 - Math.random() * 26), i * 90);
      }
    };

    function render() {
      const lv = level(cfg.aff);
      const next = TIERS[lv] != null ? TIERS[lv] : TIERS[TIERS.length - 1];
      const prev = TIERS[lv - 1] || 0;
      const pct = Math.min(100, Math.round((cfg.aff - prev) / Math.max(1, next - prev) * 100));
      const hearts = Math.max(1, Math.round(pct / 25));
      hud.querySelector('b').textContent = 'Lv.' + lv;
      hud.querySelector('.chips').textContent = '♥'.repeat(hearts) + '♡'.repeat(4 - hearts);
      hud.querySelector('.n').textContent = `${cfg.aff}/${next}`;
      body.classList.toggle('sad', !botAlive());
    }

    function addAff(n) {
      const before = level(cfg.aff);
      cfg.aff += n;
      render(); save();
      if (level(cfg.aff) > before) {
        body.classList.remove('hop'); void body.offsetWidth; body.classList.add('hop');
        burst(['✨', '💙', '⭐'], 6);
        setTimeout(() => say(LINES.lv[level(cfg.aff)] || '', 4200), 500);
      }
    }

    function hop() { body.classList.remove('hop'); void body.offsetWidth; body.classList.add('hop'); }

    function pat() {
      const now = Date.now();
      if (now - cfg.lastPat < 2500) { say('等一下啦，头要晕了……', 1800); return; }
      cfg.lastPat = now;
      hop(); burst(['💙', '💕', '✨'], 4);
      say(pick(LINES.pat), 2600); addAff(1);
    }
    function feed() {
      const now = Date.now();
      if (now - cfg.lastFeed < 20000) { say('刚吃过，肚子还鼓着呢。', 2000); return; }
      cfg.lastFeed = now;
      fx('🐟', -8, 18); hop();
      setTimeout(() => { burst(['✨', '💙'], 3); say(pick(LINES.feed), 2800); }, 260);
      addAff(3);
    }
    function talk() {
      say(pick(botAlive() ? LINES.botOn.concat(LINES.idle) : LINES.botOff), 3200);
    }

    /* 拖拽 / 点击 */
    let drag = null, moved = false;
    body.addEventListener('pointerdown', (e) => {
      if (e.button === 2) return;
      drag = { sx: e.clientX, sy: e.clientY, ox: cfg.x, oy: cfg.y, t: Date.now() };
      moved = false;
      body.setPointerCapture(e.pointerId);
      body.classList.add('drag');
      hideMenu();
      if (window.setPetHold) window.setPetHold(true);
    });
    body.addEventListener('pointermove', (e) => {
      if (!drag) return;
      const dx = e.clientX - drag.sx, dy = e.clientY - drag.sy;
      if (Math.abs(dx) > 3 || Math.abs(dy) > 3) moved = true;
      if (moved) place(drag.ox + dx, drag.oy + dy);
    });
    body.addEventListener('pointerup', () => {
      if (!drag) return;
      body.classList.remove('drag');
      if (moved) { save(); if (Math.random() < .3) say(pick(LINES.drag), 1800); }
      drag = null;
      setTimeout(() => { moved = false; }, 60);
      if (window.setPetHold) window.setPetHold(false);
    });
    body.addEventListener('pointercancel', () => {
      drag = null;
      if (window.setPetHold) window.setPetHold(false);
    });
    body.addEventListener('click', () => { if (!moved) talk(); });
    body.addEventListener('dblclick', (e) => { e.preventDefault(); pat(); });
    body.addEventListener('contextmenu', (e) => {
      e.preventDefault();
      openMenu(self, e.clientX, e.clientY);
    });

    function destroy() {
      el.remove();
      const i = instances.indexOf(self);
      if (i >= 0) instances.splice(i, 1);
      db.pets = db.pets.filter(p => p !== cfg);
      save();
      syncNoDrag();
    }

    place(cfg.x, cfg.y);
    render();
    instances.push(self);
    return self;
  }

  /* ── 菜单 ── */
  let menuTarget = null;
  function openMenu(pet, cx, cy) {
    menuTarget = pet;
    const now = Date.now();
    menu.querySelector('[data-act="pat"]').disabled = now - pet.cfg.lastPat < 2500;
    menu.querySelector('[data-act="feed"]').disabled = now - pet.cfg.lastFeed < 20000;
    menu.querySelector('[data-act="remove"]').disabled = db.pets.length <= 1;
    menu.classList.add('on');
    const w = menu.offsetWidth || 150, h = menu.offsetHeight || 250;
    menu.style.left = Math.min(cx, window.innerWidth - w - 8) + 'px';
    menu.style.top = Math.min(cy, window.innerHeight - h - 8) + 'px';
  }
  const hideMenu = () => menu.classList.remove('on');

  menu.addEventListener('pointerdown', (e) => {
    if (window.setPetHold) window.setPetHold(true);
  });
  document.addEventListener('pointerup', () => {
    if (window.setPetHold) window.setPetHold(false);
  }, true);

  menu.addEventListener('click', (e) => {
    const b = e.target.closest('button');
    if (!b || b.disabled || !menuTarget) return;
    const act = b.dataset.act, pet = menuTarget;
    hideMenu();
    if (act === 'pat') pet.cfg.lastPat = Date.now(), pet.cfg.aff += 1, pet.render(), save(),
      pet.body.classList.add('hop'), pet.say(pick(LINES.pat), 2600);
    else if (act === 'feed') {
      pet.cfg.lastFeed = Date.now(); pet.cfg.aff += 3; pet.render(); save();
      pet.body.classList.add('hop'); pet.say(pick(LINES.feed), 2800);
    } else if (act === 'talk') pet.say(pick(botAlive() ? LINES.botOn : LINES.botOff), 3200);
    else if (act === 'art') pickArtFor(pet);
    else if (act === 'add') addPet();
    else if (act === 'reset') { pet.cfg.x = null; pet.cfg.y = null; pet.place(null, null); save(); }
    else if (act === 'remove') pet.destroy();
    else if (act === 'hide') hideAll();
  });

  document.addEventListener('pointerdown', (e) => {
    if (!menu.contains(e.target)) hideMenu();
  }, true);

  /* ── 换形象 ── */
  let artTarget = null;
  function pickArtFor(pet) {
    artTarget = pet;
    filePick.value = '';
    filePick.click();
  }
  filePick.addEventListener('change', () => {
    const f = filePick.files && filePick.files[0];
    if (!f || !artTarget) return;
    const fr = new FileReader();
    fr.onload = () => {
      const im = new Image();
      im.onload = () => {
        // 缩到 256px 再存，控制 localStorage 占用
        const max = 256;
        const k = Math.min(1, max / Math.max(im.width, im.height));
        const cv = document.createElement('canvas');
        cv.width = Math.round(im.width * k);
        cv.height = Math.round(im.height * k);
        cv.getContext('2d').drawImage(im, 0, 0, cv.width, cv.height);
        let data = null;
        try { data = cv.toDataURL('image/png'); } catch (e) { data = fr.result; }
        artTarget.cfg.art = data;
        artTarget.img.src = data;
        save();
        artTarget.say('这只好看！就它了。', 3000);
        artTarget.body.classList.add('hop');
      };
      im.src = fr.result;
    };
    fr.readAsDataURL(f);
  });

  /* ── 多只 ── */
  function addPet() {
    if (db.pets.length >= 4) { instances[0] && instances[0].say('最多养四只啦，海面挤不下。', 2600); return; }
    const cfg = { id: 'p' + (++seq), art: null, x: null, y: null, aff: 0, lastPat: 0, lastFeed: 0 };
    // 错开位置
    cfg.x = null; cfg.y = null;
    db.pets.push(cfg);
    save();
    const p = createPet(cfg);
    const n = db.pets.length;
    p.cfg.x = null; p.cfg.y = null;
    p.place(window.innerWidth - SIZE - 26 - (n - 1) * 104, window.innerHeight - SIZE - 26);
    save();
    p.say('你好，我是新来的。', 3000);
  }

  function hideAll() {
    stage.style.display = 'none';
    try { localStorage.setItem(KEY + '.hidden', '1'); } catch (e) { }
  }

  /* ── 状态轮询 ── */
  let lastState = null;
  setInterval(() => {
    const on = botAlive();
    instances.forEach(p => p.render());
    if (lastState === null) { lastState = on; return; }
    if (on !== lastState) {
      lastState = on;
      instances.forEach(p => p.say(pick(on ? LINES.botOn : LINES.botOff), 3600));
    }
  }, 4000);

  // 随机自言自语
  setInterval(() => {
    if (document.hidden || stage.style.display === 'none') return;
    instances.forEach(p => {
      if (Math.random() < .35 && !p.bubble.classList.contains('on')) {
        p.say(pick(botAlive() ? LINES.idle : LINES.botOff), 3200);
      }
    });
  }, 47000);

  // 在线好感
  let lastJoin = Date.now();
  setInterval(() => {
    if (Date.now() - lastJoin > 6 * 60000) {
      lastJoin = Date.now();
      instances.forEach(p => { p.cfg.aff += 1; p.render(); });
      save();
    }
  }, 60000);

  /* ── 启动 ── */
  function mount() {
    const host = document.querySelector('.window') || document.body;
    host.appendChild(stage);
    host.appendChild(menu);
    host.appendChild(filePick);
    let hidden = false;
    try { hidden = localStorage.getItem(KEY + '.hidden') === '1'; } catch (e) { }
    if (hidden) stage.style.display = 'none';
    db.pets.forEach(createPet);
    syncNoDrag();
    setTimeout(syncNoDrag, 600);
    window.addEventListener('resize', () => instances.forEach(p => p.place(p.cfg.x, p.cfg.y)));
    setTimeout(() => {
      instances.forEach((p, i) => p.say(
        i === 0 ? (p.cfg.aff > 0 ? (LINES.lv[level(p.cfg.aff)] || '我在。') : '咕噜……我是溟月，负责看家。') : '我也在。',
        4400));
    }, 1200);
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', mount);
  else mount();

  window.whalePet = {
    show() { stage.style.display = ''; try { localStorage.setItem(KEY + '.hidden', '0'); } catch (e) { } },
    hide: hideAll,
    add: addPet,
    setArt(url) { instances.forEach(p => { p.cfg.art = url; p.img.src = url; }); save(); },
    say: (t) => instances[0] && instances[0].say(t || '咕噜'),
    state: () => ({ count: db.pets.length, pets: db.pets.map(p => ({ aff: p.aff, lv: level(p.aff), x: p.x, y: p.y })) })
  };
})();
