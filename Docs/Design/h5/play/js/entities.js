/* =====================================================================
   实体：玩家 / 敌人 / 投射物 / 掉落物 / 特效
   ===================================================================== */

/* 俯视角精灵的朝向修正。
   实测（见 Docs/Design/_tools/out/rot_test.png）：精灵在 rot=0 时「正面」朝画面
   下方，即单位向量 +Y，对应角度 +π/2。要让正面转到瞄准角 aim，需满足
        π/2 + rot = aim   →   rot = aim − π/2
   所以这里的偏移是 −π/2（早先写成 +π/2，结果整体差了 180°）。 */
const SPRITE_ROT_OFFSET = -Math.PI / 2;

/* =====================================================================
   玩家
   ===================================================================== */
class Player {
  constructor(cls){
    this.cls = cls;
    this.reset();
  }
  reset(){
    const c = this.cls, W = WEAPONS[c.weapon];
    this.x = 0; this.y = 0;
    this.vx = 0; this.vy = 0;
    this.radius = CFG.PLAYER_RADIUS;

    this.maxHp = c.hp; this.hp = c.hp;
    this.baseSpeed = c.speed; this.speed = c.speed;
    this.baseDmg = c.dmg;     this.dmgMul = 1;
    this.baseRate = c.rate;   this.rateMul = 1;
    this.rangeMul = 1; this.projSpeedMul = 1;

    this.armor = 0; this.pierce = 0; this.shots = W.shots || 1;
    this.spread = W.spread || 0; this.burn = 0; this.crit = 0;
    this.vamp = 0; this.thorn = 0; this.expMul = 1;

    // 弹药（仅步枪/火焰喷射器消耗）
    this.maxAmmo = W.ammo || 0; this.ammo = W.ammo || 0; this.ammoRegen = W.ammoRegen || 0;

    this.aim = -Math.PI/2;
    this.state = 'idle';         // idle | walk | atk | death
    this.animT = 0; this.frame = 0;
    this.cd = 0;                 // 攻击冷却
    this.burstLeft = 0; this.burstT = 0;
    this.hitDone = false;
    this.iframe = 0;             // 受击无敌帧
    this.dead = false;

    // 限时增益
    this.buffs = { armor:0, speed:0, power:0 };
  }

  get W(){ return WEAPONS[this.cls.weapon]; }
  get dmg(){ return this.baseDmg * this.dmgMul * (this.buffs.power > 0 ? 2 : 1); }
  get rate(){ return this.baseRate * this.rateMul; }
  get range(){ return this.W.range * this.rangeMul; }
  get atkInterval(){ return 1 / this.rate; }

  /** 当前应播放的精灵 key */
  spriteKey(){
    const b = this.cls.body, w = this.cls.weapon;
    if (this.state === 'death') return `u:death:${b}`;
    if (this.state === 'atk')   return `u:atk:${b}:${w}`;
    if (this.state === 'walk')  return `u:walk:${b}:${w}`;
    return `u:idle:${b}:${w}`;
  }

  update(dt, game){
    // 增益计时
    for (const k of ['armor','speed','power']) if (this.buffs[k] > 0) this.buffs[k] = Math.max(0, this.buffs[k] - dt);
    if (this.iframe > 0) this.iframe -= dt;

    if (this.state === 'death'){ this.animT += dt; this.updateFrame(0.14); return; }

    // 弹药缓慢回复
    if (this.ammoRegen && this.ammo < this.maxAmmo)
      this.ammo = Math.min(this.maxAmmo, this.ammo + this.ammoRegen * dt);

    // 移动
    const mv = Input.moveVec();
    const sp = this.speed * (this.buffs.speed > 0 ? 1.3 : 1);
    this.vx = mv.x * sp; this.vy = mv.y * sp;
    this.x += this.vx * dt; this.y += this.vy * dt;
    game.clampToArena(this);

    const moving = mv.x !== 0 || mv.y !== 0;
    if (this.state !== 'atk') this.state = moving ? 'walk' : 'idle';

    // 瞄准最近敌人；没有敌人时朝移动方向
    const t = game.nearestEnemy(this.x, this.y, this.range + 1.5);
    if (t) this.aim = Math.atan2(t.y - this.y, t.x - this.x);
    else if (moving) this.aim = Math.atan2(mv.y, mv.x);

    // 攻击
    if (this.cd > 0) this.cd -= dt;
    this.updateBurst(dt, game);
    if (this.cd <= 0 && !this.burstLeft) this.tryAttack(game);

    // 动画
    if (this.state === 'atk'){
      this.animT += dt;
      const W = this.W, dur = W.atkFrames / 18;
      const f = Math.floor(this.animT / dur * W.atkFrames);
      this.frame = f;
      if (this.animT >= dur){ this.animT = 0; this.state = moving ? 'walk' : 'idle'; this.hitDone = false; }
    } else {
      this.updateFrame(this.state === 'walk' ? 0.062 : 0.105);
    }
  }

  updateFrame(perFrame){
    this.animT += 1/60;
    const sp = S(this.spriteKey());
    const n = sp ? sp.frames : 1;
    this.frame = Math.floor(this.animT / perFrame) % n;
  }

  canFire(game){
    const W = this.W;
    if (W.ammo && this.ammo <= 0) return false;
    if (W.model === 'arc' || W.model === 'cone') {
      // 近战/火焰必须范围内有目标才出手
      return !!game.nearestEnemy(this.x, this.y, this.range);
    }
    return !!game.nearestEnemy(this.x, this.y, this.range);
  }

  tryAttack(game){
    const W = this.W;
    if (!this.canFire(game)) return;

    if (W.model === 'cone'){
      // 火焰喷射：持续 tick，不进入 atk 动画循环（保持 Walk/Idle 更顺）
      this.cd = 1 / this.rate;
      if (W.ammo) this.ammo = Math.max(0, this.ammo - 1);
      game.coneAttack(this, W);
      return;
    }

    this.cd = this.atkInterval;
    this.state = 'atk'; this.animT = 0; this.frame = 0; this.hitDone = false;
    if (W.ammo) this.ammo = Math.max(0, this.ammo - 1);

    if (W.model === 'arc'){
      // 近战：等动画到打击帧再结算
      this.pendingArc = { at: W.hitAt };
    } else if (W.model === 'proj'){
      if (W.burst && W.burst > 1){ this.burstLeft = W.burst - 1; this.burstT = W.burstGap; this.fireOne(game); }
      else this.fireOne(game);
    }
  }

  updateBurst(dt, game){
    if (!this.burstLeft) return;
    this.burstT -= dt;
    if (this.burstT <= 0){ this.fireOne(game); this.burstLeft--; this.burstT = this.W.burstGap; }
  }

  fireOne(game){
    const W = this.W;
    const n = this.shots;
    for (let i = 0; i < n; i++){
      const off = n === 1 ? 0 : (i - (n-1)/2) * (this.spread || 0.12);
      game.spawnProjectile(this, this.aim + off, W);
    }
  }

  /** 供 game 在打击帧调用 */
  resolveArc(game){
    const W = this.W;
    const hit = [];
    for (const e of game.enemies.used){
      if (e.dead || e.state === 'death') continue;
      const d = dist(this.x, this.y, e.x, e.y);
      if (d > this.range + e.radius) continue;
      const a = Math.atan2(e.y - this.y, e.x - this.x);
      if (angDiff(a, this.aim) > (W.arc * Math.PI/180) / 2) continue;
      hit.push(e);
    }
    for (const e of hit){
      game.damageEnemy(e, this.dmg, this);
      if (W.knock){
        const a = Math.atan2(e.y - this.y, e.x - this.x);
        e.kx += Math.cos(a) * W.knock; e.ky += Math.sin(a) * W.knock;
      }
    }
    if (hit.length){ Camera.kick(2.2); game.sfxHit(); }
    return hit.length;
  }

  hurt(amount, game, source){
    if (this.iframe > 0 || this.state === 'death') return;
    const red = 1 - Math.min(0.75, this.armor + (this.buffs.armor > 0 ? 0.15 : 0));
    const real = Math.max(1, amount * red);
    this.hp -= real;
    this.iframe = 0.35;
    Camera.kick(4);
    game.addFloat(this.x, this.y - 0.8, '-' + Math.round(real), '#ff7b6b', 15);
    if (this.thorn > 0 && source && !source.dead){
      game.damageEnemy(source, real * this.thorn, this, true);
    }
    if (this.hp <= 0){
      this.hp = 0; this.state = 'death'; this.animT = 0; this.frame = 0;
      this.dead = true;
    }
  }

  draw(ctx){
    const flip = Math.cos(this.aim) < 0;
    const rot = this.aim + SPRITE_ROT_OFFSET;
    // 受击闪白
    const alpha = this.state === 'death' ? 1 : (this.iframe > 0 ? 0.55 : 1);
    const hUnits = 1.75 * (this.cls.body === 'man' ? 1.0 : 0.93);

    // 脚下阴影
    ctx.save();
    ctx.globalAlpha = 0.34;
    ctx.fillStyle = '#000';
    ctx.beginPath();
    ctx.ellipse(this.x, this.y + px(5), px(15), px(8), 0, 0, TAU);
    ctx.fill();
    ctx.restore();

    drawSprite(ctx, this.spriteKey(), this.x, this.y, hUnits, REF_H.unit[this.cls.body], rot, this.frame, alpha, flip);
  }
}

/* =====================================================================
   敌人
   ===================================================================== */
class Enemy {
  constructor(){ this.dead = true; }
  init(def, x, y, diff){
    this.def = def;
    this.x = x; this.y = y;
    this.kx = 0; this.ky = 0;
    this.radius = def.radius;
    this.maxHp = Math.round(def.hp * (1 + (diff - 1) * 0.35));
    this.hp = this.maxHp;
    this.spd = def.spd;
    this.dmg = def.dmg * (1 + (diff - 1) * 0.18);
    this.state = 'walk';
    this.animT = 0; this.frame = 0; this.rot = 0;
    this.atkCd = def.atkCd * (0.6 + Math.random() * 0.6);
    this.atkAnim = 0; this.hitDone = false;
    this.burn = 0; this.burnT = 0; this.burnSrc = null;
    this.slowT = 0;
    this.dead = false;
    this.flash = 0;
    return this;
  }

  gotoDeath(){
    this.state = 'death'; this.animT = 0; this.frame = 0;
  }

  update(dt, game){
    if (this.state === 'death'){
      this.animT += dt;
      const sp = S(`e:death:${this.def.sprite}`);
      const n = sp ? sp.frames : 1;
      const per = 0.1;
      this.frame = Math.floor(this.animT / per);
      if (this.frame >= n) this.dead = true;
      return;
    }
    if (this.flash > 0) this.flash -= dt;

    // 燃烧 DOT
    if (this.burnT > 0){
      this.burnT -= dt;
      this.burnAcc = (this.burnAcc || 0) + dt;
      while (this.burnAcc >= 0.5){
        this.burnAcc -= 0.5;
        game.damageEnemy(this, this.burn * 0.5, this.burnSrc, true);
        if (this.dead || this.state === 'death') return;
      }
    }
    if (this.slowT > 0) this.slowT -= dt;

    // 击退速度衰减
    this.x += this.kx * dt; this.y += this.ky * dt;
    const kd = Math.pow(0.0015, dt);
    this.kx *= kd; this.ky *= kd;

    // 追击玩家（直线 + 分离）
    const p = game.player;
    let dx = p.x - this.x, dy = p.y - this.y;
    const d = Math.hypot(dx, dy) || 1;
    dx /= d; dy /= d;
    this.rot = Math.atan2(dy, dx) + SPRITE_ROT_OFFSET;   // 始终面向玩家

    const contact = this.radius + p.radius + 0.32;
    const slowMul = this.slowT > 0 ? 0.5 : 1;

    if (this.state !== 'atk'){
      if (d > contact * 0.92){
        const v = this.spd * slowMul;
        this.x += dx * v * dt; this.y += dy * v * dt;
      }
      // 与其它敌人分离，避免叠成一坨
      let sx = 0, sy = 0;
      for (const o of game.enemies.used){
        if (o === this || o.dead || o.state === 'death') continue;
        const ox = this.x - o.x, oy = this.y - o.y;
        const rr = (this.radius + o.radius) * CFG.SEPARATION_R;
        const dd = ox*ox + oy*oy;
        if (dd > 1e-6 && dd < rr*rr){
          const dl = Math.sqrt(dd);
          sx += ox / dl * (1 - dl / rr); sy += oy / dl * (1 - dl / rr);
        }
      }
      const sep = CFG.SEPARATION * slowMul;
      this.x += sx * sep * dt; this.y += sy * sep * dt;
    }

    // 攻击
    this.atkCd -= dt;
    if (this.state === 'atk'){
      this.atkAnim += dt;
      const sp = S(`e:atk:${this.def.sprite}`);
      const n = sp ? sp.frames : 1;
      const dur = n / 18;
      this.frame = Math.floor(this.atkAnim / dur * n);
      if (!this.hitDone && this.atkAnim >= this.def.hitAt){
        this.hitDone = true;
        if (dist(this.x, this.y, game.player.x, game.player.y) < contact * 1.5)
          game.player.hurt(this.dmg, game, this);
      }
      if (this.atkAnim >= dur){ this.state = 'walk'; this.atkAnim = 0; this.frame = 0; }
    } else if (d <= contact && this.atkCd <= 0){
      this.state = 'atk'; this.atkAnim = 0; this.frame = 0; this.hitDone = false;
      this.atkCd = this.def.atkCd;
    } else {
      this.animT += dt;
      const sp = S(`e:walk:${this.def.sprite}`);
      const n = sp ? sp.frames : 1;
      this.frame = Math.floor(this.animT / 0.085) % n;
    }

    game.clampToArena(this);
  }

  draw(ctx){
    const key = this.state === 'death' ? `e:death:${this.def.sprite}`
              : this.state === 'atk'   ? `e:atk:${this.def.sprite}`
              : `e:walk:${this.def.sprite}`;

    const h = this.def.spriteH;
    ctx.save();
    ctx.globalAlpha = 0.3;
    ctx.fillStyle = '#000';
    ctx.beginPath();
    ctx.ellipse(this.x, this.y + h * 0.08, h * 0.20, h * 0.10, 0, 0, TAU);
    ctx.fill();
    ctx.restore();

    const refH = REF_H.enemy[this.def.id];
    drawSprite(ctx, key, this.x, this.y, h, refH, this.rot || 0, this.frame, this.state === 'death' ? 0.85 : 1, false);

    // 受击闪白
    if (this.flash > 0){
      ctx.save();
      ctx.globalCompositeOperation = 'lighter';
      drawSprite(ctx, key, this.x, this.y, h, refH, this.rot || 0, this.frame, this.flash * 3, false);
      ctx.restore();
    }

    // 血条：BOSS 始终显示；其余敌人受伤后才显示
    if ((this.def.boss || this.hp < this.maxHp) && this.state !== 'death'){
      const w = px(this.def.boss ? 62 : (this.def.lv >= 2 ? 38 : 26));
      const bh = px(4);
      const y = this.y - h * 0.52;
      ctx.save();
      ctx.fillStyle = 'rgba(0,0,0,.62)';
      ctx.fillRect(this.x - w/2 - px(1), y - px(1), w + px(2), bh + px(2));
      ctx.fillStyle = this.def.boss ? '#e0603a' : (this.def.lv >= 3 ? '#d8a03a' : '#a9c33c');
      ctx.fillRect(this.x - w/2, y, w * clamp(this.hp / this.maxHp, 0, 1), bh);
      ctx.restore();
    }
  }
}

/* =====================================================================
   投射物
   ===================================================================== */
class Projectile {
  init(owner, ang, W, dmgMul, speedMul, pierce, burn, crit){
    this.x = owner.x; this.y = owner.y;
    this.ang = ang;
    this.speed = W.speed * speedMul;
    this.dmg = W.dmg * dmgMul;
    this.pierce = pierce;
    this.burn = burn;
    this.crit = crit;
    this.radius = 0.13;
    this.life = (W.range * 1.25) / this.speed;
    this.t = 0;
    this.hitIds = [];
    this.dead = false;
    return this;
  }
  reset(){ this.hitIds.length = 0; }
  update(dt, game){
    this.t += dt;
    this.x += Math.cos(this.ang) * this.speed * dt;
    this.y += Math.sin(this.ang) * this.speed * dt;
    if (this.t >= this.life){ this.dead = true; return; }

    for (const e of game.enemies.used){
      if (e.dead || e.state === 'death') continue;
      if (this.hitIds.includes(e)) continue;
      if (dist(this.x, this.y, e.x, e.y) > this.radius + e.radius) continue;
      this.hitIds.push(e);
      game.damageEnemy(e, this.dmg, game.player, false, this.crit);
      if (this.burn > 0){ e.burnT = 3.0; e.burn = this.burn; e.burnSrc = game.player; }
      game.addHitFx(this.x, this.y);
      if (this.hitIds.length > this.pierce){ this.dead = true; return; }
    }
  }
  draw(ctx){
    ctx.save();
    ctx.globalCompositeOperation = 'lighter';
    ctx.fillStyle = 'rgba(210,235,120,.95)';
    ctx.shadowColor = 'rgba(169,195,60,.9)';
    ctx.shadowBlur = 6;
    ctx.beginPath();
    ctx.arc(this.x, this.y, px(4.5), 0, TAU);
    ctx.fill();
    ctx.restore();
  }
}

/* =====================================================================
   掉落物（经验 / 道具）
   ===================================================================== */
class Pickup {
  init(x, y, type, data){
    this.x = x; this.y = y;
    this.type = type;          // 'exp' | 'item'
    this.data = data;          // 经验值 或 DROPS 条目
    this.t = 0; this.dead = false;
    this.vx = (Math.random() - 0.5) * 2.4;
    this.vy = (Math.random() - 0.5) * 2.4;
    this.attracted = false;
    return this;
  }
  reset(){}
  update(dt, game){
    this.t += dt;
    // 初速衰减
    this.x += this.vx * dt; this.y += this.vy * dt;
    const k = Math.pow(0.002, dt);
    this.vx *= k; this.vy *= k;

    const p = game.player;
    const d = dist(this.x, this.y, p.x, p.y);
    const mag = CFG.PICKUP_RADIUS * (p.buffs.power > 0 ? 1.4 : 1);
    if (d < mag) this.attracted = true;
    if (this.attracted){
      const sp = Math.min(11, 3 + (mag - d) * 8);
      this.x += (p.x - this.x) / d * sp * dt;
      this.y += (p.y - this.y) / d * sp * dt;
    }
    if (d < 0.28){ game.collect(this); this.dead = true; }
    if (this.t > 45) this.dead = true;   // 防止无限堆积
  }
  draw(ctx){
    const bob = px(Math.sin(this.t * 4) * 3);
    if (this.type === 'exp'){
      const s = px(7 + Math.sin(this.t * 6) * 1.2);
      ctx.save();
      ctx.globalCompositeOperation = 'lighter';
      ctx.fillStyle = 'rgba(169,195,60,.85)';
      ctx.shadowColor = 'rgba(200,224,90,.95)'; ctx.shadowBlur = 8;
      ctx.beginPath(); ctx.arc(this.x, this.y + bob, s, 0, TAU); ctx.fill();
      ctx.restore();
    } else {
      drawIcon(ctx, 'd:' + this.data.id, this.x, this.y + bob, px(26), .95);
    }
  }
}

/* =====================================================================
   特效（爆炸 / 命中火花 / 火焰锥粒子）
   ===================================================================== */
class Effect {
  init(x, y, kind, opt){
    this.x = x; this.y = y; this.t = 0; this.kind = kind;
    this.opt = opt || {};
    this.dead = false;
    this.rot = Math.random() * TAU;
    return this;
  }
  reset(){}
  update(dt){
    this.t += dt;
    const dur = this.kind === 'explosion' ? 0.55 : this.kind === 'flame' ? 0.3 : 0.16;
    if (this.t >= dur) this.dead = true;
  }
  draw(ctx){
    const k = this.t / (this.kind === 'explosion' ? 0.55 : this.kind === 'flame' ? 0.3 : 0.16);
    if (this.kind === 'explosion'){
      const sp = S('vfx:explosion');
      const n = sp ? sp.frames : 1;
      const f = Math.min(n - 1, Math.floor(k * n));
      ctx.save();
      ctx.globalCompositeOperation = 'lighter';
      drawSprite(ctx, 'vfx:explosion', this.x, this.y, (this.opt.h || 2.2), 0, this.rot, f, 1 - k * 0.35, false);
      ctx.restore();
    } else if (this.kind === 'hit'){
      ctx.save();
      ctx.globalCompositeOperation = 'lighter';
      ctx.globalAlpha = 1 - k;
      ctx.fillStyle = '#c8e05a';
      ctx.beginPath(); ctx.arc(this.x, this.y, px(9) * (1 - k * 0.6), 0, TAU); ctx.fill();
      ctx.restore();
    } else if (this.kind === 'flame'){
      ctx.save();
      ctx.globalCompositeOperation = 'lighter';
      ctx.globalAlpha = (1 - k) * 0.55;
      ctx.fillStyle = this.opt.color || '#ffb03a';
      ctx.beginPath(); ctx.arc(this.x, this.y, (this.opt.r || px(14)) * (1 - k * 0.4), 0, TAU); ctx.fill();
      ctx.restore();
    }
  }
}
