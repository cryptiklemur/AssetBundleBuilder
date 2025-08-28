# AssetBundleBuilder

Command line tool for building Unity asset bundles for RimWorld mods. No repository cloning or Unity project setup
required.

## Quick Start

### Recommended: Using Configuration File

Create a `.assetbundle.toml` file in your project:

```toml
[global]
unity_version = "2022.3.35f1"
bundle_name = "author.modName"
asset_directory = "Assets/"
output_directory = "AssetBundles/"
build_targets = ["windows", "linux", "mac"]
link_method = "junction"  # or "copy", "symlink", "hardlink"

[bundles.textures]
filename = "resource_[bundle_name]_textures_[target]"
exclude_patterns = ["*.shader"]
build_target = "none" # will build a suffix-less asset bundle for every platform

[bundles.shaders] # specify `--target windows` (or mac or linux) when running assetbundlebuilder to build for each platform
filename = "resource_[bundle_name]_shaders_[target]"
include_patterns = ["*.shader"]
```

Then build your bundles:

```bash
# Build a specific bundle, loads automatically from .assetbundler.toml
assetbundlebuilder --bundle-config textures

# Override settings from command line
assetbundlebuilder --bundle-config shaders --target window
```

### Basic CLI Usage (Alternative)

```bash
# Simple command line usage without config file
assetbundlebuilder 2022.3.35f1 "Assets/" "author.modName" "AssetBundles/"

# With additional options
assetbundlebuilder 2022.3.35f1 "Assets/" "author.modName" "AssetBundles/" --target windows
```

## Installation

### .NET Global Tool (Cross Platform)

```bash
# Install globally
dotnet tool install --global CryptikLemur.AssetBundleBuilder

# Use anywhere
assetbundlebuilder --config myproject.toml --bundle-config mybundle
```

### Manual Installation

Download the appropriate executable for your platform from
the [Releases Page](https://github.com/CryptikLemur/AssetBundleBuilder/releases).

## Configuration File Format

The TOML configuration file allows you to define multiple asset bundles with different settings:

### Global Configuration

```toml
[global]
unity_version = "2022.3.35f1"        # Unity version to use
unity_path = "/path/to/Unity.exe"    # Or specify exact path
output_directory = "Output"          # Default output directory
build_target = "windows"             # Default build target
build_targets = ["windows", "linux"] # Restrict allowed targets
temp_project_path = "/tmp/unity"     # Custom temp directory
clean_temp_project = false           # Clean temp project after building. Disabled by default for caching
link_method = "copy"                 # How to link assets: copy/symlink/hardlink/junction
log_file = "unity.log"               # Unity log output
ci_mode = false                      # Disable Unity auto-installation
non_interactive = false              # No prompts
verbosity = "verbose"                # Output verbosity: quiet/normal/verbose/debug
exclude_patterns = ["*.meta", "*.tmp"] # Files to exclude
include_patterns = ["*.png", "*.wav"]  # Files to include
```

### Bundle Configuration

```toml
[bundles.mybundle]
description = "My custom asset bundle"
asset_directory = "Assets/MyBundle"
bundle_name = "author.mybundle"
output_directory = "CustomOutput"    # Override global output
build_target = "none"                # Platform-agnostic bundle
build_targets = ["windows"]          # Only allow specific targets
filename = "resource_[bundle_name]_[target]" # Custom filename format

# Bundle-specific patterns
exclude_patterns = ["*.backup"]
include_patterns = ["textures/*", "sounds/*"]
```

### Advanced Examples

#### Multiple Bundles with Different Targets

```toml
[global]
unity_version = "2022.3.35f1"
link_method = "junction"

[bundles.core]
asset_directory = "Core/Assets"
bundle_name = "mymod.core"
build_target = "none"  # Platform-agnostic

[bundles.windows_only]
asset_directory = "WindowsAssets"
bundle_name = "mymod.windows"
build_targets = ["windows"]  # Only for Windows

[bundles.textures_hd]
asset_directory = "HD/Textures"
bundle_name = "mymod.hd"
exclude_patterns = ["*_low.png"]  # Exclude low-res versions
```

#### CI/CD Configuration

```yaml
- name: Install AssetBundleBuilder
  run: dotnet tool install --global CryptikLemur.AssetBundleBuilder
  
- name: Build Asset Bundles
  run: |
    assetbundlebuilder --bundle-config textures --ci
    assetbundlebuilder --bundle-config shaders --ci
```

## Features

### Platform Support

- **Build Targets**: `windows`, `mac`, `linux`, or `none` for platform-agnostic bundles
- **Build Restrictions**: Use `build_targets` array to limit which platforms a bundle can be built for

### Asset Management

- **Link Methods**: Choose how assets are linked to Unity project
    - `copy`: Copy files (safest, slower)
    - `symlink`: Symbolic links (fast, requires permissions)
    - `hardlink`: Hard links (fast, same volume only)
    - `junction`: Directory junctions (Windows, fast)
- **Pattern Matching**: Include/exclude files with glob patterns
- **Custom Filenames**: Use variables like `[bundle_name]`, `[target]`, `[date]`

### Unity Integration

- **Auto-Installation**: Automatically installs Unity Hub and Editor if missing (Windows/macOS)
- **Version Discovery**: Finds Unity installations by version number
- **Temp Project Caching**: Reuses temp projects for faster rebuilds
- **Custom Unity Paths**: Override with specific Unity executable path

### CI/CD Features

- **CI Mode**: Disables auto-installation for CI environments
- **Non-Interactive**: No prompts for automation
- **Verbosity Control**: Quiet mode for cleaner logs
- **Clean Builds**: Option to force fresh builds

## Command Line Reference

While configuration files are recommended, all options are available via CLI:

```
assetbundlebuilder [options] [unity-version] [asset-dir] [bundle-name] [output-dir]
```

Common options:

- `--config <file>`: Use configuration file
- `--bundle-config <name>`: Select bundle from config
- `--target <platform>`: Override build target
- `--ci`: Enable CI mode
- `--non-interactive`: Disable prompts
- `-v, --verbose`: Increase verbosity
- `-q, --quiet`: Decrease verbosity

Run `assetbundlebuilder --help` for full options list.

## Troubleshooting

### Unity Not Found

- Ensure Unity Hub is installed in standard location
- Or specify exact path in config: `unity_path = "C:/Unity/2022.3.35f1/Editor/Unity.exe"`

### Permission Errors

- Windows: Use `junction` link method instead of `symlink`
- Linux/macOS: Ensure proper permissions or use `copy` method

### Build Target Issues

- Check `build_targets` array in your config doesn't exclude your target
- Use `build_target = "none"` for platform-agnostic bundles

## Support

- Issues: [GitHub Issues](https://github.com/CryptikLemur/AssetBundleBuilder/issues)
- RimWorld Modding: [RimWorld Discord](https://discord.gg/rimworld)