namespace CryptikLemur.AssetBundleBuilder.Interfaces;

public interface IFileSystemOperations {
    bool FileExists(string path);
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    void CopyFile(string source, string destination, bool overwrite = false);
    void CreateSymbolicLink(string linkPath, string targetPath);
    void CreateHardLink(string linkPath, string targetPath);
    void CreateJunction(string junctionPath, string targetPath);
    void DeleteFile(string path);
    void DeleteDirectory(string path, bool recursive = false);
    void WriteAllText(string path, string contents);
    string[] GetFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);
    string[] GetDirectories(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);
    FileInfo GetFileInfo(string path);
    DirectoryInfo GetDirectoryInfo(string path);
}