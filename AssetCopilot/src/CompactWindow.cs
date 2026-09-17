using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Runtime.InteropServices;
using System.Windows.Threading;
namespace AssetCopilot;

public sealed partial class CopilotWindow
{
    readonly TextBlock compactStatus=new(){Foreground=UiTheme.Muted,FontSize=10.5,TextTrimming=TextTrimming.CharacterEllipsis,VerticalAlignment=VerticalAlignment.Center};
    readonly Button compactCancel=new(){Content="停止",FontSize=10.5,Padding=new Thickness(8,3,8,3),Margin=new Thickness(8,0,0,0)};
    readonly DockPanel compactFeedback=new(){Margin=new Thickness(8,6,8,0)};
    readonly TextBlock compactCaption=new(){Text="Remy",Foreground=UiTheme.Muted,FontWeight=FontWeights.Medium,FontSize=11,VerticalAlignment=VerticalAlignment.Center,Visibility=Visibility.Collapsed};
    readonly List<(UIElement Element,Visibility Visibility)> hiddenSections=new();
    StackPanel modeRoot=null!,modeBody=null!;
    Border bodyCard=null!;
    FrameworkElement fullFooter=null!;
    DockPanel modeHeader=null!;
    ScrollViewer fullScroll=null!;
    Brush? cardBackground;
    Button viewToggle=null!;
    System.Windows.Shapes.Path toggleChevron=null!;
    bool compactMode,switchingView;
    DispatcherOperation? compactResize;
    double chromeWidth,chromeHeight;
    string compactMessage="";
    Rect expandedBounds;
    WindowState expandedState;
    double expandedScroll;
    public bool IsCompact=>compactMode;

    Button ViewButton()
    {
        toggleChevron=new System.Windows.Shapes.Path{
            Data=Geometry.Parse("M 2,9 L 8,3 L 14,9"),Stroke=UiTheme.Muted,StrokeThickness=1.7,
            StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,
            Width=14,Height=10,Stretch=Stretch.Uniform,IsHitTestVisible=false,
            RenderTransformOrigin=new Point(.5,.5)
        };
        var button=new Button{
            Content=toggleChevron,Style=UiTheme.RoundButton,Width=32,Height=32,
            Padding=new Thickness(0),Background=Brushes.Transparent,
            HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,
            Margin=new Thickness(0,14,24+SystemParameters.VerticalScrollBarWidth,0)
        };
        button.Click+=(_,_)=>SetCompact(!compactMode);return button;
    }
    void UpdateViewButton()
    {
        string label=compactMode?"展开完整界面":"收起为输入小窗";
        toggleChevron.RenderTransform=new ScaleTransform(1,compactMode?-1:1);
        viewToggle.ToolTip=label+" · Ctrl+Shift+Space";AutomationProperties.SetName(viewToggle,label);
    }
    void ConfigureWindowModes(DockPanel layout,ScrollViewer scroll,StackPanel body,DockPanel header)
    {
        fullScroll=scroll;viewport.SetClipViewport(scroll);modeRoot=(StackPanel)scroll.Content;modeBody=body;modeHeader=header;
        bodyCard=(Border)body.Parent;cardBackground=bodyCard.Background;fullFooter=(FrameworkElement)layout.Children[0];
        modeRoot.Children.Remove(header);modeRoot.Margin=new Thickness(18,0,18,18);
        modeRoot.VerticalAlignment=VerticalAlignment.Top;
        header.Margin=new Thickness(24,14,24+SystemParameters.VerticalScrollBarWidth,14);
        DockPanel.SetDock(header,Dock.Top);layout.Children.Insert(layout.Children.IndexOf(scroll),header);
        var slot=new Border{Width=32,Height=32,VerticalAlignment=VerticalAlignment.Top};
        DockPanel.SetDock(slot,Dock.Right);header.Children.Insert(0,slot);header.Children.Add(compactCaption);
        compactCancel.Click+=(_,_)=>cancellation?.Cancel();DockPanel.SetDock(compactCancel,Dock.Right);
        compactFeedback.Children.Add(compactCancel);compactFeedback.Children.Add(compactStatus);
        modeRoot.Children.Insert(modeRoot.Children.IndexOf(bodyCard)+1,compactFeedback);
        AutomationProperties.SetLiveSetting(compactStatus,AutomationLiveSetting.Polite);
        AutomationProperties.SetName(compactCancel,"停止当前任务等待");
        // One persistent visual tree: the composer never changes parent or scroll host.
        var shell=new Grid();Content=null;shell.Children.Add(layout);
        viewToggle=ViewButton();Panel.SetZIndex(viewToggle,1);shell.Children.Add(viewToggle);Content=shell;
        UpdateViewButton();RefreshCompactFeedback();
        PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Space&&Keyboard.Modifiers==(ModifierKeys.Control|ModifierKeys.Shift)){SetCompact(!compactMode);e.Handled=true;}};
        composerFrame.SizeChanged+=(_,_)=>QueueCompactResize();
        compactFeedback.IsVisibleChanged+=(_,_)=>QueueCompactResize();
    }
    void HideForCompact(UIElement element)
    {
        hiddenSections.Add((element,element.Visibility));element.Visibility=Visibility.Collapsed;
    }
    public void SetCompact(bool value)
    {
        if(value==compactMode||switchingView)return;
        int start=prompt.SelectionStart,length=prompt.SelectionLength;
        var work=MonitorWorkArea();compactResize?.Abort();compactResize=null;switchingView=true;
        try
        {
            if(value)
            {
                expandedState=WindowState;expandedBounds=new Rect(Left,Top,ActualWidth,ActualHeight);
                expandedScroll=fullScroll.VerticalOffset;
                var client=(FrameworkElement)Content;
                chromeWidth=Math.Max(0,ActualWidth-client.ActualWidth);chromeHeight=Math.Max(0,ActualHeight-client.ActualHeight);
                compactMode=true;MinHeight=0;viewport.SetSurfaceActive(false);
                if(WindowState==WindowState.Maximized){WindowState=WindowState.Normal;Width=expandedBounds.Width;Left=work.Left;Top=work.Top;}
                foreach(UIElement child in modeBody.Children)if(child!=composerFrame)HideForCompact(child);
                foreach(UIElement child in modeRoot.Children)if(child!=bodyCard&&child!=compactFeedback)HideForCompact(child);
                HideForCompact(fullFooter);HideForCompact(logo);
                compactCaption.Visibility=Visibility.Visible;modeHeader.Height=32;
                modeHeader.Margin=new Thickness(20,14,24+SystemParameters.VerticalScrollBarWidth,8);
                modeRoot.Margin=new Thickness(12,0,12,12);modeBody.Margin=new Thickness(0);
                bodyCard.Background=Brushes.Transparent;composerFrame.Margin=new Thickness(0);
                fullScroll.VerticalScrollBarVisibility=ScrollBarVisibility.Disabled;
                RefreshCompactFeedback();FitCompactHeight(work);fullScroll.ScrollToTop();
            }
            else
            {
                compactMode=false;
                foreach(var section in hiddenSections)section.Element.Visibility=section.Visibility;
                hiddenSections.Clear();compactCaption.Visibility=Visibility.Collapsed;modeHeader.Height=double.NaN;
                modeHeader.Margin=new Thickness(24,14,24+SystemParameters.VerticalScrollBarWidth,14);
                modeRoot.Margin=new Thickness(18,0,18,18);modeBody.Margin=new Thickness(16);
                bodyCard.Background=cardBackground;composerFrame.Margin=new Thickness(0,0,0,16);
                fullScroll.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;RefreshCompactFeedback();
                if(expandedState==WindowState.Maximized)WindowState=WindowState.Maximized;
                else{double h=Math.Min(expandedBounds.Height,work.Height);Top=Math.Clamp(Top,work.Top,Math.Max(work.Top,work.Bottom-h));Height=h;}
                MinHeight=Math.Min(620,work.Height);viewport.SetSurfaceActive(true);
            }
            UpdateViewButton();UpdateLayout();
            if(value){fullScroll.ScrollToTop();UpdateLayout();}
            else fullScroll.ScrollToVerticalOffset(expandedScroll);
        }
        finally{switchingView=false;}
        // The control stays attached, so focus no longer depends on deferred reparenting.
        prompt.Focus();prompt.Select(start,length);
    }
    void FitCompactHeight(Rect work)
    {
        double clientWidth=Math.Max(1,Width-chromeWidth);
        var available=new Size(Math.Max(1,clientWidth-modeRoot.Margin.Left-modeRoot.Margin.Right),double.PositiveInfinity);
        modeHeader.Measure(new Size(clientWidth,double.PositiveInfinity));
        composerFrame.Measure(available);compactFeedback.Measure(available);
        double contentHeight=modeHeader.DesiredSize.Height+modeRoot.Margin.Top+composerFrame.DesiredSize.Height+compactFeedback.DesiredSize.Height+modeRoot.Margin.Bottom;
        double target=Math.Min(work.Height,Math.Ceiling(contentHeight+chromeHeight+1));
        MinHeight=target;if(Math.Abs(Height-target)>.5)Height=target;
        Top=Math.Clamp(Top,work.Top,Math.Max(work.Top,work.Bottom-target));
    }
    void QueueCompactResize()
    {
        if(!compactMode||switchingView||compactResize?.Status==DispatcherOperationStatus.Pending)return;
        compactResize=Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>{
            compactResize=null;if(compactMode&&!switchingView){FitCompactHeight(MonitorWorkArea());fullScroll.ScrollToTop();}
        }));
    }
    void EnsureExpanded(){if(compactMode)SetCompact(false);}
    void CompactMessage(string text){compactMessage=text;RefreshCompactFeedback();}
    void RefreshCompactFeedback()
    {
        compactStatus.Text=compactMessage;compactStatus.ToolTip=compactMessage;
        compactFeedback.Visibility=compactMode&&(busy||!string.IsNullOrWhiteSpace(compactMessage))?Visibility.Visible:Visibility.Collapsed;
        compactCancel.Visibility=busy?Visibility.Visible:Visibility.Collapsed;
    }
    [StructLayout(LayoutKind.Sequential)] struct NativeRect{public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)] struct MonitorInfo{public int Size;public NativeRect Monitor,Work;public uint Flags;}
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hwnd,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Auto)] static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
    Rect MonitorWorkArea()
    {
        var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};var monitor=MonitorFromWindow(new WindowInteropHelper(this).Handle,2);
        if(monitor==IntPtr.Zero||!GetMonitorInfo(monitor,ref info))return SystemParameters.WorkArea;
        var transform=PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice??Matrix.Identity;
        var a=transform.Transform(new Point(info.Work.Left,info.Work.Top));var b=transform.Transform(new Point(info.Work.Right,info.Work.Bottom));return new Rect(a,b);
    }
}
