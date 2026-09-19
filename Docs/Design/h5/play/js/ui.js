/* =====================================================================
   界面层：屏幕切换 / HUD / 兵种选择 / 升级三选一 / 死亡结算
   所有视觉元素来自素材包真实 PNG（见 css/style.css）
   ===================================================================== */

const UI = {
  el: {},
  selClass: null,
  hudAcc: 0,
  bannerT: 0,
  upSel: 0,
  upOptions: [],

  init(){
    const $ = id => document.getElementById(id);
    this.el = {
      menu: $('scr-menu'), pick: $('scr-pick'), hud: $('scr-hud'),
      upgrade: $('scr-upgrade'), pause: $('scr-pause'),
      over: $('scr-over'), help: $('scr-help'),
      // HUD
      hpFill: $('hp-fill'), hpText: $('hp-text'), expFill: $('exp-fill'), lvText: $('lv-text'),
      waveText: $('wave-text'), waveLeft: $('wave-left'),
      timeText: $('time-text'), killText: $('kill-text'), diffText: $('diff-text'),
      moneyText: $('money-text'), hudWeapon: $('hud-weapon'),
      ammoFill: $('ammo-fill'), ammoText: $('ammo-text'), skillRow: $('skill-row'),
      // 三选一
      upLv: $('up-lv'), upCards: $('up-cards'),
      // 结算
      overPortrait: $('over-portrait'),
      ovTime: $('ov-time'), ovKills: $('ov-kills'), ovWave: $('ov-wave'),
      ovLv: $('ov-lv'), ovMoney: $('ov-money'), ovRank: $('ov-rank'), ovLoot: $('ov-loot'),
      // 兵种
      pickList: $('pick-list'), pickPortrait: $('pick-portrait'),
      pickName: $('pick-name'), pickRole: $('pick-role'),
      pickStats: $('pick-stats'), pickWeapons: $('pick-weapons'),
    };
    this.buildClassSelect();

    // 按钮
    $('btn-start').onclick      = () => { SFX.init(); this.show('pick'); };
    $('btn-help').onclick       = () => this.show('help');
    $('btn-help-close').onclick = () => this.show('menu');
    $('btn-pick-back').onclick  = () => this.show('menu');
    $('btn-pick-go').onclick    = () => Main.startRun(this.selClass);
    $('btn-resume').onclick     = () => Main.resume();
    $('btn-quit').onclick       = () => { Main.game.state = 'idle'; this.show('menu'); };
    $('btn-retry').onclick      = () => Main.startRun(this.selClass);
    $('btn-menu').onclick       = () => { Main.game.state = 'idle'; this.show('menu'); };

    // 三选一：键盘
    addEventListener('keydown', e => {
      if (!this.el.upgrade.classList.contains('on')) return;
      if (e.code === 'ArrowLeft'  || e.code === 'KeyA') this.moveUpSel(-1);
      if (e.code === 'ArrowRight' || e.code === 'KeyD') this.moveUpSel(1);
      if (e.code === 'Digit1' || e.code === 'Digit2' || e.code === 'Digit3'){
        this.upSel = +e.code.slice(-1) - 1;
        this.paintUpSel();
      }
      if (e.code === 'Space' || e.code === 'Enter'){ e.preventDefault(); this.confirmUpgrade(); }
    });
  },

  show(name){
    for (const k of ['menu','pick','hud','upgrade','pause','over','help'])
      this.el[k].classList.toggle('on', k === name);
  },

  /* ================= 兵种选择 ================= */
  buildClassSelect(){
    this.selClass = CLASSES[0];
    this.el.pickList.innerHTML = CLASSES.map((c,i) =>
      `<div class="pick-item${i===0?' on':''}" data-i="${i}">
         <img src="../assets/portrait/${c.body}%20icon_no_bg.png" alt="">
         <div><div class="pi-n">${c.name}</div><div class="pi-r">${c.role}</div></div>
       </div>`).join('');
    this.el.pickList.querySelectorAll('.pick-item').forEach(el => {
      el.onclick = () => {
        this.selClass = CLASSES[+el.dataset.i];
        this.el.pickList.querySelectorAll('.pick-item').forEach(x => x.classList.toggle('on', x === el));
        this.paintClassDetail();
      };
    });
    this.el.pickWeapons.innerHTML = WEAPON_ORDER.map(w =>
      `<img src="../assets/item/${WEAPONS[w].icon}.png" data-w="${w}" alt="${WEAPONS[w].name}">`).join('');
    this.paintClassDetail();
  },

  paintClassDetail(){
    const c = this.selClass;
    this.el.pickPortrait.innerHTML =
      `<img src="../assets/portrait/${c.body}%20icon_no_bg.png" alt="">`;
    this.el.pickName.textContent = c.name;
    this.el.pickRole.textContent = `${c.role}　|　${WEAPONS[c.weapon].desc}`;
    this.el.pickStats.innerHTML = [
      ['生命', Math.round(c.hp)],
      ['移速', c.speed.toFixed(1) + ' 格/秒'],
      ['伤害', Math.round(c.dmg)],
      ['攻速', c.rate.toFixed(1) + ' 次/秒'],
      ['射程', c.range.toFixed(1) + ' 格'],
      ['攻击模型', WEAPONS[c.weapon].model === 'arc' ? '扇形瞬时'
                : WEAPONS[c.weapon].model === 'cone' ? '锥形持续' : '投射物'],
    ].map(([k,v]) => `<dt>${k}</dt><dd>${v}</dd>`).join('');
    this.el.pickWeapons.querySelectorAll('img').forEach(im =>
      im.classList.toggle('on', im.dataset.w === c.weapon));
  },

  /* ================= HUD ================= */
  syncHud(g){
    const p = g.player;
    if (!p) return;
    const hpK = clamp(p.hp / p.maxHp, 0, 1);
    this.el.hpFill.style.clipPath = `inset(0 ${(1-hpK)*100}% 0 0)`;
    this.el.hpText.textContent = `HP ${Math.ceil(p.hp)} / ${Math.round(p.maxHp)}`;

    const need = expNeed(g.level);
    const exK = clamp(g.exp / need, 0, 1);
    this.el.expFill.style.clipPath = `inset(0 ${(1-exK)*100}% 0 0)`;
    this.el.lvText.textContent = g.level;

    this.el.waveText.textContent = g.waveN;
    const left = Math.max(0, Math.ceil(g.wave.dur - g.waveT));
    this.el.waveLeft.textContent = g.bossAlive && !g.bossAlive.dead ? '· BOSS' : `· ${left}s`;

    const t = Math.floor(g.time);
    this.el.timeText.textContent =
      `${String(Math.floor(t/60)).padStart(2,'0')}:${String(t%60).padStart(2,'0')}`;
    this.el.killText.textContent = g.kills;
    this.el.diffText.textContent = '×' + g.wave.diff.toFixed(1);
    this.el.moneyText.textContent = g.money;

    this.el.hudWeapon.src = `../assets/item/${WEAPONS[p.cls.weapon].icon}.png`;
    const W = WEAPONS[p.cls.weapon];
    if (W.ammo){
      const k = clamp(p.ammo / p.maxAmmo, 0, 1);
      this.el.ammoFill.style.clipPath = `inset(0 ${(1-k)*100}% 0 0)`;
      this.el.ammoText.textContent = Math.floor(p.ammo);
    } else {
      this.el.ammoFill.style.clipPath = 'inset(0 0 0 0)';
      this.el.ammoText.textContent = '∞';
    }

    // 已获得的强化 → 技能槽（最多 6 格）
    const counts = new Map();
    for (const u of g.takenUpgrades) counts.set(u, (counts.get(u) || 0) + 1);
    const slots = [...counts.entries()].slice(0, 6);
    let html = '';
    for (let i = 0; i < 6; i++){
      if (i < slots.length){
        const [u, n] = slots[i];
        html += `<div class="skill-slot" title="${u.name}：${u.desc}">
                   <img src="../assets/ui/Icons/${u.icon}.png" alt="">
                   ${n > 1 ? `<span class="stk">${n}</span>` : ''}
                 </div>`;
      } else {
        html += `<div class="skill-slot empty"></div>`;
      }
    }
    if (this._slotSig !== html){ this.el.skillRow.innerHTML = html; this._slotSig = html; }
  },

  waveBanner(text, isBoss){
    this.el.waveText.textContent = text.replace(/[^0-9]/g,'') || this.el.waveText.textContent;
    this._banner = { text, isBoss, t: 2.2 };
  },

  /* ================= 升级三选一 ================= */
  openUpgrade(g){
    this.upOptions = [];
    if (g.pendingLevels > 1){
      // 一次升多级：连续弹多次，这里先给第一批
    }
    const chosen = [];
    for (let i = 0; i < 3; i++){
      const u = g.rng.weighted(UPGRADES, chosen);
      if (!u) break;
      chosen.push(u);
      this.upOptions.push(u);
    }
    this.upSel = 0;
    this.el.upLv.textContent = g.level;
    this.paintUpgrade();
    this.show('upgrade');
  },

  paintUpgrade(){
    const rarName = ['普通','稀有','史诗'];
    this.el.upCards.innerHTML = this.upOptions.map((u,i) => `
      <div class="up-card${i===this.upSel?' sel':''}" data-i="${i}">
        <div class="uc-icon"><img src="../assets/ui/Icons/${u.icon}.png" alt=""></div>
        <h4>${u.name}</h4>
        <p>${u.desc}</p>
        <div class="uc-rar rar-${u.rar}">${rarName[u.rar]}</div>
      </div>`).join('');
    this.el.upCards.querySelectorAll('.up-card').forEach(el => {
      el.onmouseenter = () => { this.upSel = +el.dataset.i; this.paintUpSel(); };
      el.onclick = () => { this.upSel = +el.dataset.i; this.confirmUpgrade(); };
    });
  },

  paintUpSel(){
    this.el.upCards.querySelectorAll('.up-card').forEach(el =>
      el.classList.toggle('sel', +el.dataset.i === this.upSel));
  },

  moveUpSel(d){
    const n = this.upOptions.length;
    if (!n) return;
    this.upSel = (this.upSel + d + n) % n;
    this.paintUpSel();
  },

  confirmUpgrade(){
    const u = this.upOptions[this.upSel];
    if (!u) return;
    Main.game.applyUpgrade(u);
    SFX.beep(660, 0.08, 'sine', .5);
    if (Main.game.state === 'levelup') this.openUpgrade(Main.game);
    else this.show('hud');
  },

  /* ================= 死亡结算 ================= */
  showOver(g){
    const p = g.player;
    this.el.overPortrait.innerHTML =
      `<img src="../assets/portrait/${p.cls.body}%20icon_no_bg.png" alt="">`;
    const t = Math.floor(g.time);
    this.el.ovTime.textContent = `${String(Math.floor(t/60)).padStart(2,'0')}:${String(t%60).padStart(2,'0')}`;
    this.el.ovKills.textContent = g.kills;
    this.el.ovWave.textContent = 'Wave ' + g.waveN;
    this.el.ovLv.textContent = 'LV ' + g.level;
    this.el.ovMoney.textContent = g.money;
    this.el.ovRank.textContent = this.rank(g);
    const counts = new Map();
    for (const u of g.takenUpgrades) counts.set(u, (counts.get(u)||0)+1);
    let html = '';
    for (const [u,n] of [...counts.entries()].slice(0,16))
      html += `<div class="loot-cell" title="${u.name} ×${n}"><img src="../assets/ui/Icons/${u.icon}.png" alt=""></div>`;
    for (let i = counts.size; i < 16; i++) html += '<div class="loot-cell"></div>';
    this.el.ovLoot.innerHTML = html;
    this.show('over');
    SFX.over();
  },

  rank(g){
    const s = g.time * 0.6 + g.kills * 0.35 + g.waveN * 14;
    if (s > 260) return 'S';
    if (s > 185) return 'A';
    if (s > 120) return 'B';
    if (s > 70)  return 'C';
    return 'D';
  },

  /* ================= 每帧 ================= */
  tick(dt, g){
    if (g.state === 'playing'){ this.hudAcc += dt; if (this.hudAcc > 1/20){ this.hudAcc = 0; this.syncHud(g); } }
    if (this._banner){
      this._banner.t -= dt;
      if (this._banner.t <= 0){ this._banner = null; this.el.waveLeft.classList.remove('flash'); }
    }
  },
};
