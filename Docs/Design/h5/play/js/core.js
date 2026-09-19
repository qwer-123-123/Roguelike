/* =====================================================================
   核心：数学 / 随机 / 输入 / 相机 / 对象池
   策划案「项目约定」要求核心战斗必须确定性计算 —— 因此：
     · 逻辑走固定步长累加器（见 main.js），不用 deltaTime 直接驱动
     · 随机数用带种子的 mulberry32，同一局种子结果可复现
   随机只用于策划案允许的地方（刷怪位置、三选一抽牌、掉落），
   伤害/命中判定不含随机 —— 唯一的例外是暴击，那是升级卡明确给的效果。
   ===================================================================== */

/* ---------- 数学 ---------- */
const TAU = Math.PI * 2;
function clamp(v, a, b){ return v < a ? a : v > b ? b : v; }
function lerp(a, b, t){ return a + (b - a) * t; }
function dist2(ax, ay, bx, by){ const dx = ax-bx, dy = ay-by; return dx*dx + dy*dy; }
function dist(ax, ay, bx, by){ return Math.sqrt(dist2(ax, ay, bx, by)); }
/** 把「想画成多少像素」换算成世界单位（格）。
    Camera 加了 scale(PX_PER_UNIT)，所以相机变换内的绘图一律用世界单位，
    想让某个东西看起来是 n 像素，就写 px(n)。 */
function px(n){ return n / CFG.PX_PER_UNIT; }
/** 把角度归一化到 (-PI, PI] */
function normAng(a){ a = (a + Math.PI) % TAU; if (a < 0) a += TAU; return a - Math.PI; }
/** 两角之间的最小夹角（绝对值） */
function angDiff(a, b){ return Math.abs(normAng(a - b)); }

/* ---------- 带种子的随机 ---------- */
function makeRng(seed){
  let s = seed >>> 0;
  const r = () => {
    s |= 0; s = (s + 0x6D2B79F5) | 0;
    let t = Math.imul(s ^ (s >>> 15), 1 | s);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
  r.range = (a, b) => a + r() * (b - a);
  r.int = (a, b) => Math.floor(a + r() * (b - a + 1));
  r.pick = arr => arr[Math.floor(r() * arr.length)];
  r.chance = p => r() < p;
  /** 按 w 字段加权抽取；从数组中挑，可排除已选（避免三选一重复） */
  r.weighted = (arr, exclude) => {
    const pool = exclude ? arr.filter(x => !exclude.includes(x)) : arr;
    if (!pool.length) return null;
    let total = 0;
    for (const x of pool) total += (x.w || 1);
    let t = r() * total;
    for (const x of pool) { t -= (x.w || 1); if (t <= 0) return x; }
    return pool[pool.length - 1];
  };
  return r;
}

/* ---------- 输入 ---------- */
const Input = {
  keys: Object.create(null),
  // 虚拟摇杆（触屏）
  stick: { active:false, id:null, ox:0, oy:0, dx:0, dy:0 },

  init(){
    addEventListener('keydown', e => {
      this.keys[e.code] = true;
      // 防止空格/方向键把页面滚走
      if (['Space','ArrowUp','ArrowDown','ArrowLeft','ArrowRight'].includes(e.code)) e.preventDefault();
    });
    addEventListener('keyup', e => { this.keys[e.code] = false; });
    addEventListener('blur', () => { this.keys = Object.create(null); });

    const touch = ('ontouchstart' in window);
    if (touch) document.body.classList.add('touch');

    const el = document.getElementById('ui');
    addEventListener('pointerdown', e => {
      if (!touch) return;
      if (e.clientX > innerWidth * 0.5) return;      // 右半屏留给界面按钮
      this.stick.active = true; this.stick.id = e.pointerId;
      this.stick.ox = e.clientX; this.stick.oy = e.clientY;
      this.stick.dx = 0; this.stick.dy = 0;
    });
    addEventListener('pointermove', e => {
      if (!this.stick.active || e.pointerId !== this.stick.id) return;
      const dx = e.clientX - this.stick.ox, dy = e.clientY - this.stick.oy;
      const d = Math.hypot(dx, dy) || 1, max = 60;
      const k = Math.min(1, d / max);
      this.stick.dx = dx / d * k; this.stick.dy = dy / d * k;
    });
    const end = e => {
      if (e.pointerId === this.stick.id){ this.stick.active = false; this.stick.dx = this.stick.dy = 0; }
    };
    addEventListener('pointerup', end);
    addEventListener('pointercancel', end);
  },

  /** 归一化的移动方向（长度 ≤ 1） */
  moveVec(){
    let x = 0, y = 0;
    if (this.keys['KeyA'] || this.keys['ArrowLeft'])  x -= 1;
    if (this.keys['KeyD'] || this.keys['ArrowRight']) x += 1;
    if (this.keys['KeyW'] || this.keys['ArrowUp'])    y -= 1;
    if (this.keys['KeyS'] || this.keys['ArrowDown'])  y += 1;
    if (x === 0 && y === 0 && this.stick.active){ x = this.stick.dx; y = this.stick.dy; }
    const d = Math.hypot(x, y);
    if (d > 1){ x /= d; y /= d; }
    return { x, y };
  },
  once(code){ if (this.keys[code]){ this.keys[code] = false; return true; } return false; },
};

/* ---------- 相机 ---------- */
const Camera = {
  x:0, y:0, shake:0,
  follow(tx, ty, dt){
    // 轻微延迟跟随，避免割草时画面晃动过猛
    this.x = lerp(this.x, tx, 1 - Math.pow(0.001, dt));
    this.y = lerp(this.y, ty, 1 - Math.pow(0.001, dt));
    if (this.shake > 0) this.shake = Math.max(0, this.shake - dt * 26);
  },
  kick(v){ this.shake = Math.min(14, this.shake + v); },
  /** 世界坐标 → 屏幕：screen = world * PX_PER_UNIT + (w/2 - cam*PX_PER_UNIT)
      所以先 translate 再 scale。**漏掉 scale 会让所有实体的位置被当成像素**，
      表现为「逻辑上散开的敌人全挤在屏幕中心」，只改尺寸不改位置。
      加上 scale 后，相机变换内的一切绘图都必须用**世界单位**；
      想把某个东西画成 n 像素，用 px(n) 换算。 */
  apply(ctx, w, h){
    const sx = this.shake ? (Math.random() * 2 - 1) * this.shake : 0;
    const sy = this.shake ? (Math.random() * 2 - 1) * this.shake : 0;
    ctx.translate(w/2 - this.x * CFG.PX_PER_UNIT + sx, h/2 - this.y * CFG.PX_PER_UNIT + sy);
    ctx.scale(CFG.PX_PER_UNIT, CFG.PX_PER_UNIT);
  },
  /** 世界坐标 → 屏幕坐标 */
  toScreen(wx, wy, w, h){
    return { x: w/2 + (wx - this.x) * CFG.PX_PER_UNIT, y: h/2 + (wy - this.y) * CFG.PX_PER_UNIT };
  },
};

/* ---------- 对象池 ----------
   策划案要求敌人/投射物走对象池，避免割草时频繁 GC。 */
class Pool {
  constructor(factory, reset, initial){
    this.factory = factory; this.reset = reset;
    this.free = []; this.used = [];
    for (let i = 0; i < (initial || 0); i++) this.free.push(factory());
  }
  spawn(){
    const o = this.free.length ? this.free.pop() : this.factory();
    this.used.push(o);
    return o;
  }
  /** 回收本帧标记为 dead 的对象 */
  sweep(){
    for (let i = this.used.length - 1; i >= 0; i--) {
      const o = this.used[i];
      if (o.dead) {
        this.used.splice(i, 1);
        this.reset(o);
        this.free.push(o);
      }
    }
  }
  clear(){
    for (const o of this.used){ this.reset(o); this.free.push(o); }
    this.used.length = 0;
  }
  get count(){ return this.used.length; }
}

/* ---------- 轻量伤害数字 / 飘字 ---------- */
class FloatText {
  constructor(x, y, text, color, size){ this.reset(x, y, text, color, size); }
  reset(x, y, text, color, size){
    this.x = x; this.y = y; this.text = text; this.color = color || '#fff';
    this.size = size || 13; this.life = 0.75; this.t = 0; this.dead = false;
    // 初始位置抖动大一些 —— 连发武器同一帧打出多个数字会完全重叠成「158」这种假读数
    this.x += (Math.random() - 0.5) * 16;
    this.y += (Math.random() - 0.5) * 10;
    this.vx = (Math.random() - 0.5) * 1.6; this.vy = -1.6;
    return this;
  }
  update(dt){
    this.t += dt;
    this.x += this.vx * dt; this.y += this.vy * dt;
    this.vy += 2.2 * dt;
    if (this.t >= this.life) this.dead = true;
  }
  /** 在世界坐标处绘制（内部转屏幕空间，字号保持像素） */
  draw(ctx){
    const s = Camera.toScreen(this.x, this.y, innerWidth, innerHeight);
    this.drawAt(ctx, s.x, s.y);
  }
  drawAt(ctx, x, y){
    const k = this.t / this.life;
    ctx.save();
    ctx.globalAlpha = k < 0.7 ? 1 : (1 - k) / 0.3;
    ctx.font = `700 ${this.size}px Consolas, monospace`;
    ctx.textAlign = 'center';
    ctx.lineWidth = 3; ctx.strokeStyle = 'rgba(0,0,0,.85)';
    ctx.strokeText(this.text, x, y);
    ctx.fillStyle = this.color;
    ctx.fillText(this.text, x, y);
    ctx.restore();
  }
}
