# Unity Export Script Error - Better Logging

## Issue

The Unity build is failing with exit code 1, but no clear error message is shown in the logs:

```
###########################
#         Failure         #
###########################
Error: Build failed with exit code 1
```

## Root Causes

### 1. Missing Error Handling
The export script didn't have try-catch blocks or detailed logging, making it impossible to diagnose failures.

### 2. Silent Failures
Unity was failing but not logging the reason why the `ExportPackage.Export` method failed.

### 3. Path Issues
The output path might not be created correctly, or the `Assets/ColouringBook` folder might not exist.

## Solutions Applied

### 1. Enhanced Export Script with Logging

**Added:**
- ? Try-catch block to catch all exceptions
- ? Detailed logging at each step
- ? Validation that asset paths exist
- ? File size logging after export
- ? Explicit `EditorApplication.Exit()` calls with correct exit codes

**New Script Features:**

```csharp
public static void Export()
{
    try
    {
        Debug.Log("=== Starting Package Export ===");
        
        // Log all environment variables
        Debug.Log($"Package Name: {packageName}");
        Debug.Log($"Version: {version}");
        Debug.Log($"Current Directory: {Directory.GetCurrentDirectory()}");
        
        // Create and verify Builds directory
        string buildsDir = Path.Combine(Application.dataPath, "..", "Builds");
        if (!Directory.Exists(buildsDir))
        {
            Directory.CreateDirectory(buildsDir);
            Debug.Log($"Created Builds directory: {buildsDir}");
        }
        
        // Validate asset paths exist
        foreach (string assetPath in assetPaths)
        {
            if (!Directory.Exists(assetPath) && !File.Exists(assetPath))
            {
                Debug.LogError($"Asset path does not exist: {assetPath}");
                EditorApplication.Exit(1);
                return;
            }
            Debug.Log($"Including: {assetPath}");
        }
        
        // Export with logging
        Debug.Log("Starting AssetDatabase.ExportPackage...");
        AssetDatabase.ExportPackage(assetPaths, outputPath, options);
        Debug.Log("AssetDatabase.ExportPackage completed");
        
        // Verify file was created
        if (File.Exists(outputPath))
        {
            FileInfo fileInfo = new FileInfo(outputPath);
            Debug.Log($"? Package exported successfully!");
            Debug.Log($"  Path: {outputPath}");
            Debug.Log($"  Size: {fileInfo.Length} bytes ({fileInfo.Length / 1024 / 1024} MB)");
            EditorApplication.Exit(0);  // Explicit success exit
        }
        else
        {
            Debug.LogError($"? Package file not found after export");
            EditorApplication.Exit(1);  // Explicit failure exit
        }
    }
    catch (Exception e)
    {
        Debug.LogError($"? Exception: {e.Message}");
        Debug.LogError($"Stack trace: {e.StackTrace}");
        EditorApplication.Exit(1);
    }
}
```

### 2. Added Meta File

Created `.meta` file for the export script to ensure Unity recognizes it:

```yaml
fileFormatVersion: 2
guid: 12345678901234567890123456789012
MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
```

### 3. Enhanced Unity Build Parameters

Added custom parameters for better logging:

```yaml
customParameters: -quit -nographics -logFile -
```

**What this does:**
- `-quit`: Exit Unity after build
- `-nographics`: Don't initialize graphics (faster, server-friendly)
- `-logFile -`: Output logs to stdout (visible in CI)

### 4. Added Log Checking Step

```yaml
- name: Check Unity Logs
  if: always()  # Run even if previous step fails
  run: |
    # Find and display all log files
    find . -name "*.log" -type f -exec cat {} \;
    
    # Check all possible build output locations
    ls -la build/
    ls -la Builds/
    ls -la ../Builds/
```

## Debugging Process

### Step 1: Check if Script is Found

The logs should now show:
```
=== Starting Package Export ===
Package Name: FelinaARColoringBook
Version: 2025.12.22-fc44cc8
Current Directory: /github/workspace
```

If you don't see this, the script isn't being executed.

### Step 2: Check Asset Path

The logs should show:
```
Including: Assets/ColouringBook
```

If you see `Asset path does not exist`, the folder name or path is wrong.

### Step 3: Check Export Process

The logs should show:
```
Starting AssetDatabase.ExportPackage...
AssetDatabase.ExportPackage completed
? Package exported successfully!
  Path: /github/workspace/Builds/FelinaARColoringBook_v2025.12.22-fc44cc8.unitypackage
  Size: 12345678 bytes (11 MB)
```

### Step 4: Check for Exceptions

If there's an exception, you'll see:
```
? Exception: [error message]
Stack trace: [stack trace]
```

## Possible Issues & Solutions

### Issue 1: "Assets/ColouringBook" Not Found

**Error:**
```
Asset path does not exist: Assets/ColouringBook
```

**Solution:**
Check the actual folder name:
```bash
ls -la Assets/
```

It might be:
- `Assets/ColouringBook` ?
- `Assets/ColoringBook` (American spelling)
- `Assets/Coloring Book` (with space)
- Case sensitivity issue

**Fix:**
```yaml
string[] assetPaths = new string[]
{
    "Assets/ColouringBook"  # Verify this exact path
};
```

### Issue 2: Builds Directory Not Created

**Error:**
```
? Package file not found after export
```

**Solution:**
The script now creates the directory explicitly:
```csharp
string buildsDir = Path.Combine(Application.dataPath, "..", "Builds");
Directory.CreateDirectory(buildsDir);
```

### Issue 3: Permission Issues

**Error:**
```
UnauthorizedAccessException: Access to the path is denied
```

**Solution:**
Add write permissions:
```yaml
- name: Set Permissions
  run: chmod -R 777 .
```

### Issue 4: Script Not Compiled

**Error:**
```
error CS0103: The name 'ExportPackage' does not exist
```

**Solution:**
1. Ensure script is in `Assets/Editor/` folder ?
2. Ensure `.meta` file is created ?
3. Unity needs to compile the script first

**Alternative:** Use full namespace:
```yaml
buildMethod: ExportPackage.Export
```

## Expected Output

### Successful Export:

```
=== Starting Package Export ===
Package Name: FelinaARColoringBook
Version: 2025.12.22-fc44cc8
Current Directory: /github/workspace
Created Builds directory: /github/workspace/Builds
Output Path: /github/workspace/Builds/FelinaARColoringBook_v2025.12.22-fc44cc8.unitypackage
Including: Assets/ColouringBook
Starting AssetDatabase.ExportPackage...
AssetDatabase.ExportPackage completed
? Package exported successfully!
  Path: /github/workspace/Builds/FelinaARColoringBook_v2025.12.22-fc44cc8.unitypackage
  Size: 15728640 bytes (15 MB)
```

### Failed Export:

```
=== Starting Package Export ===
Package Name: FelinaARColoringBook
Version: 2025.12.22-fc44cc8
Current Directory: /github/workspace
Asset path does not exist: Assets/ColouringBook
? Package export failed
```

OR

```
=== Starting Package Export ===
[... successful logs ...]
? Exception: [Error details]
Stack trace: [Full stack trace]
```

## Testing Locally

To test the export script locally:

1. **Copy the script:**
```bash
mkdir -p Assets/Editor
# Copy ExportPackage.cs to Assets/Editor/
```

2. **Run Unity in batch mode:**
```bash
Unity -quit -batchmode -projectPath . \
  -executeMethod ExportPackage.Export \
  -logFile - \
  PACKAGE_NAME=TestPackage \
  PACKAGE_VERSION=1.0.0
```

3. **Check output:**
```bash
ls -la Builds/
```

## Commit These Changes

```bash
git add .github/workflows/build-and-package.yml
git commit -m "fix(ci): enhance Unity export script with detailed logging

- Add comprehensive error handling and try-catch blocks
- Add detailed logging at every step of export process
- Add validation that asset paths exist before export
- Add explicit EditorApplication.Exit() calls with exit codes
- Add meta file for export script to ensure Unity recognition
- Add custom Unity parameters for better log output
- Add log checking step that runs even on failure
- Log file size and full path after successful export

This will help diagnose why the package export is failing."

git push origin feature/store-version
```

---

**Status**: Enhanced with detailed logging and error handling ?

**Next Steps:**
1. Push changes
2. Check workflow logs for detailed error messages
3. Fix any issues revealed by the new logging
4. Package export should succeed with clear success/failure messages
