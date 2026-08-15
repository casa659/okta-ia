// Wizard do diagnóstico: mostra/esconde perguntas condicionais e pinta a opção escolhida.
//
// Em arquivo externo porque a CSP do projeto é `script-src 'self'` — script inline é bloqueado em
// silêncio, e foi exatamente assim que o teclado do MFA deixou de funcionar uma vez.
//
// O servidor continua sendo a autoridade: `CalculadoraDoDiagnostico.Visivel` decide o que entra no
// cálculo. Isto aqui é só para o consultor não precisar salvar a página para ver a próxima pergunta
// aparecer no meio de uma reunião.
(function () {
    'use strict';

    var form = document.querySelector('[data-diag-form]');
    if (!form) { return; }

    var cores = {
        sim: '#00E0A4',
        parcial: '#F5D547',
        nao: '#FF3B5C',
        naosei: '#7A8FAB'
    };

    function valorDe(codigo) {
        var marcado = form.querySelector('input[name="opcao[' + codigo + ']"]:checked');
        if (marcado) { return marcado.value; }
        var select = form.querySelector('select[name="opcao[' + codigo + ']"]');
        return select ? select.value : null;
    }

    function aplicarCondicoes() {
        var blocos = form.querySelectorAll('[data-diag-pergunta][data-cond-de]');
        for (var i = 0; i < blocos.length; i++) {
            var bloco = blocos[i];
            var atual = valorDe(bloco.getAttribute('data-cond-de'));
            var aceitos = (bloco.getAttribute('data-cond-valores') || '').split(',');
            bloco.style.display = (atual && aceitos.indexOf(atual) !== -1) ? '' : 'none';
        }
    }

    function pintar(input) {
        var grupo = form.querySelectorAll('input[name="' + input.name + '"]');
        for (var i = 0; i < grupo.length; i++) {
            var pill = grupo[i].nextElementSibling;
            if (!pill) { continue; }
            var cor = cores[grupo[i].value] || '#7A8FAB';
            var ativo = grupo[i].checked;
            pill.style.borderColor = ativo ? cor : 'rgba(120,170,255,.14)';
            pill.style.background = ativo ? cor + '1F' : 'transparent';
            pill.style.color = ativo ? cor : '#8FA3BC';
        }
    }

    form.addEventListener('change', function (e) {
        var alvo = e.target;
        if (!alvo || !alvo.hasAttribute) { return; }
        if (alvo.type === 'radio' && alvo.name.indexOf('opcao[') === 0) { pintar(alvo); }
        if (alvo.hasAttribute('data-diag-opcao')) { aplicarCondicoes(); }
    });

    aplicarCondicoes();
})();
