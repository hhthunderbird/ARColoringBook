# AR Foundation Sample Management System

## ?? What Is This?

**Automatic protection against Unity version conflicts** - This system prevents compilation errors when switching Unity versions by detecting and safely removing incompatible AR Foundation sample files.

### Quick Summary

**The Problem**: Different Unity versions use different AR Foundation versions. Old sample scripts from a previous Unity version can cause hundreds of compilation errors in your new version.

**The Solution**: This system automatically detects incompatible samples when you open your project and helps you remove them safely.

**For You**: ? No more version conflicts, ? Clean project, ? Fast Unity upgrades

---

## ?? Quick Start

### If You See the Warning Dialog

**You'll see a dialog like this when opening your project:**
```
?? Found 3 incompatible AR Foundation sample folder(s) for Unity 6000.3.0f1:

?? Assets/Samples/AR Foundation/4.0.0/ImageTracking
   AR Foundation 4.0.0
   
?? Assets/Samples/AR Foundation/4.0.0/FaceTracking
   AR Foundation 4.0.0
```

**What to do:**
1. Click **"Remove Automatically"** ? (Recommended - instant cleanup)
2. Or click **"Show in Explorer"** ?? (if you want to see files first)
3. Or click **"Ignore"** ?? (not recommended - may cause errors)

### Manual Cleanup Anytime

Run this menu command to check for issues:
```
Felina > Clean AR Foundation Samples
```

This is useful:
- After switching Unity versions
- When you see compilation errors
- Before importing new AR Foundation samples

---

## ?? How It Works

### Automatic Detection
When you open your Unity project, the system:

1. **Scans** for AR Foundation sample folders in `Assets/Samples/`
2. **Detects** your current Unity version (e.g., Unity 6.3 or Unity 2019.4)
3. **Checks** compatibility of found samples with your Unity version
4. **Alerts** you if incompatible samples are detected

### Version Compatibility Rules

| Unity Version | Compatible AR Foundation Samples |
|---------------|----------------------------------|
| Unity 2019.4 - 2021.x | AR Foundation 4.x |
| Unity 2022.x | AR Foundation 5.x |
| Unity 2023.x, Unity 6.x+ | AR Foundation 6.x+ |

### Sample Detection Patterns

The system looks for folders matching these patterns:
- `AR Foundation`
- `ARFoundation`
- `XR AR Foundation`

Examples of detected paths:
```
Assets/Samples/AR Foundation/2.1.18/ImageTracking/
Assets/Samples/ARFoundation/6.3.1/PlaneDetection/
Assets/Samples/XR AR Foundation/5.1.5/FaceTracking/
```

---

## Usage

### Automatic Mode (Recommended)

The system runs automatically when Unity starts. If incompatible samples are found, you'll see a dialog with options:

**Option 1: Remove Automatically** ? (Recommended)
- Instantly removes all incompatible sample folders
- Cleans up `.meta` files
- Refreshes AssetDatabase
- Shows summary of removed items

**Option 2: Show in Explorer** ??
- Opens the sample folder in your file explorer
- Allows manual inspection
- You delete files yourself

**Option 3: Ignore** ??
- Skips cleanup for this session
- May cause compilation errors
- Not recommended unless you know what you're doing

### Manual Mode

You can manually trigger sample cleanup anytime:

**Via Unity Menu:**
```
Felina > Clean AR Foundation Samples
```

This forces a re-scan regardless of previous session state.

---

## Example Scenario

### Problem: Unity 6.3 with AR Foundation 4.x Samples

You're using **Unity 6.3** but your project has old **AR Foundation 4.0.0 samples** imported from a previous Unity version. These samples use deprecated APIs and cause hundreds of compilation errors.

### Solution: Automatic Cleanup

1. Open project in Unity 6.3
2. Sample manager detects incompatibility:
   ```
   Found 3 incompatible AR Foundation sample folder(s) for Unity 6000.3.0f1:
   
   ? Assets/Samples/AR Foundation/4.0.0/ImageTracking
      AR Foundation 4.0.0
   
   ? Assets/Samples/AR Foundation/4.0.0/FaceTracking
      AR Foundation 4.0.0
   
   ? Assets/Samples/AR Foundation/4.0.0/PlaneDetection
      AR Foundation 4.0.0
   
   These samples were designed for a different Unity version and may cause compilation errors.
   
   Recommended action: Remove them and import samples matching your Unity version.
   ```
3. Click **"Remove Automatically"**
4. ? Sample folders deleted
5. ? Compilation errors gone
6. Import AR Foundation 6.3.1 samples via Package Manager (if needed)

---

## Importing Compatible Samples

### After Cleanup

Once incompatible samples are removed, you can safely import the correct ones:

#### For Unity 2019.4:
```
Window > Package Manager
> AR Foundation (4.0.0)
> Samples > Import
```

#### For Unity 2022.3:
```
Window > Package Manager
> AR Foundation (5.1.5)
> Samples > Import
```

#### For Unity 6.3:
```
Window > Package Manager
> AR Foundation (6.3.1)
> Samples > Import
```

### What Samples to Import?

You only need samples relevant to your use case:

| Sample | Purpose | Recommended For |
|--------|---------|-----------------|
| Image Tracking | Track printed images/markers | AR Coloring Book (Yes!) |
| Plane Detection | Detect floor/walls | AR placement apps |
| Face Tracking | Track human faces | AR filters/masks |
| Meshing | 3D environment scanning | Advanced AR apps |

**For AR Coloring Book:** Import **Image Tracking** sample only.

---

## Git Integration

### .gitignore Configuration

The project `.gitignore` is configured to **exclude all AR Foundation sample folders**:

```gitignore
# AR Foundation Samples - Version-specific, do not commit
/Samples/
/Assets/Samples/
**/Samples/*AR Foundation*/
**/Samples/*ARFoundation*/
**/Samples/*XR AR Foundation*/

# Specific paths
Assets/Samples/AR Foundation/
Assets/Samples/ARFoundation/
Assets/Samples/XR AR Foundation/
```

### Why Exclude Samples from Git?

1. **Version Conflicts:** Different team members may use different Unity versions
2. **Large File Sizes:** Samples include meshes, textures, prefabs (unnecessary bloat)
3. **User-Specific:** Each developer imports samples matching their Unity version
4. **Package Manager:** Samples can be imported anytime from Package Manager

### Best Practice

**? DO:**
- Import samples matching your Unity version
- Use `Felina > Clean AR Foundation Samples` when switching Unity versions
- Add your own scripts/assets to version control

**? DON'T:**
- Commit `Assets/Samples/` folder to Git
- Share sample folders between projects with different Unity versions
- Ignore cleanup warnings

---

## Troubleshooting

### Issue: Dialog Doesn't Appear

**Cause:** Sample manager already ran this session

**Solution:**
```
Felina > Clean AR Foundation Samples
```

### Issue: Cleanup Failed

**Error:** "Failed to remove X folder(s)"

**Cause:** Files are in use or locked

**Solution:**
1. Close Unity
2. Manually delete `Assets/Samples/AR Foundation/` folder
3. Reopen Unity
4. AssetDatabase will auto-refresh

### Issue: Still Getting Compilation Errors

**Cause:** Other incompatible files remain

**Solution:**
1. Check Console for specific error messages
2. Run `Felina > Validate Package Setup`
3. Verify AR Foundation version matches Unity version:
   ```
   Window > Package Manager > AR Foundation
   ```
4. Reimport compatible samples if needed

### Issue: Lost My Custom Sample Modifications

**Cause:** Custom changes were in imported sample folder

**Solution (Prevention):**
- ? **Never modify imported samples directly**
- ? **Copy samples to a different folder** (e.g., `Assets/MyScripts/`)
- ? **Make modifications in your copy**
- ? **Original samples remain pristine for cleanup**

---

## Technical Details

### Detection Algorithm

1. Scan `Assets/Samples/` for AR Foundation folders
2. Parse version from path (e.g., `4.0.0`, `6.3.1`)
3. Compare major version with Unity compatibility matrix
4. Flag incompatible versions

### Session State

Sample check uses `SessionState` to avoid repeated dialogs:
- First run: Shows dialog if issues found
- Subsequent runs (same session): Silent unless manual trigger
- New session (Unity restart): Checks again

### Safe Deletion

The cleanup process:
1. Starts batch asset editing mode
2. Deletes folder (recursive)
3. Removes `.meta` file
4. Stops batch editing
5. Refreshes AssetDatabase
6. Reports results

---

## Integration with Other Systems

### Package Validator

The Sample Manager works alongside `PackageValidator.cs`:

**PackageValidator:**
- Checks installed AR Foundation version
- Verifies Unity Mathematics
- Validates platform plugins (ARKit/ARCore)
- Recommends correct versions

**Sample Manager:**
- Cleans up incompatible sample **files**
- Prevents compilation errors from old APIs
- Ensures clean slate for fresh imports

### Workflow

```
Unity Startup
    ?
PackageValidator validates installed packages
    ?
ARFoundationSampleManager checks sample files
    ?
User gets complete validation report
    ?
User fixes issues (install packages, clean samples)
    ?
Project compiles successfully
```

---

## FAQ

### Q: Can I disable automatic scanning?

**A:** Not currently. Automatic scanning prevents most issues. If you need to bypass it temporarily, ignore the dialog and it won't show again until next Unity restart.

### Q: Will this delete my custom AR scripts?

**A:** No. Only sample folders matching AR Foundation patterns are affected. Your custom scripts in `Assets/Scripts/` or other folders are safe.

### Q: What if I need samples from multiple versions?

**A:** Not recommended. Unity can only use one AR Foundation version at a time. Import samples matching your current AR Foundation version only.

### Q: How do I know which samples I have?

**A:** Run `Felina > Clean AR Foundation Samples` to see a list of detected sample folders and their versions.

### Q: Can I commit samples to Git for my team?

**A:** Not recommended. Each team member should import samples matching their Unity version. The `.gitignore` prevents accidental commits.

---

## Summary

### What This System Does

? **Automatically detects** incompatible AR Foundation samples  
? **Provides clear warnings** with version information  
? **Safely removes** problematic files  
? **Prevents compilation errors** when switching Unity versions  
? **Works with Git** to keep repository clean  

### What You Should Do

1. **Let it run automatically** - Don't ignore the dialog
2. **Remove incompatible samples** - Click "Remove Automatically"
3. **Import matching samples** - Use Package Manager for your Unity version
4. **Don't commit samples to Git** - Let `.gitignore` handle it
5. **Run manual check** when switching Unity versions

---

**With this system, you can confidently switch between Unity versions without worrying about sample file conflicts!** ??