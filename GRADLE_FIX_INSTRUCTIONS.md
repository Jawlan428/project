# Gradle Build Error Fix

## ✅ What Was Fixed
All Gradle caches have been cleared:
- **User Gradle Cache (8.13)**: `C:\Users\ASUS\.gradle\caches\8.13`
- **Gradle Daemon Cache**: `C:\Users\ASUS\.gradle\daemon`
- **Gradle Wrapper Cache**: `C:\Users\ASUS\.gradle\wrapper\dists`
- **Unity Build Cache**: `Library\Bee\Android\Prj\IL2CPP\Gradle`
- **Unity Artifacts Cache**: `Library\Bee\artifacts\Android\Gradle`
- **Unity Temp Gradle**: `Temp\StagingArea\gradleWarmupArea`

## 🔄 Next Steps

### 1. Try Building Again
Simply try building your Unity project again. The corrupted cache has been cleared and Gradle will regenerate it.

### 2. If the Error Persists

#### Option A: Clear Entire Groovy-DSL Cache
Run this PowerShell command:
```powershell
Remove-Item -Path "$env:USERPROFILE\.gradle\caches\8.13\groovy-dsl" -Recurse -Force
```

#### Option B: Clear Entire Gradle Cache (Nuclear Option)
If Option A doesn't work, clear the entire Gradle cache:
```powershell
Remove-Item -Path "$env:USERPROFILE\.gradle\caches" -Recurse -Force
```
**Note:** This will make the next build slower as Gradle will need to re-download dependencies.

### 3. Unity-Specific Fixes

#### Clear Unity's Gradle Cache
Unity also maintains its own Gradle cache. You can clear it:
1. Close Unity
2. Delete: `C:\Users\ASUS\AppData\Local\Unity\cache\packages\packages.unity.com\com.unity.external.tool\gradle`
3. Reopen Unity and try building again

#### Use Unity's Built-in Gradle
In Unity Editor:
1. Go to **Edit > Preferences > External Tools**
2. Make sure **Android > Gradle** is set to Unity's bundled version
3. Or try switching to a different Gradle version

### 4. Alternative: Use Custom Gradle Template
If issues persist, you can use a custom Gradle template:
1. In Unity: **Edit > Project Settings > Player > Android > Publishing Settings**
2. Check **Custom Main Gradle Template**
3. Unity will generate a `mainTemplate.gradle` file in `Assets/Plugins/Android/`
4. You can modify this if needed

## 🐛 Why This Happens
Gradle cache corruption can occur due to:
- Interrupted builds
- Disk I/O errors
- Antivirus software interfering
- File system issues
- Network interruptions during dependency downloads

## ✅ Verification
After clearing the cache, your next build should:
1. Take longer (Gradle re-downloads dependencies)
2. Complete successfully without the metadata.bin error

---

**Status:** Corrupted cache directory removed. Try building now!

