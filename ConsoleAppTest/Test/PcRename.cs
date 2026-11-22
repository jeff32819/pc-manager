using PcManager;

namespace ConsoleAppTest.Test
{
    public static partial class Run
    {
        public static void PcRename()
        {

            Console.WriteLine("Enter your PC name:");
            var pcName = Console.ReadLine();
            Console.WriteLine($"Renaming PC to {pcName}...");

            try
            {
                PcManager.ComputerRenamer.Rename(pcName);
                Console.WriteLine($"PC renamed to {pcName} successfully.");
                Console.WriteLine("PC will now restart automatically, press any key to continue");
                Console.ReadKey();
                PowerController.Execute(PowerAction.Restart);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error renaming PC: {ex.Message}");
            }

        }
    }
}
