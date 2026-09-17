using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace AssetCopilot;

// Render the supplied transparent wordmark unchanged; animate only the cat's eyes and tail.
// The supplied cat movie includes a checkerboard, so its movement is implemented as native layers.
public sealed class RemyLogo : FrameworkElement, IDisposable
{
    readonly BitmapImage artwork;
    readonly CroppedBitmap tail;
    readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(1000.0/30)};
    readonly Stopwatch clock=new();
    bool disposed;
    static readonly Rect sourceBounds=new(65,115,1835,535);
    static readonly Rect tailBounds=new(520,520,192,93);
    static readonly Brush eyelid=new SolidColorBrush(Color.FromRgb(25,25,23));
    public RemyLogo()
    {
        using var stream=File.OpenRead(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,"viewer","media","remy-logo.png"));
        artwork=new BitmapImage();artwork.BeginInit();artwork.CacheOption=BitmapCacheOption.OnLoad;artwork.StreamSource=stream;artwork.EndInit();artwork.Freeze();
        tail=new CroppedBitmap(artwork,new Int32Rect(520,520,192,93));tail.Freeze();
        RenderOptions.SetBitmapScalingMode(this,BitmapScalingMode.HighQuality);
        AutomationProperties.SetName(this,"Remy Asset Copilot");
        IsHitTestVisible=false;timer.Tick+=(_,_)=>InvalidateVisual();
        Loaded+=(_,_)=>RefreshClock();IsVisibleChanged+=(_,_)=>RefreshClock();Unloaded+=(_,_)=>{timer.Stop();clock.Stop();};
    }
    void RefreshClock(){if(IsVisible&&!disposed){clock.Start();timer.Start();}else{timer.Stop();clock.Stop();}}
    public static (double Blink,double Tail) MotionAt(double seconds)
    {
        double blink=0,angle=0;
        if(seconds>=1.5){double phase=(seconds-1.5)%1.5;if(phase<.20)blink=Math.Sin(Math.PI*phase/.20);}
        if(seconds>=5){double phase=(seconds-5)%5;if(phase<.8)angle=22*Math.Sin(2*Math.PI*phase/.8)*Math.Sin(Math.PI*phase/.8);}
        return (blink,angle);
    }
    protected override void OnRender(DrawingContext drawing){base.OnRender(drawing);DrawAt(drawing,clock.Elapsed.TotalSeconds);}
    public void DrawAt(DrawingContext drawing,double seconds)
    {
        var motion=MotionAt(seconds);double scale=Math.Min(ActualWidth/sourceBounds.Width,ActualHeight/sourceBounds.Height);
        drawing.PushTransform(new ScaleTransform(scale,scale));drawing.PushTransform(new TranslateTransform(-sourceBounds.X,-sourceBounds.Y));
        // Exclude the stationary tail while keeping the original body and all lettering pixels.
        drawing.PushClip(new CombinedGeometry(GeometryCombineMode.Exclude,new RectangleGeometry(sourceBounds),new RectangleGeometry(new Rect(530,500,195,150))));
        drawing.DrawImage(artwork,new Rect(0,0,artwork.PixelWidth,artwork.PixelHeight));drawing.Pop();
        drawing.PushTransform(new RotateTransform(motion.Tail,522,574));drawing.DrawImage(tail,tailBounds);drawing.Pop();
        if(motion.Blink>0)
        {
            double cover=24*motion.Blink;
            foreach(double x in new[]{254.0,323.0})
            {
                drawing.DrawRectangle(eyelid,null,new Rect(x,275,43,cover));
                drawing.DrawRectangle(eyelid,null,new Rect(x,324-cover,43,cover));
            }
        }
        drawing.Pop();drawing.Pop();
    }
    public void Dispose(){disposed=true;timer.Stop();clock.Stop();}
}
