# AssetBundleBuilder

Command line tool for building Unity asset bundles for RimWorld mods. No repository cloning or Unity project setup
required.

## Prerequisites

**Automatic Unity Installation**: On Windows and macOS, if Unity is not found, the tool will automatically prompt to
download and install Unity Hub and the required Unity Editor version. On Linux, manual Unity Hub installation is
required.

The tool will:

- Auto-detect Unity installations in standard locations
- Automatically install Unity Hub and Unity Editor if missing (Windows/macOS)
- Create temporary Unity projects automatically
- Handle all Unity project configuration

For manual installation, download Unity Hub from [unity.com/download](https://unity.com/download) and install the
required Unity version through it.

## Installation

### .NET Global Tool (Cross Platform)

```bash
# Install globally
dotnet tool install --global CryptikLemur.AssetBundleBuilder

# Use anywhere
assetbundlebuilder 2022.3.35f1 "/path/to/assets" "mybundle" "/path/to/output"
```

### Manual Installation

Download the appropriate executable for your platform from
the [Releases Page](https://github.com/CryptikLemur/AssetBundleBuilder/releases):

Extract and run the executable directly. No installation required.

## Quick Start

### Basic Usage

```bash
# Auto find Unity and build asset bundles
assetbundlebuilder 2022.3.35f1 "/path/to/your/mod/assets" "mybundle" "/path/to/output/directory"
```

### With Custom Bundle Name

```bash
# Specify a custom bundle name
assetbundlebuilder 2022.3.35f1 "/path/to/assets" "author.modname" "/path/to/output"
```

### Specific Platform

```bash
# Build only for Windows
assetbundlebuilder 2022.3.35f1 "/path/to/assets" "mybundle" "/path/to/output" --target windows
```

## How It Works

1. Creates a temporary Unity project
2. Finds Unity installations by version number across common paths
3. Copies your assets and applies proper import settings
4. Builds compressed asset bundles with platform specific optimizations
5. Removes temporary files automatically

## Supported Features

- **Automatic Unity Installation**: Downloads and installs Unity Hub and Unity Editor if missing (Windows/macOS)
- **Unity Version Auto Discovery**: Just specify the version (e.g., `2022.3.35f1`)
- **Cross Platform**: Windows, macOS, and Linux native executables
- **Multiple Build Targets**: Windows, macOS, Linux asset bundles
- **Automatic Asset Import Settings**: Textures, audio, shaders
- **Terrain Texture Support**: Special handling for terrain assets
- **PSD File Support**: Basic Photoshop document import
- **Temporary Project Management**: No manual Unity project required
- **CI/CD Friendly**: Works in automated build pipelines

## Command Reference

```
assetbundlebuilder <unity-path-or-version> <asset-directory> <bundle-name> [output-directory] [options]
```

### Arguments

- **unity-path-or-version**: Either Unity version (e.g., `2022.3.35f1`) or full path to Unity executable
- **asset-directory**: Directory containing your mod's assets (textures, sounds, etc.)
- **bundle-name**: Name for the asset bundle (e.g., "mymod", "author.modname")
- **output-directory**: Where to create the asset bundles (optional, defaults to current directory)

### Options

- `--unity-version <version>`: Explicitly specify Unity version
- `--bundle-name <name>`: Override bundle name (alternative to positional argument)
- `--target <target>`: Build target: `windows`, `mac`, or `linux` (default: `windows`)
- `--temp-project <path>`: Custom location for temporary Unity project
- `--keep-temp`: Don't delete temporary project (for debugging)

## Examples

### Windows

```powershell
# Using .NET global tool
assetbundlebuilder 2022.3.35f1 "C:\MyMod\Assets" "mymod" "C:\MyMod\Output"

# Using specific Unity installation
assetbundlebuilder "C:\Unity\2022.3.35f1\Editor\Unity.exe" "C:\MyMod\Assets" "mymod" "C:\MyMod\Output"
```

### macOS

```bash
# Using .NET global tool
assetbundlebuilder 2022.3.35f1 "/Users/me/MyMod/Assets" "mymod" "/Users/me/MyMod/Output"

# Using specific Unity installation
assetbundlebuilder "/Applications/Unity/Hub/Editor/2022.3.35f1/Unity.app/Contents/MacOS/Unity" "/Users/me/MyMod/Assets" "mymod" "/Users/me/MyMod/Output"
```

### Linux

```bash
# Using .NET Global Tool
assetbundlebuilder 2022.3.35f1 "/home/user/MyMod/Assets" "mymod" "/home/user/MyMod/Output"

# Build only Linux bundles
assetbundlebuilder 2022.3.35f1 "/home/user/MyMod/Assets" "mymod" "/home/user/MyMod/Output" --target linux
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
  run: dotnet tool install --global CryptikLemur.AssetBundleBuilder

 - uses: buildalon/unity-setup@v1
   with:
     unity-version: 2022.3.35f1
     build-targets: StandaloneWindows64 StandaloneOSX StandaloneLinux64
     modules: windows-mono mac-mono linux-il2cpp

 - uses: buildalon/activate-unity-license@v1
   with:
     license: 'Personal'
     username: ${{ secrets.UNITY_EMAIL }}
     password: ${{ secrets.UNITY_PASSWORD }}

 - name: Build AssetBundles for Windows
   run: |
       assetbundlebuilder 2022.3.35f1 "./Assets" "mymod" "./Output" --target windows
       assetbundlebuilder 2022.3.35f1 "./Assets" "mymod" "./Output" --target linux
       assetbundlebuilder 2022.3.35f1 "./Assets" "mymod" "./Output" --target mac
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
assetbundlebuilder 2022.3.35f1 "/path/to/assets" "valid_bundle_name" "/path/to/output"
```

### Debugging

```bash
# Keep temporary project for inspection
assetbundlebuilder 2022.3.35f1 "/path/to/assets" "mybundle" "/path/to/output" --keep-temp
```

## Uninstallation

### .NET Global Tool

```bash
dotnet tool uninstall --global CryptikLemur.AssetBundleBuilder
```

## Support

- Issues: [GitHub Issues](https://github.com/CryptikLemur/AssetBundleBuilder/issues)
- RimWorld Modding: [RimWorld Discord](https://discord.gg/rimworld)