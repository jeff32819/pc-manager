namespace ConsoleAppTest.Test
{
    public static partial class Run
    {
        public static async Task DownloadInstall()
        {
            try
            {
                var destinationPath = Path.Combine(Path.GetTempPath(), "CalcTape-6.0.8.1-Setup.exe");

                var exitCode = await PcManager.DownloadInstaller.DownloadAndInstallAsync(
                    "https://dl.calctape.com/win/CalcTape-6.0.8.1-Setup.exe",
                    destinationPath,
                    onProgress: progress =>
                    {
                        if (progress.PercentComplete.HasValue)
                        {
                            Console.Write($"\rDownloading... {progress.PercentComplete:0}% ({progress.BytesReceived / 1024.0 / 1024.0:0.0} MB / {progress.TotalBytes / 1024.0 / 1024.0:0.0} MB)");
                        }
                        else
                        {
                            Console.Write($"\rDownloading... {progress.BytesReceived / 1024.0 / 1024.0:0.0} MB");
                        }
                    });

                Console.WriteLine();
                Console.WriteLine($"Installer exited with code {exitCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading/installing: {ex.Message}");
            }
        }
    }
}
