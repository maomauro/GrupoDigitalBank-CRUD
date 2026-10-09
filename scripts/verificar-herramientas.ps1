# Verifica las herramientas del entorno local. No instala ni modifica nada.
# Uso:  powershell -ExecutionPolicy Bypass -File scripts\verificar-herramientas.ps1
# Salida: codigo 0 si no falta ninguna herramienta obligatoria, 1 si falta alguna.

$faltan = 0

function Mostrar($estado, $nombre, $detalle) {
    $color = @{ OK = 'Green'; FALTA = 'Red'; OPCIONAL = 'Yellow'; AVISO = 'Yellow' }[$estado]
    Write-Host ("[{0,-8}] {1,-22} {2}" -f $estado, $nombre, $detalle) -ForegroundColor $color
}

function Probar($nombre, $comando, [scriptblock]$version, $obligatoria, $winget) {
    if (Get-Command $comando -ErrorAction SilentlyContinue) {
        $v = try { (& $version | Select-Object -First 1) } catch { '' }
        Mostrar 'OK' $nombre "$v"
    }
    elseif ($obligatoria) {
        Mostrar 'FALTA' $nombre "winget install --id $winget -e"
        $script:faltan++
    }
    else {
        Mostrar 'OPCIONAL' $nombre "no instalado (winget install --id $winget -e)"
    }
}

Write-Host "`n== Herramientas obligatorias ==" -ForegroundColor Cyan
Probar 'Git'            'git'        { git --version }                  $true 'Git.Git'
Probar '.NET SDK'       'dotnet'     { dotnet --version }               $true 'Microsoft.DotNet.SDK.10'
Probar 'Docker CLI'     'docker'     { docker --version }               $true 'Docker.DockerDesktop'
Probar 'GitHub CLI'     'gh'         { (gh --version) }                 $true 'GitHub.cli'
Probar 'OpenSSH (ssh)'  'ssh'        { ssh -V 2>&1 }                    $true 'Microsoft.OpenSSH.Beta'
Probar 'ssh-keygen'     'ssh-keygen' { 'presente' }                     $true 'Microsoft.OpenSSH.Beta'

Write-Host "`n== Herramientas opcionales ==" -ForegroundColor Cyan
Probar 'VS Code'        'code'       { code --version }                 $false 'Microsoft.VisualStudioCode'
Probar 'psql (cliente)' 'psql'       { psql --version }                 $false 'PostgreSQL.PostgreSQL.17'
Probar 'WSL'            'wsl'        { 'presente' }                     $false 'Microsoft.WSL'

Write-Host "`n== Validaciones de funcionamiento ==" -ForegroundColor Cyan

# .NET: debe haber un SDK 10.x
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    $sdk10 = dotnet --list-sdks | Where-Object { $_ -match '^10\.' }
    if ($sdk10) { Mostrar 'OK' 'SDK .NET 10' (($sdk10 | Select-Object -Last 1) -replace '\s*\[.*$', '') }
    else { Mostrar 'FALTA' 'SDK .NET 10' 'hay dotnet pero ningun SDK 10.x'; $faltan++ }
}

# Docker: daemon activo y Compose v2
if (Get-Command docker -ErrorAction SilentlyContinue) {
    $srv = docker version --format '{{.Server.Version}}' 2>$null
    if ($LASTEXITCODE -eq 0 -and $srv) { Mostrar 'OK' 'Docker daemon' "activo (servidor $srv)" }
    else { Mostrar 'FALTA' 'Docker daemon' 'Docker Desktop no esta en ejecucion'; $faltan++ }

    $comp = docker compose version --short 2>$null
    if ($LASTEXITCODE -eq 0 -and $comp) { Mostrar 'OK' 'Docker Compose' "v$comp" }
    else { Mostrar 'FALTA' 'Docker Compose' 'plugin "docker compose" no disponible'; $faltan++ }
}

# Git: identidad configurada
if (Get-Command git -ErrorAction SilentlyContinue) {
    $n = git config --global --get user.name
    $e = git config --global --get user.email
    if ($n -and $e) { Mostrar 'OK' 'Git identidad' "$n <$e>" }
    else { Mostrar 'AVISO' 'Git identidad' 'configurar user.name y user.email'; }
    $rama = git config --global --get init.defaultBranch
    if ($rama -eq 'main') { Mostrar 'OK' 'Git rama inicial' 'main' }
    else { Mostrar 'AVISO' 'Git rama inicial' "git config --global init.defaultBranch main (actual: '$rama')" }
}

# GitHub CLI autenticado
if (Get-Command gh -ErrorAction SilentlyContinue) {
    gh auth status 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) { Mostrar 'OK' 'GitHub autenticado' 'gh auth status correcto' }
    else { Mostrar 'AVISO' 'GitHub autenticado' 'ejecutar: gh auth login' }
}

# Llave SSH personal
if (Test-Path "$HOME\.ssh\id_ed25519.pub") { Mostrar 'OK' 'Llave SSH personal' '~/.ssh/id_ed25519' }
else { Mostrar 'AVISO' 'Llave SSH personal' 'ssh-keygen -t ed25519' }

# Llave SSH de despliegue (se usa desde la fase 6)
if (Test-Path "$HOME\.ssh\gdb_deploy.pub") { Mostrar 'OK' 'Llave de despliegue' '~/.ssh/gdb_deploy' }
else { Mostrar 'AVISO' 'Llave de despliegue' 'pendiente hasta la fase 6 del plan' }

# Puertos que usara Caddy en local
foreach ($p in 80, 443) {
    $uso = Get-NetTCPConnection -LocalPort $p -State Listen -ErrorAction SilentlyContinue
    if ($uso) { Mostrar 'AVISO' "Puerto $p" "ocupado (PID $($uso[0].OwningProcess)); Caddy no podra publicarlo" }
    else { Mostrar 'OK' "Puerto $p" 'libre' }
}

Write-Host ''
if ($faltan -eq 0) { Write-Host 'Entorno listo: no falta nada obligatorio.' -ForegroundColor Green; exit 0 }
else { Write-Host "Faltan $faltan elemento(s) obligatorio(s)." -ForegroundColor Red; exit 1 }
