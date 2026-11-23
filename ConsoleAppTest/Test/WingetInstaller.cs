namespace ConsoleAppTest.Test
{
    public static partial class Run
    {
        public static void WingetInstaller()
        {
            var failures = new List<(PcManager.WingetApp App, PcManager.WingetInstallResult Result)>();

            PcManager.WingetInstaller.InstallAll(
                onProgress: (app, result, current, total) =>
                {
                    var percent = (int)(current / (double)total * 100);
                    Console.WriteLine($"[{percent,3}%] ({current}/{total}) {app.Name}: {result.Status}");

                    if (result.Status == PcManager.WingetInstallStatus.Failed)
                    {
                        failures.Add((app, result));
                    }
                },
                liveConsoleOutput: false,
                onOutputLine: Console.WriteLine);

            Console.WriteLine();
            Console.WriteLine("-------------------------------------------------------------");

            if (failures.Count == 0)
            {
                Console.WriteLine("All apps installed successfully.");
            }
            else
            {
                Console.WriteLine($"{failures.Count} app(s) did not install successfully:");

                foreach (var (app, result) in failures)
                {
                    Console.WriteLine($"  - {app.Name} ({app.PackageId}): exit code {result.ExitCode}");
                    Console.WriteLine($"      {result.Output.Trim().Replace("\n", "\n      ")}");
                }
            }
        }
    }
}
