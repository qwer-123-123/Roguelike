/* =====================================================================
   启动与主循环
   固定步长累加器：逻辑固定 60Hz 推进，渲染按刷新率插值绘制。
   这是策划案「核心战斗必须确定性计算」的落地方式 —— 同一份输入序列
   在 60Hz 和 144Hz 机器上会得到相同结果。
   ===================================================================== */

const Main = {
  canvas: null, ctx: null,
  game: null,
  acc: 0,
  last: 0,
  w: 0, h: 0,

  boot(){
    this.canvas = document.getElementById('stage');
    this.ctx = this.canvas.getContext('2d');
    this.game = new Game();

    this.dbg = /[?&]dbg=1/.test(location.search);
    Input.init();
    this.resize();
    addEventListener('resize', () => this.resize());

    // ESC 暂停 / 继续
    addEventListener('keydown', e => {
      if (e.code !== 'Escape') return;
      const s = this.game.state;
      if (s === 'playing') this.pause();
      else if (s === 'paused') this.resume();
    });

    // 载入素材
    loadAll((done, total) => {
      const k = done / total;
      document.getElementById('load-fill').style.width = (k * 100) + '%';
      document.getElementById('load-text').textContent =
        `载入素材… ${done} / ${total}`;
    }).then(() => {
      document.getElementById('load-fill').style.width = '100%';
      document.getElementById('load-text').textContent = '就绪';
      setTimeout(() => {
        const l = document.getElementById('loading');
        l.classList.add('done');
        // 过渡结束后彻底移出布局 —— 否则某些环境（如无头浏览器）过渡不推进，
        // 会留下一层半透明遮罩把整个画面压暗
        setTimeout(() => { l.style.display = 'none'; }, 450);
      }, 220);
      UI.init();
      UI.show('menu');
      this.last = performance.now();
      requestAnimationFrame(t => this.frame(t));
    });
  },

  resize(){
    const dpr = Math.min(devicePixelRatio || 1, 2);
    this.w = innerWidth; this.h = innerHeight;
    this.canvas.width  = Math.floor(this.w * dpr);
    this.canvas.height = Math.floor(this.h * dpr);
    this.canvas.style.width  = this.w + 'px';
    this.canvas.style.height = this.h + 'px';
    this.ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    this.dpr = dpr;
  },

  startRun(cls){
    this.game.start(cls);
    UI.syncHud(this.game);
    UI.show('hud');
  },

  pause(){ this.game.state = 'paused'; UI.show('pause'); },
  resume(){ this.game.state = 'playing'; UI.show('hud'); },

  /** ?dbg=1 时的状态浮层 */
  drawDbg(g){
    if (!this._dbgEl){
      this._dbgEl = document.createElement('div');
      this._dbgEl.style.cssText =
        'position:fixed;left:8px;bottom:8px;z-index:150;background:rgba(0,0,0,.78);' +
        'color:#8fe08f;font:11.5px/1.45 Consolas,monospace;padding:7px 10px;' +
        'border:1px solid #2c4a2c;border-radius:3px;white-space:pre;pointer-events:none';
      document.body.appendChild(this._dbgEl);
    }
    let alive = 0;
    const near = [];
    for (const e of g.enemies.used){
      if (e.dead || e.state === 'death') continue;
      alive++;
      if (near.length < 5){
        const d = Math.hypot(e.x - g.player.x, e.y - g.player.y) * CFG.PX_PER_UNIT;
        near.push(`${d.toFixed(0)}px`);
      }
    }
    this._dbgEl.textContent =
      `state   ${g.state}\n` +
      `敌 used ${g.enemies.used.length}  alive ${alive}  free ${g.enemies.free.length}  cap ${g.wave ? g.wave.cap : '-'}\n` +
      `spawnT  ${g.spawnT !== undefined ? g.spawnT.toFixed(2) : '-'}\n` +
      `投射物  ${g.projectiles.used.length}\n` +
      `掉落    ${g.pickups.used.length}\n` +
      `wave    ${g.waveN}  diff ${g.wave ? g.wave.diff : '-'}  t ${g.waveT ? g.waveT.toFixed(1) : '-'}\n` +
      `玩家    x ${g.player ? g.player.x.toFixed(1) : '-'} y ${g.player ? g.player.y.toFixed(1) : '-'} hp ${g.player ? Math.ceil(g.player.hp) : '-'}\n` +
      `最近5只 ${near.join(' ')}\n` +
      `前3只 xy ${(() => { let s = []; for (const e of g.enemies.used){ if (e.dead || e.state==='death') continue; s.push(e.x.toFixed(2)+','+e.y.toFixed(2)); if (s.length>=3) break; } return s.join('  '); })()}\n` +
      `cam     ${Camera.x.toFixed(2)},${Camera.y.toFixed(2)}\n` +
      `视口    半宽 ${(innerWidth/2).toFixed(0)} 半高 ${(innerHeight/2).toFixed(0)}`;
  },

  frame(now){
    requestAnimationFrame(t => this.frame(t));
    let dt = (now - this.last) / 1000;
    this.last = now;
    if (dt > 0.25) dt = 0.25;           // 切标签页回来时不要一次性补几百帧

    const g = this.game;

    // ---- 逻辑：固定步长 ----
    const step = 1 / CFG.TICK_HZ;
    this.acc += dt;
    let guard = 0;
    while (this.acc >= step && guard++ < 8){
      this.acc -= step;
      if (g.state === 'playing'){
        g.update(step);
        if (g.state === 'over'){ UI.showOver(g); break; }
      }
    }
    if (guard >= 8) this.acc = 0;

    UI.tick(dt, g);
    if (this.dbg) this.drawDbg(g);

    // ---- 渲染 ----
    const ctx = this.ctx;
    ctx.setTransform(this.dpr, 0, 0, this.dpr, 0, 0);
    if (g.state === 'idle'){
      // 菜单背后也画一层世界（首帧尚未开局时给个底）
      ctx.fillStyle = '#08090c';
      ctx.fillRect(0, 0, this.w, this.h);
    } else {
      g.draw(ctx, this.w, this.h);
      if (this.dbg && g.state !== 'idle'){
        // 把每只「活着」的敌人位置打红圈 —— 用来对比「逻辑位置」与「被画出来的精灵」
        ctx.save();
        ctx.setTransform(this.dpr, 0, 0, this.dpr, 0, 0);
        Camera.apply(ctx, this.w, this.h);
        for (const e of g.enemies.used){
          if (e.dead || e.state === 'death') continue;
          ctx.strokeStyle = '#ff3b3b';
          ctx.lineWidth = px(2);
          ctx.beginPath(); ctx.arc(e.x, e.y, e.radius, 0, TAU); ctx.stroke();
          ctx.fillStyle = '#ff3b3b';
          ctx.fillRect(e.x - px(2), e.y - px(2), px(4), px(4));
        }
        ctx.restore();
      }
    }
  },
};

/* ---------- 错误浮层 ----------
   原型没有构建流程，脚本错误会静默死在控制台里，画面上只表现为「没反应」。
   这里把错误直接显示出来，便于自查与反馈。 */
function showFatal(msg){
  let box = document.getElementById('fatal');
  if (!box){
    box = document.createElement('div');
    box.id = 'fatal';
    box.style.cssText =
      'position:fixed;left:0;right:0;bottom:0;z-index:200;max-height:40%;overflow:auto;' +
      'background:rgba(60,12,12,.94);color:#ffb3b3;font:12px/1.5 Consolas,monospace;' +
      'padding:10px 14px;border-top:2px solid #a33;white-space:pre-wrap';
    document.body.appendChild(box);
  }
  box.textContent += msg + '\n';
}
addEventListener('error', e => showFatal(`✗ ${e.message}\n  ${e.filename}:${e.lineno}`));
addEventListener('unhandledrejection', e => showFatal('✗ Promise: ' + (e.reason && e.reason.message || e.reason)));

/* ---------- ?selftest=1 端到端自检 ----------
   走真实的按钮点击链路，而不是直接调 startRun()。
   结果写进 #selftest，配合 --dump-dom 读取。
   之前只用 ?auto= 验证过渲染与主循环，那条路径绕过了所有按钮，交互接线没测过。 */
function runSelfTest(){
  const log = [];
  let failed = 0;
  const step = (name, fn) => {
    try { fn(); log.push('OK   ' + name); }
    catch (e){ failed++; log.push('FAIL ' + name + ' :: ' + e.message); }
  };
  const $ = id => document.getElementById(id);
  const ticks = n => { for (let i = 0; i < n; i++){ if (Main.game.state !== 'playing') break; Main.game.update(1/60); } };

  step('主菜单可见',        () => { if (!$('scr-menu').classList.contains('on')) throw new Error('菜单未显示'); });
  step('点击「开始战斗」',  () => { $('btn-start').click(); });
  step('切到兵种选择界面',  () => { if (!$('scr-pick').classList.contains('on')) throw new Error('pick 界面未显示'); });
  step('兵种列表 10 项',    () => { const n = UI.el.pickList.children.length; if (n !== 10) throw new Error(`实际 ${n} 项`); });
  step('列表头像全部加载',  () => {
    const imgs = [...UI.el.pickList.querySelectorAll('img')];
    const bad = imgs.filter(i => !i.complete || i.naturalWidth === 0);
    if (bad.length) throw new Error(`${bad.length}/${imgs.length} 张头像加载失败: ${bad[0] && bad[0].src.split('/').pop()}`);
  });
  step('默认选中第一项',    () => { if (!UI.el.pickName.textContent.includes('匕首')) throw new Error('详情=' + UI.el.pickName.textContent); });
  step('切换到第 3 个(手枪)', () => { UI.el.pickList.children[2].click(); });
  step('详情随之刷新',      () => { if (!UI.el.pickName.textContent.includes('手枪')) throw new Error('详情=' + UI.el.pickName.textContent); });
  step('点击「开始战斗」',  () => { $('btn-pick-go').click(); });
  step('进入 playing',      () => { if (Main.game.state !== 'playing') throw new Error('state=' + Main.game.state); });
  step('HUD 已显示',        () => { if (!$('scr-hud').classList.contains('on')) throw new Error('HUD 未显示'); });
  step('兵器图标已加载',    () => { const i = $('hud-weapon'); if (!i.complete || i.naturalWidth === 0) throw new Error('武器图标未加载'); });

  step('跑 600 tick 后刷出敌人', () => {
    ticks(600);
    let alive = 0; for (const e of Main.game.enemies.used) if (!e.dead && e.state !== 'death') alive++;
    if (alive === 0) throw new Error('没有敌人');
  });
  step('WASD 键位能移动玩家', () => {
    Input.keys['KeyD'] = true;
    const x0 = Main.game.player.x;
    ticks(45);
    Input.keys['KeyD'] = false;
    if (Main.game.player.x <= x0 + 0.1) throw new Error(`x ${x0.toFixed(2)} -> ${Main.game.player.x.toFixed(2)}`);
  });
  step('掉落物已生成',      () => {
    if (Main.game.pickups.used.length === 0) throw new Error('没有掉落物');
  });
  step('走到经验球上能拾取', () => {
    // 静止时远程击杀掉落散在远处，捡不到是正常的；这里直接把玩家挪到球上验证拾取链路
    const orb = Main.game.pickups.used.find(p => p.type === 'exp');
    if (!orb) throw new Error('没有经验球');
    const e0 = Main.game.exp, lv0 = Main.game.level;
    Main.game.player.x = orb.x; Main.game.player.y = orb.y;
    orb.dead = false; orb.t = 0;
    ticks(6);
    if (Main.game.exp === e0 && Main.game.level === lv0) throw new Error('经验未增加');
  });

  step('升级面板能弹出',    () => {
    Main.game.pendingLevels = 0;              // 先归零，避免与上面拾取触发的升级叠加
    Main.game.collect({ type:'exp', data: expNeed(Main.game.level) });
    if (!$('scr-upgrade').classList.contains('on')) throw new Error('三选一面板未弹出');
    if (UI.upOptions.length !== 3) throw new Error(`选项 ${UI.upOptions.length} 个`);
    if (Main.game.pendingLevels !== 1) throw new Error(`待选级数 ${Main.game.pendingLevels}`);
  });
  step('三选一卡面图标加载', () => {
    const imgs = [...$('up-cards').querySelectorAll('img')];
    const bad = imgs.filter(i => !i.complete || i.naturalWidth === 0);
    if (bad.length) throw new Error(`${bad.length} 张图标加载失败`);
  });
  step('点击卡片能生效',    () => {
    const before = Main.game.takenUpgrades.length;
    $('up-cards').children[0].click();
    if (Main.game.takenUpgrades.length !== before + 1) throw new Error('强化未生效');
  });
  step('升级后回到战斗',    () => {
    // 连点直到待选级数清空（一次升多级会连弹多次，这是预期行为）
    let guard = 0;
    while (Main.game.state === 'levelup' && guard++ < 10) $('up-cards').children[0].click();
    if (Main.game.state !== 'playing') throw new Error('state=' + Main.game.state + ' 待选=' + Main.game.pendingLevels);
  });

  step('死亡能进入结算',    () => {
    Main.game.player.iframe = 0;
    Main.game.player.hurt(99999, Main.game, null);
    ticks(120);
    if (Main.game.state !== 'over') throw new Error('state=' + Main.game.state);
    UI.showOver(Main.game);
    if (!$('scr-over').classList.contains('on')) throw new Error('结算界面未显示');
    if ($('ov-kills').textContent === '0') throw new Error('结算数据未填充');
  });
  step('点击「重开」能重开', () => { $('btn-retry').click(); if (Main.game.state !== 'playing' || Main.game.kills !== 0) throw new Error('重开失败'); });
  step('点击「返回主菜单」', () => { Main.game.player.iframe = 0; Main.game.player.hurt(99999, Main.game, null); ticks(120); UI.showOver(Main.game); $('btn-menu').click(); if (!$('scr-menu').classList.contains('on')) throw new Error('未回主菜单'); });

  const el = document.createElement('pre');
  el.id = 'selftest';
  el.style.cssText = 'position:fixed;inset:0;z-index:999;background:#0b0d10;color:#9fe09f;' +
    'font:12px/1.6 Consolas,monospace;padding:16px;overflow:auto;white-space:pre-wrap';
  el.textContent = `自检：${log.length - failed}/${log.length} 通过\n\n` + log.join('\n') +
    `\n\n结果：${failed ? 'FAILED (' + failed + ')' : 'ALL PASS'}`;
  document.body.appendChild(el);
}

addEventListener('DOMContentLoaded', () => {
  Main.boot();
  if (/[?&]selftest=1/.test(location.search)){
    const t = setInterval(() => { if (UI.el.pickList){ clearInterval(t); setTimeout(runSelfTest, 400); } }, 60);
  }
  // ?screen=pick|help|upgrade|over|pause 直接打开某个界面（自动化核对用）
  const sm = /[?&]screen=(\w+)/.exec(location.search);
  if (sm){
    const want = sm[1];
    const t = setInterval(() => {
      if (!UI.el.pickList) return;
      clearInterval(t);
      if (want === 'over'){ Main.startRun(CLASSES[2]); Main.game.time = 224; Main.game.kills = 317;
        Main.game.waveN = 7; Main.game.level = 4; Main.game.money = 1284;
        Main.game.takenUpgrades = [UPGRADES[0], UPGRADES[0], UPGRADES[1], UPGRADES[2]];
        UI.showOver(Main.game); }
      else UI.show(want);
    }, 60);
  }
  // ?auto=<兵种下标>[&ff=<逻辑帧数>] 跳过菜单直接开局。
  // ff 会在首帧渲染前同步跑 N 个逻辑 tick —— 用于自动化截图核对
  // （无头浏览器 rAF 帧数很少，不这样做截到的永远是第 0 秒）。
  const m = /[?&]auto=(\d+)/.exec(location.search);
  if (m){
    const idx = Math.min(CLASSES.length - 1, +m[1]);
    const ff = (() => { const f = /[?&]ff=(\d+)/.exec(location.search); return f ? Math.min(20000, +f[1]) : 0; })();
    const wait = setInterval(() => {
      if (!UI.el.pickList) return;          // UI 尚未初始化
      clearInterval(wait);
      UI.selClass = CLASSES[idx];
      UI.paintClassDetail();
      setTimeout(() => {
        Main.startRun(CLASSES[idx]);
        // ?wave=N 直接跳到第 N 波（用于验证 BOSS 波等后期内容）
        const wm = /[?&]wave=(\d+)/.exec(location.search);
        if (wm){ Main.game.waveN = Math.min(40, +wm[1]) - 1; Main.game.nextWave(); }
        const step = 1 / CFG.TICK_HZ;
        for (let i = 0; i < ff; i++){
          // 快进时自动选第一张升级卡，否则一升级循环就停住，跑不到后期与死亡
          if (Main.game.state === 'levelup'){
            if (UI.upOptions[0]) Main.game.applyUpgrade(UI.upOptions[0]);
            UI.show('hud');
          }
          if (Main.game.state !== 'playing') break;
          Main.game.update(step);
        }
        UI.syncHud(Main.game);
        if (Main.game.state === 'over') UI.showOver(Main.game);
      }, 120);
    }, 60);
  }
});
