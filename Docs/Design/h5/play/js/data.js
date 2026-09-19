/* =====================================================================
   数值表 —— 全部来自 Docs/Design/h5/index.html 策划案
   凡与策划案不一致处均标 【原型调整】 并说明原因。
   ===================================================================== */

const CFG = {
  // 1 「格」= 多少屏幕像素（策划案里射程/移速都以格为单位）
  PX_PER_UNIT: 48,
  // 逻辑帧率。策划案要求核心战斗必须确定性计算 —— 所以用固定步长驱动，
  // 不用 deltaTime 直接累加，保证不同刷新率下命中结果一致。
  TICK_HZ: 60,
  PLAYER_RADIUS: 0.34,
  PICKUP_RADIUS: 0.9,
  // 敌人分离力（避免叠在一起），策划案 2.5 节「直线追击 + 简单分离」。
  // 初版给 0.55 太弱 —— 实测敌人会全部叠在玩家身上糊成一团，调到 3.2 才散得开。
  SEPARATION: 3.2,
  SEPARATION_R: 1.18,   // 距离小于「半径和 × 该系数」就开始互推
};

/* ---------- 角色层（策划案「角色层差异 BodySO」表） ---------- */
const BODIES = {
  man:  { key:'man',  name:'男性', hp:120, speed:3.2, dmgMul:1.10, rateMul:1.00, radius:0.42,
          desc:'生命与伤害更高，站桩输出更稳。' },
  girl: { key:'girl', name:'女性', hp:100, speed:3.6, dmgMul:1.00, rateMul:1.08, radius:0.36,
          desc:'移速与攻速更快，容错靠走位而非血量。' },
};

/* ---------- 武器层（策划案「武器层差异 WeaponSO」表） ----------
   model: arc=扇形瞬时判定 / proj=投射物 / cone=锥形持续
   rangeAtk: 起手到判定的时间（秒），用于把伤害结算挂在打击帧附近   */
const WEAPONS = {
  knife: { key:'knife', name:'匕首', role:'近战·高频', icon:'items_0015_knife',
    model:'arc', rate:2.5, range:1.0, dmg:8,  arc:100, knock:0.6, ammo:0, ammoRegen:0,
    atkFrames:8, hitAt:0.30, desc:'挥砍判定，范围小但频率最高。' },

  bat:   { key:'bat', name:'棒球棍', role:'近战·范围', icon:'items_0002_bat',
    model:'arc', rate:0.9, range:1.3, dmg:34, arc:120, knock:2.6, ammo:0, ammoRegen:0,
    atkFrames:12, hitAt:0.38, desc:'大范围横扫并击退，清群效率高但前后摇长。' },

  gun:   { key:'gun', name:'手枪', role:'远程·均衡', icon:'items_0000_gun',
    model:'proj', rate:1.6, range:6.0, dmg:12, speed:13, pierce:0, shots:1, spread:0,
    ammo:0, ammoRegen:0, atkFrames:5, hitAt:0.22,
    desc:'单发点射，最贴合当前战斗模型的兵种。' },

  riffle:{ key:'riffle', name:'步枪', role:'远程·连射', icon:'items_0014_gun',
    model:'proj', rate:4.0, range:7.0, dmg:7, speed:15, pierce:0, shots:1, spread:0.05,
    burst:3, burstGap:0.055,
    ammo:200, ammoRegen:9, atkFrames:9, hitAt:0.18,
    desc:'三连发高射速，消耗弹药，需靠补给维持。' },

  flame: { key:'flame', name:'火焰喷射器', role:'远程·扇形持续', icon:'items_0001_fire',
    model:'cone', rate:8, range:3.5, dmg:3, cone:38, burn:3.0, burnDps:4,
    ammo:100, ammoRegen:7, atkFrames:9, hitAt:0,
    desc:'近距离锥形持续伤害，可叠燃烧。生命最低的角色用它在刀尖上跳舞。' },
};

const WEAPON_ORDER = ['knife','bat','gun','riffle','flame'];

/* ---------- 兵种 = 角色 × 武器 = 10 种（策划案「兵种矩阵总表」） ---------- */
const CLASSES = [];
for (const b of ['man','girl']) {
  for (const w of WEAPON_ORDER) {
    const B = BODIES[b], W = WEAPONS[w];
    CLASSES.push({
      id: b + '_' + w, body:b, weapon:w,
      name: B.name + ' · ' + W.name,
      role: W.role,
      hp: B.hp, speed: B.speed,
      dmg: W.dmg * B.dmgMul,               // 角色伤害修正乘在武器基础伤害上
      rate: W.rate * B.rateMul,            // 角色攻速修正作用于攻击间隔
      range: W.range,
      desc: W.desc,
    });
  }
}

/* ---------- 敌人（策划案「敌人总表」） ----------
   【原型调整】BOSS 三档血量按 1/2.4 缩放。
   策划案给的 boss1 2200 / boss2 2600 / mega 9000 是「未实测的首版基准」；
   以本原型满配约 90~120 DPS 计算，9000 血要打 75 秒以上，单局体验不成立。
   缩放后：boss1 900（约 8~12 秒）、boss2 1100、mega 2600。
   lv1~lv3 的数值原样保留 —— 实测手感正常。 */
const ENEMIES = {
  z1: { id:'z1', name:'女性僵尸 A', lv:1, kind:'小怪', hp:30,  spd:1.6,  dmg:6,  exp:5,   money:2,
        radius:0.34, spriteH:1.35, sprite:'z1', hitAt:0.35, atkCd:1.0 },
  z2: { id:'z2', name:'女性僵尸 B', lv:1, kind:'小怪', hp:30,  spd:1.6,  dmg:6,  exp:5,   money:2,
        radius:0.34, spriteH:1.35, sprite:'z2', hitAt:0.35, atkCd:1.0 },
  z3: { id:'z3', name:'男性僵尸 A', lv:1, kind:'小怪', hp:34,  spd:1.5,  dmg:6,  exp:5,   money:2,
        radius:0.36, spriteH:1.4,  sprite:'z3', hitAt:0.35, atkCd:1.0 },
  z4: { id:'z4', name:'男性僵尸 B', lv:1, kind:'小怪', hp:34,  spd:1.5,  dmg:6,  exp:5,   money:2,
        radius:0.36, spriteH:1.4,  sprite:'z4', hitAt:0.35, atkCd:1.0 },
  z5: { id:'z5', name:'军装僵尸',  lv:2, kind:'精英', hp:70,  spd:1.8,  dmg:12, exp:14,  money:6,
        radius:0.44, spriteH:1.65, sprite:'z5', hitAt:0.4,  atkCd:1.1 },
  z6: { id:'z6', name:'警察僵尸',  lv:2, kind:'精英', hp:80,  spd:1.7,  dmg:14, exp:16,  money:7,
        radius:0.46, spriteH:1.7,  sprite:'z6', hitAt:0.4,  atkCd:1.1 },
  z7: { id:'z7', name:'巨手僵尸',  lv:3, kind:'重装', hp:180, spd:1.2,  dmg:24, exp:40,  money:14,
        radius:0.60, spriteH:2.1,  sprite:'z7', hitAt:0.45, atkCd:1.3 },
  z8: { id:'z8', name:'巨头僵尸',  lv:3, kind:'重装', hp:200, spd:1.1,  dmg:26, exp:44,  money:15,
        radius:0.62, spriteH:2.15, sprite:'z8', hitAt:0.45, atkCd:1.3 },
  boss1:{ id:'boss1', name:'BOSS 01', lv:4, kind:'BOSS', hp:900,  spd:1.0,  dmg:40, exp:300, money:120,
        radius:0.95, spriteH:3.4, sprite:'boss1', hitAt:0.5, atkCd:1.35, boss:true },
  boss2:{ id:'boss2', name:'BOSS 02', lv:4, kind:'BOSS', hp:1100, spd:0.95, dmg:44, exp:340, money:150,
        radius:0.98, spriteH:3.5, sprite:'boss2', hitAt:0.5, atkCd:1.25, boss:true },
  mega: { id:'mega',  name:'巨型 BOSS', lv:5, kind:'终局', hp:2600, spd:0.8, dmg:70, exp:1200, money:400,
        radius:1.45, spriteH:5.0, sprite:'mega', hitAt:0.55, atkCd:1.5, boss:true },
};

/* ---------- 波次（策划案「波次接入表」） ----------
   dur 为每波时长（秒）。策划案未给时长，取 22 秒兼顾推到 BOSS 的节奏与单局时长。 */
const WAVES = [
  { n:1,  dur:22, cap:12, diff:1.0, pool:['z1','z2','z3','z4'] },
  { n:2,  dur:22, cap:12, diff:1.0, pool:['z1','z2','z3','z4'] },
  { n:3,  dur:22, cap:16, diff:1.3, pool:['z1','z2','z3','z4','z5'] },
  { n:4,  dur:22, cap:20, diff:1.7, pool:['z1','z2','z3','z4','z5','z6'] },
  { n:5,  dur:22, cap:20, diff:1.7, pool:['z1','z3','z5','z6'] },
  { n:6,  dur:24, cap:24, diff:2.2, pool:['z3','z4','z5','z6','z7'] },
  { n:7,  dur:24, cap:24, diff:2.2, pool:['z4','z5','z6','z7','z8'] },
  { n:8,  dur:24, cap:28, diff:2.8, pool:['z5','z6','z7','z8'] },
  { n:9,  dur:32, cap:20, diff:3.2, pool:['z1','z3','z5','z6'], boss:'boss1' },
  { n:10, dur:32, cap:22, diff:3.6, pool:['z5','z6','z7'],      boss:'boss2' },
  { n:11, dur:36, cap:30, diff:4.0, pool:['z5','z6','z7','z8'], boss:'mega' },
];
// 11 波起无限循环，难度继续爬升
const ENDLESS_STEP = 0.45;
function waveFor(n){
  if (n <= WAVES.length) return WAVES[n-1];
  const last = WAVES[WAVES.length-1];
  return { n, dur:last.dur, cap:last.cap, diff:last.diff + (n-WAVES.length)*ENDLESS_STEP,
           pool:last.pool, boss:last.boss };
}

/* ---------- 升级池（策划案指出素材只有 5 个技能图标，故图标复用） ----------
   w=权重, rar 0普通/1稀有/2史诗 */
const UPGRADES = [
  { id:'rate',    name:'高速弹匣', icon:'skills_0000_Layer-1', rar:0, w:10,
    desc:'攻击速度 +18%', apply:p => p.rateMul *= 1.18 },
  { id:'dmg',     name:'力量强化', icon:'skills_0001_Layer-2', rar:0, w:10,
    desc:'伤害 +20%',    apply:p => p.dmgMul *= 1.20 },
  { id:'speed',   name:'疾行靴',   icon:'skills_0003_Layer-4', rar:0, w:9,
    desc:'移动速度 +15%', apply:p => p.speed *= 1.15 },
  { id:'hp',      name:'生命强化', icon:'skills_0004_Layer-0', rar:0, w:9,
    desc:'最大生命 +25 并回复等量', apply:p => { p.maxHp += 25; p.hp = Math.min(p.maxHp, p.hp + 25); } },
  { id:'armor',   name:'菌核护甲', icon:'skills_0004_Layer-0', rar:0, w:8,
    desc:'受到伤害 -15%', apply:p => p.armor = Math.min(0.6, p.armor + 0.15) },
  { id:'range',   name:'弹道加速', icon:'skills_0000_Layer-1', rar:0, w:8,
    desc:'射程 +18%，弹速 +25%', apply:p => { p.rangeMul *= 1.18; p.projSpeedMul *= 1.25; } },
  { id:'exp',     name:'经验汲取', icon:'skills_0000_Layer-1', rar:0, w:7,
    desc:'经验获取 +25%', apply:p => p.expMul *= 1.25 },
  { id:'pierce',  name:'穿透弹',   icon:'skills_0002_Layer-3', rar:1, w:5,
    desc:'投射物可多穿透 1 个敌人', apply:p => p.pierce += 1 },
  { id:'multi',   name:'多重射击', icon:'skills_0001_Layer-2', rar:1, w:5,
    desc:'投射物 +1（略微散射）', apply:p => { p.shots += 1; p.spread += 0.10; } },
  { id:'burn',    name:'烈焰附魔', icon:'skills_0002_Layer-3', rar:1, w:5,
    desc:'命中附加 3 秒燃烧，每秒 4 点', apply:p => p.burn += 3.0 },
  { id:'crit',    name:'致命一击', icon:'skills_0001_Layer-2', rar:1, w:5,
    desc:'20% 概率造成双倍伤害', apply:p => p.crit = Math.min(0.8, p.crit + 0.20) },
  { id:'vamp',    name:'嗜血',     icon:'skills_0003_Layer-4', rar:1, w:4,
    desc:'每次击杀回复 2 点生命', apply:p => p.vamp += 2 },
  { id:'thorn',   name:'荆棘',     icon:'skills_0004_Layer-0', rar:2, w:3,
    desc:'受击时反弹 60% 伤害给攻击者', apply:p => p.thorn += 0.6 },
  { id:'echo',    name:'余烬回响', icon:'skills_0002_Layer-3', rar:2, w:3,
    desc:'攻击速度 +30%，但受到伤害 +10%', apply:p => { p.rateMul *= 1.30; p.armor -= 0.10; } },
];

/* ---------- 掉落表（策划案「掉落与拾取」） ---------- */
const DROPS = [
  { id:'money',  icon:'items_0013_money',        w:12, label:'金币' },
  { id:'health', icon:'items_0005_health',        w:6,  label:'医疗包 +25 HP' },
  { id:'armor',  icon:'items_0010_armor',         w:5,  label:'护甲 减伤 15 秒' },
  { id:'speed',  icon:'items_0006_speed',         w:5,  label:'加速 12 秒' },
  { id:'power',  icon:'items_0008_superpower',    w:3,  label:'超级力量 伤害×2 10 秒' },
  { id:'slow',   icon:'items_0007_slow_enemies',  w:3,  label:'减速场' },
  { id:'mag',    icon:'items_0003_magazine_gun',  w:8,  label:'弹药补给' },
  { id:'fuel',   icon:'items_0012_cylinder',      w:4,  label:'燃料补给' },
];

/* ---------- 经验曲线 ---------- */
function expNeed(lv){ return Math.round(12 + lv * 7 + lv * lv * 1.05); }
