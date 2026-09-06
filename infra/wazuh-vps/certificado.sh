#!/bin/bash
# =============================================================================
#  Certificado de verdade para o L'okta falar com o indexador.
#
#  O PROBLEMA: o instalador do Wazuh gera certificado autoassinado. Para o
#  conector aceitar isso, a plataforma teria de ligar
#  `Integracoes__Wazuh__IgnorarCertificado` — uma chave que existe como opt-in
#  explícito justamente porque desligar validação de TLS em silêncio, num
#  produto de segurança, é o que auditamos nos outros.
#
#  ⚠️ POR QUE UM PROXY, E NÃO TROCAR O CERTIFICADO DO INDEXADOR.
#
#  Trocar o certificado da porta 9200 pelo do Let's Encrypt parece o caminho
#  curto e quebra duas coisas caladas: o FILEBEAT e o PAINEL falam com o
#  indexador validando contra a CA interna (`/etc/filebeat/certs/root-ca.pem`).
#  Trocado o certificado sem trocar a confiança deles, o filebeat para de
#  entregar alerta ao indexador — e o sintoma é "o Wazuh parou de mostrar
#  alerta novo", que ninguém liga a um certificado.
#
#  Então: o indexador fica exatamente como está, e um nginx em 9443 termina o
#  TLS com o certificado público e repassa para o 127.0.0.1:9200. Nada interno
#  muda, e o que está exposto passa a ter certificado válido.
#
#  RODA NO VPS, como root, DEPOIS de instalar-wazuh.sh + pos-instalacao.sh, e
#  com o DNS já apontando para cá.
# =============================================================================
set -euo pipefail

DOMINIO="${1:-wazuh.loktaia.com}"
EMAIL="${2:-casainvestcondo@gmail.com}"
PORTA_PUBLICA=9443

IPS_LOKTA="104.41.13.4 104.41.14.246 104.41.15.87 104.41.9.139 104.41.9.254 \
191.232.240.78 191.232.67.148 191.232.68.177 191.232.69.130 191.232.69.243 \
191.232.69.244 191.232.69.78 191.234.178.82 191.234.183.71 191.235.82.38 \
191.235.87.138 20.197.208.255 4.228.160.110 4.228.160.139 4.228.160.143 \
4.228.160.145 4.228.160.146 4.228.160.158"

titulo() { echo; echo "=============================================================="; echo "  $1"; echo "=============================================================="; }

[ "$(id -u)" -eq 0 ] || { echo "FALHA: rode como root."; exit 1; }

titulo "1/4 · Conferindo o DNS"
RESOLVIDO=$(dig +short A "$DOMINIO" @8.8.8.8 | tail -1)
MEU_IP=$(curl -s --max-time 10 https://api.ipify.org)
echo "$DOMINIO -> ${RESOLVIDO:-(nada)} · este servidor -> $MEU_IP"

# Sem esta conferência, o certbot falha lá na frente com uma mensagem sobre
# desafio HTTP que não diz "o DNS está errado" — e a pessoa vai procurar o
# problema no nginx.
[ "$RESOLVIDO" = "$MEU_IP" ] || {
  echo "FALHA: o domínio não aponta para este servidor. Corrija o registro A antes."
  exit 1
}

titulo "2/4 · Emitindo o certificado"
apt-get update -qq
apt-get install -y -qq nginx certbot >/dev/null

# ⚠️ A 80 FICA ABERTA, e não é descuido: a RENOVAÇÃO automática (o timer do
# certbot, a cada 12h) usa o mesmo desafio. Fechada, o certificado vence em 90
# dias e o L'okta para de sincronizar num dia em que ninguém mexeu em nada.
ufw allow 80/tcp comment 'desafio do certbot' >/dev/null

systemctl stop nginx 2>/dev/null || true
certbot certonly --standalone --non-interactive --agree-tos \
  -m "$EMAIL" -d "$DOMINIO" --keep-until-expiring

VIVO="/etc/letsencrypt/live/${DOMINIO}"
[ -f "${VIVO}/fullchain.pem" ] || { echo "FALHA: o certificado não foi emitido."; exit 1; }

titulo "3/4 · Publicando o indexador em ${PORTA_PUBLICA} com o certificado"

cat > /etc/nginx/sites-available/wazuh-indexer <<NGINX
# TLS público na frente do indexador. Ver o cabeçalho de certificado.sh: o
# indexador continua com os certificados internos dele, intocados.
server {
    listen ${PORTA_PUBLICA} ssl;
    server_name ${DOMINIO};

    ssl_certificate     ${VIVO}/fullchain.pem;
    ssl_certificate_key ${VIVO}/privkey.pem;
    ssl_protocols TLSv1.2 TLSv1.3;

    # Alerta de SIEM traz log inteiro: o padrão de 1 MB devolveria 413 numa
    # consulta grande, e o erro apareceria como "sync falhou" sem motivo.
    client_max_body_size 20m;

    location / {
        proxy_pass https://127.0.0.1:9200;

        # ⚠️ `off` aqui é para o certificado INTERNO do indexador, em localhost.
        # Não é a validação que importa: a que protege o dado na internet é a
        # de cima, com o certificado público. Entre o nginx e o indexador o
        # tráfego não sai da máquina.
        proxy_ssl_verify off;

        proxy_set_header Authorization \$http_authorization;
        proxy_pass_header Authorization;
        proxy_read_timeout 120s;
    }
}
NGINX

ln -sf /etc/nginx/sites-available/wazuh-indexer /etc/nginx/sites-enabled/wazuh-indexer
nginx -t
systemctl enable --now nginx >/dev/null
systemctl restart nginx

titulo "4/4 · Firewall"

# A porta nova, só para o L'okta.
for ip in $IPS_LOKTA; do
  ufw allow from "$ip" to any port ${PORTA_PUBLICA} proto tcp comment 'L okta' >/dev/null
done

# ⚠️ E a 9200 SAI DE VEZ da internet. Ela continuava aberta para os mesmos IPs;
# agora o caminho é o proxy, com certificado válido. Duas portas fazendo a mesma
# coisa, uma delas sem certificado bom, é a que alguém usa por engano.
for ip in $IPS_LOKTA; do
  ufw delete allow from "$ip" to any port 9200 proto tcp >/dev/null 2>&1 || true
done

ufw reload >/dev/null

titulo "CONFERINDO"

# Sem `-k`: se a cadeia estiver ruim, isto falha — que é o ponto do exercício.
echo -n "certificado público válido: "
curl -s -o /dev/null -w "HTTP %{http_code}\n" --max-time 20 "https://${DOMINIO}:${PORTA_PUBLICA}/" || echo "FALHOU"

echo -n "vence em: "
openssl x509 -enddate -noout -in "${VIVO}/fullchain.pem" | cut -d= -f2

echo
echo "  URL para o conector do L'okta:"
echo "    https://${DOMINIO}:${PORTA_PUBLICA}"
echo
ufw status | grep -E "9200|9443|80" || true
