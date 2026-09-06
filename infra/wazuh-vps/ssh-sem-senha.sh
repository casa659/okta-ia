#!/bin/bash
# =============================================================================
#  Desliga o login por SENHA no SSH. Só chave entra.
#
#  POR QUE: porta 22 aberta aceitando senha é o que robô mais tenta na internet.
#  Com chave, adivinhar deixa de ser possível — não há o que adivinhar.
#
#  ⚠️ A SAÍDA DE EMERGÊNCIA CONTINUA EXISTINDO. O "Web console" do painel da
#  Hostinger NÃO é SSH: ele entra pelo console da máquina virtual e continua
#  aceitando a senha de root. Ou seja, isto não tranca ninguém para fora — só
#  fecha a porta que a internet enxerga.
#
#  ⚠️ O ERRO CLÁSSICO QUE ESTE SCRIPT EVITA: mexer só em /etc/ssh/sshd_config e
#  achar que resolveu. O Ubuntu na nuvem inclui /etc/ssh/sshd_config.d/*.conf, e
#  o `50-cloud-init.conf` costuma trazer `PasswordAuthentication yes`. Como no
#  sshd o PRIMEIRO valor lido é o que vale, e o Include fica no topo, aquele
#  arquivo ganha do que estiver embaixo — a senha continua funcionando e ninguém
#  percebe. Por isso aqui: um arquivo `00-` (lido antes de todos) E a limpeza dos
#  outros E a conferência final pelo `sshd -T`, que é o valor EFETIVO.
#
#  RODA NO VPS, como root. Idempotente.
# =============================================================================
set -euo pipefail

titulo() { echo; echo "=============================================================="; echo "  $1"; echo "=============================================================="; }

[ "$(id -u)" -eq 0 ] || { echo "FALHA: rode como root."; exit 1; }

titulo "1/4 · Conferindo que existe chave para entrar"

# ⚠️ SEM ISTO O SCRIPT PODE TRANCAR A PORTA SEM NINGUÉM TER A CHAVE. É a
# conferência mais importante do arquivo.
CHAVES=$(grep -cvE '^\s*(#|$)' /root/.ssh/authorized_keys 2>/dev/null || echo 0)
echo "chaves em /root/.ssh/authorized_keys: ${CHAVES}"
[ "$CHAVES" -ge 1 ] || {
  echo "FALHA: nenhuma chave autorizada. Desligar a senha aqui deixaria só o"
  echo "       console web do painel como entrada. Adicione a chave antes."
  exit 1
}

titulo "2/4 · Escrevendo a regra"

# `00-` para ser lido ANTES de qualquer outro arquivo do sshd_config.d — no sshd
# o primeiro valor lido é o que vale.
cat > /etc/ssh/sshd_config.d/00-sem-senha.conf <<CONF
# Só chave. Ver ssh-sem-senha.sh — 06/09/2026.
PasswordAuthentication no
KbdInteractiveAuthentication no
ChallengeResponseAuthentication no
PermitRootLogin prohibit-password
CONF

# E cala quem diz o contrário nos outros arquivos, para não depender só da ordem.
for f in /etc/ssh/sshd_config /etc/ssh/sshd_config.d/*.conf; do
  [ -f "$f" ] || continue
  case "$f" in */00-sem-senha.conf) continue;; esac
  if grep -qE '^\s*(PasswordAuthentication|KbdInteractiveAuthentication|ChallengeResponseAuthentication)\s+yes' "$f"; then
    cp -n "$f" "${f}.antes-sem-senha" 2>/dev/null || true
    sed -i -E 's/^\s*(PasswordAuthentication|KbdInteractiveAuthentication|ChallengeResponseAuthentication)\s+yes/# desligado em 06\/09\/2026 (ssh-sem-senha.sh): \1 yes/' "$f"
    echo "ajustado: $f"
  fi
done

titulo "3/4 · Validando e recarregando"
sshd -t || { echo "FALHA: configuração inválida. NADA foi recarregado."; exit 1; }
systemctl reload ssh 2>/dev/null || systemctl reload sshd

titulo "4/4 · O que o servidor diz que vale AGORA"

# `sshd -T` mostra o valor EFETIVO, depois de todos os includes. É a única
# resposta que não é palpite.
sshd -T | grep -E '^(passwordauthentication|kbdinteractiveauthentication|permitrootlogin|pubkeyauthentication) '
