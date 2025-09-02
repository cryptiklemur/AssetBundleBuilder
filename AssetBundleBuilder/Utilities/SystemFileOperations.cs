using System.Diagnostics;
using CryptikLemur.AssetBundleBuilder.Interfaces;

namespace CryptikLemur.AssetBundleBuilder.Utilities;

public class SystemFileOperations : IFileSystemOperations {
    public bool FileExists(string path) {
        return File.Exists(path);
    }

    public bool DirectoryExists(string path) {
        return Directory.Exists(path);
    }

    public void CreateDirectory(string path) {
        Directory.CreateDirectory(path);
    }

    public void CopyFile(string source, string destination, bool overwrite = false) {
        File.Copy(source, destination, overwrite);
    }

    public void CreateSymbolicLink(string linkPath, string targetPath) {
        File.CreateSymbolicLink(linkPath, targetPath);
    }

    public void CreateHardLink(string linkPath, string targetPath) {
        using Process process = new Process();

        if (OperatingSystem.IsWindows()) {
            // mklink is a built-in Windows command, must be run through cmd.exe
            process.StartInfo = new ProcessStartInfo {
                FileName = "cmd.exe",
                Arguments = $"/C mklink /H \"{linkPath}\" \"{targetPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
        }
        else {
            process.StartInfo = new ProcessStartInfo {
                FileName = "ln",
                Arguments = $"\"{targetPath}\" \"{linkPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
        }

        process.Start();
        process.WaitForExit();

        if (process.ExitCode != 0) {
            string error = process.StandardError.ReadToEnd();
            throw new InvalidOperationException($"Failed to create hard link: {error}");
        }
    }

    public void CreateJunction(string junctionPath, string targetPath) {
        if (!OperatingSystem.IsWindows()) {
            throw new PlatformNotSupportedException("Junctions are only supported on Windows");
        }

        using Process process = new Process {
            StartInfo = new ProcessStartInfo {
                FileName = "cmd.exe",
                Arguments = $"/C mklink /J \"{junctionPath}\" \"{targetPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();
        process.WaitForExit();

        if (process.ExitCode != 0) {
            string error = process.StandardError.ReadToEnd();
            throw new InvalidOperationException($"Failed to create junction: {error}");
        }
    }

    public void DeleteDirectory(string path, bool recursive = false) {
        Directory.Delete(path, recursive);
    }

    public void WriteAllText(string path, string contents) {
        File.WriteAllText(path, contents);
    }

    public string[] GetFiles(string path, string searchPattern = "*",
        SearchOption searchOption = SearchOption.TopDirectoryOnly) {
        return Directory.GetFiles(path, searchPattern, searchOption);
    }

    public string[] GetDirectories(string path, string searchPattern = "*",
        SearchOption searchOption = SearchOption.TopDirectoryOnly) {
        return Directory.GetDirectories(path, searchPattern, searchOption);
    }

    public void DeleteFile(string path) {
        File.Delete(path);
    }

    public FileInfo GetFileInfo(string path) {
        return new FileInfo(path);
    }

    public DirectoryInfo GetDirectoryInfo(string path) {
        return new DirectoryInfo(path);
    }
}