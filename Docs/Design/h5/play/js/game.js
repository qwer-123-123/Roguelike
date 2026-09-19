/* =====================================================================
   游戏世界：竞技场 / 波次 / 刷怪 / 战斗结算 / 掉落
   ===================================================================== */

/* 极简合成音 —— 素材包不含任何音频（策划案能力清单已列为缺口），
   这里用 WebAudio 现场合成几个提示音，避免原型完全静音。 */
const SFX = {
  ctx: null, gain: null, on: true,
  init(){
    try {
      const AC = window.AudioContext || window.webkitAudioContext;
      if (!AC) return;
      this.ctx = new AC();
      this.gain = this.ctx.createGain();
      this.gain.gain.value = 0.10;
      this.gain.connect(this.ctx.destination);
    } catch (e) { this.ctx = null; }
  },
  beep(freq, dur, type, vol){
    if (!this.on || !this.ctx) return;
    if (this.ctx.state === 'suspended') this.ctx.resume();
    const t = this.ctx.currentTime;
    const o = this.ctx.createOscillator(), g = this.ctx.createGain();
    o.type = type || 'square';
    o.frequency.setValueAtTime(freq, t);
    o.frequency.exponentialRampToValueAtTime(Math.max(40, freq * 0.55), t + dur);
    g.gain.setValueAtTime((vol || 1) * 0.5, t);
    g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    o.connect(g); g.connect(this.gain);
    o.start(t); o.stop(t + dur + 0.02);
  },
  shot(){ this.beep(760, 0.05, 'square', .35); },
  hit(){  this.beep(320, 0.04, 'triangle', .4); },
  kill(){ this.beep(180, 0.12, 'sawtooth', .4); },
  hurt(){ this.beep(120, 0.18, 'sawtooth', .7); },
  level(){ this.beep(520, 0.1, 'sine', .6); setTimeout(() => this.beep(780, 0.16, 'sine', .6), 90); },
  boss(){ this.beep(90, 0.5, 'sawtooth', .8); },
  over(){ [0,140,280].forEach((d,i) => setTimeout(() => this.beep(300 - i*70, 0.28, 'sawtooth', .6), d)); },
};

/* ---------- 竞技场尺寸（格） ---------- */
const ARENA = { w: 60, h: 60 };

/* ---------- 自动拼接（autotile）映射 ----------
   ground/water 每套 13 张：1 内部 + 4 边 + 4 角，正好覆盖区域边界的所有情况。
   键 = 缺席邻边的掩码（N=1 E=2 S=4 W=8），值 = 可用瓦片索引列表。

   这张表是**实测**出来的，不是猜的：用 _tools/edgeprofile.html 逐张读四条边的
   透明像素比例（>50% 视为该边敞开）。要重新测量就跑它 —— 注意它依赖
   canvas.getImageData，在 file:// 下会被判为污染画布，必须加
   --allow-file-access-from-files 才读得到。 */
const TILE_MASK = {
  ground: {
    0:  [3, 7, 10, 11, 12],   // 内部
    1:  [1],  2: [9],  4: [6],  8: [4],     // 缺一边
    3:  [2],  6: [8],  12: [5], 9: [0],     // 缺两相邻边（角）
  },
  water: {
    0:  [5, 9, 10, 11, 12],
    1:  [1],  2: [7],  4: [6],  8: [3],
    3:  [2],  6: [8],  12: [4], 9: [0],
  },
};

class Game {
  constructor(){
    this.player = null;
    this.rng = null;

    this.enemies     = new Pool(() => new Enemy(), e => { e.dead = true; }, 80);
    this.projectiles = new Pool(() => new Projectile(), p => { p.dead = true; }, 120);
    this.pickups     = new Pool(() => new Pickup(), p => { p.dead = true; }, 150);
    this.effects     = new Pool(() => new Effect(), e => { e.dead = true; }, 60);
    this.floats      = new Pool(() => new FloatText(0,0,''), f => { f.dead = true; }, 40);

    this.state = 'idle';        // idle | playing | paused | levelup | over
    this.floorDecals = [];
    this._floorPat = null;
  }

  /* ================= 地面 =================
     素材包给了 4 套地形瓦片（各 13 张 89×89）。用法：
       grass_tiles_0000  完全不透明且无缝 -> 整块基底平铺
       ground/water      多数带透明边缘 -> 成片铺在草地上
       asphalt           不透明的马路拼块 -> 未使用

     两个踩过的坑（改这里前先看）：
     1. **不能把瓦片当独立贴花**：随机旋转 + 放大到 2~5 格，圆滑边被拉长成近似
        直线，整片地面看起来就是一堆贴歪的方块。
     2. **也不能只按格子成片铺**：这些是自动拼接的边角件，区域外边界正好落在
        瓦片的直边上，会露出明显的直角。
     正确做法是按 TILE_MASK **按掩码选瓦片**（见下），让边界由瓦片自带的
     透明边处理。整块地面烘焙到离屏画布，每帧只 blit 一次。
     */
  /** 生成地面布局（固定种子，每局一致）。
      返回「区域」列表而不是逐格瓦片 —— 烘焙时才展开成瓦片。 */
  buildFloorPlan(){
    const rng = makeRng(0x5EEDF00D);
    const hw = ARENA.w/2, hh = ARENA.h/2;
    const plan = [];
    for (let i = 0; i < 8; i++){
      plan.push({ set:'water', cx: rng.range(-hw+6, hw-6), cy: rng.range(-hh+6, hh-6),
                  r: rng.range(2.5, 5.0), a: rng.range(0.80, 0.95), seed: rng.int(0,9999) });
    }
    for (let i = 0; i < 24; i++){
      plan.push({ set:'ground', cx: rng.range(-hw+4, hw-4), cy: rng.range(-hh+4, hh-4),
                  r: rng.range(2.0, 6.5), a: rng.range(0.45, 0.75), seed: rng.int(0,9999) });
    }
    return plan;
  }

  /** 把整块地面烘焙到一张离屏画布，之后每帧只 blit 一次。
      为什么这么做：这些地形瓦片是**自动拼接的边角件**，直接铺一片区域，
      区域外边界就是瓦片的直边，会很显眼。这里改为每个区域先画进临时画布，
      再用径向渐变 destination-in 把边缘擦虚 —— 得到自然的不规则轮廓。
      顺带把每帧 300+ 次贴图绘制变成 1 次 drawImage。 */
  bakeFloor(){
    const __t0 = (performance || Date).now();
    const P = CFG.PX_PER_UNIT;
    const hw = ARENA.w/2, hh = ARENA.h/2;
    const cw = Math.ceil(ARENA.w * P), ch = Math.ceil(ARENA.h * P);
    const c = document.createElement('canvas');
    c.width = cw; c.height = ch;
    const g = c.getContext('2d');

    // ---- 1. 草地基底：铺满整张画布 ----
    const grass = S('t:grass:0');
    let ok = false;
    if (grass){
      const pat = g.createPattern(grass.img, 'repeat');
      if (pat){
        const k = P / grass.fw;               // 一张瓦片铺 1 个世界单位
        if (pat.setTransform) pat.setTransform(new DOMMatrix([k, 0, 0, k, 0, 0]));
        g.fillStyle = pat;
        g.fillRect(0, 0, cw, ch);
        ok = true;
      }
    }
    if (!ok){ g.fillStyle = '#232c19'; g.fillRect(0, 0, cw, ch); }   // 兜底
    this._floorBaked = ok;

    // ---- 2. 逐个区域铺瓦片 ----
    // 边界的处理：**按到区域边缘的距离逐格算透明度**，让最外一圈渐隐。
    // 试过两条弯路，都记在这里免得重走：
    //   a) 整片用统一透明度 —— 区域外边界就是瓦片的直边，一眼看出是方块
    //   b) 画进临时画布再用径向渐变 destination-in 擦边 —— 渐变半径稍不留神
    //      就落在瓦片铺到的范围之外，等于没擦；而且多一层离屏画布，更慢
    // 逐格渐隐只影响最外一圈，内部仍是统一透明度，不会暴露格子边界。
    const STEP = 3.6;                          // 瓦片在世界里的尺寸（比原图略放大，少些重复感）
    const plan = this.buildFloorPlan();
    this._planCount = plan.length;

    for (const p of plan){
      const map = TILE_MASK[p.set] || TILE_MASK.ground;
      // 每格一个**稳定**的随机值（按坐标哈希，不能依赖遍历顺序 —— 判断邻格时要重算）
      const cellRand = (gx, gy) => {
        let h = (p.seed ^ Math.imul(gx, 374761393) ^ Math.imul(gy, 668265263)) | 0;
        h = Math.imul(h ^ (h >>> 13), 1274126177);
        return ((h ^ (h >>> 16)) >>> 0) / 4294967296;
      };
      const inside = (gx, gy) =>
        Math.hypot(gx * STEP, gy * STEP) <= p.r * (0.55 + cellRand(gx, gy) * 0.85);

      const n = Math.ceil(p.r * 1.6 / STEP) + 1;
      for (let gy = -n; gy <= n; gy++){
        for (let gx = -n; gx <= n; gx++){
          if (!inside(gx, gy)) continue;
          // 掩码 = 缺席的邻边（N=1 E=2 S=4 W=8）
          let mask = 0;
          if (!inside(gx, gy - 1)) mask |= 1;
          if (!inside(gx + 1, gy)) mask |= 2;
          if (!inside(gx, gy + 1)) mask |= 4;
          if (!inside(gx - 1, gy)) mask |= 8;
          const list = map[mask] || map[0];          // 罕见的鞍点情况退回内部瓦片
          const idx = list[Math.floor(cellRand(gx, gy) * list.length)];
          const sp = S(`t:${p.set}:${idx}`);
          if (!sp) continue;
          g.globalAlpha = p.a;
          const dw = STEP * P, dh = STEP * P;
          g.drawImage(sp.img, 0, 0, sp.fw, sp.fh,
            (p.cx + gx * STEP + hw) * P - dw/2, (p.cy + gy * STEP + hh) * P - dh/2, dw, dh);
        }
      }
      g.globalAlpha = 1;
    }

    this.floorCanvas = c;
    this.bakeMs = Math.round((performance || Date).now() - __t0);
    console.log('[floor] baked in ' + this.bakeMs + 'ms, patches=' + this._planCount);
  }

  /* ================= 生命周期 ================= */
  start(cls){
    this.rng = makeRng((Date.now() ^ 0x9e3779b9) >>> 0);
    this.player = new Player(cls);
    this.enemies.clear(); this.projectiles.clear();
    this.pickups.clear(); this.effects.clear(); this.floats.clear();

    this.time = 0;
    this.kills = 0;
    this.money = 0;
    this.level = 1;
    this.exp = 0;
    this.pendingLevels = 0;
    this.takenUpgrades = [];

    this.waveN = 0;
    this.waveT = 0;
    this.spawnT = 0;
    this.bossAlive = null;
    this.state = 'playing';
    Camera.x = 0; Camera.y = 0; Camera.shake = 0;
    if (!this.floorCanvas) this.bakeFloor();   // 地面只烘焙一次，重开复用
    this.nextWave();
  }

  nextWave(){
    this.waveN++;
    this.waveT = 0;
    this.spawnT = 0.6;
    this.wave = waveFor(this.waveN);
    if (this.wave.boss){
      this.spawnBoss(this.wave.boss);
      SFX.boss();
      UI.waveBanner(`WAVE ${this.waveN} — ${ENEMIES[this.wave.boss].name}`, true);
    } else {
      UI.waveBanner(`WAVE ${this.waveN}`, false);
    }
  }

  /* ================= 刷怪 ================= */
  /** 沿视野矩形的四条边、在外侧一点生成。
      早先用「对角线半径」画圆 —— 上下方向刷出的怪离玩家 865px，
      以 72px/s 的速度要走 8 秒才进画面，实测开局 15 秒场上空荡荡。
      改成贴边生成后敌人几乎立刻进场。 */
  spawnRing(){
    const p = this.player;
    const hw = innerWidth / 2 / CFG.PX_PER_UNIT;     // 视野半宽（格）
    const hh = innerHeight / 2 / CFG.PX_PER_UNIT;
    const pad = 1.4;
    const side = Math.floor(this.rng() * 4);
    let x, y;
    if (side === 0)      { x = p.x + (this.rng()*2-1)*(hw+pad); y = p.y - hh - pad; }
    else if (side === 1) { x = p.x + (this.rng()*2-1)*(hw+pad); y = p.y + hh + pad; }
    else if (side === 2) { x = p.x - hw - pad; y = p.y + (this.rng()*2-1)*(hh+pad); }
    else                 { x = p.x + hw + pad; y = p.y + (this.rng()*2-1)*(hh+pad); }
    return {
      x: clamp(x, -ARENA.w/2 + 2, ARENA.w/2 - 2),
      y: clamp(y, -ARENA.h/2 + 2, ARENA.h/2 - 2),
    };
  }

  spawnEnemy(id){
    const def = ENEMIES[id];
    const { x, y } = this.spawnRing();
    const e = this.enemies.spawn().init(def, x, y, this.wave.diff);
    return e;
  }

  spawnBoss(id){
    const def = ENEMIES[id];
    const p = this.player;
    const a = this.rng() * TAU;
    const r = Math.hypot(innerWidth, innerHeight) / 2 / CFG.PX_PER_UNIT + 3.5;
    const e = this.enemies.spawn().init(def,
      clamp(p.x + Math.cos(a)*r, -ARENA.w/2+3, ARENA.w/2-3),
      clamp(p.y + Math.sin(a)*r, -ARENA.h/2+3, ARENA.h/2-3),
      this.wave.diff);
    this.bossAlive = e;
    return e;
  }

  updateSpawn(dt){
    if (this.bossAlive && !this.bossAlive.dead) return;   // BOSS 在场时不再刷杂兵
    this.spawnT -= dt;
    if (this.spawnT > 0) return;
    // 难度越高刷得越密；同时上限来自波次表
    const base = 1.35 / (0.75 + this.wave.diff * 0.35);
    this.spawnT = base * (0.7 + this.rng() * 0.6);
    if (this.enemies.count >= this.wave.cap) return;
    this.spawnEnemy(this.rng.pick(this.wave.pool));
    // 高难度时成组刷
    if (this.wave.diff >= 2.2 && this.enemies.count < this.wave.cap && this.rng.chance(0.45))
      this.spawnEnemy(this.rng.pick(this.wave.pool));
  }

  /* ================= 主更新（固定步长调用） ================= */
  update(dt){
    if (this.state !== 'playing') return;
    this.time += dt;
    this.waveT += dt;

    this.player.update(dt, this);
    if (this.player.state === 'atk' && this.player.pendingArc){
      this.player.pendingArc.at -= dt;
      if (this.player.pendingArc.at <= 0){ this.player.pendingArc = null; this.player.resolveArc(this); }
    }

    this.updateSpawn(dt);

    for (const e of this.enemies.used) e.update(dt, this);
    for (const p of this.projectiles.used) p.update(dt, this);
    for (const p of this.pickups.used) p.update(dt, this);
    for (const f of this.effects.used) f.update(dt);
    for (const f of this.floats.used) f.update(dt);

    this.enemies.sweep(); this.projectiles.sweep();
    this.pickups.sweep(); this.effects.sweep(); this.floats.sweep();

    if (this.bossAlive && this.bossAlive.dead) this.bossAlive = null;

    // 波次推进
    if (this.waveT >= this.wave.dur && !(this.bossAlive)) this.nextWave();

    Camera.follow(this.player.x, this.player.y, dt);

    if (this.player.state === 'death' && this.player.animT > 0.9) this.state = 'over';
  }

  /* ================= 战斗 ================= */
  nearestEnemy(x, y, maxR){
    let best = null, bd = maxR * maxR;
    for (const e of this.enemies.used){
      if (e.dead || e.state === 'death') continue;
      const d = dist2(x, y, e.x, e.y);
      if (d < bd){ bd = d; best = e; }
    }
    return best;
  }

  spawnProjectile(owner, ang, W){
    const p = this.player;
    this.projectiles.spawn().init(owner, ang, W, p.dmg, p.projSpeedMul, p.pierce, p.burn, p.crit);
    SFX.shot();
  }

  coneAttack(owner, W){
    const p = this.player;
    const half = (W.cone * Math.PI / 180) / 2;
    let hits = 0;
    for (const e of this.enemies.used){
      if (e.dead || e.state === 'death') continue;
      const d = dist(p.x, p.y, e.x, e.y);
      if (d > p.range + e.radius) continue;
      const a = Math.atan2(e.y - p.y, e.x - p.x);
      if (angDiff(a, p.aim) > half) continue;
      this.damageEnemy(e, p.dmg, p, true);
      e.burnT = 2.0; e.burn = W.burnDps; e.burnSrc = p;
      hits++;
    }
    // 火焰粒子
    for (let i = 0; i < 3; i++){
      const a = p.aim + (this.rng() - 0.5) * (W.cone * Math.PI/180);
      const rr = p.range * (0.35 + this.rng() * 0.65);
      this.effects.spawn().init(
        p.x + Math.cos(a) * rr, p.y + Math.sin(a) * rr, 'flame',
        { r: px(12 + this.rng()*12), color: this.rng.chance(.5) ? '#ffb03a' : '#ff6a2a' });
    }
    if (p.ammo <= 0 && this.rng.chance(.5)) return;
  }

  damageEnemy(e, dmg, src, isDot, critChance){
    if (e.dead || e.state === 'death') return;
    let d = dmg, crit = false;
    if (!isDot && critChance > 0 && this.rng.chance(critChance)){ d *= 2; crit = true; }
    e.hp -= d;
    e.flash = 0.12;
    if (!isDot) this.addFloat(e.x, e.y - e.def.spriteH*0.35, String(Math.round(d)),
                              crit ? '#ffd24a' : '#ffffff', crit ? 17 : 13);
    if (e.hp <= 0) this.killEnemy(e, src);
  }

  killEnemy(e, src){
    if (e.state === 'death') return;
    e.gotoDeath();
    e.burnT = 0;
    this.kills++;

    const p = this.player;
    if (p.vamp) p.hp = Math.min(p.maxHp, p.hp + p.vamp);

    // 经验（必掉）
    this.pickups.spawn().init(e.x, e.y, 'exp', Math.round(e.def.exp * p.expMul));
    // 金币
    if (e.def.money) this.money += e.def.money;

    // 道具掉落
    const roll = this.rng() * 100;
    let acc = 0;
    for (const d of DROPS){
      acc += d.w;
      if (roll < acc){
        // 弹药类只对对应武器有意义
        if (d.id === 'mag' && !(p.cls.weapon === 'riffle')) break;
        if (d.id === 'fuel' && !(p.cls.weapon === 'flame')) break;
        this.pickups.spawn().init(e.x, e.y, 'item', d);
        break;
      }
    }

    if (e.def.boss){
      this.effects.spawn().init(e.x, e.y, 'explosion', { h: 5.5 });
      Camera.kick(10);
      this.pickups.spawn().init(e.x + 1.2, e.y, 'item', { id:'health', icon:'items_0005_health', label:'医疗包' });
      this.pickups.spawn().init(e.x - 1.2, e.y, 'item', { id:'money',  icon:'items_0013_money',  label:'金币' });
      this.money += 200;
    }
    SFX.kill();
  }

  collect(pk){
    const p = this.player;
    if (pk.type === 'exp'){
      this.exp += pk.data;
      let need = expNeed(this.level);
      while (this.exp >= need){
        this.exp -= need;
        this.level++;
        this.pendingLevels++;
        need = expNeed(this.level);
      }
      if (this.pendingLevels > 0 && this.state === 'playing'){
        this.state = 'levelup';
        SFX.level();
        UI.openUpgrade(this);
      }
    } else {
      const d = pk.data;
      switch (d.id){
        case 'health': p.hp = Math.min(p.maxHp, p.hp + 25); this.addFloat(p.x, p.y-1, '+25', '#7fdd6a', 15); break;
        case 'armor':  p.buffs.armor = 15; break;
        case 'speed':  p.buffs.speed = 12; break;
        case 'power':  p.buffs.power = 10; break;
        case 'slow':   for (const e of this.enemies.used) e.slowT = Math.max(e.slowT, 6); break;
        case 'money':  this.money += 20; break;
        case 'mag':    if (p.maxAmmo) p.ammo = Math.min(p.maxAmmo, p.ammo + 60); break;
        case 'fuel':   if (p.maxAmmo) p.ammo = Math.min(p.maxAmmo, p.ammo + 40); break;
      }
      if (d.id !== 'health') this.addFloat(p.x, p.y-1, d.label.split(' ')[0], '#c8e05a', 12);
    }
  }

  applyUpgrade(u){
    u.apply(this.player);
    this.takenUpgrades.push(u);
    this.pendingLevels--;
    if (this.pendingLevels <= 0) this.state = 'playing';
  }

  /* ================= 辅助 ================= */
  clampToArena(o){
    const m = o.radius + 0.4;
    o.x = clamp(o.x, -ARENA.w/2 + m, ARENA.w/2 - m);
    o.y = clamp(o.y, -ARENA.h/2 + m, ARENA.h/2 - m);
  }
  addFloat(x, y, text, color, size){ this.floats.spawn().reset(x, y, text, color, size); }
  addHitFx(x, y){ this.effects.spawn().init(x, y, 'hit'); }
  sfxHit(){ SFX.hit(); }

  /* ================= 绘制 ================= */
  draw(ctx, w, h){
    // 地面
    ctx.save();
    ctx.fillStyle = '#0a0c0f';
    ctx.fillRect(0, 0, w, h);

    Camera.apply(ctx, w, h);

    const hw = ARENA.w/2, hh = ARENA.h/2;

    // ---- 地面：烘焙好的整块地面，按视野裁一小块贴上来 ----
    const P = CFG.PX_PER_UNIT;
    if (this.floorCanvas){
      const vw = w / P, vh = h / P;                 // 视野尺寸（世界单位）
      const wx0 = Camera.x - vw/2, wy0 = Camera.y - vh/2;
      ctx.drawImage(this.floorCanvas,
        (wx0 + hw) * P, (wy0 + hh) * P, vw * P, vh * P,   // 源：地面画布上的像素区
        wx0, wy0, vw, vh);                                 // 目标：世界坐标
    } else {
      ctx.fillStyle = '#232c19';                    // 素材缺失兜底
      ctx.fillRect(-hw, -hh, ARENA.w, ARENA.h);
    }

    // ---- 网格 + 边界 ----
    // 地面烘焙成功时不再画网格（草地已经很花，网格只会像调试叠层）；
    // 素材缺失时保留网格，否则画面是一片空黑，看不出坐标感。
    ctx.save();
    if (!this.floorCanvas || !this._floorBaked){
      ctx.strokeStyle = 'rgba(90,105,125,.10)';
      ctx.lineWidth = px(1);
      for (let gx = -hw; gx <= hw; gx += 4){
        ctx.beginPath(); ctx.moveTo(gx, -hh); ctx.lineTo(gx, hh); ctx.stroke();
      }
      for (let gy = -hh; gy <= hh; gy += 4){
        ctx.beginPath(); ctx.moveTo(-hw, gy); ctx.lineTo(hw, gy); ctx.stroke();
      }
    }
    // 边界警示（保留 —— 让玩家看得出场地范围）
    ctx.strokeStyle = 'rgba(224,96,58,.60)';
    ctx.lineWidth = px(3);
    ctx.setLineDash([px(14), px(10)]);
    ctx.strokeRect(-hw, -hh, ARENA.w, ARENA.h);
    ctx.restore();

    // 拾取物 → 特效 → 敌人 → 投射物 → 玩家（按视觉层级）
    for (const p of this.pickups.used) p.draw(ctx);
    for (const e of this.effects.used) e.draw(ctx);
    for (const e of this.enemies.used) e.draw(ctx);
    for (const p of this.projectiles.used) p.draw(ctx);
    if (this.player) this.player.draw(ctx);

    ctx.restore();

    // 飘字在**屏幕空间**绘制（字号用像素，不受相机缩放影响）
    for (const f of this.floats.used){
      const s = Camera.toScreen(f.x, f.y, w, h);
      f.drawAt(ctx, s.x, s.y);
    }

    // 屏幕外敌人指示（BOSS 用）
    if (this.bossAlive && !this.bossAlive.dead){
      const s = Camera.toScreen(this.bossAlive.x, this.bossAlive.y, w, h);
      const m = 46;
      if (s.x < m || s.x > w-m || s.y < m || s.y > h-m){
        const cx = w/2, cy = h/2;
        const a = Math.atan2(s.y - cy, s.x - cx);
        const rx = Math.min(w/2 - m, Math.abs(Math.cos(a)) > 1e-3 ? Math.abs((w/2 - m) / Math.cos(a)) : 1e9);
        const ry = Math.min(h/2 - m, Math.abs(Math.sin(a)) > 1e-3 ? Math.abs((h/2 - m) / Math.sin(a)) : 1e9);
        const rr = Math.min(rx, ry);
        const ix = cx + Math.cos(a) * rr, iy = cy + Math.sin(a) * rr;
        ctx.save();
        ctx.translate(ix, iy); ctx.rotate(a);
        ctx.fillStyle = '#e0603a';
        ctx.beginPath(); ctx.moveTo(12,0); ctx.lineTo(-8,-8); ctx.lineTo(-8,8); ctx.closePath(); ctx.fill();
        ctx.restore();
      }
    }
  }
}
