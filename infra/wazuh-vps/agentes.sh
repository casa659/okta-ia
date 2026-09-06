#!/bin/bash
# =============================================================================
#  Registro de agentes: fecha a porta de entrada e imprime os comandos.
#
#  ⚠️ O INSTALADOR DEIXA O REGISTRO ABERTO. `use_password: no` no bloco <auth>
#  significa que qualquer um que alcance a porta 1515 cadastra um agente no
#  manager — e a 1515 precisa ficar aberta para a internet, porque as máquinas
#  da empresa têm IP doméstico, que muda.
#
#  O estrago não é teórico: agente falso entra na lista, gera evento que ninguém
#  escreveu, e o SOC passa a mostrar alerta de uma máquina que não existe. Num
#  produto que vende "olhar tudo junto", é o pior tipo de dado.
#
#  Este script liga a senha de registro. Sem ela, a 1515 aberta é um convite.
#
#  RODA NO VPS, como root. Idempotente: rodar de novo troca a senha.
# =============================================================================
set -euo pipefail

CONF="/var/ossec/etc/ossec.conf"
ARQUIVO_SENHA="/var/ossec/etc/authd.pass"

titulo() { echo; echo "=============================================================="; echo "  $1"; echo "=============================================================="; }

[ "$(id -u)" -eq 0 ] || { echo "FALHA: rode como root."; exit 1; }
[ -f "$CONF" ] || { echo "FALHA: não achei $CONF — o manager está instalado?"; exit 1; }

titulo "1/3 · Ligando a senha de registro"

cp -n "$CONF" "${CONF}.antes-da-senha" 2>/dev/null || true

# `openssl rand`, e não pipe com head — ver o comentário em instalar-wazuh.sh.
SENHA_REGISTRO=$(openssl rand -hex 16)
echo -n "$SENHA_REGISTRO" > "$ARQUIVO_SENHA"
chmod 640 "$ARQUIVO_SENHA"
chown root:wazuh "$ARQUIVO_SENHA" 2>/dev/null || true

# Troca só a linha DENTRO do bloco <auth>. Um `sed` global trocaria qualquer
# outro `use_password` que exista no arquivo.
sed -i '/<auth>/,/<\/auth>/ s|<use_password>no</use_password>|<use_password>yes</use_password>|' "$CONF"

grep -q "<use_password>yes</use_password>" "$CONF" || {
  echo "FALHA: não consegui ligar use_password em $CONF. Edite à mão."
  exit 1
}

titulo "2/3 · Reiniciando o manager"
systemctl restart wazuh-manager
sleep 5
systemctl is-active --quiet wazuh-manager || { echo "FALHA: o manager não voltou."; exit 1; }

# Prova que a trava vale, pelo LOG DO PRÓPRIO MANAGER.
#
# ⚠️ NÃO use `/var/ossec/bin/agent-auth` para conferir: ele é binário do AGENTE
# e não existe num servidor. A primeira versão disto chamava esse caminho, o
# `timeout` respondia "No such file or directory", o `grep` não achava "password"
# e o script imprimia "ATENÇÃO: o registro sem senha NÃO foi recusado" — um
# instrumento que dava alarme falso sobre a própria trava que acabara de ligar.
#
# O que o log diz é o que aconteceu de verdade: antes, "No password required";
# depois, "Using password specified on file".
titulo "3/3 · Conferindo que a senha passou a valer"
sleep 2
if grep -q "Using password specified on file" /var/ossec/logs/ossec.log; then
  echo "OK — o authd subiu exigindo senha:"
  grep "Accepting connections on port 1515" /var/ossec/logs/ossec.log | tail -1
else
  echo "⚠️ ATENÇÃO: o authd não registrou que passou a exigir senha. Confira:"
  grep "Accepting connections on port 1515" /var/ossec/logs/ossec.log | tail -2
fi

IP=$(curl -s --max-time 10 https://api.ipify.org || echo "<ip>")
VERSAO=$(grep -oP 'WAZUH_VERSION="v\K[0-9.]+' /var/ossec/etc/ossec-init.conf 2>/dev/null \
         || /var/ossec/bin/wazuh-control info | grep -oP 'v\K[0-9.]+' | head -1)

titulo "COMANDOS PARA INSTALAR O AGENTE"
cat <<FIM

  Senha de registro (a mesma para todas as máquinas):
    ${SENHA_REGISTRO}
  Guardada em ${ARQUIVO_SENHA}

  ── WINDOWS (PowerShell como Administrador) ──────────────────────────────────

  Invoke-WebRequest -Uri https://packages.wazuh.com/4.x/windows/wazuh-agent-${VERSAO}-1.msi -OutFile \$env:TEMP\\wazuh-agent.msi
  msiexec.exe /i \$env:TEMP\\wazuh-agent.msi /q ^
    WAZUH_MANAGER='${IP}' ^
    WAZUH_REGISTRATION_SERVER='${IP}' ^
    WAZUH_REGISTRATION_PASSWORD='${SENHA_REGISTRO}' ^
    WAZUH_AGENT_NAME='NOME-DA-MAQUINA'
  NET START WazuhSvc

  ── LINUX (Ubuntu/Debian, como root) ─────────────────────────────────────────

  curl -sO https://packages.wazuh.com/4.x/apt/pool/main/w/wazuh-agent/wazuh-agent_${VERSAO}-1_amd64.deb
  WAZUH_MANAGER='${IP}' \\
  WAZUH_REGISTRATION_SERVER='${IP}' \\
  WAZUH_REGISTRATION_PASSWORD='${SENHA_REGISTRO}' \\
  WAZUH_AGENT_NAME='NOME-DA-MAQUINA' \\
    dpkg -i ./wazuh-agent_${VERSAO}-1_amd64.deb
  systemctl daemon-reload && systemctl enable --now wazuh-agent

  ⚠️ TROQUE 'NOME-DA-MAQUINA' em cada instalação. Dois agentes com o mesmo nome
     disputam o mesmo registro, e o alerta passa a sair com o nome errado — o
     SOC aponta para a máquina que não é.

  Conferir depois, aqui no servidor:
    /var/ossec/bin/agent_control -l

FIM
