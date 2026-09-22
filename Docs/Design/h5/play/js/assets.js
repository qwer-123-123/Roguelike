/* =====================================================================
   素材加载
   全部图片来自 ../../assets/（Docs/Design/h5/assets，由 _tools/build-assets.ps1 合成）
   精灵条是「横向排列的逐帧 PNG」，绘制时按帧切分：
       ctx.drawImage(img, frame*fw, 0, fw, fh, dx, dy, dw, dh)
   帧数为当初合成时写死的值（见 data.js 注释），此处逐一核对过：
     unit   idle 8 / walk 6 / death 6 / atk 匕首8 棒球棍12 手枪5 步枪9 火焰9
     enemy  walk z1~z8=9, boss1/boss2/mega=8
            atk  z1~z8=9, boss1=14, boss2=8, mega=16
            death z1~z8=6, boss1=10, boss2=10, mega=14
   ===================================================================== */

const ASSET_BASE = '../assets/';

const ENEMY_WALK_F  = { z1:9,z2:9,z3:9,z4:9,z5:9,z6:9,z7:9,z8:9, boss1:8, boss2:8, mega:8 };
const ENEMY_ATK_F   = { z1:9,z2:9,z3:9,z4:9,z5:9,z6:9,z7:9,z8:9, boss1:14, boss2:8, mega:16 };
const ENEMY_DEATH_F = { z1:6,z2:6,z3:6,z4:6,z5:6,z6:6,z7:6,z8:6, boss1:10, boss2:10, mega:14 };

const SPRITE_DEFS = [];

// 玩家：2 角色 × 5 武器 × (idle/walk/atk) + death
for (const body of ['man','girl']) {
  for (const w of WEAPON_ORDER) {
    SPRITE_DEFS.push({ k:`u:idle:${body}:${w}`, src:`unit/idle_${body}_${w}.png`, f:8 });
    SPRITE_DEFS.push({ k:`u:walk:${body}:${w}`, src:`unit/walk_${body}_${w}.png`, f:6 });
    SPRITE_DEFS.push({ k:`u:atk:${body}:${w}`,  src:`unit/atk_${body}_${w}.png`,  f:WEAPONS[w].atkFrames });
  }
  SPRITE_DEFS.push({ k:`u:death:${body}`, src:`unit/death_${body}.png`, f:6 });
}

// 敌人
for (const id of Object.keys(ENEMIES)) {
  SPRITE_DEFS.push({ k:`e:walk:${id}`,  src:`enemy/walk_${id}.png`,  f:ENEMY_WALK_F[id] });
  SPRITE_DEFS.push({ k:`e:atk:${id}`,   src:`enemy/atk_${id}.png`,   f:ENEMY_ATK_F[id] });
  SPRITE_DEFS.push({ k:`e:death:${id}`, src:`enemy/death_${id}.png`, f:ENEMY_DEATH_F[id] });
}

// 特效
SPRITE_DEFS.push({ k:'vfx:explosion', src:'vfx/explosion.png', f:6 });
SPRITE_DEFS.push({ k:'vfx:shots',     src:'vfx/shots_fire.png', f:13 });

/* ---------- 地面瓦片 ----------
   每套 13 张 89×89。文件名里的 Layer 号是线性递减的（base - i），
   实测 base：ground=50 / grass=55 / asphalt=12 / water=29。
   用法（见 game.js 的 ensureFloor / buildFloor）：
     grass  0000 是无缝的，做整块基底平铺
     ground 多数带透明边缘，当泥土贴花散铺
     water  带透明边缘，当水洼散铺
     asphalt 不透明、是马路拼块，散铺会像贴方块，故未用于基底
   只加载实际用到的，避免多拉 30 张没用上的图。 */
const TILE_BASE = { ground:50, grass:55, water:29 };
// 必须**整 13 张全加载** —— 自动拼接的每个掩码都指向具体索引，
// 少一张就会出现「某些格子取不到图直接不画」的空洞（water mask 0 用到索引 5）。
const TILE_USE  = {
  grass:  [0, 1, 2],
  ground: [0,1,2,3,4,5,6,7,8,9,10,11,12],
  water:  [0,1,2,3,4,5,6,7,8,9,10,11,12],
};
for (const name of Object.keys(TILE_USE)) {
  for (const i of TILE_USE[name]) {
    const num = String(i).padStart(4, '0');
    SPRITE_DEFS.push({
      k: `t:${name}:${i}`,
      src: `tile/${name}_tiles_${num}_Layer-${TILE_BASE[name] - i}.png`,
      f: 1,
    });
  }
}

// 图标 / 立绘（单帧）
// 角色头像在 assets/portrait/ —— ui/Icons/ 里只有女性头像（素材包的 UI 套件
// 没带男性），早先写 ui/Icons/ 会让男性头像静默加载失败。
const PLAIN = [
  ['p:man',        'portrait/man%20icon_no_bg.png'],
  ['p:girl',       'portrait/girl%20icon_no_bg.png'],
  ['p:man_box',    'portrait/man%20icon.png'],
  ['p:girl_box',   'portrait/girl%20icon.png'],
];
for (const w of WEAPON_ORDER) PLAIN.push([`w:${w}`, `item/${WEAPONS[w].icon}.png`]);
for (const u of UPGRADES)          PLAIN.push([`s:${u.icon}`, `ui/Icons/${u.icon}.png`]);
for (const d of DROPS)             PLAIN.push([`d:${d.id}`,   `item/${d.icon}.png`]);
PLAIN.push(['d:exp', 'ui/element_0045_Layer-47.png']);
for (const [k, src] of PLAIN) SPRITE_DEFS.push({ k, src, f:1 });

/* ---------- 载入 ---------- */
const IMG = Object.create(null);
let loadDone = 0;

function loadAll(onProgress){
  const uniq = new Map();
  for (const d of SPRITE_DEFS) if (!uniq.has(d.k)) uniq.set(d.k, d);
  const list = [...uniq.values()];
  const total = list.length;

  return new Promise((resolve, reject) => {
    let failed = 0;
    for (const def of list) {
      const im = new Image();
      im.onload = () => {
        IMG[def.k] = { img: im, frames: def.f, fw: Math.floor(im.naturalWidth / def.f), fh: im.naturalHeight };
        done();
      };
      im.onerror = () => { failed++; IMG[def.k] = null; console.warn('素材缺失:', def.src); done(); };
      im.src = ASSET_BASE + def.src;
    }
    function done(){
      loadDone++;
      if (onProgress) onProgress(loadDone, total);
      if (loadDone === total) {
        if (failed) console.warn(`有 ${failed} 个素材加载失败 —— 多半是 assets/ 不在位（它未入 git，见 Docs/Design/README.md）`);
        computeRefH();
        resolve(failed);
      }
    }
  });
}

/** 取一个精灵；缺失时返回 null，绘制处需判空 */
function S(key){ return IMG[key] || null; }

/* 参考帧高：同一角色的所有动画共用一个缩放比（见 _tools/build-sprites.ps1），
   所以取一条「最贴合角色本体、不含特效」的条当基准就够 —— 玩家用手枪 idle，
   敌人用 walk。绘制时以它为换算基准，保证 idle/walk/atk 之间角色大小一致。 */
const REF_H = { unit: Object.create(null), enemy: Object.create(null) };
function computeRefH(){
  for (const body of ['man', 'girl']) {
    const sp = IMG[`u:idle:${body}:gun`];
    REF_H.unit[body] = sp ? sp.fh : 0;
  }
  for (const id of Object.keys(ENEMIES)) {
    const sp = IMG[`e:walk:${id}`];
    REF_H.enemy[id] = sp ? sp.fh : 0;
  }
}

/**
 * 画整帧精灵。**在相机变换内调用，坐标与尺寸都必须是世界单位（格）**。
 * cx,cy   世界坐标
 * hUnits  参考条的世界高度（格）—— 即 refH 那一帧对应多高
 * refH    参考帧高（像素）；不传则退回该条自身的帧高
 * rot/frame/alpha/flip 同前
 *
 * ★ 这里**不能**把帧高归一到 hUnits。攻击动画的原始帧更高（枪口火焰、刀光
 *   往上延伸），归一化会把角色本体一起缩小 —— 表现就是「攻击时人物变小」。
 *   正确做法是按固定像素换算，帧越高只是向上延伸得越多。
 */
function drawSprite(ctx, key, cx, cy, hUnits, refH, rot, frame, alpha, flip){
  const sp = S(key);
  if (!sp) return;
  const k = hUnits / (refH > 0 ? refH : sp.fh);   // 世界单位 / 像素
  const dw = sp.fw * k, dh = sp.fh * k;
  const n = sp.frames;
  const fi = n > 1 ? (((frame % n) + n) % n) : 0;

  ctx.save();
  ctx.globalAlpha = alpha === undefined ? 1 : alpha;
  ctx.translate(cx, cy);
  if (rot) ctx.rotate(rot);
  if (flip) ctx.scale(-1, 1);
  // 合成时每一帧都是**底对齐**的，帧底 = 脚底，位置在各动画间一致。
  // 以参考条的中线为锚点：参考动画正好居中，更高的动画向上延伸、脚不动。
  ctx.drawImage(sp.img, fi * sp.fw, 0, sp.fw, sp.fh, -dw/2, -dh + hUnits*0.5, dw, dh);
  ctx.restore();
}

/** 画图标，等比放进 sizeWorld 见方的框（世界单位；想按像素写用 px(n)） */
function drawIcon(ctx, key, cx, cy, sizeWorld, alpha){
  const sp = S(key);
  if (!sp) return;
  const k = Math.min(sizeWorld / sp.fw, sizeWorld / sp.fh);
  const w = sp.fw * k, h = sp.fh * k;
  ctx.save();
  ctx.globalAlpha = alpha === undefined ? 1 : alpha;
  ctx.drawImage(sp.img, cx - w/2, cy - h/2, w, h);
  ctx.restore();
}
