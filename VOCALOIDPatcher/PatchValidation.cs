using MessageBox = System.Windows.MessageBox;

namespace Microsoft.Xaml.Behaviors;

class PatchValidation
{
    public static bool Prefix(ref bool __result)
    {
        MessageBox.Show("Patch hit");
        __result = false;
        return false;
    }
}