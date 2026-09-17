using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Markup;
using System.Windows.Media;
using Rhino;
namespace AssetCopilot;

// This component occupies only the existing size section; no global styles or animations change.
public sealed class SizingPanel : StackPanel
{
    readonly ComboBox axis,units;
    readonly TextBox dimension,percentage;
    readonly StackPanel manual=new(),automatic=new(),referencePanel=new();
    readonly TextBlock summary=new(){TextWrapping=TextWrapping.Wrap,FontSize=10.5,Margin=new Thickness(0,6,0,2)},referenceInfo=new(){TextWrapping=TextWrapping.Wrap,FontSize=10.5,Foreground=UiTheme.Muted};
    readonly Button adjust=new(){Content="Use estimate & adjust",HorizontalAlignment=HorizontalAlignment.Left,FontSize=11};
    readonly RadioButton[] choices;
    FaceReference? reference;
    SizingDecision? lastDecision;
    SizeEstimate? estimate;
    bool loading;
    public event Action? Changed;
    public event Action? ReferenceRequested;
    public SizeMode Mode {get;private set;}=SizeMode.Manual;
    public FaceReference? Reference=>reference;
    public string Summary=>summary.Text;
    public bool ManualVisible=>manual.Visibility==Visibility.Visible;
    public bool ReferenceVisible=>referencePanel.Visibility==Visibility.Visible;
    public SizingPanel(ComboBox axis,TextBox dimension,ComboBox units,TextBox percentage)
    {
        this.axis=axis;this.dimension=dimension;this.units=units;this.percentage=percentage;
        var style=(Style)XamlReader.Parse("""
<Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="RadioButton">
 <Setter Property="Background" Value="#EEEEF1"/><Setter Property="Foreground" Value="#52525A"/><Setter Property="Cursor" Value="Hand"/><Setter Property="FontSize" Value="11"/><Setter Property="MinHeight" Value="30"/>
 <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="RadioButton"><Border x:Name="Tile" CornerRadius="10" Background="{TemplateBinding Background}" BorderBrush="Transparent" BorderThickness="1" Padding="6,5"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border>
 <ControlTemplate.Triggers><Trigger Property="IsChecked" Value="True"><Setter Property="Background" Value="#1D1D1F"/><Setter Property="Foreground" Value="White"/></Trigger><Trigger Property="IsMouseOver" Value="True"><Setter Property="Opacity" Value="0.85"/></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Tile" Property="BorderBrush" Value="#94949C"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.4"/></Trigger></ControlTemplate.Triggers>
 </ControlTemplate></Setter.Value></Setter>
</Style>
""");
        var tabs=new Grid{Margin=new Thickness(0,2,0,8)};string group="Sizing_"+Guid.NewGuid().ToString("N");
        string[] titles=["Manual","Real-world","Reference"];choices=new RadioButton[3];
        for(int i=0;i<3;i++)
        {
            tabs.ColumnDefinitions.Add(new());int index=i;
            var choice=new RadioButton{Content=titles[i],GroupName=group,Style=style,Margin=new Thickness(i==0?0:4,0,i==2?0:4,0)};
            AutomationProperties.SetName(choice,titles[i]);choice.Checked+=(_,_)=>{Mode=(SizeMode)index;ShowMode();Notify();};Grid.SetColumn(choice,i);tabs.Children.Add(choice);choices[i]=choice;
        }
        Children.Add(tabs);
        foreach(var a in new[]{"Width X","Depth Y","Height Z"})axis.Items.Add(a);axis.SelectedIndex=0;
        foreach(var u in new[]{"mm","cm","m","in","ft"})units.Items.Add(u);units.SelectedIndex=0;
        var fields=new Grid();fields.ColumnDefinitions.Add(new(){Width=new GridLength(80)});fields.ColumnDefinitions.Add(new());fields.ColumnDefinitions.Add(new(){Width=new GridLength(108)});
        foreach(var f in new Control[]{axis,dimension,units}){f.Margin=new Thickness(0,4,6,4);f.Padding=new Thickness(6);fields.Children.Add(f);}
        Grid.SetColumn(dimension,1);Grid.SetColumn(units,2);manual.Children.Add(fields);
        var ratio=new DockPanel();ratio.Children.Add(new TextBlock{Text="Scale (%)",Width=100,VerticalAlignment=VerticalAlignment.Center});percentage.Padding=new Thickness(5);percentage.Width=95;ratio.Children.Add(percentage);manual.Children.Add(ratio);Children.Add(manual);
        var actions=new WrapPanel();var pick=new Button{Content="Pick reference face in Rhino",FontSize=11};pick.Click+=(_,_)=>ReferenceRequested?.Invoke();AutomationProperties.SetName(pick,"Select a planar face in Rhino as a size reference");actions.Children.Add(pick);
        var clear=new Button{Content="Clear",FontSize=11};clear.Click+=(_,_)=>SetReference(null);actions.Children.Add(clear);referencePanel.Children.Add(actions);referencePanel.Children.Add(referenceInfo);automatic.Children.Add(referencePanel);
        Children.Add(automatic);Children.Add(summary);adjust.Click+=(_,_)=>UseManual();Children.Add(adjust);
        AutomationProperties.SetName(axis,"Size axis");AutomationProperties.SetName(dimension,"Manual model size");AutomationProperties.SetName(units,"Input units");AutomationProperties.SetName(percentage,"Uniform scale percentage");
        choices[0].IsChecked=true;ShowMode();
    }
    void Notify(){lastDecision=null;adjust.IsEnabled=false;if(!loading)Changed?.Invoke();}
    void ShowMode(){manual.Visibility=Mode==SizeMode.Manual?Visibility.Visible:Visibility.Collapsed;automatic.Visibility=Mode==SizeMode.Manual?Visibility.Collapsed:Visibility.Visible;referencePanel.Visibility=Mode==SizeMode.ReferenceFace?Visibility.Visible:Visibility.Collapsed;adjust.Visibility=Mode==SizeMode.Manual?Visibility.Collapsed:Visibility.Visible;}
    public void SelectMode(SizeMode value){if(!Enum.IsDefined(typeof(SizeMode),value))throw new ArgumentException("Invalid size mode.");choices[(int)value].IsChecked=true;}
    public void SetEstimate(SizeEstimate? value){estimate=value;lastDecision=null;adjust.IsEnabled=false;}
    public void ShowPending(){SetEstimate(null);ShowHint("Analyzing the image and description to estimate real-world size…");}
    public void ShowHint(string message){summary.Text=message;summary.Foreground=UiTheme.Muted;summary.Visibility=Mode==SizeMode.Manual?Visibility.Collapsed:Visibility.Visible;lastDecision=null;adjust.IsEnabled=false;}
    public void SetReference(FaceReference? value){reference=value;Notify();}
    public SizingSettings Capture()
    {
        bool sizeOk=double.TryParse(dimension.Text,NumberStyles.Float,CultureInfo.CurrentCulture,out double size),ratioOk=float.TryParse(percentage.Text,NumberStyles.Float,CultureInfo.CurrentCulture,out float ratio);
        if(Mode==SizeMode.Manual&&(!sizeOk||!ratioOk||!Compat.IsFinite(size)||!Compat.IsFinite(ratio)))throw new ArgumentException("Size and scale must be valid numbers.");
        if(Mode!=SizeMode.Manual){if(!Compat.IsFinite(size)||size<=0||size>1e9)size=600;if(!Compat.IsFinite(ratio)||ratio<=0||ratio>10000)ratio=100;}
        return new SizingSettings{Mode=Mode,Category="auto",ManualAxis=axis.SelectedIndex,ManualUnit=units.SelectedIndex,ManualSize=sizeOk?size:600,Percent=ratioOk?ratio:100,Reference=reference};
    }
    public void Restore(SizingSettings? settings)
    {
        if(settings==null)return;loading=true;
        try
        {
            dimension.Text=settings.ManualSize.ToString(CultureInfo.CurrentCulture);percentage.Text=settings.Percent.ToString(CultureInfo.CurrentCulture);
            axis.SelectedIndex=Compat.Clamp(settings.ManualAxis,0,2);units.SelectedIndex=Compat.Clamp(settings.ManualUnit,0,4);estimate=null;reference=settings.Reference;
            SelectMode(Enum.IsDefined(typeof(SizeMode),settings.Mode)?settings.Mode:SizeMode.Manual);
        }
        finally{loading=false;}
    }
    public SizingDecision Resolve(AssetData? raw,string description,string fileName,RhinoDoc doc)
    {
        double meters=RhinoPlacement.Meters(doc);
        if(Mode!=SizeMode.Manual&&estimate==null)throw new ArgumentException("Waiting for a size estimate. Add an image or describe the target object.");
        if(Mode==SizeMode.ReferenceFace)reference=RhinoSizing.Refresh(doc,reference);
        var result=Sizing.Resolve(Capture(),description,fileName,raw==null?null:Sizing.RhinoSpan(raw),meters,reference,estimate);
        result.DocumentUnit=doc.ModelUnitSystem.ToString();lastDecision=result;adjust.IsEnabled=true;summary.Foreground=UiTheme.Muted;
        summary.Visibility=Mode==SizeMode.Manual?Visibility.Collapsed:Visibility.Visible;
        summary.Text=$"{(estimate?.Axis>=0?"Target":"Estimated")} {(result.Axis==2?"height":estimate?.Axis==1?"depth":estimate?.Axis==0?"width":"longest horizontal side")}: {result.TargetMeters/meters:0.###} {doc.ModelUnitSystem}\n{result.Basis}";
        referenceInfo.Text=reference==null?"Select a planar face such as a floor, tabletop, or wall.":$"In-plane bounds: {reference.ShortMeters/meters:0.###} × {reference.LongMeters/meters:0.###} {doc.ModelUnitSystem}\nSurface area: {reference.AreaSquareMeters/(meters*meters):0.###} {doc.ModelUnitSystem}²";
        return result;
    }
    public void ShowError(string message){summary.Text=message;summary.Foreground=Brushes.Firebrick;summary.Visibility=Visibility.Visible;lastDecision=null;adjust.IsEnabled=false;if(Mode==SizeMode.ReferenceFace)referenceInfo.Text="Select a planar surface or mesh. Use Ctrl+Shift to select a single face.";}
    public void UseManual()
    {
        if(lastDecision==null)return;var selected=lastDecision;
        dimension.Text=(selected.TargetMeters/Sizing.UnitMeters[Compat.Clamp(units.SelectedIndex,0,4)]).ToString("0.######",CultureInfo.CurrentCulture);percentage.Text="100";axis.SelectedIndex=selected.Axis;SelectMode(SizeMode.Manual);
    }
}
