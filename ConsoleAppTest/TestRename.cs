using PcManager;
using System;
using System.Collections.Generic;
using System.Text;

namespace ConsoleAppTest
{
    public static partial class Test
    {
        public static void TestRename()
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
