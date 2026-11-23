Console.WriteLine($"starting... {DateTime.Now}");
Console.WriteLine();


// ConsoleAppTest.Test.Run.PcRename();
// ConsoleAppTest.Test.Run.Rdp();
// ConsoleAppTest.Test.Run.FontInstall(@"C:\Users\jeff3\Downloads\JetBrainsMono-2.304\fonts\ttf\JetBrainsMono-Regular.ttf");
// ConsoleAppTest.Test.Run.Time24Hour();
// ConsoleAppTest.Test.Run.Settings();

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
    liveConsoleOutput: true);

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
    }
}

/************************************************************************************/
Console.WriteLine();
Console.WriteLine("-------------------------------------------------------------");
Console.BackgroundColor = ConsoleColor.DarkYellow;
Console.ForegroundColor = ConsoleColor.Red;
Console.WriteLine("press any key to exit");
Console.ResetColor();
Console.ReadKey();