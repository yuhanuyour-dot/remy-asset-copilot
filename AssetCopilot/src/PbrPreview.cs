using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Text.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
namespace AssetCopilot;

// Local GLB viewer. No API credentials or remote scripts are exposed to the browser.
public sealed class PbrPreview : WebView2
{
    Task? initialization;
    readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    TaskCompletionSource? pending;
    string request="", loadedPath="";
    readonly SemaphoreSlim loadGate=new(1,1);
    string sessionFolder="";
    bool disposed,interactionBlocked,statusPersistent,surfaceActive=true;
    FrameworkElement? clipViewport;
    IntPtr clippedHandle;
    (int Left,int Top,int Right,int Bottom)? lastClip;
    string progressText="";
    public event Action? ImageRequested;
    public string Statistics { get; private set; }="";
    public bool SurfaceReady {get;private set;}
    public string StartupError {get;private set;}="";
    public event Action? StartupStateChanged;
    public PbrPreview()
    {
        DefaultBackgroundColor=System.Drawing.Color.White;Visibility=Visibility.Hidden;
        LayoutUpdated+=PreviewLayoutUpdated;
        Loaded+=async(_,_)=>{try{await(initialization??=Initialize());}catch{StartupFailed();}};
    }
    public void SetSurfaceActive(bool value){surfaceActive=value;UpdateSurfaceVisibility();}
    void UpdateSurfaceVisibility()
    {
        UpdateNativeClip();
        Visibility=surfaceActive&&SurfaceReady?Visibility.Visible:Visibility.Hidden;
    }
    public void SetClipViewport(FrameworkElement value){clipViewport=value;lastClip=null;UpdateNativeClip();}
    void PreviewLayoutUpdated(object? sender,EventArgs e)=>UpdateNativeClip();
    protected override void OnWindowPositionChanged(Rect bounds)
    {
        base.OnWindowPositionChanged(bounds);
        UpdateNativeClip();
    }
    // WebView2 is a child HWND: WPF's ScrollViewer clipping alone cannot contain it.
    // Apply the visible scroll rectangle to that HWND, without resizing/reloading its content.
    void UpdateNativeClip()
    {
        if(disposed||clipViewport==null||!IsLoaded||Handle==IntPtr.Zero||
           PresentationSource.FromVisual(clipViewport)==null||!GetWindowRect(Handle,out var host))return;
        var clip=(Left:0,Top:0,Right:0,Bottom:0);
        if(surfaceActive&&clipViewport.IsVisible)
        {
            var topLeft=clipViewport.PointToScreen(new Point());
            var bottomRight=clipViewport.PointToScreen(new Point(clipViewport.ActualWidth,clipViewport.ActualHeight));
            int width=Math.Max(0,host.Right-host.Left),height=Math.Max(0,host.Bottom-host.Top);
            int left=Math.Clamp((int)Math.Ceiling(topLeft.X-host.Left),0,width);
            int top=Math.Clamp((int)Math.Ceiling(topLeft.Y-host.Top),0,height);
            int right=Math.Clamp((int)Math.Floor(bottomRight.X-host.Left),0,width);
            int bottom=Math.Clamp((int)Math.Floor(bottomRight.Y-host.Top),0,height);
            if(right>left&&bottom>top)clip=(left,top,right,bottom);
        }
        if(clippedHandle==Handle&&lastClip==clip)return;
        var region=CreateRectRgn(clip.Left,clip.Top,clip.Right,clip.Bottom);
        if(region==IntPtr.Zero)return;
        if(SetWindowRgn(Handle,region,true)!=0){clippedHandle=Handle;lastClip=clip;}
        else DeleteObject(region); // Windows owns the region only after a successful SetWindowRgn.
    }
    [StructLayout(LayoutKind.Sequential)] struct NativeBounds{public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd,out NativeBounds rect);
    [DllImport("user32.dll")] static extern int SetWindowRgn(IntPtr hwnd,IntPtr region,bool redraw);
    [DllImport("gdi32.dll")] static extern IntPtr CreateRectRgn(int left,int top,int right,int bottom);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr handle);
    void StartupFailed()
    {
        if(disposed)return;SurfaceReady=false;Visibility=Visibility.Hidden;StartupError="请关闭插件后重新打开。";StartupStateChanged?.Invoke();
    }
    async Task Initialize()
    {
        var folder=Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        CoreWebView2Environment.SetLoaderDllFolderPath(Path.Combine(folder,"runtimes","win-x64","native"));
        var env=await CoreWebView2Environment.CreateAsync(null,Path.Combine(AssetStore.Work,"webview"));
        await EnsureCoreWebView2Async(env);
        if(disposed)return;
        CoreWebView2.ProcessFailed+=(_,_)=>{StartupFailed();pending?.TrySetException(new InvalidOperationException("预览进程已停止，请重新打开插件。"));};
        CoreWebView2.Settings.AreDefaultContextMenusEnabled=false;
        CoreWebView2.Settings.AreDevToolsEnabled=false;
        CoreWebView2.Settings.IsStatusBarEnabled=false;
        CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled=false;
        CoreWebView2.PermissionRequested+=(_,e)=>e.State=CoreWebView2PermissionState.Deny;
        CoreWebView2.NewWindowRequested+=(_,e)=>e.Handled=true;
        CoreWebView2.NavigationStarting+=(_,e)=>{if(e.Uri!="https://copilot.local/index.html")e.Cancel=true;};
        sessionFolder=Path.Combine(AssetStore.Work,"viewer-sessions",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sessionFolder);
        var viewerFiles=Path.Combine(folder,"viewer");
        foreach(var file in Directory.EnumerateFiles(viewerFiles,"*",SearchOption.AllDirectories))
        {
            var target=Path.Combine(sessionFolder,Path.GetRelativePath(viewerFiles,file));Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(file,target);
        }
        CoreWebView2.SetVirtualHostNameToFolderMapping("copilot.local",sessionFolder,CoreWebView2HostResourceAccessKind.DenyCors);
        CoreWebView2.WebMessageReceived+=(_,e)=>
        {
            if(!e.Source.StartsWith("https://copilot.local/",StringComparison.Ordinal))return;
            using var json=JsonDocument.Parse(e.WebMessageAsJson);var root=json.RootElement;var type=root.GetProperty("type").GetString();
            if(type=="ready"){SurfaceReady=true;UpdateSurfaceVisibility();ready.TrySetResult();PublishStatus();StartupStateChanged?.Invoke();return;}
            if(type=="pick-image"){if(!interactionBlocked)ImageRequested?.Invoke();return;}
            if(!root.TryGetProperty("id",out var id)){ready.TrySetException(new InvalidOperationException("PBR 预览初始化失败。"));return;}
            if(id.GetString()!=request)return;
            if(type=="loaded"){Statistics=root.GetProperty("stats").GetRawText();pending?.TrySetResult();}
            else if(type=="error")pending?.TrySetException(new InvalidOperationException("PBR 预览失败："+root.GetProperty("message").GetString()));
        };
        CoreWebView2.Navigate("https://copilot.local/index.html");
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(45));
    }
    public async Task ShowModel(string path)
    {
        await(initialization??=Initialize());await loadGate.WaitAsync();
        try
        {
            if(disposed)return;
            path=Path.GetFullPath(path);if(path==loadedPath)return;
            AssetStore.ValidateFolder(Path.GetDirectoryName(path)!);
            // WebView2 serves an isolated local copy; no user directory is mapped into the browser.
            await Task.Run(()=>File.Copy(path,Path.Combine(sessionFolder,"asset.glb"),true));
            if(disposed)return;
            pending?.TrySetCanceled();pending=new(TaskCreationOptions.RunContinuationsAsynchronously);request=Guid.NewGuid().ToString("N");
            CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new{type="load",id=request,url="https://copilot.local/asset.glb?v="+request}));
            await pending.Task.WaitAsync(TimeSpan.FromSeconds(90));loadedPath=path;
        }
        finally{loadGate.Release();}
    }
    public void SetBusy(bool value)
    {
        if(value&&!interactionBlocked){progressText="准备中…";statusPersistent=false;}
        interactionBlocked=value;PublishStatus();
    }
    public void ReportStatus(string text,bool persistent=false){progressText=text;statusPersistent=persistent;PublishStatus();}
    void PublishStatus()
    {
        if(!disposed&&ready.Task.IsCompletedSuccessfully&&CoreWebView2!=null)
            CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new{type="presentation",busy=interactionBlocked,text=progressText,persistent=statusPersistent}));
    }
    public void ClearModel()
    {
        loadedPath="";pending?.TrySetCanceled();statusPersistent=false;
        if(!interactionBlocked)progressText="";
        if(CoreWebView2!=null)CoreWebView2.PostWebMessageAsJson("{\"type\":\"clear\"}");
        PublishStatus();
    }
    public async Task Capture(string path){using var output=File.Create(path);await CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,output);}
    public void ClosePreview(){disposed=true;LayoutUpdated-=PreviewLayoutUpdated;clipViewport=null;pending?.TrySetCanceled();Dispose();
        try{if(sessionFolder.StartsWith(Path.Combine(AssetStore.Work,"viewer-sessions")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))Directory.Delete(sessionFolder,true);}catch(IOException){}catch(UnauthorizedAccessException){}}
}
