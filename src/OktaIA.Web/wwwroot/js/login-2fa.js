(function () {
    var KEYS = ['1', '2', '3', '4', '5', '6', '7', '8', '9', '←', '0', '✓'];
    var cellsEl = document.getElementById('okmfa-cells');
    var keypadEl = document.getElementById('okmfa-keypad');
    var codeInput = document.getElementById('okmfa-code');
    var submitBtn = document.getElementById('okmfa-submit');
    var form = document.getElementById('okmfa-form');
    var code = '';

    function renderCells() {
        cellsEl.innerHTML = '';
        for (var i = 0; i < 6; i++) {
            var cell = document.createElement('div');
            cell.className = 'okmfa-cell' + (code.length === i ? ' okmfa-active' : '');
            cell.textContent = code[i] || '';
            cellsEl.appendChild(cell);
        }
        codeInput.value = code;
        submitBtn.disabled = code.length !== 6;
    }

    function shake() {
        cellsEl.classList.add('okmfa-shake');
        setTimeout(function () { cellsEl.classList.remove('okmfa-shake'); }, 300);
    }

    KEYS.forEach(function (k) {
        var btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'okmfa-key' + (k === '✓' ? ' okmfa-ok' : '');
        btn.textContent = k;
        btn.addEventListener('click', function () {
            if (k === '←') { code = code.slice(0, -1); renderCells(); }
            else if (k === '✓') { if (code.length === 6) form.submit(); else shake(); }
            else if (code.length < 6) { code += k; renderCells(); }
        });
        keypadEl.appendChild(btn);
    });

    document.addEventListener('keydown', function (ev) {
        if (/^[0-9]$/.test(ev.key) && code.length < 6) { code += ev.key; renderCells(); }
        else if (ev.key === 'Backspace') { code = code.slice(0, -1); renderCells(); }
        else if (ev.key === 'Enter' && code.length === 6) { form.submit(); }
    });

    renderCells();

    var recoveryToggle = document.getElementById('okmfa-recovery-toggle');
    var recoveryBox = document.getElementById('okmfa-recovery-box');
    recoveryToggle.addEventListener('click', function (ev) {
        ev.preventDefault();
        recoveryBox.style.display = 'block';
        recoveryToggle.style.display = 'none';
    });

    document.getElementById('okmfa-recovery-submit').addEventListener('click', function () {
        var recInput = document.getElementById('okmfa-recovery-input');
        document.querySelector('input[name="Input.UsarCodigoRecuperacao"]').value = 'true';
        codeInput.value = recInput.value;
        form.submit();
    });
})();
