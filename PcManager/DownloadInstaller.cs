using System.Diagnostics;
using System.Net.Http;

namespace PcManager;

public sealed record DownloadProgress(long BytesReceived, long? TotalBytes, double? PercentComplete);

public static class DownloadInstaller
{
    public static async Task<string> DownloadAsync(
        string url,
        string destinationPath,
        Action<DownloadProgress>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        using var httpClient = new HttpClient();
        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;

        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

        var buffer = new byte[81920];
        long bytesReceived = 0;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            bytesReceived += bytesRead;

            var percent = totalBytes.HasValue ? bytesReceived / (double)totalBytes.Value * 100 : (double?)null;
            onProgress?.Invoke(new DownloadProgress(bytesReceived, totalBytes, percent));
        }

        return destinationPath;
    }

    // Exit code meaning depends entirely on the installer itself - 0 is the common "success"
    // convention (and the only one MSI-based installers formally document, alongside things
    // like 3010 for "success, reboot required"), but NSIS/Inno Setup/custom installers can
    // return whatever their author chose. Always read ExitCode, but don't assume what it means
    // for an installer you haven't checked.
    public static int Install(string installerPath, string arguments = "", bool waitForExit = true)
    {
        var startInfo = new ProcessStartInfo(installerPath)
        {
            Arguments = arguments,
            UseShellExecute = false
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the installer process.");

        if (!waitForExit)
        {
            return 0;
        }

        process.WaitForExit();
        return process.ExitCode;
    }

    public static async Task<int> DownloadAndInstallAsync(
        string url,
        string destinationPath,
        string arguments = "",
        Action<DownloadProgress>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        await DownloadAsync(url, destinationPath, onProgress, cancellationToken);
        return Install(destinationPath, arguments);
    }
}
