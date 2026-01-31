# Multi-Version Support

Felina AR Texture Mapping supports Unity 2019.4 LTS and newer, and AR Foundation 4.x or higher (no known max version).

## Supported Platforms
- iOS (ARKit) — uses native plugin: `InternalSolver.a`
- Android (ARCore) — uses native plugin: `InternalSolver.so` (armv7/arm64)
- Editor (for configuration and setup only)

## Version Handling
- Uses platform abstraction and conditional compilation to support multiple Unity and AR Foundation versions.
- Compatible with both ARSessionOrigin and XR Origin setups.
- All features tested with AR Foundation 4.x and above.

## Not Supported
- macOS runtime AR is not supported.

## References
All information is based on the package's included documentation and source files. No unsupported features are claimed.
