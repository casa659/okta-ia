#!/bin/bash
# =============================================================================
#  O que vem DEPOIS do Wazuh instalado: o usuário do L'okta e o firewall.
#
#  ⚠️ EXISTE SEPARADO PORQUE O INSTALADOR NÃO SE REPETE. O `wazuh-install.sh`
#  recusa rodar duas vezes, e faz certo — reinstalar por cima geraria
#  certificados novos e quebraria o que já funciona. Então quando esta parte
#  falha (foi o que aconteceu em 06/09/2026, um SIGPIPE na geração da senha),
#  não dá para "rodar tudo de novo": tem de haver por onde continuar.
#
#  ⚠️ ESTE, SIM, É IDEMPOTENTE. Rodar duas vezes só troca a senha do usuário do
#  L'okta e reaplica as mesmas regras de firewall.
# =============================================================================
set -euo pipefail

USUARIO_LOKTA="lokta"
ARQUIVO_SEGREDOS="/root/wazuh-credenciais.txt"

IPS_LOKTA="104.41.13.4 104.41.14.246 104.41.15.87 104.41.9.139 104.41.9.254 \
191.232.240.78 191.232.67.148 191.232.68.177 191.232.69.130 191.232.69.243 \
191.232.69.244 191.232.69.78 191.234.178.82 191.234.183.71 191.235.82.38 \
191.235.87.138 20.197.208.255 4.228.160.110 4.228.160.139 4.228.160.143 \
4.228.160.145 4.228.160.146 4.228.160.158"

titulo() { echo; echo "=============================================================="; echo "  $1"; echo "=============================================================="; }

[ "$(id -u)" -eq 0 ] || { echo "FALHA: rode como root."; exit 1; }
command -v jq >/dev/null || apt-get install -y -qq jq >/dev/null

# A senha do admin sai do arquivo que o instalador deixou. Se ela não estiver
# lá, não há o que fazer aqui — e adivinhar seria pior.
[ -f "$ARQUIVO_SEGREDOS" ] || {
  echo "FALHA: $ARQUIVO_SEGREDOS não existe. Extraia com:"
  echo "  tar -O -xf /root/wazuh-install-files.tar wazuh-install-files/wazuh-passwords.txt > $ARQUIVO_SEGREDOS"
  exit 1
}

SENHA_ADMIN=$(grep -A1 "indexer_username: 'admin'" "$ARQUIVO_SEGREDOS" | grep password | cut -d"'" -f2)
[ -n "$SENHA_ADMIN" ] || { echo "FALHA: não achei a senha do admin em $ARQUIVO_SEGREDOS"; exit 1; }

titulo "1/2 · Usuário somente-leitura do L'okta"

echo "Conferindo o indexador..."
curl -sk -u "admin:${SENHA_ADMIN}" https://localhost:9200/ >/dev/null || {
  echo "FALHA: o indexador não respondeu em https://localhost:9200."
  echo "       Veja: systemctl status wazuh-indexer"
  exit 1
}

# ⚠️ `openssl rand`, e NÃO `tr -dc ... </dev/urandom | head -c 32`.
#
# Aquele pipe é o que matou a primeira execução: `head` fecha o cano ao juntar
# os 32 caracteres, o `tr` recebe SIGPIPE e morre com 141 — e com `set -o
# pipefail` o script inteiro cai junto. O Wazuh já estava instalado, o firewall
# não. Sem pipe, sem armadilha.
SENHA_LOKTA=$(openssl rand -hex 16)

# O papel dá EXATAMENTE o que o conector usa e nada além:
#   - `_cat/indices/wazuh-alerts-*` no teste  -> cluster_monitor + indices_monitor
#   - `wazuh-alerts-*/_search` no sync         -> read
# Sem write, sem delete, sem acesso a outro índice.
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

# Prova a credencial nova ANTES de declarar sucesso. Script que diz "pronto" e
# entrega senha que não entra é pior que um que falha.
CONFERE=$(curl -sk -u "${USUARIO_LOKTA}:${SENHA_LOKTA}" \
  "https://localhost:9200/_cat/indices/wazuh-alerts-*?format=json" | jq 'length' 2>/dev/null || echo "ERRO")

# E prova que ele NÃO escreve — a permissão mínima só vale se for verdade.
NEGADO=$(curl -sk -o /dev/null -w "%{http_code}" -u "${USUARIO_LOKTA}:${SENHA_LOKTA}" \
  -X DELETE "https://localhost:9200/wazuh-alerts-nao-existe" || echo "000")

titulo "2/2 · Firewall"

ufw --force reset >/dev/null
ufw default deny incoming >/dev/null
ufw default allow outgoing >/dev/null

ufw allow 22/tcp comment 'SSH' >/dev/null

# Portas dos AGENTES: abertas porque os computadores da empresa têm IP
# doméstico, que muda. Quem autentica o agente é a chave dele, não o IP.
ufw allow 1514/tcp comment 'agentes wazuh' >/dev/null
ufw allow 1515/tcp comment 'registro de agentes' >/dev/null

# O painel. Tem login e senha forte, mas fica exposto — ver o LEIA-ME para
# restringi-lo a um IP depois.
ufw allow 443/tcp comment 'painel wazuh' >/dev/null

# ⚠️ O INDEXADOR NÃO FICA ABERTO PARA A INTERNET. Só os IPs de saída do L'okta
# alcançam a 9200: é a porta que dá acesso a TODO o dado de segurança da
# empresa, e deixá-la aberta seria publicar o SIEM.
for ip in $IPS_LOKTA; do
  ufw allow from "$ip" to any port 9200 proto tcp comment 'L okta' >/dev/null
done

ufw --force enable >/dev/null

IP_PUBLICO=$(curl -s --max-time 10 https://api.ipify.org || echo "<ip do vps>")

titulo "PRONTO"
cat <<FIM

  Credencial do conector, para /Admin/Conectores no L'okta
    URL do serviço:     https://${IP_PUBLICO}:9200
    Usuário do Indexer: ${USUARIO_LOKTA}
    Senha:              ${SENHA_LOKTA}

  Conferido agora, com essa credencial:
    lê alertas:  ${CONFERE} índice(s)
    escrever:    HTTP ${NEGADO}  (403 = negado, que é o esperado)

  Painel: https://${IP_PUBLICO}  ·  usuário admin
  Senhas do Wazuh: ${ARQUIVO_SEGREDOS} (só o root lê)

FIM

ufw status numbered | head -20
