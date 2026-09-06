// Escolher uma empresa já cadastrada preenche o que a plataforma já sabe sobre ela.
//
// POR QUE EXISTE: o L'okta já conhece o CNPJ, o domínio e quantos ativos a empresa tem. Fazer
// quem monta o orçamento digitar isso de novo é pedir para errar — e um CNPJ digitado errado numa
// proposta vira contrato com CNPJ errado.
//
// ⚠️ Arquivo EXTERNO porque a CSP do projeto é `script-src 'self'`: script inline é bloqueado em
// silêncio, e o campo simplesmente não preencheria, sem erro visível.
(function () {
  'use strict';

  var seletor = document.querySelector('[data-orc-empresa]');
  if (!seletor) { return; }

  var campoId = document.querySelector('[data-orc-company-id]');
  var campoNome = document.querySelector('[data-orc-nome]');
  var campoCnpj = document.querySelector('[data-orc-cnpj]');
  var dica = document.querySelector('[data-orc-dica]');

  // ⚠️ SÓ PREENCHE O QUE ESTÁ VAZIO, e é a regra inteira desta função.
  //
  // Quem já digitou o nome do contato e depois escolheu a empresa na lista não pode ver o que
  // escreveu ser apagado. Trocar de empresa numa proposta pela metade é o caso em que isso dói:
  // o campo some sem ninguém ter mandado apagar.
  function preencher(campo, valor) {
    if (!campo || !valor) { return; }
    if (campo.value && campo.value.trim().length > 0) { return; }
    campo.value = valor;
  }

  seletor.addEventListener('change', function () {
    var op = seletor.options[seletor.selectedIndex];
    if (!op || !op.value) {
      // "— nova empresa —": desliga o vínculo. Sem isto, o orçamento de um prospecto ficaria
      // pendurado na empresa que estava escolhida antes.
      if (campoId) { campoId.value = ''; }
      if (dica) { dica.textContent = ''; }
      return;
    }

    if (campoId) { campoId.value = op.value; }

    // O NOME é o único que sobrescreve: ele é o próprio seletor. Deixar o texto antigo ao lado de
    // uma empresa escolhida faria a proposta sair com um nome e o vínculo com outro.
    if (campoNome) { campoNome.value = op.getAttribute('data-nome') || ''; }

    preencher(campoCnpj, op.getAttribute('data-cnpj'));

    // O número de ativos NÃO preenche as caixas do parque: ele é contagem do inventário da
    // plataforma, que mistura estação, servidor e equipamento de rede. Preenchê-lo daria um preço
    // com cara de medido a partir de um palpite. Fica como DICA, para a pessoa decidir.
    if (dica) {
      var ativos = op.getAttribute('data-ativos');
      var dominio = op.getAttribute('data-dominio');
      var partes = [];
      if (ativos && ativos !== '0') { partes.push(ativos + ' ativo(s) no inventário da plataforma'); }
      if (dominio) { partes.push(dominio); }
      dica.textContent = partes.length ? '· ' + partes.join(' · ') : '';
    }
  });
})();
