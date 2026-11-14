param(
    [Parameter(Mandatory = $true)][string]$Player,
    [Parameter(Mandatory = $true)][string]$Request,
    [Parameter(Mandatory = $true)][string]$EvidenceDirectory,
    [ValidateRange(0, 65536)][int]$MaximumIdleMemoryMiB = 2048,
    [ValidateRange(30, 3600)][int]$TimeoutSeconds = 900
)

$ErrorActionPreference = 'Stop'
$playerPath = (Resolve-Path -LiteralPath $Player).Path
$requestPath = (Resolve-Path -LiteralPath $Request).Path
$native = Join-Path (Split-Path $playerPath) (([IO.Path]::GetFileNameWithoutExtension($playerPath)) + '_Data/Plugins/x86_64/MNN.dll')
$configuration = Get-Content -LiteralPath $requestPath -Raw | ConvertFrom-Json
if (Test-Path -LiteralPath $configuration.output) { throw 'Result already exists; use a fresh output.' }
if (Test-Path -LiteralPath $EvidenceDirectory) { throw 'Evidence directory already exists.' }
New-Item -ItemType Directory -Path $EvidenceDirectory | Out-Null
Copy-Item -LiteralPath $requestPath -Destination (Join-Path $EvidenceDirectory 'request.json')
Get-FileHash -LiteralPath @($playerPath, $native) -Algorithm SHA256 |
    Select-Object Path, Hash | ConvertTo-Json | Set-Content (Join-Path $EvidenceDirectory 'identities.json') -Encoding UTF8

# All counters below describe the entire GPU, including display/other applications.
# Reject an occupied device before launch instead of changing or stopping services.
$idle = @(& nvidia-smi --query-gpu=index,name,memory.used,utilization.gpu --format=csv,noheader,nounits)
if ($LASTEXITCODE -ne 0 -or $idle.Count -ne 1) { throw 'This runner requires one queryable NVIDIA GPU.' }
$idle | Set-Content (Join-Path $EvidenceDirectory 'idle-gpu.csv') -Encoding UTF8
$fields = $idle[0].Split(',')
$occupied = [int]$fields[2].Trim() -gt $MaximumIdleMemoryMiB -or [int]$fields[3].Trim() -gt 5
@{ memory_mib = [int]$fields[2].Trim(); utilization_percent = [int]$fields[3].Trim();
    memory_limit_mib = $MaximumIdleMemoryMiB; occupied = $occupied } |
    ConvertTo-Json | Set-Content (Join-Path $EvidenceDirectory 'idle-gpu.json') -Encoding UTF8
if ($occupied) {
    throw 'GPU is occupied; retained idle evidence and did not launch inference.'
}
if (Get-Process Unity, PlayerWithTests, Benchmark -ErrorAction SilentlyContinue) { throw 'Another Unity workload is active.' }
& nvidia-smi -q | Set-Content (Join-Path $EvidenceDirectory 'gpu-inventory.txt') -Encoding UTF8
if ($LASTEXITCODE -ne 0) { throw 'Cannot capture GPU inventory.' }

$telemetry = Join-Path $EvidenceDirectory 'gpu.csv'
$sampler = $null
$process = $null
$record = [ordered]@{ started = $false; timed_out = $false; exit_status = $null; error = $null }
try {
    $sampler = Start-Process (Get-Command nvidia-smi).Source -ArgumentList @(
        '--query-gpu=timestamp,index,pstate,memory.used,utilization.gpu,utilization.memory,power.draw,clocks.sm,clocks.mem,temperature.gpu',
        '--format=csv', '-lms', '100') -RedirectStandardOutput $telemetry `
        -RedirectStandardError (Join-Path $EvidenceDirectory 'gpu.stderr.txt') -PassThru -WindowStyle Hidden
    # nvidia-smi -f buffers short traces. Redirect stdout and wait for a real
    # sample; after termination, WaitForExit drains the redirected streams.
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    while (-not (Test-Path -LiteralPath $telemetry) -or @(Get-Content -LiteralPath $telemetry -TotalCount 2).Count -lt 2) {
        if ($sampler.HasExited -or [DateTime]::UtcNow -gt $deadline) { throw 'No GPU telemetry sample received.' }
        Start-Sleep -Milliseconds 100
    }
    $env:MNN_WINDOWS_BENCHMARK_REQUEST = $requestPath
    $env:MNN_INFERENCE_CACHE_DIRECTORY = Join-Path $EvidenceDirectory 'Cache'
    $record.start_utc = [DateTime]::UtcNow.ToString('o')
    $process = Start-Process $playerPath -ArgumentList @('-batchmode', '-nographics', '-logFile',
        ('"' + (Join-Path $EvidenceDirectory 'player.log') + '"')) -PassThru
    $record.started = $true
    $record.process_id = $process.Id
    $record.timed_out = -not $process.WaitForExit($TimeoutSeconds * 1000)
    if ($record.timed_out) { Stop-Process -Id $process.Id; $process.WaitForExit() }
    $record.exit_status = $process.ExitCode
    $record.end_utc = [DateTime]::UtcNow.ToString('o')
    if ($sampler.HasExited) { throw 'GPU telemetry sampler exited unexpectedly.' }
    if ($record.timed_out -or $record.exit_status -ne 0) { throw 'Benchmark process failed; inspect retained evidence.' }
    $result = Get-Content -LiteralPath $configuration.output -Raw | ConvertFrom-Json
    if (-not $result.passed) { throw 'Benchmark correctness gate failed.' }
}
catch {
    $record.error = $_.Exception.Message
    throw
}
finally {
    if ($sampler) {
        if (-not $sampler.HasExited) { Stop-Process -Id $sampler.Id -ErrorAction SilentlyContinue }
        $sampler.WaitForExit()
    }
    $record | ConvertTo-Json | Set-Content (Join-Path $EvidenceDirectory 'process.json') -Encoding UTF8
}
