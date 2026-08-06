(function () {
  // ---------- Menu mobile ----------
  var menuBtn = document.querySelector('[data-mk-menu-toggle]');
  var mobileMenu = document.querySelector('[data-mk-mobile-menu]');
  if (menuBtn && mobileMenu) {
    menuBtn.addEventListener('click', function () {
      mobileMenu.classList.toggle('mk-open');
    });
  }

  // ---------- FAQ accordion (Planos/Contato) ----------
  document.querySelectorAll('[data-mk-faq]').forEach(function (item) {
    var q = item.querySelector('[data-mk-faq-q]');
    if (!q) return;
    q.addEventListener('click', function () {
      var wasOpen = item.classList.contains('mk-open');
      item.parentNode.querySelectorAll('[data-mk-faq]').forEach(function (i) { i.classList.remove('mk-open'); });
      if (!wasOpen) item.classList.add('mk-open');
    });
  });

  // ---------- Contadores animados (estatísticas) ----------
  var counters = document.querySelectorAll('[data-mk-counter]');
  if (counters.length && 'IntersectionObserver' in window) {
    var obs = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (!entry.isIntersecting) return;
        var el = entry.target;
        obs.unobserve(el);
        var target = parseFloat(el.getAttribute('data-mk-counter'));
        var decimals = (el.getAttribute('data-mk-decimals') || '0') | 0;
        var suffix = el.getAttribute('data-mk-suffix') || '';
        var duration = 1100;
        var start = performance.now();
        function tick(now) {
          var p = Math.min(1, (now - start) / duration);
          var eased = 1 - Math.pow(1 - p, 3);
          el.textContent = (target * eased).toFixed(decimals) + suffix;
          if (p < 1) requestAnimationFrame(tick);
        }
        requestAnimationFrame(tick);
      });
    }, { threshold: 0.4 });
    counters.forEach(function (el) { obs.observe(el); });
  }

  // ---------- Compartilhar (modal, todas as páginas) ----------
  var shareBackdrop = document.querySelector('[data-mk-share-backdrop]');
  if (shareBackdrop) {
    var shareOpenBtns = document.querySelectorAll('[data-mk-share-open]');
    var shareCloseBtn = shareBackdrop.querySelector('[data-mk-share-close]');
    var sharePanel = shareBackdrop.querySelector('[data-mk-share-panel]');
    var shareUrlEl = shareBackdrop.querySelector('[data-mk-share-url]');
    var shareCopyBtn = shareBackdrop.querySelector('[data-mk-share-copy]');
    var shareNativeBtn = shareBackdrop.querySelector('[data-mk-share-native]');
    var shareChannelsEl = shareBackdrop.querySelector('[data-mk-share-channels]');
    var shareMsg = 'Conheça a L\'okta IA: SOC as a Service (MSSP) com monitoramento, resposta, relatórios, consultoria e gestão de segurança, agregando tecnologias de parceiros. Assessment gratuito em 48 horas.';

    function openShare() {
      var url = location.href;
      if (shareUrlEl) shareUrlEl.textContent = url.replace(/^https?:\/\//, '');
      buildChannels(url);
      shareBackdrop.classList.add('mk-open');
    }
    function closeShare() { shareBackdrop.classList.remove('mk-open'); }

    shareOpenBtns.forEach(function (btn) { btn.addEventListener('click', openShare); });
    if (shareCloseBtn) shareCloseBtn.addEventListener('click', closeShare);
    shareBackdrop.addEventListener('click', closeShare);
    if (sharePanel) sharePanel.addEventListener('click', function (ev) { ev.stopPropagation(); });
    document.addEventListener('keydown', function (ev) {
      if (ev.key === 'Escape') closeShare();
    });

    if (shareNativeBtn) {
      if (typeof navigator !== 'undefined' && navigator.share) {
        shareNativeBtn.classList.add('mk-show');
        shareNativeBtn.addEventListener('click', function () {
          navigator.share({ title: "L'okta IA · Cyber Security & AI", text: shareMsg, url: location.href }).catch(function () {});
        });
      }
    }

    if (shareCopyBtn) {
      shareCopyBtn.addEventListener('click', function () {
        var url = location.href;
        if (navigator.clipboard) { navigator.clipboard.writeText(url).catch(function () {}); }
        shareCopyBtn.textContent = 'Copiado';
        shareCopyBtn.classList.add('mk-copied');
        clearTimeout(shareCopyBtn._t);
        shareCopyBtn._t = setTimeout(function () {
          shareCopyBtn.textContent = 'Copiar';
          shareCopyBtn.classList.remove('mk-copied');
        }, 2200);
      });
    }

    function buildChannels(url) {
      if (!shareChannelsEl) return;
      var u = encodeURIComponent(url);
      var t = encodeURIComponent(shareMsg);
      var targets = [
        { n: 'WhatsApp', c: '#25D366', href: 'https://wa.me/?text=' + t + '%20' + u },
        { n: 'LinkedIn', c: '#0A66C2', href: 'https://www.linkedin.com/sharing/share-offsite/?url=' + u },
        { n: 'X', c: '#C4D3E6', href: 'https://twitter.com/intent/tweet?text=' + t + '&url=' + u },
        { n: 'Telegram', c: '#2AABEE', href: 'https://t.me/share/url?url=' + u + '&text=' + t },
        { n: 'E-mail', c: '#FF8A3D', href: 'mailto:?subject=' + encodeURIComponent("L'okta IA · Cyber Security & AI") + '&body=' + t + '%0A%0A' + u },
        { n: 'Teams', c: '#6264A7', href: 'https://teams.microsoft.com/share?href=' + u + '&msgText=' + t }
      ];
      shareChannelsEl.innerHTML = '';
      targets.forEach(function (x) {
        var a = document.createElement('a');
        a.className = 'mk-share-channel';
        a.href = x.href;
        a.target = '_blank';
        a.rel = 'noopener noreferrer';
        a.innerHTML = '<span class="mk-share-channel-dot" style="background:' + x.c + ';box-shadow:0 0 10px ' + x.c + '66;"></span><span class="mk-share-channel-name"></span>';
        a.querySelector('.mk-share-channel-name').textContent = x.n;
        shareChannelsEl.appendChild(a);
      });
    }
  }

  // ---------- Dialog genérico (Contato · gerenciar canais, Admin) ----------
  document.querySelectorAll('[data-adm-open-dialog]').forEach(function (btn) {
    btn.addEventListener('click', function () {
      var dialog = document.getElementById(btn.getAttribute('data-adm-open-dialog'));
      if (dialog) dialog.showModal();
    });
  });
  document.querySelectorAll('dialog').forEach(function (dialog) {
    dialog.querySelectorAll('[data-adm-dialog-close]').forEach(function (btn) {
      btn.addEventListener('click', function () { dialog.close(); });
    });
    dialog.addEventListener('click', function (ev) {
      if (ev.target === dialog) dialog.close();
    });
  });
  document.querySelectorAll('form[data-adm-confirm]').forEach(function (form) {
    form.addEventListener('submit', function (ev) {
      if (!window.confirm(form.getAttribute('data-adm-confirm'))) {
        ev.preventDefault();
      }
    });
  });

  // ---------- Calculadora de preço (Planos) ----------
  var calcInput = document.getElementById('mk-calc-ativos');
  if (calcInput) {
    var calcOut = document.getElementById('mk-calc-out');
    var base = parseFloat(calcInput.getAttribute('data-mk-base') || '0');
    var perAtivo = parseFloat(calcInput.getAttribute('data-mk-per-ativo') || '0');
    function recalc() {
      var n = Math.max(1, parseInt(calcInput.value || '1', 10));
      var total = base + n * perAtivo;
      if (calcOut) calcOut.textContent = total.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL', maximumFractionDigits: 0 });
      var label = document.getElementById('mk-calc-ativos-val');
      if (label) label.textContent = n;
    }
    calcInput.addEventListener('input', recalc);
    recalc();
  }
})();
