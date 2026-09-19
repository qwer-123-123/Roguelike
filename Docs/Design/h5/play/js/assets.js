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

// 图标 / 立绘（单帧）
const PLAIN = [
  ['p:man',        'ui/Icons/man%20icon_no_bg.png'],
  ['p:girl',       'ui/Icons/girl%20icon_no_bg.png'],
  ['p:man_box',    'ui/Icons/man%20icon.png'],
  ['p:girl_box',   'ui/Icons/girl%20icon.png'],
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
        resolve(failed);
      }
    }
  });
}

/** 取一个精灵；缺失时返回 null，绘制处需判空 */
function S(key){ return IMG[key] || null; }

/**
 * 画整帧精灵。**在相机变换内调用，坐标与尺寸都必须是世界单位（格）**。
 * cx,cy = 世界坐标；hUnits = 显示高度（格）
 * rot = 弧度旋转（俯视角精灵按朝向旋转）；flip = 水平镜像
 * frame = 第几帧；alpha = 不透明度
 */
function drawSprite(ctx, key, cx, cy, hUnits, rot, frame, alpha, flip){
  const sp = S(key);
  if (!sp) return;
  const dh = hUnits;
  const dw = dh * (sp.fw / sp.fh);
  const n = sp.frames;
  const fi = n > 1 ? (((frame % n) + n) % n) : 0;

  ctx.save();
  ctx.globalAlpha = alpha === undefined ? 1 : alpha;
  ctx.translate(cx, cy);
  if (rot) ctx.rotate(rot);
  if (flip) ctx.scale(-1, 1);
  ctx.drawImage(sp.img, fi * sp.fw, 0, sp.fw, sp.fh, -dw/2, -dh/2, dw, dh);
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
