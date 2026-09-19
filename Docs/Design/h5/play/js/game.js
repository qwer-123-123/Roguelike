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

    // 竞技场网格 + 边界（相机已 scale，这里一律用世界单位）
    ctx.save();
    ctx.strokeStyle = 'rgba(90,105,125,.10)';
    ctx.lineWidth = px(1);
    const hw = ARENA.w/2, hh = ARENA.h/2;
    for (let gx = -hw; gx <= hw; gx += 4){
      ctx.beginPath(); ctx.moveTo(gx, -hh); ctx.lineTo(gx, hh); ctx.stroke();
    }
    for (let gy = -hh; gy <= hh; gy += 4){
      ctx.beginPath(); ctx.moveTo(-hw, gy); ctx.lineTo(hw, gy); ctx.stroke();
    }
    // 边界警示
    ctx.strokeStyle = 'rgba(224,96,58,.55)';
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
