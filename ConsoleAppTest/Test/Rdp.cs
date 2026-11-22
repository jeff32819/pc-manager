namespace ConsoleAppTest.Test
{
    public static partial class Run
    {
        public static void Rdp()
        {
            try
            {
                PcManager.RemoteDesktopManager.Enable();

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error enabling RDP: {ex.Message}");
            }
        }
    }
}
