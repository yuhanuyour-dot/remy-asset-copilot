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
    readonly Button adjust=new(){Content="使用建议并手动调整",HorizontalAlignment=HorizontalAlignment.Left,FontSize=11};
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
        string[] titles=["手动尺寸","现实尺寸","参考面"];choices=new RadioButton[3];
        for(int i=0;i<3;i++)
        {
            tabs.ColumnDefinitions.Add(new());int index=i;
            var choice=new RadioButton{Content=titles[i],GroupName=group,Style=style,Margin=new Thickness(i==0?0:4,0,i==2?0:4,0)};
            AutomationProperties.SetName(choice,titles[i]);choice.Checked+=(_,_)=>{Mode=(SizeMode)index;ShowMode();Notify();};Grid.SetColumn(choice,i);tabs.Children.Add(choice);choices[i]=choice;
        }
        Children.Add(tabs);
        foreach(var a in new[]{"宽 X","深 Y","高 Z"})axis.Items.Add(a);axis.SelectedIndex=0;
        foreach(var u in new[]{"毫米 mm","厘米 cm","米 m","英寸 in","英尺 ft"})units.Items.Add(u);units.SelectedIndex=0;
        var fields=new Grid();fields.ColumnDefinitions.Add(new(){Width=new GridLength(80)});fields.ColumnDefinitions.Add(new());fields.ColumnDefinitions.Add(new(){Width=new GridLength(108)});
        foreach(var f in new Control[]{axis,dimension,units}){f.Margin=new Thickness(0,4,6,4);f.Padding=new Thickness(6);fields.Children.Add(f);}
        Grid.SetColumn(dimension,1);Grid.SetColumn(units,2);manual.Children.Add(fields);
        var ratio=new DockPanel();ratio.Children.Add(new TextBlock{Text="等比缩放 %",Width=100,VerticalAlignment=VerticalAlignment.Center});percentage.Padding=new Thickness(5);percentage.Width=95;ratio.Children.Add(percentage);manual.Children.Add(ratio);Children.Add(manual);
        var actions=new WrapPanel();var pick=new Button{Content="在 Rhino 选择参考面",FontSize=11};pick.Click+=(_,_)=>ReferenceRequested?.Invoke();AutomationProperties.SetName(pick,"选择 Rhino 平面作为尺寸参照");actions.Children.Add(pick);
        var clear=new Button{Content="清除",FontSize=11};clear.Click+=(_,_)=>SetReference(null);actions.Children.Add(clear);referencePanel.Children.Add(actions);referencePanel.Children.Add(referenceInfo);automatic.Children.Add(referencePanel);
        Children.Add(automatic);Children.Add(summary);adjust.Click+=(_,_)=>UseManual();Children.Add(adjust);
        AutomationProperties.SetName(axis,"尺寸方向");AutomationProperties.SetName(dimension,"手动模型尺寸");AutomationProperties.SetName(units,"尺寸输入单位");AutomationProperties.SetName(percentage,"等比缩放百分比");
        choices[0].IsChecked=true;ShowMode();
    }
    void Notify(){lastDecision=null;adjust.IsEnabled=false;if(!loading)Changed?.Invoke();}
    void ShowMode(){manual.Visibility=Mode==SizeMode.Manual?Visibility.Visible:Visibility.Collapsed;automatic.Visibility=Mode==SizeMode.Manual?Visibility.Collapsed:Visibility.Visible;referencePanel.Visibility=Mode==SizeMode.ReferenceFace?Visibility.Visible:Visibility.Collapsed;adjust.Visibility=Mode==SizeMode.Manual?Visibility.Collapsed:Visibility.Visible;}
    public void SelectMode(SizeMode value){if(!Enum.IsDefined(value))throw new ArgumentException("无效尺寸方式。");choices[(int)value].IsChecked=true;}
    public void SetEstimate(SizeEstimate? value){estimate=value;lastDecision=null;adjust.IsEnabled=false;}
    public void ShowPending(){SetEstimate(null);ShowHint("正在分析图片与描述，估算现实尺寸…");}
    public void ShowHint(string message){summary.Text=message;summary.Foreground=UiTheme.Muted;summary.Visibility=Mode==SizeMode.Manual?Visibility.Collapsed:Visibility.Visible;lastDecision=null;adjust.IsEnabled=false;}
    public void SetReference(FaceReference? value){reference=value;Notify();}
    public SizingSettings Capture()
    {
        bool sizeOk=double.TryParse(dimension.Text,NumberStyles.Float,CultureInfo.CurrentCulture,out double size),ratioOk=float.TryParse(percentage.Text,NumberStyles.Float,CultureInfo.CurrentCulture,out float ratio);
        if(Mode==SizeMode.Manual&&(!sizeOk||!ratioOk||!double.IsFinite(size)||!float.IsFinite(ratio)))throw new ArgumentException("尺寸和比例必须是有效数字。");
        if(Mode!=SizeMode.Manual){if(!double.IsFinite(size)||size<=0||size>1e9)size=600;if(!float.IsFinite(ratio)||ratio<=0||ratio>10000)ratio=100;}
        return new SizingSettings{Mode=Mode,Category="auto",ManualAxis=axis.SelectedIndex,ManualUnit=units.SelectedIndex,ManualSize=sizeOk?size:600,Percent=ratioOk?ratio:100,Reference=reference};
    }
    public void Restore(SizingSettings? settings)
    {
        if(settings==null)return;loading=true;
        try
        {
            dimension.Text=settings.ManualSize.ToString(CultureInfo.CurrentCulture);percentage.Text=settings.Percent.ToString(CultureInfo.CurrentCulture);
            axis.SelectedIndex=Math.Clamp(settings.ManualAxis,0,2);units.SelectedIndex=Math.Clamp(settings.ManualUnit,0,4);estimate=null;reference=settings.Reference;
            SelectMode(Enum.IsDefined(settings.Mode)?settings.Mode:SizeMode.Manual);
        }
        finally{loading=false;}
    }
    public SizingDecision Resolve(AssetData? raw,string description,string fileName,RhinoDoc doc)
    {
        double meters=RhinoPlacement.Meters(doc);
        if(Mode!=SizeMode.Manual&&estimate==null)throw new ArgumentException("正在等待自动尺寸估算，请添加图片或描述目标物体。");
        if(Mode==SizeMode.ReferenceFace)reference=RhinoSizing.Refresh(doc,reference);
        var result=Sizing.Resolve(Capture(),description,fileName,raw==null?null:Sizing.RhinoSpan(raw),meters,reference,estimate);
        result.DocumentUnit=doc.ModelUnitSystem.ToString();lastDecision=result;adjust.IsEnabled=true;summary.Foreground=UiTheme.Muted;
        summary.Visibility=Mode==SizeMode.Manual?Visibility.Collapsed:Visibility.Visible;
        summary.Text=$"{(estimate?.Axis>=0?"输出":"估算")}{(result.Axis==2?"高度":estimate?.Axis==1?"深度":estimate?.Axis==0?"宽度":"水平长边")}：{result.TargetMeters/meters:0.###} {doc.ModelUnitSystem}\n{result.Basis}";
        referenceInfo.Text=reference==null?"请选择地面、桌面或墙面等平面。":$"面内包围：{reference.ShortMeters/meters:0.###} × {reference.LongMeters/meters:0.###} {doc.ModelUnitSystem}\n实际面积：{reference.AreaSquareMeters/(meters*meters):0.###} {doc.ModelUnitSystem}²";
        return result;
    }
    public void ShowError(string message){summary.Text=message;summary.Foreground=Brushes.Firebrick;summary.Visibility=Visibility.Visible;lastDecision=null;adjust.IsEnabled=false;if(Mode==SizeMode.ReferenceFace)referenceInfo.Text="可选平面曲面、平面网格；Ctrl+Shift 可选单个面。";}
    public void UseManual()
    {
        if(lastDecision==null)return;var selected=lastDecision;
        dimension.Text=(selected.TargetMeters/Sizing.UnitMeters[Math.Clamp(units.SelectedIndex,0,4)]).ToString("0.######",CultureInfo.CurrentCulture);percentage.Text="100";axis.SelectedIndex=selected.Axis;SelectMode(SizeMode.Manual);
    }
}
