(function () {
  var menuBtn = document.querySelector('[data-adm-menu-toggle]');
  var sidebar = document.querySelector('[data-adm-sidebar]');
  if (menuBtn && sidebar) {
    menuBtn.addEventListener('click', function () {
      sidebar.classList.toggle('adm-open');
    });
  }

  document.querySelectorAll('[data-adm-reveal]').forEach(function (btn) {
    btn.addEventListener('click', function () {
      var target = document.getElementById(btn.getAttribute('data-adm-reveal'));
      if (target) target.style.display = 'block';
      btn.style.display = 'none';
    });
  });

  // ---------- Confirmação em ações destrutivas (excluir/desativar) ----------
  document.querySelectorAll('form[data-adm-confirm]').forEach(function (form) {
    form.addEventListener('submit', function (ev) {
      if (!window.confirm(form.getAttribute('data-adm-confirm'))) {
        ev.preventDefault();
      }
    });
  });

  // ---------- Editar usuário (dialog nativo) ----------
  var dialogEditarUsuario = document.getElementById('adm-dialog-editar-usuario');
  if (dialogEditarUsuario) {
    document.querySelectorAll('[data-adm-editar-usuario]').forEach(function (btn) {
      btn.addEventListener('click', function () {
        dialogEditarUsuario.querySelector('[name="EditInput.Id"]').value = btn.getAttribute('data-id') || '';
        dialogEditarUsuario.querySelector('[name="EditInput.NomeCompleto"]').value = btn.getAttribute('data-nome') || '';
        dialogEditarUsuario.querySelector('[name="EditInput.Email"]').value = btn.getAttribute('data-email') || '';
        var papeisSelecionados = (btn.getAttribute('data-papeis') || '').split(',').filter(Boolean);
        dialogEditarUsuario.querySelectorAll('[name="EditInput.Papeis"]').forEach(function (cb) {
          cb.checked = papeisSelecionados.indexOf(cb.value) !== -1;
        });
        dialogEditarUsuario.showModal();
      });
    });
    dialogEditarUsuario.querySelectorAll('[data-adm-dialog-close]').forEach(function (btn) {
      btn.addEventListener('click', function () { dialogEditarUsuario.close(); });
    });
    dialogEditarUsuario.addEventListener('click', function (ev) {
      if (ev.target === dialogEditarUsuario) dialogEditarUsuario.close();
    });
  }

  // ---------- Editar empresa (dialog nativo) ----------
  var dialogEditarEmpresa = document.getElementById('adm-dialog-editar-empresa');
  if (dialogEditarEmpresa) {
    document.querySelectorAll('[data-adm-editar-empresa]').forEach(function (btn) {
      btn.addEventListener('click', function () {
        dialogEditarEmpresa.querySelector('[name="EditInput.Id"]').value = btn.getAttribute('data-id') || '';
        dialogEditarEmpresa.querySelector('[name="EditInput.Nome"]').value = btn.getAttribute('data-nome') || '';
        dialogEditarEmpresa.querySelector('[name="EditInput.Setor"]').value = btn.getAttribute('data-setor') || '';
        dialogEditarEmpresa.querySelector('[name="EditInput.Plano"]').value = btn.getAttribute('data-plano') || 'Business';
        dialogEditarEmpresa.querySelector('[name="EditInput.Cnpj"]').value = btn.getAttribute('data-cnpj') || '';
        dialogEditarEmpresa.querySelector('[name="EditInput.Dominio"]').value = btn.getAttribute('data-dominio') || '';
        dialogEditarEmpresa.showModal();
      });
    });
    dialogEditarEmpresa.querySelectorAll('[data-adm-dialog-close]').forEach(function (btn) {
      btn.addEventListener('click', function () { dialogEditarEmpresa.close(); });
    });
    dialogEditarEmpresa.addEventListener('click', function (ev) {
      if (ev.target === dialogEditarEmpresa) dialogEditarEmpresa.close();
    });
  }

  // ---------- Editar perfil (dialog nativo) ----------
  var dialogEditarPerfil = document.getElementById('adm-dialog-editar-perfil');
  if (dialogEditarPerfil) {
    document.querySelectorAll('[data-adm-editar-perfil]').forEach(function (btn) {
      btn.addEventListener('click', function () {
        dialogEditarPerfil.querySelector('[name="EditInput.Id"]').value = btn.getAttribute('data-id') || '';
        dialogEditarPerfil.querySelector('[name="EditInput.Nome"]').value = btn.getAttribute('data-nome') || '';
        dialogEditarPerfil.showModal();
      });
    });
    dialogEditarPerfil.querySelectorAll('[data-adm-dialog-close]').forEach(function (btn) {
      btn.addEventListener('click', function () { dialogEditarPerfil.close(); });
    });
    dialogEditarPerfil.addEventListener('click', function (ev) {
      if (ev.target === dialogEditarPerfil) dialogEditarPerfil.close();
    });
  }

  // ---------- Máscara de CNPJ (00.000.000/0000-00) ----------
  document.querySelectorAll('[data-cnpj-mask]').forEach(function (input) {
    input.addEventListener('input', function () {
      var d = input.value.replace(/\D/g, '').slice(0, 14);
      var out = d;
      if (d.length > 12) out = d.replace(/^(\d{2})(\d{3})(\d{3})(\d{4})(\d{0,2})/, '$1.$2.$3/$4-$5');
      else if (d.length > 8) out = d.replace(/^(\d{2})(\d{3})(\d{3})(\d{0,4})/, '$1.$2.$3/$4');
      else if (d.length > 5) out = d.replace(/^(\d{2})(\d{3})(\d{0,3})/, '$1.$2.$3');
      else if (d.length > 2) out = d.replace(/^(\d{2})(\d{0,3})/, '$1.$2');
      input.value = out;
    });
  });
})();
