# Comprehensive Gradle Cache Fix for Unity Android Build
# This script performs a complete cleanup of Gradle cache issues

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Gradle Cache Corruption Fix" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Stop all Java processes that might be Gradle daemons
Write-Host "[1/5] Stopping Gradle daemon processes..." -ForegroundColor Yellow
$javaProcesses = Get-Process -Name "java" -ErrorAction SilentlyContinue
if ($javaProcesses) {
    $javaProcesses | ForEach-Object {
        try {
            $processPath = $_.Path
            if ($processPath -like "*gradle*" -or $processPath -like "*Unity*") {
                Write-Host "  Stopping Java process (PID: $($_.Id))" -ForegroundColor Gray
                Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
            }
        } catch {
            # Ignore errors
        }
    }
    Start-Sleep -Seconds 3
    Write-Host "  ✓ Processes stopped" -ForegroundColor Green
} else {
    Write-Host "  ✓ No Java processes found" -ForegroundColor Green
}

# Step 2: Clear the specific corrupted cache directory
Write-Host "[2/5] Clearing corrupted groovy-dsl cache..." -ForegroundColor Yellow
$corruptedPath = "$env:USERPROFILE\.gradle\caches\8.13\groovy-dsl\f03b508a5f4f815f5da32881786d9c41"
if (Test-Path $corruptedPath) {
    Remove-Item -Path $corruptedPath -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "  ✓ Corrupted directory removed" -ForegroundColor Green
} else {
    Write-Host "  ✓ Already cleared" -ForegroundColor Green
}

# Step 3: Clear entire groovy-dsl cache
Write-Host "[3/5] Clearing entire groovy-dsl cache..." -ForegroundColor Yellow
$groovyDslPath = "$env:USERPROFILE\.gradle\caches\8.13\groovy-dsl"
if (Test-Path $groovyDslPath) {
    Remove-Item -Path $groovyDslPath -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "  ✓ groovy-dsl cache cleared" -ForegroundColor Green
} else {
    Write-Host "  ✓ Already cleared" -ForegroundColor Green
}

# Step 4: Clear Gradle daemon cache
Write-Host "[4/5] Clearing Gradle daemon cache..." -ForegroundColor Yellow
$daemonPath = "$env:USERPROFILE\.gradle\daemon"
if (Test-Path $daemonPath) {
    Get-ChildItem -Path $daemonPath -Filter "*.lock" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
    Write-Host "  ✓ Daemon locks cleared" -ForegroundColor Green
} else {
    Write-Host "  ✓ No daemon cache found" -ForegroundColor Green
}

# Step 5: Clear Unity's build cache (Temp folder)
Write-Host "[5/5] Clearing Unity temp build files..." -ForegroundColor Yellow
$unityTempGradle = Get-ChildItem -Path "$env:TEMP" -Filter "*gradle*" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 5
if ($unityTempGradle) {
    $unityTempGradle | ForEach-Object {
        Remove-Item -Path $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
    }
    Write-Host "  ✓ Unity temp files cleared" -ForegroundColor Green
} else {
    Write-Host "  ✓ No Unity temp files found" -ForegroundColor Green
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "✅ Fix Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Close Unity Editor completely (if open)" -ForegroundColor White
Write-Host "2. Reopen Unity" -ForegroundColor White
Write-Host "3. Try building again" -ForegroundColor White
Write-Host ""
Write-Host "Note: The first build will take longer as Gradle re-downloads dependencies." -ForegroundColor Cyan
Write-Host ""

