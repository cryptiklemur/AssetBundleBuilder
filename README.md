# AssetBundleBuilder

Command line tool for building Unity asset bundles for RimWorld mods. No repository cloning or Unity project setup
required.

## Quick Start

### Recommended: Using Configuration File

Create a `.assetbundler.toml` file in your project:

```toml
[global]
unity_version = "2022.3.35f1"
bundle_name = "author.modName"
asset_directory = "Assets/"
output_directory = "AssetBundles/"
link_method = "junction"  # or "copy", "symlink", "hardlink"

# Note, if you are creating multiple assetbundles for a single mod, they need to have unique bundle_name's
[bundles.textures]
bundle_path = "author.modName"
bundle_name = "author.modName_textures"
filename = "resource_[bundle_name]_textures_[target]"
exclude_patterns = ["*.shader"]
targetless = true  # No platform suffix - creates single bundle for all platforms

[bundles.shaders] # specify `--target windows` (or mac or linux) when running assetbundlebuilder to build for each platform
bundle_path = "author.modName"
bundle_name = "author.modName_shaders"
filename = "resource_[bundle_name]_shaders_[target]"
include_patterns = ["*.shader"]
targetless = false  # Platform-specific - creates separate bundle per platform
```

Then build your bundles:

```bash
# Build a specific bundle, loads automatically from .assetbundler.toml
assetbundlebuilder --bundle-config textures

# Override settings from command line
assetbundlebuilder --bundle-config shaders --target windows
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
unity_version = "2022.3.35f1"         # Unity version to use
unity_path = "/path/to/Unity.exe"     # Or specify exact path
output_directory = "AssetBundles"     # Default output directory
build_targets = ["windows", "linux"]  # Restrict allowed targets
temp_project_path = "/tmp/unity"      # Custom temp directory
clean_temp_project = false            # Clean temp project after building. Disabled by default for caching
link_method = "copy"                  # How to link assets: copy/symlink/hardlink/junction
exclude_patterns = ["*.meta", "*.tmp"] # Files to exclude
include_patterns = ["*.png", "*.wav"]  # Files to include
targetless = true                      # Default for bundles: no platform suffix
```

### Bundle Configuration

```toml
[bundles.mybundle]
description = "My custom asset bundle"
asset_directory = "Assets/MyBundle"
bundle_name = "author.mybundle"
output_directory = "1.6/AssetBundles"        # Override global output
build_targets = ["windows"]                  # Only allow specific targets
filename = "resource_[bundle_name]_[target]" # Custom filename format
targetless = false                           # Platform-specific bundle (default: true)

# Bundle-specific patterns
exclude_patterns = ["*.backup"]
include_patterns = ["textures/*", "sounds/*"]
```

### Advanced Examples

#### Multiple Bundles with Different Targets

> Note, if you are creating multiple assetbundles for a single mod, they need to have unique bundle_name's

```toml
[global]
unity_version = "2022.3.35f1"
link_method = "junction"

[bundles.core]
asset_directory = "Core/Assets"
bundle_name = "author.core_mod"

[bundles.windows_only]
asset_directory = "WindowsAssets"
bundle_name = "author.myMod_windows"
build_targets = ["windows"]  # Only for Windows

[bundles.textures_hd]
asset_directory = "HD/Textures"
bundle_path = "author.myMod"
bundle_name = "author.myMod_textures_high"
exclude_patterns = ["*_low.png"]  # Exclude low-res versions

[bundles.textures_low]
asset_directory = "SD/Textures"
bundle_path = "author.myMod"
bundle_name = "mymod.low_textures"
exclude_patterns = ["*_high.png"]  # Exclude low-res versions
```

#### CI/CD Configuration

```yaml
- name: Install AssetBundleBuilder
  run: dotnet tool install --global CryptikLemur.AssetBundleBuilder
  
- name: Build Asset Bundles
  run: assetbundlebuilder --ci
```

## Features

### Platform Support

- **Build Targets**: `windows`, `mac`, `linux`
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
- Use `--target` CLI flag to specify build target

## Support

- Issues: [GitHub Issues](https://github.com/CryptikLemur/AssetBundleBuilder/issues)
- RimWorld Modding: [RimWorld Discord](https://discord.gg/rimworld)