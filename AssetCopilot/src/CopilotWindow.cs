using System.Globalization;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using System.Windows.Markup;
using System.Windows.Threading;
using Microsoft.Win32;
using Rhino;
namespace AssetCopilot;

public sealed partial class CopilotWindow : Window
{
    readonly RemyLogo logo=new();
    readonly TextBlock info=new(), document=new(), notice=new(), faceRange=new(), savedPath=new();
    readonly TextBox dimension=new(){Text="600"},percentage=new(){Text="100"},task=new(),prompt=new(){AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MinHeight=62,MaxHeight=140,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MaxLength=1024},faces=new(){Text="5000",Width=95},saveRoot=new();
    readonly PasswordBox key=new();
    readonly ComboBox axis=new(),units=new(),model=new();
    readonly Slider faceSlider=new(){Minimum=500,Maximum=25000,Value=5000,TickFrequency=100,IsSnapToTickEnabled=true};
    readonly Button generate=new(),place=new(),resume=new(),stop=new(),reset=new(),check=new();
    readonly CheckBox autoPlace=new(){Content="生成完成后直接进入放置（在 Rhino 中点选位置）",IsChecked=true,Margin=new Thickness(0,8,0,4)};
    readonly PbrPreview viewport=new();
    readonly Border composerFrame;
    readonly SizingPanel sizing;
    readonly LocalSizeInference sizeInference=new();
    CancellationTokenSource? sizeRequest;
    readonly Border attachment=new(){Visibility=Visibility.Collapsed};
    readonly TextBlock attachmentName=new(),inputHint=new(),placeholder=new(){Text="描述你想生成的模型，或粘贴一张图片…",Foreground=Brushes.Gray,IsHitTestVisible=false,Margin=new Thickness(9)};
    readonly Image photo=new(){Stretch=Stretch.Uniform,Width=78,Height=78,IsHitTestVisible=false};
    readonly Grid preview=new(){ClipToBounds=true,Background=Brushes.White,Focusable=true};
    readonly List<Control> editing=new();
    readonly DispatcherTimer changeTimer=new(){Interval=TimeSpan.FromMilliseconds(280)};
    CancellationTokenSource? cancellation;
    AssetData? raw; AssetRecord? record; string? imagePath;
    bool busy,ready,syncFaces,rendering,renderValid,localAttachment,modelPreviewReady;
    int renderSequence;

    public CopilotWindow(string? sample=null)
    {
        AssetStore.Initialize();
        Title="Remy Asset Copilot 0.5.2";Width=540;Height=Math.Min(910,SystemParameters.WorkArea.Height-40);MinWidth=450;MinHeight=620;
        WindowStartupLocation=WindowStartupLocation.CenterScreen;Background=new SolidColorBrush(Color.FromRgb(245,245,247));FontFamily=new FontFamily("Microsoft YaHei UI");FontSize=12;
        Resources.Add(typeof(Button),XamlReader.Parse("<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Setter Property='Cursor' Value='Hand'/><Setter Property='Padding' Value='12,8'/><Setter Property='Background' Value='#EEEEF1'/><Setter Property='Foreground' Value='#252527'/><Setter Property='BorderThickness' Value='0'/><Setter Property='Margin' Value='0,3,6,3'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border CornerRadius='16' Background='{TemplateBinding Background}' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.4'/></Trigger><Trigger Property='IsMouseOver' Value='True'><Setter Property='Opacity' Value='0.82'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>"));
        var root=new StackPanel{Margin=new Thickness(18,14,18,18)};
        var layout=new DockPanel();var footer=new StackPanel{Margin=new Thickness(18,5,18,12)};
        DockPanel.SetDock(footer,Dock.Bottom);layout.Children.Add(footer);
        var detailScroll=new ScrollViewer{Content=root,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};layout.Children.Add(detailScroll);Content=layout;
        var header=new DockPanel{Margin=new Thickness(6,0,6,14)};
        logo.Width=268;logo.Height=78;logo.HorizontalAlignment=HorizontalAlignment.Left;
        header.Children.Add(logo);root.Children.Add(header);
        var body=new StackPanel{Margin=new Thickness(16)};
        root.Children.Add(new Border{Background=Brushes.White,CornerRadius=new CornerRadius(22),Child=body});
        var composer=new StackPanel{Margin=new Thickness(10,8,10,8)};
        var inputBox=new Border{CornerRadius=new CornerRadius(18),BorderBrush=UiTheme.Line,BorderThickness=new Thickness(1),Background=Brushes.White,Child=composer,Margin=new Thickness(0,0,0,16)};
        composerFrame=inputBox;
        var attachmentRow=new DockPanel();var remove=EditButton("×",RemoveAttachment);remove.ToolTip="移除附件";DockPanel.SetDock(remove,Dock.Right);attachmentRow.Children.Add(remove);
        photo.Width=52;photo.Height=52;attachmentRow.Children.Add(photo);attachmentName.Margin=new Thickness(9,0,0,0);attachmentName.VerticalAlignment=VerticalAlignment.Center;attachmentName.TextTrimming=TextTrimming.CharacterEllipsis;attachmentRow.Children.Add(attachmentName);
        attachment.Child=attachmentRow;attachment.Background=new SolidColorBrush(Color.FromRgb(247,247,249));attachment.CornerRadius=new CornerRadius(10);attachment.Padding=new Thickness(5);composer.Children.Add(attachment);
        var textArea=new Grid();prompt.Background=Brushes.Transparent;prompt.BorderThickness=new Thickness(0);prompt.Padding=new Thickness(9);prompt.MinHeight=58;prompt.ToolTip="支持纯文字、图片，或图片加文字。Ctrl+V 可直接粘贴图片。";textArea.Children.Add(prompt);textArea.Children.Add(placeholder);placeholder.Foreground=UiTheme.Muted;AutomationProperties.SetName(prompt,"描述模型或粘贴图片");composer.Children.Add(textArea);
        var toolbar=new Grid{Margin=new Thickness(4,8,4,0)};
        toolbar.ColumnDefinitions.Add(new(){Width=GridLength.Auto});toolbar.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});toolbar.ColumnDefinitions.Add(new(){Width=GridLength.Auto});toolbar.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        var plus=EditButton("",()=>{});UiTheme.IconButton(plus,"plus","添加图片或 GLB");
        var menu=new ContextMenu();var addImage=new MenuItem{Header="添加图片…"};addImage.Click+=(_,_)=>ChoosePhoto();menu.Items.Add(addImage);
        var addGlb=new MenuItem{Header="添加 GLB 模型…"};addGlb.Click+=async(_,_)=>{var d=new OpenFileDialog{Filter="GLB 模型|*.glb",InitialDirectory=AssetStore.SaveRoot()};if(d.ShowDialog(this)==true)await Local(d.FileName);};menu.Items.Add(addGlb);
        plus.Click+=(_,_)=>{menu.PlacementTarget=plus;menu.IsOpen=true;};toolbar.Children.Add(plus);
        ActionButton(generate,"",async()=>await RunGeneration(false));UiTheme.IconButton(generate,"send","生成模型");Grid.SetColumn(generate,3);toolbar.Children.Add(generate);
        model.Style=UiTheme.ModelPicker;model.Width=104;model.Margin=new Thickness(0,0,8,0);model.VerticalAlignment=VerticalAlignment.Center;model.HorizontalAlignment=HorizontalAlignment.Right;AutomationProperties.SetName(model,"生成模型版本");Grid.SetColumn(model,2);toolbar.Children.Add(model);
        inputHint.FontSize=11;inputHint.Foreground=UiTheme.Muted;inputHint.TextWrapping=TextWrapping.Wrap;inputHint.Margin=new Thickness(8,6,8,0);composer.Children.Add(toolbar);composer.Children.Add(inputHint);
        inputBox.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.V&&Keyboard.Modifiers.HasFlag(ModifierKeys.Control)&&!busy&&(Clipboard.ContainsImage()||Clipboard.ContainsFileDropList())){PastePhoto();e.Handled=true;}};
        inputBox.AllowDrop=true;inputBox.PreviewDragOver+=(_,e)=>{e.Effects=e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;};
        inputBox.Drop+=async(_,e)=>{if(busy)return;try{if(e.Data.GetData(DataFormats.FileDrop) is string[] paths&&paths.Length==1){if(Path.GetExtension(paths[0]).Equals(".glb",StringComparison.OrdinalIgnoreCase))await Local(paths[0]);else LoadPhoto(paths[0]);}else throw new ArgumentException("请一次添加一张图片或一个 GLB。");}catch(Exception ex){Error(ex);}e.Handled=true;};
        preview.Children.Add(viewport);InstallPreviewStartup();
        viewport.ImageRequested+=RequestPhoto;
        preview.HorizontalAlignment=HorizontalAlignment.Stretch;body.SizeChanged+=(_,_)=>{preview.Width=body.ActualWidth;preview.Height=Math.Min(360,body.ActualWidth);};
        preview.Margin=new Thickness(0,0,0,16);body.Children.Add(preview);
        body.Children.Add(inputBox);
        body.Children.Add(Label("网格精度 · GLB"));
        model.Items.Add("P2.0 · 快速生成");model.Items.Add("V3.1 · 高质量");model.SelectedIndex=0;
        var faceRow=new DockPanel();faces.Padding=new Thickness(6);DockPanel.SetDock(faces,Dock.Right);faceRow.Children.Add(faces);faceSlider.VerticalAlignment=VerticalAlignment.Center;faceSlider.Margin=new Thickness(0,4,12,4);faceRow.Children.Add(faceSlider);body.Children.Add(faceRow);
        faceRange.Foreground=Brushes.Gray;faceRange.FontSize=10;body.Children.Add(faceRange);UpdateFaceRange();
        model.SelectionChanged+=(_,_)=>UpdateFaceRange();
        faceSlider.ValueChanged+=(_,_)=>{if(syncFaces)return;syncFaces=true;faces.Text=((int)faceSlider.Value).ToString();syncFaces=false;};
        faces.TextChanged+=(_,_)=>{if(syncFaces)return;if(int.TryParse(faces.Text,out int n)&&n>=500&&n<=faceSlider.Maximum){syncFaces=true;faceSlider.Value=n;syncFaces=false;}};
        body.Children.Add(Label("尺寸与比例"));
        sizing=new SizingPanel(axis,dimension,units,percentage);body.Children.Add(sizing);
        sizing.Changed+=QueueRender;sizing.ReferenceRequested+=SelectSizeReference;
        document.Foreground=Brushes.Gray;document.Margin=new Thickness(0,8,0,3);document.TextWrapping=TextWrapping.Wrap;body.Children.Add(document);
        info.TextWrapping=TextWrapping.Wrap;info.Margin=new Thickness(0,3,0,4);body.Children.Add(info);
        notice.TextWrapping=TextWrapping.Wrap;notice.FontSize=10;notice.Foreground=Brushes.DarkGoldenrod;body.Children.Add(notice);footer.Children.Add(autoPlace);
        ActionButton(place,"放入 Rhino 场景",Place);footer.Children.Add(place);
        var storage=new StackPanel();storage.Children.Add(Label("模型保存目录 · 新任务生效"));
        saveRoot.Text=AssetStore.SaveRoot();saveRoot.Padding=new Thickness(6);storage.Children.Add(saveRoot);
        var storageActions=new WrapPanel();
        storageActions.Children.Add(EditButton("选择目录",()=>{var folder=FolderPicker.Pick(this,AssetStore.SaveRoot());if(folder!=null){saveRoot.Text=folder;SaveDirectory();}}));
        storageActions.Children.Add(EditButton("保存路径",SaveDirectory));
        storageActions.Children.Add(Button("打开资产文件夹",()=>OpenFolder(record?.Folder??AssetStore.SaveRoot())));
        storage.Children.Add(storageActions);savedPath.TextWrapping=TextWrapping.Wrap;savedPath.Foreground=Brushes.Gray;savedPath.FontSize=10;storage.Children.Add(savedPath);
        root.Children.Add(new Expander{Header="模型与贴图保存位置",Content=storage,IsExpanded=true,Margin=new Thickness(7,12,7,0)});
        var settings=new StackPanel();settings.Children.Add(Label("Tripo API Key · 仅保留在本次窗口内"));key.Padding=new Thickness(7);settings.Children.Add(key);
        check.Content="检查连接（不生成）";check.Click+=async(_,_)=>await CheckConnection();settings.Children.Add(check);
        settings.Children.Add(new TextBlock{Text="点击生成会上传所选图片或文字，并消耗 Tripo API 额度。",FontSize=10,Foreground=Brushes.Gray,TextWrapping=TextWrapping.Wrap});
        settings.Children.Add(Label("任务编号 · 失败或中断后可继续查询"));task.Padding=new Thickness(6);settings.Children.Add(task);
        resume.Content="继续查询 / 载入";resume.Click+=async(_,_)=>await RunGeneration(true);
        stop.Content="停止等待";stop.Click+=(_,_)=>cancellation?.Cancel();stop.IsEnabled=false;
        reset.Content="新任务";reset.Click+=(_,_)=>{if(localAttachment)RemoveAttachment();record=null;raw=null;renderValid=false;modelPreviewReady=false;renderSequence++;viewport.ClearModel();task.Clear();AssetStore.ClearPointer();SetStatus("可创建新任务");Buttons();QueueRender();};
        var recovery=new WrapPanel();recovery.Children.Add(resume);recovery.Children.Add(stop);recovery.Children.Add(reset);settings.Children.Add(recovery);
        root.Children.Add(new Expander{Header="连接设置与任务恢复",Content=settings,IsExpanded=false,Margin=new Thickness(7,12,7,0)});
        root.Children.Add(new TextBlock{Text="窗口支持 PBR 材质预览；场景灯光不同，明暗和反射会有差异。\n放置后保存在 AssetCopilot 子图层；Ctrl+Z 可撤销。",Foreground=Brushes.Gray,FontSize=10,Margin=new Thickness(8,12,8,0),TextWrapping=TextWrapping.Wrap});
        ConfigureWindowModes(layout,detailScroll,body,header);
        editing.AddRange(new Control[]{prompt,model,faces,faceSlider,axis,dimension,units,percentage,saveRoot,key,autoPlace,reset});
        prompt.TextChanged+=(_,_)=>{Buttons();QueueRender();};
        dimension.TextChanged+=(_,_)=>QueueRender();percentage.TextChanged+=(_,_)=>QueueRender();axis.SelectionChanged+=(_,_)=>QueueRender();units.SelectionChanged+=(_,_)=>QueueRender();
        changeTimer.Tick+=async(_,_)=>{changeTimer.Stop();await Render();};

        Activated+=(_,_)=>{Context();if(!busy)QueueRender();};Closing+=(_,_)=>cancellation?.Cancel();Closed+=(_,_)=>{sizeRequest?.Cancel();sizeInference.Dispose();changeTimer.Stop();key.Clear();renderSequence++;logo.Dispose();viewport.ClosePreview();};
        record=AssetStore.Restore();if(record!=null){task.Text=record.ActiveTaskId;savedPath.Text=record.Folder;sizing.Restore(record.Sizing);}
        ready=true;Context();Buttons();QueueRender();
        if(sample!=null)Loaded+=async(_,_)=>await Local(sample);
    }
    static TextBlock Label(string text)=>new(){Text=text,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,9,0,5)};
    Button Button(string text,Action action){var b=new Button{Content=text};b.Click+=(_,_)=>{try{action();}catch(Exception e){Error(e);}};return b;}
    Button EditButton(string text,Action action){var b=Button(text,action);editing.Add(b);return b;}
    void ActionButton(Button b,string text,Action action){b.Content=text;b.Background=new SolidColorBrush(Color.FromRgb(29,29,31));b.Foreground=Brushes.White;b.Margin=new Thickness(0,5,0,0);b.Padding=new Thickness(12);b.Click+=(_,_)=>action();}
    void SetStatus(string text){viewport.ReportStatus(text,text.StartsWith("已停止"));CompactMessage(text);}
    void Error(Exception e){if(!IsVisible)return;CompactMessage(e.Message);viewport.ReportStatus(e.Message,true);info.Text=e.Message;MessageBox.Show(this,e.Message,"Asset Copilot",MessageBoxButton.OK,MessageBoxImage.Information);}
    void Context(){var doc=RhinoDoc.ActiveDoc;document.Text=doc==null?"请打开 Rhino 文档":$"当前文档：{doc.ModelUnitSystem} · {doc.Name ?? "未命名"}";}
    void Buttons()
    {
        if(!ready)return;
        bool local=localAttachment;
        generate.IsEnabled=!busy&&!local&&(imagePath!=null||!string.IsNullOrWhiteSpace(prompt.Text));
        placeholder.Visibility=string.IsNullOrEmpty(prompt.Text)?Visibility.Visible:Visibility.Collapsed;
        inputHint.Text="按描述处理图片，再生成模型 · 两步计费";
        inputHint.Visibility=!local&&imagePath!=null&&!string.IsNullOrWhiteSpace(prompt.Text)?Visibility.Visible:Visibility.Collapsed;
        place.IsEnabled=!busy&&!rendering&&renderValid&&raw!=null;resume.IsEnabled=!busy;check.IsEnabled=!busy;stop.IsEnabled=busy;RefreshCompactFeedback();
    }
    void RemoveAttachment()
    {
        localAttachment=false;photo.Source=null;attachment.Visibility=Visibility.Collapsed;imagePath=null;raw=null;modelPreviewReady=false;record=null;task.Clear();renderValid=false;renderSequence++;viewport.ClearModel();info.Text="";notice.Text="";AssetStore.ClearPointer();Buttons();QueueRender();
    }
    void Busy(bool value){busy=value;if(value)CompactMessage("准备中…");viewport.SetBusy(value);sizing.IsEnabled=!value;foreach(var c in editing)c.IsEnabled=!value;Buttons();}
    void UpdateFaceRange()
    {
        if(model.SelectedIndex<0)return;
        syncFaces=true;faceSlider.Maximum=model.SelectedIndex==0?25000:2000000;
        if(!int.TryParse(faces.Text,out int n))n=5000;
        n=Compat.Clamp(n,500,(int)faceSlider.Maximum);faceSlider.Value=n;faces.Text=n.ToString();
        faceRange.Text=$"目标三角面数：500–{faceSlider.Maximum:N0} · 格式固定 GLB";syncFaces=false;
    }
    GenerationOptions Options(){if(!int.TryParse(faces.Text,out int n))throw new ArgumentException("面数必须是整数。");var o=new GenerationOptions(model.SelectedIndex==0?GenerationOptions.P2:GenerationOptions.V31,n);o.Validate();return o;}
    void SaveDirectory(){try{AssetStore.SetRoot(saveRoot.Text);saveRoot.Text=AssetStore.SaveRoot();SetStatus("保存目录已更新");}catch(Exception e){Error(e);}}
    void OpenFolder(string path){Directory.CreateDirectory(path);var start=new ProcessStartInfo("explorer.exe"){UseShellExecute=false,CreateNoWindow=true};Compat.SetArguments(start,path);Process.Start(start);}
    void RequestPhoto()
    {
        // Defer native dialogs until the WebView2 message callback has returned.
        if(!busy)Dispatcher.BeginInvoke(new Action(ChoosePhoto));
    }
    void ChoosePhoto()
    {
        if(busy||!IsVisible)return;
        var dialog=new OpenFileDialog{Filter="图片|*.png;*.jpg;*.jpeg",Title="添加参考图片"};
        if(dialog.ShowDialog(this)==true)LoadPhoto(dialog.FileName);
    }
    void LoadPhoto(string path)
    {
        try
        {
            var file=new FileInfo(path);if(!file.Exists||file.Length==0||file.Length>20*1024*1024||file.Extension.ToLowerInvariant() is not(".jpg" or ".jpeg" or ".png"))throw new ArgumentException("请选择不超过 20 MB 的 JPG / PNG。");
            using var stream=File.OpenRead(path);var bmp=BitmapFrame.Create(stream,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);bmp.Freeze();
            localAttachment=false;imagePath=path;photo.Source=bmp;attachmentName.Text=Path.GetFileName(path);attachment.Visibility=Visibility.Visible;record=null;task.Clear();AssetStore.ClearPointer();raw=null;modelPreviewReady=false;rendering=false;renderValid=false;renderSequence++;viewport.ClearModel();notice.Text="";info.Text=Path.GetFileName(path);SetStatus("图片已就绪");Buttons();QueueRender();
        }catch(Exception e){Error(e);}
    }
    async void PastePhoto()
    {
        if(busy)return;
        try
        {
            if(Clipboard.ContainsImage())
            {
                var bmp=Clipboard.GetImage();if(bmp==null)return;
                var path=Path.Combine(AssetStore.Work,"clipboard-"+Guid.NewGuid().ToString("N")+".png");
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));using(var stream=File.Create(path))encoder.Save(stream);LoadPhoto(path);
            }
            else if(Clipboard.ContainsFileDropList()){var list=Clipboard.GetFileDropList();if(list.Count==1){if(string.Equals(Path.GetExtension(list[0]),".glb",StringComparison.OrdinalIgnoreCase))await Local(list[0]!);else LoadPhoto(list[0]!);}else throw new ArgumentException("请一次复制一张图片或一个 GLB。");}
            else throw new ArgumentException("剪贴板里没有图片。请复制图片本身，在描述框内按 Ctrl+V。");
        }catch(Exception e){Error(e);}
    }
    AssetRecord CreateRecord(string source,string description,GenerationOptions? options)
    {
        AssetStore.SetRoot(saveRoot.Text);var doc=RhinoDoc.ActiveDoc??throw new InvalidOperationException("请先打开 Rhino 文档。");
        var created=AssetStore.Create(saveRoot.Text,doc.Name??"Untitled",string.IsNullOrWhiteSpace(doc.Path)?("Unsaved-"+doc.RuntimeSerialNumber):doc.Path,source,description,options);
        created.Sizing=sizing.Capture();created.SourceName=Path.GetFileName(imagePath??"");AssetStore.Save(created);return created;
    }
    async Task Local(string path)
    {
        if(busy)return;Busy(true);raw=null;modelPreviewReady=false;rendering=false;renderValid=false;renderSequence++;
        try
        {
            record=CreateRecord("local","",null);record.SourceName=Path.GetFileName(path);File.Copy(path,record.Glb,false);task.Clear();photo.Source=null;imagePath=null;localAttachment=true;attachmentName.Text=Path.GetFileName(path);attachment.Visibility=Visibility.Visible;prompt.Clear();
            await ReadRecord();SetStatus(renderValid?"模型已载入 · 可放置":"模型已载入 · 请完成尺寸设置");
        }catch(Exception e){Error(e);}finally{Busy(false);}
    }
    async Task ReadRecord()
    {
        if(record==null)throw new InvalidOperationException("没有资产记录。");
        var current=record;SetStatus("正在读取模型与贴图");
        var loaded=await Task.Run(()=>{var a=GlbReader.Read(current.Glb);MaterialMaps.Export(a,current.Textures);return a;});
        raw=loaded;record.ActualFaces=loaded.FaceCount;record.State="ready";AssetStore.Save(record);
        savedPath.Text=record.Folder;await Render();
        if(!modelPreviewReady)throw new InvalidOperationException(string.IsNullOrWhiteSpace(info.Text)?"模型预览未完成，请重新载入。":info.Text);
        notice.Text=string.Join("\n",loaded.Warnings.Distinct())+(loaded.Warnings.Count>0?"\n":"")+"PBR 材质与贴图已保存在资产文件夹。";
    }
    async Task CheckConnection()
    {
        Busy(true);cancellation=new();
        try{using var client=new TripoClient(key.Password);await client.CheckConnection(cancellation.Token);SetStatus("连接检查通过");}
        catch(OperationCanceledException){SetStatus("已停止检查");}catch(Exception e){Error(e);}finally{cancellation.Dispose();cancellation=null;Busy(false);}
    }
    async Task RunGeneration(bool existing)
    {
        if(busy)return;
        Busy(true);cancellation=new();var ct=cancellation.Token;bool succeeded=false,preflight=!existing;
        changeTimer.Stop();sizeRequest?.Cancel();renderSequence++;
        var originatingDoc=RhinoDoc.ActiveDoc?.RuntimeSerialNumber;
        try
        {
            if(!existing)
            {
                var doc=RhinoDoc.ActiveDoc??throw new InvalidOperationException("请打开 Rhino 文档。");
                SizeEstimate? estimate=null;
                if(sizing.Mode!=SizeMode.Manual){SetStatus("正在估算尺寸…");sizing.ShowPending();estimate=await sizeInference.InferAsync(prompt.Text,imagePath,ct);}
                ct.ThrowIfCancellationRequested();
                if(RhinoDoc.ActiveDoc?.RuntimeSerialNumber!=doc.RuntimeSerialNumber)throw new InvalidOperationException("当前文档已切换，请确认后重新点击生成。");
                sizing.SetEstimate(estimate);sizing.Resolve(null,prompt.Text,Path.GetFileName(imagePath??""),doc);
            }
            preflight=false;
            if(existing&&record!=null&&File.Exists(record.Glb)&&(string.IsNullOrWhiteSpace(task.Text)||task.Text.Trim()==record.ActiveTaskId)){await ReadRecord();succeeded=true;}
            else
            {
                using var client=new TripoClient(key.Password);
                if(existing)
                {
                    var id=task.Text.Trim();
                    if(id==GenerationWorkflow.Uncertain||string.IsNullOrEmpty(id))throw new ArgumentException("请输入该阶段的原任务编号。提交结果不确定时，请在 Tripo 控制台查到编号后继续。");
                    if(record==null)record=CreateRecord("recovered","",null);
                    if(record.Source=="image-text"&&string.IsNullOrEmpty(record.TaskId))record.ImageTaskId=id;
                    else record.TaskId=id;
                    AssetStore.Save(record);
                }
                else
                {
                    if(!string.IsNullOrWhiteSpace(task.Text))throw new InvalidOperationException("已有任务记录。可继续查询原任务；如需再次生成，请先点连接设置中的“新任务”或添加新图片。");
                    var options=Options();string description=prompt.Text.Trim();var source=GenerationWorkflow.Route(imagePath!=null,description);
                    options.Payload(source=="text"?description:"validate",source=="text");
                    raw=null;modelPreviewReady=false;renderValid=false;rendering=false;renderSequence++;viewport.ClearModel();record=CreateRecord(source,description,options);
                    if(imagePath!=null){record.ReferencePath=Path.Combine(record.Folder,"reference"+Path.GetExtension(imagePath));File.Copy(imagePath,record.ReferencePath);AssetStore.Save(record);}
                }
                await GenerationWorkflow.Run(client,record!,r=>{AssetStore.Save(r);task.Text=r.ActiveTaskId;savedPath.Text=r.Folder;},SetStatus,ct);
                await ReadRecord();succeeded=true;
            }
            SetStatus("模型已就绪");
        }
        catch(OperationCanceledException){SetStatus("已停止等待");info.Text=preflight?"尺寸估算已停止，尚未提交生成任务。":"远端任务可能继续。已保留各阶段任务编号，可继续查询。";}
        catch(Exception e){if(preflight){EnsureExpanded();sizing.ShowError(e.Message);sizing.BringIntoView();SetStatus("请完善尺寸描述");}else Error(e);}
        finally{cancellation.Dispose();cancellation=null;Busy(false);}
        if(succeeded&&renderValid&&autoPlace.IsChecked==true&&IsVisible)
        {
            if(originatingDoc!=RhinoDoc.ActiveDoc?.RuntimeSerialNumber){info.Text="当前 Rhino 文档已切换，请确认目标文档后手动放置。";return;}
            await Dispatcher.InvokeAsync(Place,DispatcherPriority.Background);
        }
    }
    string SizingText=>raw!=null&&record!=null&&record.Source!="local"?record.Prompt:prompt.Text;
    string? SizingImage=>raw!=null&&record!=null?
        (record.ImageComplete&&File.Exists(Path.Combine(record.Folder,"processed-reference.png"))?Path.Combine(record.Folder,"processed-reference.png"):record.ReferencePath):imagePath;
    string SizingFile=>raw!=null&&record!=null?record.SourceName:Path.GetFileName(imagePath??"");
    void SelectSizeReference()
    {
        if(busy)return;
        try
        {
            var doc=RhinoDoc.ActiveDoc??throw new InvalidOperationException("请打开 Rhino 文档。");
            Hide();var picked=RhinoSizing.Pick(doc);if(picked!=null)sizing.SetReference(picked);
        }
        catch(Exception e){sizing.SetReference(null);sizing.ShowError(e.Message);}
        finally{Show();Activate();QueueRender();}
    }
    void QueueRender()
    {
        if(!ready)return;sizeRequest?.Cancel();sizing.SetEstimate(null);changeTimer.Stop();renderSequence++;renderValid=false;Buttons();changeTimer.Start();
    }
    async Task Render()
    {
        changeTimer.Stop();Context();var doc=RhinoDoc.ActiveDoc;
        sizeRequest?.Cancel();var request=new CancellationTokenSource();sizeRequest=request;
        int sequence=++renderSequence;var source=raw;var current=record;string description=SizingText;string? image=SizingImage;
        rendering=true;renderValid=false;modelPreviewReady=false;Buttons();
        try
        {
            if(doc==null)throw new InvalidOperationException("请打开 Rhino 文档。");
            if(source!=null)
            {
                if(current==null)throw new InvalidOperationException("没有模型文件。");
                await viewport.ShowModel(current.Glb);if(sequence!=renderSequence)return;modelPreviewReady=true;
            }
            SizeEstimate? estimate=null;
            if(sizing.Mode!=SizeMode.Manual)
            {
                if(string.IsNullOrWhiteSpace(description)&&string.IsNullOrWhiteSpace(image)){sizing.SetEstimate(null);sizing.ShowHint("添加图片或描述目标物体后，会自动估算尺寸。");return;}
                sizing.ShowPending();estimate=await sizeInference.InferAsync(description,image,request.Token);
            }
            if(sequence!=renderSequence||RhinoDoc.ActiveDoc?.RuntimeSerialNumber!=doc.RuntimeSerialNumber)return;
            sizing.SetEstimate(estimate);
            var decision=sizing.Resolve(source,description,SizingFile,doc);
            if(source==null)return;
            double documentMeters=RhinoPlacement.Meters(doc);
            var result=await Task.Run(()=>{var prepared=source.Prepare(decision.Axis,decision.TargetMeters,1,documentMeters,100,0,0);return (bounds:prepared.Bounds(),prepared.FaceCount);});
            if(sequence!=renderSequence||RhinoDoc.ActiveDoc?.RuntimeSerialNumber!=doc.RuntimeSerialNumber)return;
            var span=result.bounds.Max-result.bounds.Min;
            current!.Sizing=sizing.Capture();current.AppliedSize=decision;current.InferredSize=estimate;
            if(current.Source=="local")current.Prompt=description;AssetStore.Save(current);
            renderValid=true;if(!busy)SetStatus("模型已就绪");info.Text=$"{span.X:0.###} × {span.Y:0.###} × {span.Z:0.###} {doc.ModelUnitSystem} · {result.FaceCount:N0} 三角面";
        }
        catch(OperationCanceledException){}
        catch(Exception e)
        {
            if(sequence==renderSequence)
            {
                if(source==null||modelPreviewReady){sizing.ShowError(e.Message);if(source!=null)info.Text="模型已载入，请完善尺寸描述后放置。";}
                else{info.Text=e.Message;viewport.ReportStatus(e.Message,true);}
                place.IsEnabled=false;
            }
        }
        finally{if(sizeRequest==request)sizeRequest=null;request.Dispose();if(sequence==renderSequence){rendering=false;Buttons();}}
    }
    void Place()
    {
        if(busy||raw==null||record==null||!renderValid)return;
        try
        {
            var doc=RhinoDoc.ActiveDoc??throw new InvalidOperationException("请打开 Rhino 文档。");
            var decision=sizing.Resolve(raw,SizingText,SizingFile,doc);
            var prepared=raw.Prepare(decision.Axis,decision.TargetMeters,1,RhinoPlacement.Meters(doc),100,0,0);
            record.Sizing=sizing.Capture();record.AppliedSize=decision;AssetStore.Save(record);
            Hide();
            try{var ok=RhinoPlacement.Place(doc,prepared,record.Textures,record.TaskId);SetStatus(ok?"已放入场景 · Ctrl+Z 可撤销":"已取消放置");if(ok){record.State="placed";AssetStore.Save(record);doc.Strings.SetString("AssetCopilot",Path.GetFileName(record.Folder),record.Folder);}}
            finally{Show();Activate();}
        }catch(Exception e){if(!IsVisible)Show();Error(e);}
    }
}

