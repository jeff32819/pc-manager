Console.WriteLine($"starting... {DateTime.Now}");
Console.WriteLine();


// ConsoleAppTest.Test.Run.PcRename();
// ConsoleAppTest.Test.Run.Rdp();
// ConsoleAppTest.Test.Run.FontInstall(@"C:\Users\jeff3\Downloads\JetBrainsMono-2.304\fonts\ttf\JetBrainsMono-Regular.ttf");
// ConsoleAppTest.Test.Run.Time24Hour();
// ConsoleAppTest.Test.Run.Settings();
// ConsoleAppTest.Test.Run.WingetInstaller();

await ConsoleAppTest.Test.Run.DownloadInstall();




/************************************************************************************/
Console.WriteLine();
Console.WriteLine("-------------------------------------------------------------");
Console.BackgroundColor = ConsoleColor.DarkYellow;
Console.ForegroundColor = ConsoleColor.Red;
Console.WriteLine("press any key to exit");
Console.ResetColor();
Console.ReadKey();