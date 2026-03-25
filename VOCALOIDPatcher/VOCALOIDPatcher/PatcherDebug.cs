namespace VOCALOIDPatcher;

using MessageBox = System.Windows.MessageBox;

public class PatcherDebug
{
    public static void ShowDbgMessage(string message)
    {
#if PATCHER_DEBUG
        MessageBox.Show(message);
#endif
    }
    
    public static void ShowErrorMessage(string message, string title = "VOCALOIDPatcher Error")
    {
        MessageBox.Show(message, title);
    }
}