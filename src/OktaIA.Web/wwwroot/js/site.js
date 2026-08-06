(function () {
  // ---------- Menu mobile (sidebar vira drawer em telas estreitas) ----------
  var menuBtn = document.querySelector('[data-oi-menu-toggle]');
  var sidebar = document.querySelector('[data-oi-sidebar]');
  if (menuBtn && sidebar) {
    menuBtn.addEventListener('click', function () {
      sidebar.classList.toggle('oi-open');
    });
    sidebar.querySelectorAll('a, button').forEach(function (el) {
      el.addEventListener('click', function () { sidebar.classList.remove('oi-open'); });
    });
  }

  // ---------- Seletor de organização (tenant) ----------
  var tenantOpenBtn = document.querySelector('[data-tenant-open]');
  var tenantMenu = document.querySelector('[data-tenant-menu]');
  if (tenantOpenBtn && tenantMenu) {
    tenantOpenBtn.addEventListener('click', function (ev) {
      ev.stopPropagation();
      tenantMenu.classList.toggle('oi-open');
    });
    document.addEventListener('click', function (ev) {
      if (!tenantMenu.contains(ev.target) && ev.target !== tenantOpenBtn) {
        tenantMenu.classList.remove('oi-open');
      }
    });
    tenantMenu.querySelectorAll('[data-tenant-id]').forEach(function (item) {
      item.addEventListener('click', function () {
        var id = item.getAttribute('data-tenant-id');
        document.cookie = 'okia_tenant=' + id + ';path=/;max-age=31536000;samesite=lax';
        // Todo o dado do console (Dashboard/Ativos/Vulnerabilidades/Incidentes/SIEM) é
        // renderizado no servidor a partir do cookie acima — precisa recarregar a página pra
        // realmente trocar de organização, não só o texto do botão.
        window.location.reload();
      });
    });
  }

  // ---------- Dialog genérico (Ativos · adicionar ativo real) ----------
  document.querySelectorAll('[data-adm-open-dialog]').forEach(function (btn) {
    btn.addEventListener('click', function () {
      var dialog = document.getElementById(btn.getAttribute('data-adm-open-dialog'));
      if (dialog) dialog.showModal();
    });
  });
  document.querySelectorAll('dialog').forEach(function (dialog) {
    if (dialog.getAttribute('data-oi-autoopen') === 'true') dialog.showModal();
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

  // ---------- Prefill do domínio ao escolher a empresa (Ativos · adicionar ativo real) ----------
  var empresaDominioSelect = document.querySelector('[data-oi-empresa-dominio-select]');
  var dominioInput = document.querySelector('[data-oi-dominio-input]');
  if (empresaDominioSelect && dominioInput) {
    var prefillDominio = function () {
      var opt = empresaDominioSelect.options[empresaDominioSelect.selectedIndex];
      var dominio = opt ? opt.getAttribute('data-dominio') : '';
      if (dominio) {
        dominioInput.value = dominio;
      }
    };
    // Troca de empresa sempre atualiza o campo (é o gatilho explícito pedido: escolher a
    // empresa já traz o domínio dela) — o operador ainda pode editar o valor depois.
    empresaDominioSelect.addEventListener('change', prefillDominio);
  }

  // ---------- Termo de Autorização em PDF (Ativos · adicionar ativo real) ----------
  // Gerado a partir do que está digitado no formulário — o ativo ainda não existe no banco
  // nesse ponto, então empresa/domínio vão soltos na URL, não como IDs de registro salvo.
  var termoBtn = document.querySelector('[data-oi-termo-autorizacao]');
  if (termoBtn && empresaDominioSelect && dominioInput) {
    termoBtn.addEventListener('click', function () {
      var empresaId = empresaDominioSelect.value;
      var dominio = encodeURIComponent(dominioInput.value || '');
      window.open('/Ativos?handler=TermoAutorizacao&empresaId=' + empresaId + '&dominio=' + dominio, '_blank');
    });
  }

  // ---------- Scan de ativo real: confirmação + estado "Escaneando..." (Ativos) ----------
  document.querySelectorAll('form[data-oi-scan-confirm]').forEach(function (form) {
    form.addEventListener('submit', function (ev) {
      if (!window.confirm(form.getAttribute('data-oi-scan-confirm'))) {
        ev.preventDefault();
        return;
      }
      var btn = form.querySelector('button[type="submit"]');
      if (btn) {
        btn.disabled = true;
        btn.textContent = btn.getAttribute('data-oi-scan-running-label') || btn.textContent;
      }
    });
  });

  // ---------- Filtro de empresa (auto-submit ao trocar) ----------
  document.querySelectorAll('[data-oi-autosubmit]').forEach(function (el) {
    el.addEventListener('change', function () {
      if (el.form) el.form.submit();
    });
  });

  // ---------- Vulnerabilidades: expandir achado real (recomendação + reverificar) ----------
  document.querySelectorAll('.oi-vuln-toggle').forEach(function (row) {
    row.addEventListener('click', function (ev) {
      if (ev.target.closest('form, button')) return; // não expande/recolhe ao clicar em Reverificar
      var item = row.closest('.oi-vuln-item');
      if (item) item.classList.toggle('oi-open');
    });
  });

  // ---------- Idioma (cookie simples, sem middleware de localização) ----------
  document.querySelectorAll('[data-lang-link]').forEach(function (el) {
    el.addEventListener('click', function (ev) {
      ev.preventDefault();
      var lang = el.getAttribute('data-lang-link');
      document.cookie = 'okia_lang=' + lang + ';path=/;max-age=31536000;samesite=lax';
      window.location.reload();
    });
  });

  // ---------- Copiloto de IA (painel lateral) ----------
  var aiHeaderBtn = document.querySelector('.oi-ai-btn');
  var aiPanel = document.getElementById('oi-copilot-panel');

  function toggleCopilot() {
    if (!aiPanel) return;
    aiPanel.classList.toggle('oi-hidden');
    if (aiHeaderBtn) aiHeaderBtn.classList.toggle('oi-active');
  }

  document.querySelectorAll('[data-ai-toggle]').forEach(function (el) {
    el.addEventListener('click', toggleCopilot);
  });

  var chat = document.getElementById('oi-copilot-chat');

  function addBubble(role, text, evidence) {
    if (!chat) return;
    var wrap = document.createElement('div');
    wrap.className = role === 'user' ? 'oi-msg-wrap-user' : 'oi-msg-wrap-ai';

    var bubble = document.createElement('div');
    bubble.className = role === 'user' ? 'oi-bubble oi-bubble-user' : 'oi-bubble oi-bubble-ai';
    bubble.textContent = text;
    wrap.appendChild(bubble);

    if (evidence && evidence.length) {
      var evWrap = document.createElement('div');
      evWrap.className = 'oi-evidence';
      evidence.forEach(function (e) {
        var row = document.createElement('div');
        row.className = 'oi-evidence-row';
        row.innerHTML =
          '<span class="oi-evidence-dot" style="background:' + e.Cor + '"></span>' +
          '<span class="oi-evidence-key"></span>' +
          '<span class="oi-evidence-val" style="color:' + e.Cor + '"></span>';
        row.querySelector('.oi-evidence-key').textContent = e.Chave;
        row.querySelector('.oi-evidence-val').textContent = e.Valor;
        evWrap.appendChild(row);
      });
      wrap.appendChild(evWrap);
    }

    chat.appendChild(wrap);
    chat.scrollTop = chat.scrollHeight;
  }

  function askQuestion(pergunta, resposta, evidenceJson) {
    if (aiPanel && aiPanel.classList.contains('oi-hidden')) toggleCopilot();
    addBubble('user', pergunta, null);

    var typing = document.createElement('div');
    typing.className = 'oi-typing';
    typing.innerHTML = '<span class="oi-spinner"></span><span style="font-size:10.5px;color:var(--oi-muted-2)"></span>';
    if (chat) {
      chat.appendChild(typing);
      chat.scrollTop = chat.scrollHeight;
    }

    setTimeout(function () {
      if (typing.parentNode) typing.parentNode.removeChild(typing);
      var evidence = [];
      try { evidence = JSON.parse(evidenceJson || '[]'); } catch (e) { evidence = []; }
      addBubble('ai', resposta, evidence);
    }, 1100);
  }

  document.querySelectorAll('[data-ask-question]').forEach(function (btn) {
    btn.addEventListener('click', function () {
      askQuestion(btn.getAttribute('data-question'), btn.getAttribute('data-answer'), btn.getAttribute('data-evidence'));
    });
  });

  var input = document.getElementById('oi-copilot-input');
  if (input) {
    input.addEventListener('keydown', function (ev) {
      if (ev.key !== 'Enter' || !input.value.trim()) return;
      var pergunta = input.value.trim();
      input.value = '';
      // Sem LLM real nesta fase — cai numa resposta padrão sugerindo as perguntas prontas.
      askQuestion(pergunta,
        document.documentElement.lang === 'en'
          ? 'I don’t have a specific answer for that yet — try one of the suggested questions below.'
          : 'Ainda não tenho uma resposta específica pra isso — tente uma das perguntas sugeridas abaixo.',
        '[]');
    });
  }
})();
