namespace VOCALOIDPatcher;

#if PATCHER_DEBUG
using MessageBox = System.Windows.MessageBox;
#endif

public class PatcherDebug
{
    public static void ShowDbgMessage(string message)
    {
#if PATCHER_DEBUG
        MessageBox.Show(message);
#endif
    }

    public static void ShowErrorMessage(string message)
    {
        MessageBox.Show(message, "VOCALOIDPatcher Error");
    }
}