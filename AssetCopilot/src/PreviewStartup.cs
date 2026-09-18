using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
namespace AssetCopilot;
public sealed partial class CopilotWindow
{
    readonly TextBlock startupTitle=new(){Text="Add reference image",FontSize=15,Foreground=new SolidColorBrush(Color.FromRgb(66,66,73)),HorizontalAlignment=HorizontalAlignment.Center};
    readonly TextBlock startupHint=new(){Text="Drop an image here, click to upload, or paste below",FontSize=12,Foreground=UiTheme.Muted,Margin=new Thickness(0,7,0,0),HorizontalAlignment=HorizontalAlignment.Center,TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Center};
    readonly TextBlock startupProgress=new(){FontSize=12,Foreground=UiTheme.Muted,TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Center,Margin=new Thickness(24,14,24,0)};
    readonly MediaPlayer startupPlayer=new(){Volume=0,IsMuted=true};
    readonly StackPanel startupFeedback=new(){VerticalAlignment=VerticalAlignment.Center};
    readonly Button retryPreview=new(){Content="Retry preview",HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,0,16),Visibility=Visibility.Collapsed};
    Button startupPlaceholder=null!;
    bool startupPlaying;
    void InstallPreviewStartup()
    {
        var content=new StackPanel();
        var icon=new System.Windows.Shapes.Path{Data=Geometry.Parse("M 3,3 L 21,3 L 21,21 L 3,21 Z M 4,18 L 10,12 L 14,16 L 17,13 L 21,17 M 10,8 A 2,2 0 1 0 6,8 A 2,2 0 1 0 10,8"),Stroke=UiTheme.Muted,StrokeThickness=1.5,Stretch=Stretch.Uniform,Width=24,Height=24};
        content.Children.Add(new Border{Width=48,Height=48,CornerRadius=new CornerRadius(24),Background=new SolidColorBrush(Color.FromRgb(235,235,239)),Child=icon,Margin=new Thickness(0,0,0,17),HorizontalAlignment=HorizontalAlignment.Center});content.Children.Add(startupTitle);content.Children.Add(startupHint);
        var style=(Style)XamlReader.Parse("""
<Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Button">
<Setter Property="Background" Value="#F7F7F9"/><Setter Property="Cursor" Value="Hand"/>
<Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button"><Border x:Name="Surface" CornerRadius="18" Background="{TemplateBinding Background}" BorderBrush="Transparent" BorderThickness="1" Padding="16"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter Property="Background" Value="#F0F0F4"/></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Surface" Property="BorderBrush" Value="#6B6B76"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
</Style>
""");
        startupPlaceholder=new Button{Content=content,Style=style,Margin=new Thickness(0),Padding=new Thickness(0)};
        AutomationProperties.SetName(startupPlaceholder,"Add reference image");startupPlaceholder.Click+=(_,_)=>RequestPhoto();preview.Children.Add(startupPlaceholder);
        // Keep generation feedback independent of the browser, including its startup/failure states.
        var video=new VideoDrawing{Player=startupPlayer};
        var crop=new DrawingGroup{ClipGeometry=new RectangleGeometry(new Rect(0,0,165,125))};crop.Children.Add(video);
        var videoImage=new Image{Source=new DrawingImage(crop),Width=116,Height=88,Stretch=Stretch.Fill,HorizontalAlignment=HorizontalAlignment.Center};
        startupPlayer.MediaOpened+=(_,_)=>video.Rect=new Rect(-132,-12,startupPlayer.NaturalVideoWidth,startupPlayer.NaturalVideoHeight);
        startupPlayer.MediaEnded+=(_,_)=>{if(startupPlaying){startupPlayer.Position=TimeSpan.Zero;startupPlayer.Play();}};
        startupPlayer.Open(new Uri(Path.Combine(Path.GetDirectoryName(typeof(CopilotWindow).Assembly.Location)!,"viewer","media","pixel_blocks.mp4")));
        startupFeedback.Children.Add(videoImage);startupFeedback.Children.Add(startupProgress);
        var feedbackSurface=new Border{Background=new SolidColorBrush(Color.FromRgb(247,247,249)),CornerRadius=new CornerRadius(18),Child=startupFeedback,Visibility=Visibility.Collapsed,IsHitTestVisible=false};
        startupFeedback.Tag=feedbackSurface;preview.Children.Add(feedbackSurface);
        AutomationProperties.SetLiveSetting(startupProgress,AutomationLiveSetting.Polite);
        retryPreview.Click+=async(_,_)=>await RetryPreview();preview.Children.Add(retryPreview);
        preview.IsVisibleChanged+=(_,_)=>RefreshPreviewStartup();
        preview.AllowDrop=true;
        preview.PreviewDragOver+=(_,e)=>{e.Effects=!busy&&e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;};
        preview.Drop+=(_,e)=>{
            e.Handled=true;if(busy)return;
            if(e.Data.GetData(DataFormats.FileDrop) is string[] paths&&paths.Length==1)LoadPhoto(paths[0]);
            else viewport.ReportStatus("Drop one JPG or PNG image of up to 20 MB.",true);
        };
    }
    void ConnectPreview()
    {
        var connected=viewport;
        connected.ImageRequested+=RequestPhoto;
        connected.StartupStateChanged+=RefreshPreviewStartup;connected.PresentationChanged+=RefreshPreviewStartup;
        connected.DropRejected+=message=>connected.ReportStatus(message,true);
        connected.ImageDropped+=(name,bytes)=>Dispatcher.BeginInvoke(new Action(()=>{
            if(busy||!IsVisible||connected!=viewport)return;
            try{
                // Decode before writing anything. The browser supplies bytes, never an arbitrary local path.
                using(var input=new MemoryStream(bytes)){var image=BitmapFrame.Create(input,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);image.Freeze();}
                var folder=Path.Combine(AssetStore.Work,"dropped-images",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
                var path=Path.Combine(folder,name);File.WriteAllBytes(path,bytes);LoadPhoto(path);
            }catch(Exception error) when(error is IOException or NotSupportedException or ArgumentException or System.IO.FileFormatException){connected.ReportStatus("Could not read the image. Drop a valid JPG or PNG file.",true);}
        }));
        RefreshPreviewStartup();
    }
    void RefreshPreviewStartup()
    {
        bool fallback=!viewport.SurfaceReady;
        startupPlaceholder.Visibility=fallback?Visibility.Visible:Visibility.Collapsed;startupPlaceholder.IsEnabled=!viewport.IsBusy;
        bool feedback=fallback&&(viewport.IsBusy||viewport.StatusPersistent);
        ((Border)startupFeedback.Tag).Visibility=feedback?Visibility.Visible:Visibility.Collapsed;
        startupProgress.Text=viewport.ProgressText;
        var animation=(Image)startupFeedback.Children[0];animation.Visibility=viewport.IsBusy&&!viewport.StatusPersistent?Visibility.Visible:Visibility.Collapsed;
        bool play=feedback&&viewport.IsBusy&&!viewport.StatusPersistent&&preview.IsVisible;
        if(play!=startupPlaying){startupPlaying=play;if(play)startupPlayer.Play();else{startupPlayer.Pause();startupPlayer.Position=TimeSpan.Zero;}}
        bool failed=!string.IsNullOrEmpty(viewport.StartupError);
        startupTitle.Text=failed?"Preview unavailable":"Add reference image";
        startupHint.Text=failed?"Retry the preview, or drop an image to continue.":"Drop an image here, click to upload, or paste below";
        retryPreview.Visibility=fallback&&failed&&!viewport.IsBusy?Visibility.Visible:Visibility.Collapsed;
    }
    async Task RetryPreview()
    {
        if(busy)return;
        var previous=viewport;var status=previous.ProgressText;bool persistent=previous.StatusPersistent;
        preview.Children.Remove(previous);previous.ClosePreview();
        viewport=new PbrPreview();ConnectPreview();viewport.SetClipViewport(fullScroll);viewport.SetSurfaceActive(!IsCompact);
        viewport.ReportStatus(status,persistent);preview.Children.Insert(0,viewport);
        modelPreviewReady=false;rendering=false;
        if(raw!=null)await Render();
    }
}
