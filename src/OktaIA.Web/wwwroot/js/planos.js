(function () {
  // ---------- Tabs "Por porte / Por ativo / Por usuário" (troca preço pré-renderizado) ----------
  var modelTabs = document.querySelectorAll('[data-mk-model-tab]');
  modelTabs.forEach(function (tab) {
    tab.addEventListener('click', function () {
      var model = tab.getAttribute('data-mk-model-tab');
      modelTabs.forEach(function (t) { t.classList.toggle('mk-active', t === tab); });
      document.querySelectorAll('[data-price-model]').forEach(function (el) {
        el.style.display = el.getAttribute('data-price-model') === model ? '' : 'none';
      });
    });
  });

  // ---------- Simulador (calculadora por ativo) ----------
  var calc = document.getElementById('mk-calc');
  if (!calc) return;
  var plans = [];
  try { plans = JSON.parse(calc.getAttribute('data-mk-calc-plans') || '[]'); } catch (e) { plans = []; }
  if (!plans.length) return;

  var state = { plan: plans[1] ? plans[1].id : plans[0].id, ws: 120, srv: 8, fw: 2 };
  var rows = [
    { key: 'ws', step: 10 },
    { key: 'srv', step: 1 },
    { key: 'fw', step: 1 },
  ];

  function currentPlan() {
    for (var i = 0; i < plans.length; i++) if (plans[i].id === state.plan) return plans[i];
    return plans[0];
  }

  function fmt(n) { return n.toLocaleString('pt-BR'); }

  function render() {
    var pl = currentPlan();
    var total = 0;
    rows.forEach(function (r, i) {
      var rate = pl.rates[i];
      var val = state[r.key];
      var sub = rate * val;
      total += sub;
      var rateEl = document.getElementById('mk-rate-' + r.key);
      var valEl = document.getElementById('mk-val-' + r.key);
      var subEl = document.getElementById('mk-sub-' + r.key);
      if (rateEl) rateEl.textContent = 'R$ ' + rate + ' cada';
      if (valEl) valEl.textContent = val;
      if (subEl) subEl.textContent = 'R$ ' + fmt(sub);
    });
    var totalEl = document.getElementById('mk-calc-total');
    var yearEl = document.getElementById('mk-calc-year');
    var assetsEl = document.getElementById('mk-calc-assets');
    if (totalEl) totalEl.textContent = 'R$ ' + fmt(total);
    if (yearEl) yearEl.textContent = 'R$ ' + fmt(Math.round(total * 12 * 0.85));
    if (assetsEl) assetsEl.textContent = fmt(state.ws + state.srv + state.fw);

    document.querySelectorAll('[data-mk-calc-plan]').forEach(function (btn) {
      btn.classList.toggle('mk-active', btn.getAttribute('data-mk-calc-plan') === state.plan);
    });
  }

  document.querySelectorAll('[data-mk-calc-plan]').forEach(function (btn) {
    btn.addEventListener('click', function () {
      state.plan = btn.getAttribute('data-mk-calc-plan');
      render();
    });
  });

  rows.forEach(function (r) {
    var dec = calc.querySelector('[data-mk-dec="' + r.key + '"]');
    var inc = calc.querySelector('[data-mk-inc="' + r.key + '"]');
    if (dec) dec.addEventListener('click', function () { state[r.key] = Math.max(0, state[r.key] - r.step); render(); });
    if (inc) inc.addEventListener('click', function () { state[r.key] = state[r.key] + r.step; render(); });
  });

  render();
})();
