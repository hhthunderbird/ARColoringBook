# Contributing to Felina AR Coloring Book

Thank you for your interest in contributing! This document provides guidelines and instructions for contributing to this project.

## ?? Quick Start

1. Fork the repository
2. Clone your fork: `git clone https://github.com/YOUR_USERNAME/ARColoringBook.git`
3. Create a branch: `git checkout -b feature/your-feature-name`
4. Make your changes
5. Commit: `git commit -m "feat: Add your feature"`
6. Push: `git push origin feature/your-feature-name`
7. Open a Pull Request

## ?? Development Setup

### Prerequisites

- Unity 2022.3 or later
- Visual Studio / VS Code / Rider
- Git

### Optional (for native development)
- **iOS**: macOS with Xcode, CMake
- **Android**: Android NDK
- **Windows**: Visual Studio with C++ tools
- **macOS**: Xcode Command Line Tools

### Initial Setup

```bash
# Clone repository
git clone https://github.com/hhthunderbird/ARColoringBook.git
cd ARColoringBook

# Open in Unity Hub
# File > Open Project
# Select ARColoringBook directory

# Install required packages
# Window > Package Manager
# Ensure AR Foundation is installed
```

## ??? Project Structure

```
ARColoringBook/
??? Assets/
?   ??? ColouringBook/           # Main package
?       ??? Scripts/
?       ?   ??? Runtime/         # Runtime scripts
?       ?   ??? Settings/        # Configuration
?       ??? Editor/              # Editor scripts
?       ??? Shader/              # Custom shaders
?       ??? Materials/           # Sample materials
?       ??? Scenes/              # Sample scenes
??? FelinaLibrary/               # Native C++ code
?   ??? src/                     # Source files
?   ??? include/                 # Headers
?   ??? CMakeLists.txt          # Build configuration
??? .github/
?   ??? workflows/               # CI/CD pipelines
??? cmake/                       # CMake toolchains

```

## ?? Contribution Guidelines

### Code Style

#### C# Style
- Follow [Microsoft C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use `var` for local variables where type is obvious
- Prefix private fields with `_` (underscore)
- Use PascalCase for public members
- Use camelCase for local variables

```csharp
// ? Good
public class MyClass
{
    private int _privateField;
    public int PublicProperty { get; set; }
    
    private void MyMethod()
    {
        var localVariable = 10;
    }
}

// ? Bad
public class myclass
{
    private int privateField;
    public int publicProperty { get; set; }
    
    private void my_method()
    {
        int local_variable = 10;
    }
}
```

#### C++ Style
- Follow [Google C++ Style Guide](https://google.github.io/styleguide/cppguide.html)
- Use snake_case for functions
- Use PascalCase for classes
- Document public APIs with Doxygen comments

```cpp
// ? Good
class ImageProcessor
{
public:
    /// @brief Process image with homography
    /// @param image Input image
    /// @return Processed result
    bool process_image(const Image& image);
    
private:
    int internal_state_;
};

// ? Bad
class imageProcessor
{
public:
    bool ProcessImage(const Image& image);
    
private:
    int internalState;
};
```

### Commit Messages

Follow [Conventional Commits](https://www.conventionalcommits.org/):

```bash
# Format
<type>(<scope>): <subject>

# Types
feat:     # New feature
fix:      # Bug fix
docs:     # Documentation only
style:    # Formatting, no code change
refactor: # Code restructuring
test:     # Adding tests
chore:    # Maintenance

# Examples
feat(scanner): Add quality threshold configuration
fix(ios): Resolve RenderTexture format issue
docs(readme): Update installation instructions
chore(deps): Update AR Foundation to 5.1
```

### Pull Request Process

1. **Create Feature Branch**
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **Make Changes**
   - Write clear, concise code
   - Add comments for complex logic
   - Update documentation if needed

3. **Test Your Changes**
   ```bash
   # Test in Unity
   # - Open sample scene
   # - Enter Play mode
   # - Verify functionality
   
   # Build for target platforms
   # - iOS
   # - Android
   ```

4. **Commit Changes**
   ```bash
   git add .
   git commit -m "feat: Add your feature"
   ```

5. **Push Branch**
   ```bash
   git push origin feature/your-feature-name
   ```

6. **Open Pull Request**
   - Go to GitHub repository
   - Click "New Pull Request"
   - Select your branch
   - Fill in PR template
   - Request review

### PR Template

```markdown
## Description
Brief description of changes

## Type of Change
- [ ] Bug fix
- [ ] New feature
- [ ] Breaking change
- [ ] Documentation update

## Testing
- [ ] Tested in Unity Editor
- [ ] Tested on iOS device
- [ ] Tested on Android device
- [ ] Added/updated tests

## Checklist
- [ ] Code follows style guidelines
- [ ] Self-review completed
- [ ] Comments added for complex code
- [ ] Documentation updated
- [ ] No new warnings
- [ ] Dependent changes merged
```

## ?? Reporting Bugs

### Before Reporting
1. Check existing issues
2. Verify on latest version
3. Test in clean project

### Bug Report Template

```markdown
## Bug Description
Clear description of the bug

## Steps to Reproduce
1. Step one
2. Step two
3. ...

## Expected Behavior
What should happen

## Actual Behavior
What actually happens

## Environment
- Unity Version: 2022.3.x
- Platform: iOS/Android
- Device: iPhone 14 Pro
- Package Version: 1.0.0

## Screenshots/Logs
Attach relevant screenshots or logs

## Additional Context
Any other relevant information
```

## ?? Feature Requests

### Request Template

```markdown
## Feature Description
Clear description of the feature

## Use Case
Why is this needed? What problem does it solve?

## Proposed Solution
How should it work?

## Alternatives Considered
Other approaches you've considered

## Additional Context
Any other relevant information
```

## ?? Development Tasks

### Building Native Libraries

#### iOS
```bash
cd FelinaLibrary
mkdir -p build/ios
cd build/ios

cmake ../.. \
  -G Ninja \
  -DCMAKE_TOOLCHAIN_FILE=../../cmake/ios.toolchain.cmake \
  -DPLATFORM=OS64 \
  -DCMAKE_BUILD_TYPE=Release

ninja
```

#### Android
```bash
cd FelinaLibrary
mkdir -p build/android
cd build/android

cmake ../.. \
  -G Ninja \
  -DCMAKE_TOOLCHAIN_FILE=$ANDROID_NDK/build/cmake/android.toolchain.cmake \
  -DANDROID_ABI=arm64-v8a \
  -DANDROID_PLATFORM=android-24 \
  -DCMAKE_BUILD_TYPE=Release

ninja
```

#### Windows
```bash
cd FelinaLibrary
mkdir -p build/windows
cd build/windows

cmake ../.. -G "Visual Studio 17 2022" -A x64
cmake --build . --config Release
```

#### macOS
```bash
cd FelinaLibrary
mkdir -p build/macos
cd build/macos

cmake ../.. -G Xcode
cmake --build . --config Release
```

### Running Tests

```bash
# Unity Test Runner
# Window > General > Test Runner
# Run All Tests

# Or via command line
unity-editor \
  -runTests \
  -batchmode \
  -projectPath . \
  -testResults test-results.xml \
  -testPlatform EditMode
```

### Creating Unity Package

```bash
# Via Unity Editor
# Right-click Assets/ColouringBook
# Export Package...
# Ensure all items checked
# Save as .unitypackage

# Via GitHub Actions
# Push to main or create tag
# Package created automatically
```

## ?? Documentation

### Updating Documentation

- **README.md**: Installation and quick start
- **API docs**: XML comments in code
- **Wiki**: Detailed guides and tutorials
- **CHANGELOG.md**: Version history

### Documentation Style

```csharp
/// <summary>
/// Brief description of what this does
/// </summary>
/// <param name="paramName">Description of parameter</param>
/// <returns>Description of return value</returns>
/// <example>
/// <code>
/// // Example usage
/// var result = MyMethod(input);
/// </code>
/// </example>
public int MyMethod(string paramName)
{
    // Implementation
}
```

## ?? Asset Guidelines

### Creating Sample Assets

- **Scenes**: Keep simple and focused
- **Prefabs**: Document usage in inspector
- **Materials**: Use standard shaders
- **Textures**: Compress appropriately
- **Scripts**: Add example comments

### Size Limits

- Package should be < 50MB
- Individual files < 10MB
- Compress textures/audio

## ?? Testing Guidelines

### Unit Tests

```csharp
using NUnit.Framework;
using UnityEngine.TestTools;

public class MyClassTests
{
    [Test]
    public void TestMethod_ExpectedBehavior()
    {
        // Arrange
        var instance = new MyClass();
        
        // Act
        var result = instance.Method();
        
        // Assert
        Assert.AreEqual(expected, result);
    }
}
```

### Integration Tests

```csharp
[UnityTest]
public IEnumerator TestARScannerInitialization()
{
    // Setup scene
    var scanner = new GameObject().AddComponent<ARScannerManager>();
    
    // Wait for initialization
    yield return null;
    
    // Verify
    Assert.IsNotNull(scanner.Instance);
}
```

## ??? Versioning

We use [Semantic Versioning](https://semver.org/):

- **MAJOR**: Breaking changes
- **MINOR**: New features (backward compatible)
- **PATCH**: Bug fixes

```
v1.0.0 ? v1.1.0  (new feature)
v1.1.0 ? v1.1.1  (bug fix)
v1.1.1 ? v2.0.0  (breaking change)
```

## ?? License

By contributing, you agree that your contributions will be licensed under the same license as the project (see LICENSE.md).

## ?? Getting Help

- **GitHub Issues**: Bug reports and feature requests
- **Discussions**: Questions and community help
- **Email**: support@felina.dev
- **Discord**: [Join our server]

## ?? Recognition

Contributors will be:
- Listed in CONTRIBUTORS.md
- Mentioned in release notes
- Credited in package metadata

Thank you for contributing! ??

---

Last Updated: 2024-01-15
