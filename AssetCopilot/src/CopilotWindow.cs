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
    readonly CheckBox autoPlace=new(){Content="Place after generation (pick a point in Rhino)",IsChecked=true,Margin=new Thickness(0,8,0,4)};
    PbrPreview viewport=new();
    readonly Border composerFrame;
    readonly SizingPanel sizing;
    readonly LocalSizeInference sizeInference=new();
    CancellationTokenSource? sizeRequest;
    readonly Border attachment=new(){Visibility=Visibility.Collapsed};
    readonly TextBlock attachmentName=new(),inputHint=new(),placeholder=new(){Text="Describe a model, or paste an image…",Foreground=Brushes.Gray,IsHitTestVisible=false,Margin=new Thickness(9)};
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
        Title="Remy Asset Copilot 0.5.4";Width=540;Height=Math.Min(910,SystemParameters.WorkArea.Height-40);MinWidth=450;MinHeight=620;
        WindowStartupLocation=WindowStartupLocation.CenterScreen;Background=new SolidColorBrush(Color.FromRgb(245,245,247));FontFamily=new FontFamily("Segoe UI");FontSize=12;
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
        var attachmentRow=new DockPanel();var remove=EditButton("×",RemoveAttachment);remove.ToolTip="Remove attachment";DockPanel.SetDock(remove,Dock.Right);attachmentRow.Children.Add(remove);
        photo.Width=52;photo.Height=52;attachmentRow.Children.Add(photo);attachmentName.Margin=new Thickness(9,0,0,0);attachmentName.VerticalAlignment=VerticalAlignment.Center;attachmentName.TextTrimming=TextTrimming.CharacterEllipsis;attachmentRow.Children.Add(attachmentName);
        attachment.Child=attachmentRow;attachment.Background=new SolidColorBrush(Color.FromRgb(247,247,249));attachment.CornerRadius=new CornerRadius(10);attachment.Padding=new Thickness(5);composer.Children.Add(attachment);
        var textArea=new Grid();prompt.Background=Brushes.Transparent;prompt.BorderThickness=new Thickness(0);prompt.Padding=new Thickness(9);prompt.MinHeight=58;prompt.ToolTip="Use text, an image, or both. Press Ctrl+V to paste an image.";textArea.Children.Add(prompt);textArea.Children.Add(placeholder);placeholder.Foreground=UiTheme.Muted;AutomationProperties.SetName(prompt,"Describe a model or paste an image");composer.Children.Add(textArea);
        var toolbar=new Grid{Margin=new Thickness(4,8,4,0)};
        toolbar.ColumnDefinitions.Add(new(){Width=GridLength.Auto});toolbar.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});toolbar.ColumnDefinitions.Add(new(){Width=GridLength.Auto});toolbar.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        var plus=EditButton("",()=>{});UiTheme.IconButton(plus,"plus","Add an image or GLB");
        var menu=new ContextMenu();var addImage=new MenuItem{Header="Add image…"};addImage.Click+=(_,_)=>ChoosePhoto();menu.Items.Add(addImage);
        var addGlb=new MenuItem{Header="Add GLB model…"};addGlb.Click+=async(_,_)=>{var d=new OpenFileDialog{Filter="GLB model|*.glb",InitialDirectory=AssetStore.SaveRoot()};if(d.ShowDialog(this)==true)await Local(d.FileName);};menu.Items.Add(addGlb);
        plus.Click+=(_,_)=>{menu.PlacementTarget=plus;menu.IsOpen=true;};toolbar.Children.Add(plus);
        ActionButton(generate,"",async()=>await RunGeneration(false));UiTheme.IconButton(generate,"send","Generate model");Grid.SetColumn(generate,3);toolbar.Children.Add(generate);
        model.Style=UiTheme.ModelPicker;model.Width=104;model.Margin=new Thickness(0,0,8,0);model.VerticalAlignment=VerticalAlignment.Center;model.HorizontalAlignment=HorizontalAlignment.Right;AutomationProperties.SetName(model,"Generation model");Grid.SetColumn(model,2);toolbar.Children.Add(model);
        inputHint.FontSize=11;inputHint.Foreground=UiTheme.Muted;inputHint.TextWrapping=TextWrapping.Wrap;inputHint.Margin=new Thickness(8,6,8,0);composer.Children.Add(toolbar);composer.Children.Add(inputHint);
        inputBox.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.V&&Keyboard.Modifiers.HasFlag(ModifierKeys.Control)&&!busy&&(Clipboard.ContainsImage()||Clipboard.ContainsFileDropList())){PastePhoto();e.Handled=true;}};
        inputBox.AllowDrop=true;inputBox.PreviewDragOver+=(_,e)=>{e.Effects=!busy&&e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;};
        inputBox.Drop+=async(_,e)=>{if(busy)return;try{if(e.Data.GetData(DataFormats.FileDrop) is string[] paths&&paths.Length==1){if(Path.GetExtension(paths[0]).Equals(".glb",StringComparison.OrdinalIgnoreCase))await Local(paths[0]);else LoadPhoto(paths[0]);}else throw new ArgumentException("Add one image or GLB at a time.");}catch(Exception ex){Error(ex);}e.Handled=true;};
        preview.Children.Add(viewport);InstallPreviewStartup();
        ConnectPreview();
        preview.HorizontalAlignment=HorizontalAlignment.Stretch;body.SizeChanged+=(_,_)=>{preview.Width=body.ActualWidth;preview.Height=Math.Min(360,body.ActualWidth);};
        preview.Margin=new Thickness(0,0,0,16);body.Children.Add(preview);
        body.Children.Add(inputBox);
        body.Children.Add(Label("Mesh detail · GLB"));
        model.Items.Add("P2.0 · Fast");model.Items.Add("V3.1 · Quality");model.SelectedIndex=0;
        var faceRow=new DockPanel();faces.Padding=new Thickness(6);DockPanel.SetDock(faces,Dock.Right);faceRow.Children.Add(faces);faceSlider.VerticalAlignment=VerticalAlignment.Center;faceSlider.Margin=new Thickness(0,4,12,4);faceRow.Children.Add(faceSlider);body.Children.Add(faceRow);
        faceRange.Foreground=Brushes.Gray;faceRange.FontSize=10;body.Children.Add(faceRange);UpdateFaceRange();
        model.SelectionChanged+=(_,_)=>UpdateFaceRange();
        faceSlider.ValueChanged+=(_,_)=>{if(syncFaces)return;syncFaces=true;faces.Text=((int)faceSlider.Value).ToString();syncFaces=false;};
        faces.TextChanged+=(_,_)=>{if(syncFaces)return;if(int.TryParse(faces.Text,out int n)&&n>=500&&n<=faceSlider.Maximum){syncFaces=true;faceSlider.Value=n;syncFaces=false;}};
        body.Children.Add(Label("Size & scale"));
        sizing=new SizingPanel(axis,dimension,units,percentage);body.Children.Add(sizing);
        sizing.Changed+=QueueRender;sizing.ReferenceRequested+=SelectSizeReference;
        document.Foreground=Brushes.Gray;document.Margin=new Thickness(0,8,0,3);document.TextWrapping=TextWrapping.Wrap;body.Children.Add(document);
        info.TextWrapping=TextWrapping.Wrap;info.Margin=new Thickness(0,3,0,4);body.Children.Add(info);
        notice.TextWrapping=TextWrapping.Wrap;notice.FontSize=10;notice.Foreground=Brushes.DarkGoldenrod;body.Children.Add(notice);footer.Children.Add(autoPlace);
        ActionButton(place,"Place in Rhino",Place);footer.Children.Add(place);
        var storage=new StackPanel();storage.Children.Add(Label("Save folder · applies to new tasks"));
        saveRoot.Text=AssetStore.SaveRoot();saveRoot.Padding=new Thickness(6);storage.Children.Add(saveRoot);
        var storageActions=new WrapPanel();
        storageActions.Children.Add(EditButton("Browse…",()=>{var folder=FolderPicker.Pick(this,AssetStore.SaveRoot());if(folder!=null){saveRoot.Text=folder;SaveDirectory();}}));
        storageActions.Children.Add(EditButton("Save path",SaveDirectory));
        storageActions.Children.Add(Button("Open asset folder",()=>OpenFolder(record?.Folder??AssetStore.SaveRoot())));
        storage.Children.Add(storageActions);savedPath.TextWrapping=TextWrapping.Wrap;savedPath.Foreground=Brushes.Gray;savedPath.FontSize=10;storage.Children.Add(savedPath);
        root.Children.Add(new Expander{Header="Model & texture storage",Content=storage,IsExpanded=true,Margin=new Thickness(7,12,7,0)});
        var settings=new StackPanel();settings.Children.Add(Label("Tripo API key · this window only"));key.Padding=new Thickness(7);settings.Children.Add(key);
        check.Content="Check connection (no generation)";check.Click+=async(_,_)=>await CheckConnection();settings.Children.Add(check);
        settings.Children.Add(new TextBlock{Text="Generating uploads your image or text and uses Tripo API credits.",FontSize=10,Foreground=Brushes.Gray,TextWrapping=TextWrapping.Wrap});
        settings.Children.Add(Label("Task ID · resume after an interruption"));task.Padding=new Thickness(6);settings.Children.Add(task);
        resume.Content="Resume / Load";resume.Click+=async(_,_)=>await RunGeneration(true);
        stop.Content="Stop waiting";stop.Click+=(_,_)=>cancellation?.Cancel();stop.IsEnabled=false;
        reset.Content="New task";reset.Click+=(_,_)=>{if(localAttachment)RemoveAttachment();record=null;raw=null;renderValid=false;modelPreviewReady=false;renderSequence++;viewport.ClearModel();task.Clear();AssetStore.ClearPointer();SetStatus("Ready for a new task");Buttons();QueueRender();};
        var recovery=new WrapPanel();recovery.Children.Add(resume);recovery.Children.Add(stop);recovery.Children.Add(reset);settings.Children.Add(recovery);
        root.Children.Add(new Expander{Header="Connection & task recovery",Content=settings,IsExpanded=false,Margin=new Thickness(7,12,7,0)});
        root.Children.Add(new TextBlock{Text="The preview uses PBR materials. Lighting and reflections may differ in your scene.\nPlaced assets use AssetCopilot sublayers. Press Ctrl+Z to undo.",Foreground=Brushes.Gray,FontSize=10,Margin=new Thickness(8,12,8,0),TextWrapping=TextWrapping.Wrap});
        ConfigureWindowModes(layout,detailScroll,body,header);
        editing.AddRange(new Control[]{prompt,model,faces,faceSlider,axis,dimension,units,percentage,saveRoot,key,autoPlace,reset});
        prompt.TextChanged+=(_,_)=>{Buttons();QueueRender();};
        dimension.TextChanged+=(_,_)=>QueueRender();percentage.TextChanged+=(_,_)=>QueueRender();axis.SelectionChanged+=(_,_)=>QueueRender();units.SelectionChanged+=(_,_)=>QueueRender();
        changeTimer.Tick+=async(_,_)=>{changeTimer.Stop();await Render();};

        Activated+=(_,_)=>{Context();if(!busy)QueueRender();};Closing+=(_,_)=>cancellation?.Cancel();Closed+=(_,_)=>{sizeRequest?.Cancel();sizeInference.Dispose();changeTimer.Stop();key.Clear();renderSequence++;logo.Dispose();startupPlayer.Close();viewport.ClosePreview();};
        record=AssetStore.Restore();if(record!=null){task.Text=record.ActiveTaskId;savedPath.Text=record.Folder;sizing.Restore(record.Sizing);}
        ready=true;Context();Buttons();QueueRender();
        if(sample!=null)Loaded+=async(_,_)=>await Local(sample);
    }
    static TextBlock Label(string text)=>new(){Text=text,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,9,0,5)};
    Button Button(string text,Action action){var b=new Button{Content=text};b.Click+=(_,_)=>{try{action();}catch(Exception e){Error(e);}};return b;}
    Button EditButton(string text,Action action){var b=Button(text,action);editing.Add(b);return b;}
    void ActionButton(Button b,string text,Action action){b.Content=text;b.Background=new SolidColorBrush(Color.FromRgb(29,29,31));b.Foreground=Brushes.White;b.Margin=new Thickness(0,5,0,0);b.Padding=new Thickness(12);b.Click+=(_,_)=>action();}
    void SetStatus(string text){viewport.ReportStatus(text,text.StartsWith("Stopped",StringComparison.Ordinal));CompactMessage(text);}
    void Error(Exception e){if(!IsVisible)return;CompactMessage(e.Message);viewport.ReportStatus(e.Message,true);info.Text=e.Message;MessageBox.Show(this,e.Message,"Asset Copilot",MessageBoxButton.OK,MessageBoxImage.Information);}
    void Context(){var doc=RhinoDoc.ActiveDoc;document.Text=doc==null?"Open a Rhino document":$"Document: {doc.ModelUnitSystem} · {doc.Name ?? "Untitled"}";}
    void Buttons()
    {
        if(!ready)return;
        bool local=localAttachment;
        generate.IsEnabled=!busy&&!local&&(imagePath!=null||!string.IsNullOrWhiteSpace(prompt.Text));
        placeholder.Visibility=string.IsNullOrEmpty(prompt.Text)?Visibility.Visible:Visibility.Collapsed;
        inputHint.Text="Image editing + model generation · two billable steps";
        inputHint.Visibility=!local&&imagePath!=null&&!string.IsNullOrWhiteSpace(prompt.Text)?Visibility.Visible:Visibility.Collapsed;
        place.IsEnabled=!busy&&!rendering&&renderValid&&raw!=null;resume.IsEnabled=!busy;check.IsEnabled=!busy;stop.IsEnabled=busy;RefreshCompactFeedback();
    }
    void RemoveAttachment()
    {
        localAttachment=false;photo.Source=null;attachment.Visibility=Visibility.Collapsed;imagePath=null;raw=null;modelPreviewReady=false;record=null;task.Clear();renderValid=false;renderSequence++;viewport.ClearModel();info.Text="";notice.Text="";AssetStore.ClearPointer();Buttons();QueueRender();
    }
    void Busy(bool value){busy=value;if(value)CompactMessage("Preparing…");viewport.SetBusy(value);sizing.IsEnabled=!value;foreach(var c in editing)c.IsEnabled=!value;Buttons();}
    void UpdateFaceRange()
    {
        if(model.SelectedIndex<0)return;
        syncFaces=true;faceSlider.Maximum=model.SelectedIndex==0?25000:2000000;
        if(!int.TryParse(faces.Text,out int n))n=5000;
        n=Compat.Clamp(n,500,(int)faceSlider.Maximum);faceSlider.Value=n;faces.Text=n.ToString();
        faceRange.Text=$"Target triangles: 500–{faceSlider.Maximum:N0} · GLB";syncFaces=false;
    }
    GenerationOptions Options(){if(!int.TryParse(faces.Text,out int n))throw new ArgumentException("Triangle count must be a whole number.");var o=new GenerationOptions(model.SelectedIndex==0?GenerationOptions.P2:GenerationOptions.V31,n);o.Validate();return o;}
    void SaveDirectory(){try{AssetStore.SetRoot(saveRoot.Text);saveRoot.Text=AssetStore.SaveRoot();SetStatus("Save folder updated");}catch(Exception e){Error(e);}}
    void OpenFolder(string path){Directory.CreateDirectory(path);var start=new ProcessStartInfo("explorer.exe"){UseShellExecute=false,CreateNoWindow=true};Compat.SetArguments(start,path);Process.Start(start);}
    void RequestPhoto()
    {
        // Defer native dialogs until the WebView2 message callback has returned.
        if(!busy)Dispatcher.BeginInvoke(new Action(ChoosePhoto));
    }
    void ChoosePhoto()
    {
        if(busy||!IsVisible)return;
        var dialog=new OpenFileDialog{Filter="Images|*.png;*.jpg;*.jpeg",Title="Add reference image"};
        if(dialog.ShowDialog(this)==true)LoadPhoto(dialog.FileName);
    }
    void LoadPhoto(string path)
    {
        try
        {
            var file=new FileInfo(path);if(!file.Exists||file.Length==0||file.Length>20*1024*1024||file.Extension.ToLowerInvariant() is not(".jpg" or ".jpeg" or ".png"))throw new ArgumentException("Choose a JPG or PNG image of up to 20 MB.");
            using var stream=File.OpenRead(path);var bmp=BitmapFrame.Create(stream,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);bmp.Freeze();
            localAttachment=false;imagePath=path;photo.Source=bmp;attachmentName.Text=Path.GetFileName(path);attachment.Visibility=Visibility.Visible;record=null;task.Clear();AssetStore.ClearPointer();raw=null;modelPreviewReady=false;rendering=false;renderValid=false;renderSequence++;viewport.ClearModel();notice.Text="";info.Text=Path.GetFileName(path);SetStatus("Image ready");Buttons();QueueRender();
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
            else if(Clipboard.ContainsFileDropList()){var list=Clipboard.GetFileDropList();if(list.Count==1){if(string.Equals(Path.GetExtension(list[0]),".glb",StringComparison.OrdinalIgnoreCase))await Local(list[0]!);else LoadPhoto(list[0]!);}else throw new ArgumentException("Copy one image or GLB at a time.");}
            else throw new ArgumentException("No image was found on the clipboard. Copy the image itself, then press Ctrl+V in the input box.");
        }catch(Exception e){Error(e);}
    }
    AssetRecord CreateRecord(string source,string description,GenerationOptions? options)
    {
        AssetStore.SetRoot(saveRoot.Text);var doc=RhinoDoc.ActiveDoc??throw new InvalidOperationException("Open a Rhino document first.");
        var created=AssetStore.Create(saveRoot.Text,doc.Name??"Untitled",string.IsNullOrWhiteSpace(doc.Path)?("Unsaved-"+doc.RuntimeSerialNumber):doc.Path,source,description,options);
        created.Sizing=sizing.Capture();created.SourceName=Path.GetFileName(imagePath??"");AssetStore.Save(created);return created;
    }
    async Task Local(string path)
    {
        if(busy)return;Busy(true);raw=null;modelPreviewReady=false;rendering=false;renderValid=false;renderSequence++;
        try
        {
            record=CreateRecord("local","",null);record.SourceName=Path.GetFileName(path);File.Copy(path,record.Glb,false);task.Clear();photo.Source=null;imagePath=null;localAttachment=true;attachmentName.Text=Path.GetFileName(path);attachment.Visibility=Visibility.Visible;prompt.Clear();
            await ReadRecord();SetStatus(renderValid?"Model loaded · ready to place":"Model loaded · complete the size settings");
        }catch(Exception e){Error(e);}finally{Busy(false);}
    }
    async Task ReadRecord()
    {
        if(record==null)throw new InvalidOperationException("No asset record is available.");
        var current=record;SetStatus("Reading model and textures");
        var loaded=await Task.Run(()=>{var a=GlbReader.Read(current.Glb);MaterialMaps.Export(a,current.Textures);return a;});
        raw=loaded;record.ActualFaces=loaded.FaceCount;record.State="ready";AssetStore.Save(record);
        savedPath.Text=record.Folder;await Render();
        if(!modelPreviewReady)throw new InvalidOperationException(string.IsNullOrWhiteSpace(info.Text)?"The model preview did not finish loading. Load the model again.":info.Text);
        notice.Text=string.Join("\n",loaded.Warnings.Distinct())+(loaded.Warnings.Count>0?"\n":"")+"PBR materials and textures are saved in the asset folder.";
    }
    async Task CheckConnection()
    {
        Busy(true);cancellation=new();
        try{using var client=new TripoClient(key.Password);await client.CheckConnection(cancellation.Token);SetStatus("Connection check passed");}
        catch(OperationCanceledException){SetStatus("Stopped checking");}catch(Exception e){Error(e);}finally{cancellation.Dispose();cancellation=null;Busy(false);}
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
                var doc=RhinoDoc.ActiveDoc??throw new InvalidOperationException("Open a Rhino document first.");
                SizeEstimate? estimate=null;
                if(sizing.Mode!=SizeMode.Manual){SetStatus("Estimating size…");sizing.ShowPending();estimate=await sizeInference.InferAsync(prompt.Text,imagePath,ct);}
                ct.ThrowIfCancellationRequested();
                if(RhinoDoc.ActiveDoc?.RuntimeSerialNumber!=doc.RuntimeSerialNumber)throw new InvalidOperationException("The active document changed. Confirm the document, then generate again.");
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
                    if(id==GenerationWorkflow.Uncertain||string.IsNullOrEmpty(id))throw new ArgumentException("Enter the original task ID for this stage. If submission is unconfirmed, find the ID in the Tripo dashboard before continuing.");
                    if(record==null)record=CreateRecord("recovered","",null);
                    if(record.Source=="image-text"&&string.IsNullOrEmpty(record.TaskId))record.ImageTaskId=id;
                    else record.TaskId=id;
                    AssetStore.Save(record);
                }
                else
                {
                    if(!string.IsNullOrWhiteSpace(task.Text))throw new InvalidOperationException("A task already exists. Resume checking it, or choose New task in Connection & task recovery (or add a new image) before generating again.");
                    var options=Options();string description=prompt.Text.Trim();var source=GenerationWorkflow.Route(imagePath!=null,description);
                    options.Payload(source=="text"?description:"validate",source=="text");
                    raw=null;modelPreviewReady=false;renderValid=false;rendering=false;renderSequence++;viewport.ClearModel();record=CreateRecord(source,description,options);
                    if(imagePath!=null){record.ReferencePath=Path.Combine(record.Folder,"reference"+Path.GetExtension(imagePath));File.Copy(imagePath,record.ReferencePath);AssetStore.Save(record);}
                }
                await GenerationWorkflow.Run(client,record!,r=>{AssetStore.Save(r);task.Text=r.ActiveTaskId;savedPath.Text=r.Folder;},SetStatus,ct);
                await ReadRecord();succeeded=true;
            }
            SetStatus("Model ready");
        }
        catch(OperationCanceledException){SetStatus("Stopped waiting");info.Text=preflight?"Size estimation stopped. No generation task was submitted.":"The remote task may continue. Task IDs for each stage are saved so you can resume checking.";}
        catch(Exception e){if(preflight){EnsureExpanded();sizing.ShowError(e.Message);sizing.BringIntoView();SetStatus("Complete the size description");}else Error(e);}
        finally{cancellation.Dispose();cancellation=null;Busy(false);}
        if(succeeded&&renderValid&&autoPlace.IsChecked==true&&IsVisible)
        {
            if(originatingDoc!=RhinoDoc.ActiveDoc?.RuntimeSerialNumber){info.Text="The active Rhino document changed. Confirm the target document, then place the model manually.";return;}
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
            var doc=RhinoDoc.ActiveDoc??throw new InvalidOperationException("Open a Rhino document first.");
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
            if(doc==null)throw new InvalidOperationException("Open a Rhino document first.");
            if(source!=null)
            {
                if(current==null)throw new InvalidOperationException("No model file is available.");
                await viewport.ShowModel(current.Glb);if(sequence!=renderSequence)return;modelPreviewReady=true;
            }
            SizeEstimate? estimate=null;
            if(sizing.Mode!=SizeMode.Manual)
            {
                if(string.IsNullOrWhiteSpace(description)&&string.IsNullOrWhiteSpace(image)){sizing.SetEstimate(null);sizing.ShowHint("Add an image or describe the target object to estimate its size.");return;}
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
            renderValid=true;if(!busy)SetStatus("Model ready");info.Text=$"{span.X:0.###} × {span.Y:0.###} × {span.Z:0.###} {doc.ModelUnitSystem} · {result.FaceCount:N0} triangles";
        }
        catch(OperationCanceledException){}
        catch(Exception e)
        {
            if(sequence==renderSequence)
            {
                if(source==null||modelPreviewReady){sizing.ShowError(e.Message);if(source!=null)info.Text="Model loaded. Complete the size description before placing it.";}
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
            var doc=RhinoDoc.ActiveDoc??throw new InvalidOperationException("Open a Rhino document first.");
            var decision=sizing.Resolve(raw,SizingText,SizingFile,doc);
            var prepared=raw.Prepare(decision.Axis,decision.TargetMeters,1,RhinoPlacement.Meters(doc),100,0,0);
            record.Sizing=sizing.Capture();record.AppliedSize=decision;AssetStore.Save(record);
            Hide();
            try{var ok=RhinoPlacement.Place(doc,prepared,record.Textures,record.TaskId);SetStatus(ok?"Placed in scene · Ctrl+Z to undo":"Placement cancelled");if(ok){record.State="placed";AssetStore.Save(record);doc.Strings.SetString("AssetCopilot",Path.GetFileName(record.Folder),record.Folder);}}
            finally{Show();Activate();}
        }catch(Exception e){if(!IsVisible)Show();Error(e);}
    }
}

