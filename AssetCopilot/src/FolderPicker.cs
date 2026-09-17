using System.Windows;
using System.Windows.Interop;
namespace AssetCopilot;

// OpenFolderDialog is a .NET 8 API. This native Windows folder picker also works in .NET 7.
public static class FolderPicker
{
    sealed class Owner : System.Windows.Forms.IWin32Window
    {
        public IntPtr Handle { get; }
        public Owner(Window window) { Handle = new WindowInteropHelper(window).Handle; }
    }
    public static string? Pick(Window owner, string initialDirectory)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "选择模型保存目录", 
            SelectedPath = initialDirectory, ShowNewFolderButton = true
        };
        return dialog.ShowDialog(new Owner(owner)) == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
    }
}
