namespace ConsoleAppTest.Test
{
    public static partial class Run
    {
        public static void FontInstall(string fontFilePath)
        {
            try
            {
                PcManager.FontManager.Install(fontFilePath, PcManager.InstallScope.AllUsers);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error installing fonts: {ex.Message}");
            }
        }
    }
}
