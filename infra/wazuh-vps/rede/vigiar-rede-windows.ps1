<#
  Põe uma máquina Windows que JÁ TEM agente Wazuh para vigiar o equipamento de
  rede à volta dela: DNS em uso, gateway (IP e MAC) e o painel do modem.

  POR QUE ASSIM, e não syslog do modem: ver o cabeçalho de `regras-rede.xml`. Em
  resumo — o CPE de operadora quase nunca exporta log, e mandar syslog pela
  internet pediria uma porta sem autenticação nem criptografia. Aqui o dado sai
  pelo canal cifrado que o agente já mantém.

  ⚠️ RODE COMO ADMINISTRADOR. A pasta do agente é protegida de propósito.
  ⚠️ IDEMPOTENTE: rodar de novo não duplica bloco nenhum, e faz cópia do
     ossec.conf antes de tocar nele.
#>

$ErrorActionPreference = 'Stop'

$id = [Security.Principal.WindowsIdentity]::GetCurrent()
if (-not (New-Object Security.Principal.WindowsPrincipal($id)).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "Rode este script como ADMINISTRADOR." -ForegroundColor Red
    exit 1
}

$base = 'C:\Program Files (x86)\ossec-agent'
$conf = Join-Path $base 'ossec.conf'
if (-not (Test-Path $conf)) { Write-Host "Agente Wazuh nao encontrado em $base" -ForegroundColor Red; exit 1 }

# ── A sonda ──────────────────────────────────────────────────────────────────
# Cada modo imprime UMA linha estável. Estável é o requisito: a regra do manager
# usa check_diff, então qualquer variação cosmética (ordem, espaço, horário)
# viraria alerta falso. Ordenar e normalizar aqui é o que faz o alerta valer.
$sonda = Join-Path $base 'rede-modem.ps1'
@'
param([Parameter(Mandatory=$true)][ValidateSet("dns","gateway","admin")][string]$Modo)
$ErrorActionPreference = "SilentlyContinue"

function Rota {
    Get-NetRoute -DestinationPrefix "0.0.0.0/0" |
        Sort-Object RouteMetric | Select-Object -First 1
}

switch ($Modo) {
    "dns" {
        $s = Get-DnsClientServerAddress -AddressFamily IPv4 |
             Where-Object { $_.ServerAddresses -and $_.InterfaceAlias -notmatch "Loopback" } |
             ForEach-Object { $_.ServerAddresses } |
             Sort-Object -Unique
        if ($s) { $s -join "," } else { "sem-dns" }
    }
    "gateway" {
        $r = Rota
        if (-not $r) { "sem-gateway"; break }
        $mac = (Get-NetNeighbor -IPAddress $r.NextHop | Select-Object -First 1).LinkLayerAddress
        if (-not $mac) { $mac = "mac-desconhecido" }
        "$($r.NextHop) $($mac.ToUpper())"
    }
    "admin" {
        $r = Rota
        if (-not $r) { "sem-gateway"; break }
        # Só o CABEÇALHO: assinatura do servidor e para onde ele manda. O corpo da
        # página muda a cada visita em muito CPE (token, contador) e viraria ruido.
        $resp = try { Invoke-WebRequest -Uri "http://$($r.NextHop)/" -MaximumRedirection 0 `
                        -TimeoutSec 8 -UseBasicParsing } catch { $_.Exception.Response }
        if (-not $resp) { "sem-resposta"; break }
        $srv = $resp.Headers["Server"]; $loc = $resp.Headers["Location"]
        "servidor=$srv destino=$loc"
    }
}
'@ | Set-Content -Path $sonda -Encoding UTF8
Write-Host "sonda gravada em $sonda"

# ── ossec.conf ───────────────────────────────────────────────────────────────
$marca = '<!-- vigilancia-de-rede-lokta -->'
$texto = Get-Content $conf -Raw -Encoding UTF8

if ($texto -match [regex]::Escape($marca)) {
    Write-Host "os blocos ja estavam no ossec.conf - nada a acrescentar"
} else {
    $copia = "$conf.antes-da-rede"
    if (-not (Test-Path $copia)) { Copy-Item $conf $copia }
    Write-Host "copia do original em $copia"

    $psExe = "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe"
    $blocos = @"

  $marca
  <localfile>
    <log_format>full_command</log_format>
    <command>$psExe -NoProfile -ExecutionPolicy Bypass -File "$sonda" -Modo dns</command>
    <alias>rede-dns</alias>
    <frequency>600</frequency>
  </localfile>

  <localfile>
    <log_format>full_command</log_format>
    <command>$psExe -NoProfile -ExecutionPolicy Bypass -File "$sonda" -Modo gateway</command>
    <alias>rede-gateway</alias>
    <frequency>600</frequency>
  </localfile>

  <localfile>
    <log_format>full_command</log_format>
    <command>$psExe -NoProfile -ExecutionPolicy Bypass -File "$sonda" -Modo admin</command>
    <alias>rede-modem-admin</alias>
    <frequency>3600</frequency>
  </localfile>
"@

    # O ossec.conf do agente Windows fecha em </ossec_config>; entra logo antes do ULTIMO.
    $i = $texto.LastIndexOf('</ossec_config>')
    if ($i -lt 0) { Write-Host "nao achei </ossec_config> - nada foi alterado" -ForegroundColor Red; exit 1 }
    $novo = $texto.Substring(0, $i) + $blocos + "`r`n" + $texto.Substring($i)
    Set-Content -Path $conf -Value $novo -Encoding UTF8
    Write-Host "blocos acrescentados ao ossec.conf"
}

Restart-Service -Name WazuhSvc
Start-Sleep -Seconds 3
$svc = Get-Service -Name WazuhSvc
Write-Host "agente: $($svc.Status)"

Write-Host ""
Write-Host "O que a sonda le AGORA (a linha de base que sera comparada daqui em diante):"
foreach ($m in @("dns","gateway","admin")) {
    "  {0,-8} {1}" -f $m, (& $sonda -Modo $m)
}
