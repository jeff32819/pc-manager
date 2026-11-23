namespace ConsoleAppTest.Test
{
    public static partial class Run
    {
        public static void Settings()
        {
            PcManager.ShowFileExtensionsSetting.Instance.Enable();
            PcManager.CopyMoveToContextMenuSetting.Instance.Enable();
        }
    }
}
