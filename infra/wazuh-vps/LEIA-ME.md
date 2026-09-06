# Wazuh da empresa, num VPS — do zero ao alerta na tela do L'okta

Decidido em 06/09/2026. O Wazuh é **de cada cliente**, no ambiente dele, e o L'okta
lê pela API — nunca um Wazuh nosso com os clientes dentro. Este servidor é a
exceção que confirma a regra: é o Wazuh **da nossa empresa**, para termos alerta
real na plataforma e uma demonstração que não é maquete.

## O servidor

**Hostinger KVM 2** — 2 vCPU, 8 GB, 100 GB NVMe, R$ 43,99/mês no plano de 2 anos
(renova a R$ 77,99).

Três escolhas que importam na hora de criar:

| Campo | Valor | Por quê |
|---|---|---|
| Localização | **São Paulo (Brasil)** | é de lá que o `okta-ia` consulta |
| Sistema | **Ubuntu 22.04 LTS**, limpo | sem painel, sem template com Docker |
| Acesso | **chave SSH** | senha em porta 22 aberta é varrida em minutos |

> ⚠️ **NÃO reaproveitar o VPS `2.25.156.123`.** Aquele sustenta a ponte do
> WhatsApp do iAgrow. O indexador é um OpenSearch e toma a RAM que encontrar; a
> queda apareceria como **oferta que parou de chegar, sem erro nenhum** — e
> levaria dias para alguém ligar uma coisa à outra.

> ⚠️ O mínimo oficial do Wazuh tudo-em-um é **4 CPU / 8 GB**. O KVM 2 tem a RAM
> certa e metade da CPU. É suficiente para os poucos agentes da empresa — aquele
> número é dimensionado para ~25 agentes com 90 dias de histórico. Se apertar, a
> Hostinger faz upgrade no lugar, sem reinstalar.

## A instalação

Um comando, de 10 a 20 minutos:

```bash
scp instalar-wazuh.sh root@<IP>:/root/
ssh root@<IP> "bash /root/instalar-wazuh.sh"
```

O script confere a máquina antes de mexer em qualquer coisa, instala o Wazuh
4.14 tudo-em-um, cria o usuário **somente-leitura** do conector e fecha o
firewall. No fim ele imprime as credenciais — inclusive a do conector, já
**testada** contra o próprio indexador antes de dizer "pronto".

### O que fica aberto, e para quem

| Porta | Para quem | Por quê |
|---|---|---|
| 22 | qualquer origem | SSH, por chave |
| 1514 / 1515 | qualquer origem | os agentes têm IP doméstico, que muda; quem autentica é a chave do agente |
| 443 | qualquer origem | o painel do Wazuh, com login |
| **9200** | **só os 23 IPs de saída do `okta-ia`** | é a porta que dá acesso a TODO o dado de segurança da empresa |

Para restringir também o painel ao seu IP, depois:

```bash
ufw delete allow 443/tcp
ufw allow from <seu IP> to any port 443 proto tcp
```

## Ligar no L'okta

Em produção, `/Admin/Conectores` → **Instalar Wazuh**, com o que o script
imprimiu. Depois **Testar conexão** e **Sincronizar agora**.

> ⚠️ **O autofill do Chrome estraga isto**, e o erro parece credencial errada do
> Wazuh: o navegador preenche os campos com o seu login da plataforma, e a tela
> responde *"Usuário ou senha recusados pelo Wazuh Indexer (usuário …)"*. Apague
> os campos com um clique triplo antes de digitar. A mensagem diz **qual usuário**
> foi recusado justamente para denunciar esse caso.

A primeira carga puxa **7 dias para trás** e o agendador sincroniza a cada 15
minutos. Alerta mais antigo que isso não entra — de propósito: puxar o histórico
inteiro de um SIEM na instalação encheria o banco.

## A pendência que sobra: o certificado

O instalador gera certificado **autoassinado**. Para o conector aceitar isso em
produção seria preciso ligar `Integracoes__Wazuh__IgnorarCertificado=true` no
App Service — e essa chave existe como **opt-in explícito** justamente porque
desligar validação de TLS em silêncio, numa plataforma de segurança, é o tipo de
coisa que auditamos nos outros.

O certo, e o caminho recomendado: apontar `wazuh.loktaia.com` para o IP do VPS,
emitir um certificado Let's Encrypt e instalá-lo no indexador. Aí a validação
continua ligada e nada precisa ser afrouxado do lado da plataforma.

Enquanto o certificado real não existe, a alternativa é a chave acima — decisão
consciente, e reversível assim que o DNS estiver de pé.
