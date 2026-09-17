using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
namespace AssetCopilot;

// WPF desktop adaptation of the supplied monochrome reference. Phosphor icon paths are MIT licensed.
static class UiTheme
{
    public static readonly Brush Muted=new SolidColorBrush(Color.FromRgb(108,108,116));
    public static readonly Brush Line=new SolidColorBrush(Color.FromRgb(225,225,231));
    public static readonly Brush Focus=new SolidColorBrush(Color.FromRgb(107,107,118));
    public static readonly Style RoundButton=(Style)XamlReader.Parse("""
<Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Button">
 <Setter Property="Width" Value="29.333333"/><Setter Property="Height" Value="29.333333"/><Setter Property="Padding" Value="0"/><Setter Property="Margin" Value="0"/>
 <Setter Property="Cursor" Value="Hand"/><Setter Property="Background" Value="#EEEEF1"/><Setter Property="Foreground" Value="#303035"/>
 <Setter Property="HorizontalContentAlignment" Value="Center"/><Setter Property="VerticalContentAlignment" Value="Center"/>
 <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
  <Grid Background="Transparent"><Ellipse x:Name="Circle" Fill="{TemplateBinding Background}"/>
   <Ellipse x:Name="FocusRing" Margin="2" Stroke="#9999A2" StrokeThickness="1.5" Visibility="Collapsed"/>
   <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
  </Grid>
  <ControlTemplate.Triggers>
   <Trigger Property="IsMouseOver" Value="True"><Setter Property="Opacity" Value="0.84"/></Trigger>
   <Trigger Property="IsPressed" Value="True"><Setter Property="Opacity" Value="0.68"/></Trigger>
   <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="FocusRing" Property="Visibility" Value="Visible"/></Trigger>
   <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.4"/></Trigger>
  </ControlTemplate.Triggers>
 </ControlTemplate></Setter.Value></Setter>
</Style>
""");
    public static readonly Style ModelPicker=(Style)XamlReader.Parse("""
<Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="ComboBox">
 <Setter Property="Height" Value="24"/><Setter Property="FontSize" Value="10"/><Setter Property="Background" Value="#F4F4F6"/><Setter Property="Foreground" Value="#424249"/><Setter Property="BorderBrush" Value="Transparent"/>
 <Setter Property="ScrollViewer.HorizontalScrollBarVisibility" Value="Disabled"/><Setter Property="ScrollViewer.VerticalScrollBarVisibility" Value="Auto"/>
 <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBox">
  <Grid>
   <ToggleButton x:Name="Toggle" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" Focusable="False" ClickMode="Press" IsChecked="{Binding IsDropDownOpen,RelativeSource={RelativeSource TemplatedParent},Mode=TwoWay}">
    <ToggleButton.Template><ControlTemplate TargetType="ToggleButton"><Border Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="1" CornerRadius="18">
     <Path Data="M213.66,101.66l-80,80a8,8,0,0,1-11.32,0l-80-80A8,8,0,0,1,53.66,90.34L128,164.69l74.34-74.35a8,8,0,0,1,11.32,11.32Z" Fill="#6C6C74" Stretch="Uniform" Width="7" Height="5" HorizontalAlignment="Right" Margin="0,0,8,0"/>
    </Border><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter Property="Background" Value="#EAEAED"/></Trigger></ControlTemplate.Triggers></ControlTemplate></ToggleButton.Template>
   </ToggleButton>
   <ContentPresenter IsHitTestVisible="False" Content="{TemplateBinding SelectionBoxItem}" ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}" Margin="8,0,22,0" VerticalAlignment="Center"/>
   <Popup x:Name="PART_Popup" Placement="Bottom" IsOpen="{TemplateBinding IsDropDownOpen}" AllowsTransparency="True" Focusable="False" PopupAnimation="None">
    <Border Background="White" BorderBrush="#DFDFE5" BorderThickness="1" CornerRadius="12" Padding="4" Margin="0,5,0,0" MinWidth="{TemplateBinding ActualWidth}">
     <ScrollViewer MaxHeight="220" CanContentScroll="True"><ItemsPresenter KeyboardNavigation.DirectionalNavigation="Contained"/></ScrollViewer>
    </Border>
   </Popup>
  </Grid>
  <ControlTemplate.Triggers>
   <Trigger Property="IsKeyboardFocusWithin" Value="True"><Setter TargetName="Toggle" Property="BorderBrush" Value="#6B6B76"/></Trigger>
   <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.4"/></Trigger>
  </ControlTemplate.Triggers>
 </ControlTemplate></Setter.Value></Setter>
 <Setter Property="ItemContainerStyle"><Setter.Value><Style TargetType="ComboBoxItem">
  <Setter Property="FontSize" Value="12"/><Setter Property="Padding" Value="10,8"/><Setter Property="Foreground" Value="#303035"/>
  <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBoxItem"><Border x:Name="Row" CornerRadius="8" Padding="{TemplateBinding Padding}" Background="Transparent"><ContentPresenter/></Border>
   <ControlTemplate.Triggers><Trigger Property="IsHighlighted" Value="True"><Setter TargetName="Row" Property="Background" Value="#F0F0F3"/></Trigger><Trigger Property="IsSelected" Value="True"><Setter TargetName="Row" Property="Background" Value="#E9E9EE"/></Trigger></ControlTemplate.Triggers>
  </ControlTemplate></Setter.Value></Setter>
 </Style></Setter.Value></Setter>
</Style>
""");
    public static void IconButton(Button button,string name,string label)
    {
        var data=name=="send"?"M205.66,117.66a8,8,0,0,1-11.32,0L136,59.31V216a8,8,0,0,1-16,0V59.31L61.66,117.66a8,8,0,0,1-11.32-11.32l72-72a8,8,0,0,1,11.32,0l72,72A8,8,0,0,1,205.66,117.66Z":"M224,128a8,8,0,0,1-8,8H136v80a8,8,0,0,1-16,0V136H40a8,8,0,0,1,0-16h80V40a8,8,0,0,1,16,0v80h80A8,8,0,0,1,224,128Z";
        var shape=new System.Windows.Shapes.Path{Data=Geometry.Parse(data),Stretch=Stretch.Uniform,Width=12,Height=12,IsHitTestVisible=false};
        shape.SetBinding(System.Windows.Shapes.Shape.FillProperty,new Binding("Foreground"){RelativeSource=new RelativeSource(RelativeSourceMode.FindAncestor,typeof(Button),1)});
        button.Style=RoundButton;button.Width=44.0/3*2;button.Height=44.0/3*2;button.Padding=new Thickness(0);button.Margin=new Thickness(0);button.Content=shape;
        button.ToolTip=label;AutomationProperties.SetName(button,label);ToolTipService.SetShowOnDisabled(button,true);
    }
}
