Console.WriteLine($"starting... {DateTime.Now}");
Console.WriteLine();


// ConsoleAppTest.Test.Run.PcRename();
// ConsoleAppTest.Test.Run.Rdp();
// ConsoleAppTest.Test.Run.FontInstall(@"C:\Users\jeff3\Downloads\JetBrainsMono-2.304\fonts\ttf\JetBrainsMono-Regular.ttf");
// ConsoleAppTest.Test.Run.Time24Hour();
// ConsoleAppTest.Test.Run.Settings();
// ConsoleAppTest.Test.Run.WingetInstaller();
// await ConsoleAppTest.Test.Run.DownloadInstall();
// DesktopVersionWatermarkSetting.Instance.Enable();

var progressRegex = new System.Text.RegularExpressions.Regex(@"Progress:\s*(\d+)%");

PcManager.ChocoInstaller.InstallAll(
    onProgress: (app, result, index, total) =>
    {
        ClearProgressLine();
        Console.WriteLine($"[{index}/{total}] {app.Name}: {result.Status}");
    },
    onOutputLine: line =>
    {
        var match = progressRegex.Match(line);
        if (match.Success)
        {
            RenderProgressBar(int.Parse(match.Groups[1].Value));
        }
        else if (!string.IsNullOrWhiteSpace(line))
        {
            ClearProgressLine();
            Console.WriteLine(line);
        }
    });

static void RenderProgressBar(int percent)
{
    const int width = 30;
    var filled = (int)Math.Round(percent / 100.0 * width);
    var bar = new string('█', filled) + new string('░', width - filled);
    Console.Write($"\r[{bar}] {percent,3}%");
}

static void ClearProgressLine() =>
    Console.Write('\r' + new string(' ', Math.Max(0, Console.WindowWidth - 1)) + '\r');


/************************************************************************************/
Console.WriteLine();
Console.WriteLine("-------------------------------------------------------------");
Console.ForegroundColor = ConsoleColor.DarkYellow;
Console.WriteLine("press any key to exit");
Console.ResetColor();
Console.ReadKey();