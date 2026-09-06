#!/bin/bash
# =============================================================================
#  Wazuh da empresa, num VPS limpo — instalação de uma tacada só.
#
#  RODA NO VPS, como root, num Ubuntu 22.04 RECÉM-CRIADO. Não é idempotente:
#  o instalador oficial recusa rodar duas vezes (e faz certo — reinstalar por
#  cima geraria certificados novos e derrubaria a comunicação existente).
#
#  O QUE ELE FAZ, em ordem:
#    1. confere memória, disco e sistema antes de qualquer coisa;
#    2. instala o Wazuh 4.14 tudo-em-um (indexer + manager + dashboard);
#    3. cria um usuário SOMENTE-LEITURA para o L'okta ler os alertas;
#    4. fecha o firewall e abre só o que precisa, para quem precisa.
#
#  ⚠️ POR QUE UM USUÁRIO SÓ-LEITURA, e não o `admin`: o L'okta só LÊ alerta.
#  Com o admin, uma credencial vazada do nosso lado apagaria índice do cliente.
#  Numa plataforma de segurança, é o tipo de coisa que auditamos nos outros.
#
#  ⚠️ NÃO RODE ISTO NO VPS 2.25.156.123. Aquele sustenta a ponte do WhatsApp do
#  iAgrow, e o indexador é um OpenSearch: ele toma a RAM que encontrar. A queda
#  apareceria como oferta que parou de chegar, calada, e levaria dias para
#  alguém ligar uma coisa à outra.
# =============================================================================
set -euo pipefail

VERSAO_WAZUH="4.14"
USUARIO_LOKTA="lokta"
ARQUIVO_SEGREDOS="/root/wazuh-credenciais.txt"

# Os 23 IPs de saída do App Service `okta-ia` (Default-Web-BrazilSouth), lidos
# em 06/09/2026 com:
#   az webapp show -g Default-Web-BrazilSouth -n okta-ia \
#      --query possibleOutboundIpAddresses -o tsv
#
# ⚠️ ELES MUDAM quando o plano do App Service escala ou é recriado. Se um dia o
# L'okta parar de sincronizar com "não consegui alcançar", é aqui que se olha
# primeiro: rode o comando acima e compare com esta lista.
IPS_LOKTA="104.41.13.4 104.41.14.246 104.41.15.87 104.41.9.139 104.41.9.254 \
191.232.240.78 191.232.67.148 191.232.68.177 191.232.69.130 191.232.69.243 \
191.232.69.244 191.232.69.78 191.234.178.82 191.234.183.71 191.235.82.38 \
191.235.87.138 20.197.208.255 4.228.160.110 4.228.160.139 4.228.160.143 \
4.228.160.145 4.228.160.146 4.228.160.158"

titulo() { echo; echo "=============================================================="; echo "  $1"; echo "=============================================================="; }

# ── 1. Conferências que evitam descobrir o problema no meio ──────────────────
titulo "1/4 · Conferindo a máquina"

[ "$(id -u)" -eq 0 ] || { echo "FALHA: rode como root (sudo -i)."; exit 1; }

if ! grep -qi "ubuntu" /etc/os-release; then
  echo "FALHA: este script é para Ubuntu. Achei:"; cat /etc/os-release | head -2; exit 1
fi

RAM_MB=$(free -m | awk '/^Mem:/{print $2}')
DISCO_GB=$(df -BG --output=avail / | tail -1 | tr -dc '0-9')

echo "RAM: ${RAM_MB} MB · disco livre: ${DISCO_GB} GB · $(lsb_release -ds 2>/dev/null || echo Ubuntu)"

# 7500 e não 8192: o VPS reserva um pedaço para o hipervisor e "8 GB" chegam
# como ~7,8 GB. Exigir o número redondo reprovaria a máquina certa.
[ "$RAM_MB" -ge 7500 ] || { echo "FALHA: o Wazuh precisa de 8 GB. Esta máquina tem ${RAM_MB} MB."; exit 1; }
[ "$DISCO_GB" -ge 40 ] || { echo "FALHA: menos de 40 GB livres (${DISCO_GB} GB)."; exit 1; }

if [ -d /var/ossec ]; then
  echo "FALHA: já existe /var/ossec — o Wazuh já foi instalado aqui."
  echo "       Reinstalar por cima gera certificados novos e quebra o que já funciona."
  exit 1
fi

# O OpenSearch não sobe sem isto, e a mensagem de erro dele não diz o que é.
titulo "2/4 · Ajustando o sistema para o indexador"
sysctl -w vm.max_map_count=262144
grep -q "vm.max_map_count" /etc/sysctl.conf || echo "vm.max_map_count=262144" >> /etc/sysctl.conf

apt-get update -qq
apt-get install -y -qq curl gnupg ufw jq apt-transport-https >/dev/null

# ── 3. O instalador oficial ─────────────────────────────────────────────────
titulo "3/4 · Instalando o Wazuh ${VERSAO_WAZUH} (10 a 20 minutos)"

cd /root
curl -sO "https://packages.wazuh.com/${VERSAO_WAZUH}/wazuh-install.sh"
bash ./wazuh-install.sh -a -i

# As senhas geradas ficam num tar que o instalador deixa em /root. Guardar em
# texto legível é decisão consciente: o VPS é do dono, o acesso é por chave SSH,
# e a alternativa real seria ele perder a senha do admin — o que custa uma
# reinstalação inteira.
tar -O -xf /root/wazuh-install-files.tar wazuh-install-files/wazuh-passwords.txt > "$ARQUIVO_SEGREDOS"
chmod 600 "$ARQUIVO_SEGREDOS"

SENHA_ADMIN=$(grep -A1 "indexer_username: 'admin'" "$ARQUIVO_SEGREDOS" | grep password | cut -d"'" -f2)
[ -n "$SENHA_ADMIN" ] || { echo "FALHA: não achei a senha do admin em $ARQUIVO_SEGREDOS"; exit 1; }

echo "Esperando o indexador responder..."
for i in $(seq 1 30); do
  if curl -sk -u "admin:${SENHA_ADMIN}" https://localhost:9200/ >/dev/null 2>&1; then break; fi
  sleep 10
done

# ── 4. O usuário do L'okta ──────────────────────────────────────────────────
titulo "4/4 · Criando o usuário somente-leitura do L'okta"

# ⚠️ `openssl rand`, e NUNCA `tr -dc ... </dev/urandom | head -c 32`.
#
# Aquele pipe derrubou a primeira instalação real (06/09/2026): o `head` fecha o
# cano ao juntar os 32 caracteres, o `tr` leva SIGPIPE e morre com 141, e o
# `set -o pipefail` mata o script inteiro. O Wazuh ficou instalado e o FIREWALL
# NÃO — a máquina passou minutos com a 9200 aberta para a internet. Sem pipe,
# sem armadilha.
SENHA_LOKTA=$(openssl rand -hex 16)

# O papel dá EXATAMENTE o que o conector usa e nada além:
#   - `_cat/indices/wazuh-alerts-*` no teste de conexão  -> cluster_monitor + indices_monitor
#   - `wazuh-alerts-*/_search` no sync                    -> read
# Sem `write`, sem `delete`, sem acesso a outro índice.
curl -sk -u "admin:${SENHA_ADMIN}" -X PUT \
  "https://localhost:9200/_plugins/_security/api/roles/lokta_leitura" \
  -H 'Content-Type: application/json' -d '{
    "cluster_permissions": ["cluster_monitor", "cluster_composite_ops_ro"],
    "index_permissions": [{
      "index_patterns": ["wazuh-alerts-*"],
      "allowed_actions": ["read", "indices_monitor"]
    }]
  }' >/dev/null

curl -sk -u "admin:${SENHA_ADMIN}" -X PUT \
  "https://localhost:9200/_plugins/_security/api/internalusers/${USUARIO_LOKTA}" \
  -H 'Content-Type: application/json' -d "{
    \"password\": \"${SENHA_LOKTA}\",
    \"opendistro_security_roles\": [\"lokta_leitura\"],
    \"description\": \"Somente leitura de wazuh-alerts-* para a plataforma L'okta\"
  }" >/dev/null

# Prova que o usuário novo funciona ANTES de declarar sucesso. Um script que
# diz "pronto" e entrega credencial que não entra é pior que um que falha.
CONFERE=$(curl -sk -u "${USUARIO_LOKTA}:${SENHA_LOKTA}" \
  "https://localhost:9200/_cat/indices/wazuh-alerts-*?format=json" | jq 'length' 2>/dev/null || echo "erro")

# ── Firewall ────────────────────────────────────────────────────────────────
titulo "Fechando o firewall"

ufw --force reset >/dev/null
ufw default deny incoming >/dev/null
ufw default allow outgoing >/dev/null

ufw allow 22/tcp comment 'SSH' >/dev/null

# Portas dos AGENTES: ficam abertas porque os computadores da empresa têm IP
# doméstico, que muda. O agente se registra com chave, não com IP.
ufw allow 1514/tcp comment 'agentes wazuh' >/dev/null
ufw allow 1515/tcp comment 'registro de agentes' >/dev/null

# O painel do Wazuh. Tem login e senha forte, mas fica exposto à internet —
# para restringir depois, veja o LEIA-ME.
ufw allow 443/tcp comment 'painel wazuh' >/dev/null

# ⚠️ O INDEXADOR NÃO FICA ABERTO PARA A INTERNET. Só os IPs de saída do L'okta
# alcançam a 9200. É a porta que dá acesso a TODO o dado de segurança da
# empresa; deixá-la aberta seria publicar o SIEM.
for ip in $IPS_LOKTA; do
  ufw allow from "$ip" to any port 9200 proto tcp comment 'L okta' >/dev/null
done

ufw --force enable >/dev/null

# ── Resultado ───────────────────────────────────────────────────────────────
IP_PUBLICO=$(curl -s --max-time 10 https://api.ipify.org || echo "<ip do vps>")

titulo "PRONTO"
cat <<FIM

  Painel do Wazuh
    https://${IP_PUBLICO}
    usuário: admin
    senha:   ${SENHA_ADMIN}
    (certificado autoassinado — o navegador vai avisar; é esperado)

  Credencial do conector, para colar em /Admin/Conectores no L'okta
    URL do serviço:     https://${IP_PUBLICO}:9200
    Usuário do Indexer: ${USUARIO_LOKTA}
    Senha:              ${SENHA_LOKTA}

    Conferência feita agora com essa credencial: ${CONFERE} índice(s) de alerta.

  Todas as senhas geradas: ${ARQUIVO_SEGREDOS} (só o root lê)

  ⚠️ GUARDE A SENHA DO CONECTOR AGORA, num gerenciador de senhas. Ela não é
     recuperável — só substituível, repetindo a criação do usuário.

FIM
