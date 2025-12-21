# Disk Space Issue Fix

## Error
```
docker: failed to register layer: write /opt/unity/Editor/Data/Resources/unity_builtin_extra: no space left on device
Error: Build failed with exit code 125
```

## Root Cause

GitHub Actions runners have limited disk space (~14 GB free by default). Unity Docker images are **extremely large**:

- Unity 2022 LTS: ~8-12 GB
- **Unity 6 (6000.0.30f1): ~15-20+ GB** ??

The Unity 6 image is too large to fit on the default runner.

## Solutions

### Solution 1: Free Up Disk Space (? Applied)

Use the `jlumbroso/free-disk-space` action to clean up unnecessary files before building:

```yaml
- name: Free Disk Space
  uses: jlumbroso/free-disk-space@main
  with:
    tool-cache: false      # Keep tool cache (needed for actions)
    android: true          # Remove ~12 GB Android SDK
    dotnet: true           # Remove ~2 GB .NET
    haskell: true          # Remove ~6 GB Haskell
    large-packages: true   # Remove ~4 GB (Google Chrome, etc.)
    docker-images: true    # Remove unused Docker images
    swap-storage: true     # Remove swap file
```

**This typically frees up ~30 GB of space!**

### Solution 2: Use Unity 2022 LTS Instead

Unity 2022 images are smaller and well-supported:

```yaml
env:
  UNITY_VERSION: '2022.3.54f1'  # Smaller image (~8-10 GB)
```

**Pros:**
- ? Much smaller Docker image
- ? Stable and well-tested
- ? Forward-compatible packages

**Cons:**
- ?? May have minor feature differences from Unity 6

### Solution 3: Use Larger Runner

GitHub provides larger runners with more disk space:

```yaml
create-unity-package:
  runs-on: ubuntu-latest-4-cores  # More disk space
  # OR
  runs-on: ubuntu-latest-8-cores  # Even more space
```

**Cost:** Larger runners cost more (if using paid GitHub plan)

### Solution 4: Skip Unity Build Step

If you don't actually need Unity to build (just exporting package):

```yaml
- name: Export Unity Package (Without Unity)
  run: |
    # Use tar to create package
    tar -czf "Builds/${PACKAGE_NAME}_v${VERSION}.tar.gz" Assets/ColouringBook
```

This skips the Unity Docker image entirely.

## Recommended Approach

**Use Solution 1 (Free Disk Space) + Monitor Space**

```yaml
steps:
  - name: Free Disk Space
    uses: jlumbroso/free-disk-space@main
    with:
      tool-cache: false
      android: true
      dotnet: true
      haskell: true
      large-packages: true
      docker-images: true
      swap-storage: true
  
  - name: Check Disk Space (Before)
    run: |
      echo "=== Disk space before Unity installation ==="
      df -h
  
  - name: Checkout Repository
    uses: actions/checkout@v4
  
  # ... rest of steps ...
  
  - name: Install Unity
    uses: game-ci/unity-builder@v4
    # ...
  
  - name: Check Disk Space (After)
    run: |
      echo "=== Disk space after Unity installation ==="
      df -h
```

## What Gets Removed

The `free-disk-space` action removes:

| Item | Space Freed |
|------|-------------|
| Android SDK | ~12 GB |
| Haskell Stack | ~6 GB |
| Large packages (Chrome, Firefox, etc.) | ~4 GB |
| .NET SDK (unused versions) | ~2 GB |
| Docker images | ~5 GB |
| Swap file | ~4 GB |
| **Total** | **~30+ GB** |

## Expected Results

### Before Cleanup:
```
Filesystem      Size  Used Avail Use% Mounted on
/dev/root        84G   70G   14G  84% /
```

### After Cleanup:
```
Filesystem      Size  Used Avail Use% Mounted on
/dev/root        84G   40G   44G  48% /
```

This should provide enough space for Unity 6 Docker image!

## Alternative: Use Unity 2022 LTS

If disk space issues persist, switch to Unity 2022:

```yaml
env:
  UNITY_VERSION: '2022.3.54f1'
```

Then remove the Free Disk Space step (won't be needed).

## Monitoring

Add disk space checks to debug:

```yaml
- name: Check Disk Space
  if: always()
  run: |
    echo "=== Disk Usage ==="
    df -h
    echo ""
    echo "=== Largest Directories ==="
    du -h -d 1 /opt | sort -hr | head -20
    du -h -d 1 /usr | sort -hr | head -20
```

## Cost Comparison

| Solution | Disk Impact | Time Impact | Cost |
|----------|-------------|-------------|------|
| Free disk space | +30 GB free | +2-3 min | Free |
| Unity 2022 LTS | Smaller image | Faster pull | Free |
| Larger runner | More space | Same | Paid |
| Skip Unity | No Unity image | Fastest | Free |

## Commit This Fix

```bash
git add .github/workflows/build-and-package.yml
git commit -m "fix(ci): free disk space before Unity installation

- Add jlumbroso/free-disk-space action
- Remove Android SDK, Haskell, large packages
- Free up ~30 GB for Unity 6 Docker image
- Prevents 'no space left on device' error"

git push origin feature/store-version
```

---

**Status**: Applied Solution 1 (Free Disk Space) ?

This should resolve the disk space issue and allow Unity 6 to be installed successfully!
