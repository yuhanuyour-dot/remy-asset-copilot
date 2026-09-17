using System.Runtime.InteropServices;
using Rhino;
using Rhino.Commands;
using System.Windows.Interop;

[assembly: Guid("1F31B7D3-758A-43EF-8D03-6EA3BE43EEC7")]
namespace AssetCopilot;

public sealed class AssetCopilotPlugin : Rhino.PlugIns.PlugIn { }
public sealed class AssetCopilotCommand : Command
{
    static CopilotWindow? window;
    public override string EnglishName => "AssetCopilot";
    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        if(window!=null) { window.Show(); window.Activate(); return Result.Success; }
        window=new CopilotWindow(); new WindowInteropHelper(window).Owner=RhinoApp.MainWindowHandle();
        window.Closed+=(_,_)=>window=null; window.Show(); return Result.Success;
    }
}
public sealed class AssetCopilotDemoCommand : Command
{
    public override string EnglishName => "AssetCopilotDemo";
    protected override Result RunCommand(RhinoDoc doc,RunMode mode)
    {
        var root=Path.GetDirectoryName(typeof(AssetCopilotPlugin).Assembly.Location)!;
        var sample=Path.GetFullPath(Path.Combine(root,"../sample/demo-chair.glb"));
        if(!File.Exists(sample)){RhinoApp.WriteLine("样例文件未找到，请保留 dist 与 sample 文件夹的相对位置。");return Result.Failure;}
        var window=new CopilotWindow(sample);new WindowInteropHelper(window).Owner=RhinoApp.MainWindowHandle();window.Show();return Result.Success;
    }
}
