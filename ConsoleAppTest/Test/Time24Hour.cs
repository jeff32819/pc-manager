namespace ConsoleAppTest.Test
{
    public static partial class Run
    {
        public static void Time24Hour()
        {
            try
            {
                PcManager.TimeFormatManager.SetMilitaryTime();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting time format to 24-hour: {ex.Message}");
            }
        }
    }
}
