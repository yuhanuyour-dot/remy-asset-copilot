using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Markup;
namespace AssetCopilot;
public sealed partial class CopilotWindow
{
    readonly TextBlock startupTitle=new(){Text="Add reference image",FontSize=15,Foreground=new SolidColorBrush(Color.FromRgb(66,66,73)),HorizontalAlignment=HorizontalAlignment.Center};
    readonly TextBlock startupHint=new(){Text="Click to upload, or paste below",FontSize=12,Foreground=UiTheme.Muted,Margin=new Thickness(0,7,0,0),HorizontalAlignment=HorizontalAlignment.Center,TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Center};
    Button startupPlaceholder=null!;
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
        viewport.StartupStateChanged+=()=>
        {
            startupPlaceholder.Visibility=viewport.SurfaceReady?Visibility.Collapsed:Visibility.Visible;
            if(!string.IsNullOrEmpty(viewport.StartupError)){startupTitle.Text="Preview unavailable";startupHint.Text=viewport.StartupError;startupPlaceholder.IsEnabled=false;}
        };
    }
}
