using System.Diagnostics;
using System.Text;

namespace CryptikLemur.AssetBundleBuilder.Interfaces;

public interface IProcessRunner {
    Task<ProcessResult> RunAsync(ProcessStartInfo startInfo);
}

public class ProcessResult {
    public int ExitCode { get; set; }
    public string StandardOutput { get; set; } = string.Empty;
    public string StandardError { get; set; } = string.Empty;
    public bool Success => ExitCode == 0;
}

public class SystemProcessRunner : IProcessRunner {
    public async Task<ProcessResult> RunAsync(ProcessStartInfo startInfo) {
        using var process = new Process { StartInfo = startInfo };

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        if (startInfo.RedirectStandardOutput) {
            process.OutputDataReceived += (_, e) => {
                if (!string.IsNullOrEmpty(e.Data)) {
                    outputBuilder.AppendLine(e.Data);
                }
            };
        }

        if (startInfo.RedirectStandardError) {
            process.ErrorDataReceived += (_, e) => {
                if (!string.IsNullOrEmpty(e.Data)) {
                    errorBuilder.AppendLine(e.Data);
                }
            };
        }

        process.Start();

        if (startInfo.RedirectStandardOutput) {
            process.BeginOutputReadLine();
        }

        if (startInfo.RedirectStandardError) {
            process.BeginErrorReadLine();
        }

        await process.WaitForExitAsync();

        return new ProcessResult {
            ExitCode = process.ExitCode,
            StandardOutput = outputBuilder.ToString(),
            StandardError = errorBuilder.ToString()
        };
    }
}