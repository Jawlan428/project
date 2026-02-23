# Fix Gradle Cache Corruption for Unity Android Build
# This script clears the corrupted Gradle cache and stops Gradle daemons

Write-Host "Fixing Gradle cache corruption..." -ForegroundColor Yellow

# Step 1: Stop any running Java/Gradle processes
Write-Host "`nStep 1: Stopping Gradle daemon processes..." -ForegroundColor Cyan
$javaProcesses = Get-Process | Where-Object { $_.ProcessName -like "*java*" } | Where-Object { $_.Path -like "*gradle*" -or $_.CommandLine -like "*gradle*" }
if ($javaProcesses) {
    $javaProcesses | ForEach-Object {
        Write-Host "Stopping process: $($_.ProcessName) (PID: $($_.Id))" -ForegroundColor Yellow
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
    }
    Start-Sleep -Seconds 2
    Write-Host "Gradle processes stopped." -ForegroundColor Green
} else {
    Write-Host "No Gradle processes found running." -ForegroundColor Green
}

# Step 2: Clear ALL Gradle caches (comprehensive fix)
Write-Host "`nStep 2: Clearing ALL Gradle caches..." -ForegroundColor Cyan

# Clear user's Gradle cache (8.13)
Write-Host "  Clearing user Gradle cache (8.13)..." -ForegroundColor Yellow
Remove-Item -Path "$env:USERPROFILE\.gradle\caches\8.13" -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "  ✓ User Gradle cache cleared" -ForegroundColor Green

# Clear Gradle daemon cache
Write-Host "  Clearing Gradle daemon cache..." -ForegroundColor Yellow
Remove-Item -Path "$env:USERPROFILE\.gradle\daemon" -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "  ✓ Gradle daemon cache cleared" -ForegroundColor Green

# Clear Gradle wrapper cache
Write-Host "  Clearing Gradle wrapper cache..." -ForegroundColor Yellow
Remove-Item -Path "$env:USERPROFILE\.gradle\wrapper\dists" -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "  ✓ Gradle wrapper cache cleared" -ForegroundColor Green

# Step 3: Clear Unity's Gradle build cache
Write-Host "`nStep 3: Clearing Unity's Gradle build cache..." -ForegroundColor Cyan
$projectRoot = Get-Location

# Clear Unity build cache
$unityGradlePaths = @(
    "$projectRoot\Library\Bee\Android\Prj\IL2CPP\Gradle",
    "$projectRoot\Library\Bee\artifacts\Android\Gradle",
    "$projectRoot\Temp\StagingArea\gradleWarmupArea"
)

foreach ($path in $unityGradlePaths) {
    if (Test-Path $path) {
        Write-Host "  Clearing: $path" -ForegroundColor Yellow
        Remove-Item -Path $path -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "  ✓ Cleared" -ForegroundColor Green
    }
}

# Also check Unity's global cache
$unityGradleCache = "$env:LOCALAPPDATA\Unity\cache\packages\packages.unity.com\com.unity.external.tool\gradle"
if (Test-Path $unityGradleCache) {
    Write-Host "  Clearing Unity global Gradle cache..." -ForegroundColor Yellow
    Remove-Item -Path $unityGradleCache -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "  ✓ Unity global cache cleared" -ForegroundColor Green
}

Write-Host "`n✅ Done! Try building again in Unity." -ForegroundColor Green
Write-Host "Note: The first build after clearing cache may take longer." -ForegroundColor Cyan

