# Aggressive Gradle Cache Fix for Unity Android Build
# This script performs a comprehensive cleanup of all Gradle-related caches

Write-Host "=== AGGRESSIVE GRADLE CACHE FIX ===" -ForegroundColor Red
Write-Host "This will clear ALL Gradle caches and daemons" -ForegroundColor Yellow
Write-Host ""

# Step 1: Stop all Java processes (Gradle daemons)
Write-Host "Step 1: Stopping all Java/Gradle processes..." -ForegroundColor Cyan
$javaProcs = Get-Process -Name "java" -ErrorAction SilentlyContinue | Where-Object { 
    $_.Path -like "*gradle*" -or $_.Path -like "*Unity*" 
}
if ($javaProcs) {
    $javaProcs | ForEach-Object {
        Write-Host "  Stopping: $($_.ProcessName) (PID: $($_.Id))" -ForegroundColor Yellow
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
    }
    Start-Sleep -Seconds 3
    Write-Host "  ✓ Java processes stopped" -ForegroundColor Green
} else {
    Write-Host "  - No Java processes found" -ForegroundColor Gray
}

# Step 2: Clear Gradle daemon directory
Write-Host "`nStep 2: Clearing Gradle daemon..." -ForegroundColor Cyan
$daemonPath = "$env:USERPROFILE\.gradle\daemon"
if (Test-Path $daemonPath) {
    Remove-Item -Path $daemonPath -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "  ✓ Gradle daemon cleared" -ForegroundColor Green
} else {
    Write-Host "  - Daemon directory not found" -ForegroundColor Gray
}

# Step 3: Clear entire Gradle 8.13 cache
Write-Host "`nStep 3: Clearing Gradle 8.13 cache..." -ForegroundColor Cyan
$cache813 = "$env:USERPROFILE\.gradle\caches\8.13"
if (Test-Path $cache813) {
    Remove-Item -Path $cache813 -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "  ✓ Gradle 8.13 cache cleared" -ForegroundColor Green
} else {
    Write-Host "  - Gradle 8.13 cache not found" -ForegroundColor Gray
}

# Step 4: Clear workspace metadata
Write-Host "`nStep 4: Clearing workspace metadata..." -ForegroundColor Cyan
$workspacePath = "$env:USERPROFILE\.gradle\caches\8.13\groovy-dsl"
if (Test-Path $workspacePath) {
    Remove-Item -Path $workspacePath -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "  ✓ Workspace metadata cleared" -ForegroundColor Green
} else {
    Write-Host "  - Workspace metadata not found" -ForegroundColor Gray
}

# Step 5: Clear Gradle wrapper cache
Write-Host "`nStep 5: Clearing Gradle wrapper cache..." -ForegroundColor Cyan
$wrapperPath = "$env:USERPROFILE\.gradle\wrapper"
if (Test-Path $wrapperPath) {
    Remove-Item -Path $wrapperPath -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "  ✓ Gradle wrapper cache cleared" -ForegroundColor Green
} else {
    Write-Host "  - Gradle wrapper cache not found" -ForegroundColor Gray
}

# Step 6: Clear Unity's Gradle cache
Write-Host "`nStep 6: Clearing Unity Gradle cache..." -ForegroundColor Cyan
$unityCache = "$env:LOCALAPPDATA\Unity\cache\packages\packages.unity.com\com.unity.external.tool\gradle"
if (Test-Path $unityCache) {
    Remove-Item -Path $unityCache -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "  ✓ Unity Gradle cache cleared" -ForegroundColor Green
} else {
    Write-Host "  - Unity Gradle cache not found" -ForegroundColor Gray
}

# Step 7: Clear any project-level .gradle directories
Write-Host "`nStep 7: Checking for project-level Gradle directories..." -ForegroundColor Cyan
$projectGradle = ".\gradle", ".\build\.gradle", ".\Library\Android\gradle"
foreach ($path in $projectGradle) {
    if (Test-Path $path) {
        Write-Host "  Found: $path - removing..." -ForegroundColor Yellow
        Remove-Item -Path $path -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "  ✓ Removed: $path" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "=== CLEANUP COMPLETE ===" -ForegroundColor Green
Write-Host ""
Write-Host "IMPORTANT: Before building again:" -ForegroundColor Yellow
Write-Host "1. Close Unity Editor completely" -ForegroundColor White
Write-Host "2. Wait 5 seconds" -ForegroundColor White
Write-Host "3. Reopen Unity" -ForegroundColor White
Write-Host "4. Try building again" -ForegroundColor White
Write-Host ""
Write-Host "The first build will take longer as Gradle re-downloads everything." -ForegroundColor Cyan

