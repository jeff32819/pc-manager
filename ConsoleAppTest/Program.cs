
// ConsoleAppTest.Test.Run.PcRename();
// ConsoleAppTest.Test.Run.Rdp();
// ConsoleAppTest.Test.Run.FontInstall(@"C:\Users\jeff3\Downloads\JetBrainsMono-2.304\fonts\ttf\JetBrainsMono-Regular.ttf");
// ConsoleAppTest.Test.Run.Time24Hour();
// ConsoleAppTest.Test.Run.Settings();

PcManager.WingetInstaller.InstallAll((app, result, current, total) =>
{
    var percent = (int)(current / (double)total * 100);
    Console.WriteLine($"[{percent,3}%] ({current}/{total}) {app.Name}: {result.Status}");
});

/************************************************************************************/
Console.WriteLine("press any key to exit");
Console.ReadKey();