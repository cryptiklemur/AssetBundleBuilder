# AssetBundleBuilder

A command-line tool for building Unity asset bundles for RimWorld mods. **No repository cloning or Unity project setup required!**

## Installation

### .NET Global Tool (Recommended - Cross-Platform)

```bash
# Install globally
dotnet tool install --global AssetBundleBuilder.GlobalTool

# Use anywhere
assetbundlebuilder 2022.3.5f1 "/path/to/assets" "/path/to/output"
```

### Windows - Chocolatey

```powershell
# Install with Chocolatey
choco install assetbundlebuilder

# Use anywhere
assetbundlebuilder 2022.3.5f1 "C:\MyMod\Assets" "C:\MyMod\Output"
```

### macOS - Homebrew

```bash
# Install with Homebrew
brew install assetbundlebuilder

# Use anywhere
assetbundlebuilder 2022.3.5f1 "/Users/me/MyMod/Assets" "/Users/me/MyMod/Output"
```

### Manual Installation

Download the appropriate executable for your platform from the [releases page](https://github.com/CryptikLemur/RimworldCosmere/releases):

- **Windows**: `AssetBundleBuilder-win-x64-1.0.0.zip`
- **macOS Intel**: `AssetBundleBuilder-osx-x64-1.0.0.tar.gz`
- **macOS Apple Silicon**: `AssetBundleBuilder-osx-arm64-1.0.0.tar.gz`
- **Linux x64**: `AssetBundleBuilder-linux-x64-1.0.0.tar.gz`

Extract and add to your system PATH.

## Quick Start

### Basic Usage

```bash
# Auto-find Unity and build asset bundles
assetbundlebuilder 2022.3.5f1 "/path/to/your/mod/assets" "/path/to/output/directory" "mybundle"
```

### With Custom Bundle Name

```bash
# Specify a custom bundle name
assetbundlebuilder 2022.3.5f1 "/path/to/assets" "/path/to/output" "author.modname"
```

### Specific Platform

```bash
# Build only for Windows
assetbundlebuilder 2022.3.5f1 "/path/to/assets" "/path/to/output" "mybundle" --target windows
```

## How It Works

1. **No Setup Required**: Tool automatically creates a temporary Unity project
2. **Unity Auto-Discovery**: Finds Unity installations by version number across common paths
3. **Asset Processing**: Copies your assets and applies proper import settings
4. **Bundle Creation**: Builds compressed asset bundles with platform-specific optimizations
5. **Cleanup**: Removes temporary files automatically

## Supported Features

- ✅ **Unity Version Auto-Discovery** - Just specify the version (e.g., `2022.3.5f1`)
- ✅ **Cross-Platform** - Windows, macOS, and Linux native executables
- ✅ **Multiple Build Targets** - Windows, macOS, Linux asset bundles
- ✅ **Automatic Asset Import Settings** - Textures, audio, shaders
- ✅ **Terrain Texture Support** - Special handling for terrain assets
- ✅ **PSD File Support** - Basic Photoshop document import
- ✅ **Temporary Project Management** - No manual Unity project required
- ✅ **CI/CD Friendly** - Perfect for automated build pipelines

## Command Reference

```
assetbundlebuilder [unity-path-or-version] <asset-directory> <output-directory> <bundle-name> [options]
```

### Arguments

- **unity-path-or-version**: Either Unity version (e.g., `2022.3.5f1`) or full path to Unity executable
- **asset-directory**: Directory containing your mod's assets (textures, sounds, etc.)
- **output-directory**: Where to create the asset bundles
- **bundle-name**: Name for the asset bundle (e.g., "mymod", "author.modname")

### Options

- `--unity-version <version>` - Explicitly specify Unity version
- `--bundle-name <name>` - Override bundle name (alternative to positional argument)
- `--target <target>` - Build target: `windows`, `mac`, `linux`, or `all` (default)
- `--temp-project <path>` - Custom location for temporary Unity project
- `--keep-temp` - Don't delete temporary project (for debugging)

## Examples

### Windows

```powershell
# Using Chocolatey-installed version
assetbundlebuilder 2022.3.5f1 "C:\MyMod\Assets" "C:\MyMod\Output" "mymod"

# Using specific Unity installation
assetbundlebuilder "C:\Unity\2022.3.5f1\Editor\Unity.exe" "C:\MyMod\Assets" "C:\MyMod\Output" "mymod"
```

### macOS

```bash
# Using Homebrew-installed version
assetbundlebuilder 2022.3.5f1 "/Users/me/MyMod/Assets" "/Users/me/MyMod/Output" "mymod"

# Using specific Unity installation
assetbundlebuilder "/Applications/Unity/Hub/Editor/2022.3.5f1/Unity.app/Contents/MacOS/Unity" "/Users/me/MyMod/Assets" "/Users/me/MyMod/Output" "mymod"
```

### Linux

```bash
# Using .NET Global Tool
assetbundlebuilder 2022.3.5f1 "/home/user/MyMod/Assets" "/home/user/MyMod/Output" "mymod"

# Build only Linux bundles
assetbundlebuilder 2022.3.5f1 "/home/user/MyMod/Assets" "/home/user/MyMod/Output" "mymod" --target linux
```

## Unity Installation Paths

The tool automatically searches these common Unity installation locations:

### Windows
- `C:\Program Files\Unity\Hub\Editor\`
- `C:\Program Files (x86)\Unity\Hub\Editor\`
- `%USERPROFILE%\Unity\Hub\Editor\`

### macOS
- `/Applications/Unity/Hub/Editor/`
- `$HOME/Applications/Unity/Hub/Editor/`

### Linux
- `/opt/unity/editor/`
- `$HOME/Unity/Hub/Editor/`
- `$HOME/.local/share/Unity/Hub/Editor/`

## CI/CD Integration

### GitHub Actions

```yaml
- name: Install AssetBundleBuilder
  run: dotnet tool install --global AssetBundleBuilder.GlobalTool

- name: Build Asset Bundles
  run: assetbundlebuilder 2022.3.5f1 "./Assets" "./Output" "mymod"
```

### Azure DevOps

```yaml
- task: DotNetCoreCLI@2
  displayName: 'Install AssetBundleBuilder'
  inputs:
    command: 'custom'
    custom: 'tool'
    arguments: 'install --global AssetBundleBuilder.GlobalTool'

- script: assetbundlebuilder 2022.3.5f1 "$(Build.SourcesDirectory)/Assets" "$(Build.ArtifactStagingDirectory)" "mymod"
  displayName: 'Build Asset Bundles'
```

## Troubleshooting

### Unity Not Found
```bash
# Try with full path instead
assetbundlebuilder "/path/to/Unity" "/path/to/assets" "/path/to/output"

# Or install Unity in standard location
```

### Permission Errors (Linux/macOS)
```bash
# Make sure executable has proper permissions
chmod +x ./AssetBundleBuilder

# Or use .NET Global Tool instead
dotnet tool install --global AssetBundleBuilder.GlobalTool
```

### Invalid Bundle Name
```bash
# Use a valid bundle name (alphanumeric, dots, underscores)
assetbundlebuilder 2022.3.5f1 "/path/to/assets" "/path/to/output" "valid_bundle_name"
```

### Debugging
```bash
# Keep temporary project for inspection
assetbundlebuilder 2022.3.5f1 "/path/to/assets" "/path/to/output" "mybundle" --keep-temp
```

## Uninstallation

### .NET Global Tool
```bash
dotnet tool uninstall --global AssetBundleBuilder.GlobalTool
```

### Chocolatey
```powershell
choco uninstall assetbundlebuilder
```

### Homebrew
```bash
brew uninstall assetbundlebuilder
```

## Support

- **Issues**: [GitHub Issues](https://github.com/CryptikLemur/RimworldCosmere/issues)
- **Discussions**: [GitHub Discussions](https://github.com/CryptikLemur/RimworldCosmere/discussions)
- **RimWorld Modding**: [RimWorld Discord](https://discord.gg/rimworld)