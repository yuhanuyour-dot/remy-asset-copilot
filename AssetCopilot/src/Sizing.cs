using System.Text.RegularExpressions;
namespace AssetCopilot;

public enum SizeMode { Manual, RealWorld, ReferenceFace }
public sealed class FaceReference
{
    public string Session {get;set;}="";
    public uint DocumentSerial {get;set;}
    public Guid ObjectId {get;set;}
    public int FaceIndex {get;set;}
    public int FaceCount {get;set;}
    public string GeometryKind {get;set;}="Brep";
    public double ShortMeters {get;set;}
    public double LongMeters {get;set;}
    public double AreaSquareMeters {get;set;}
}
public sealed class SizingSettings
{
    public SizeMode Mode {get;set;}=SizeMode.Manual;
    // Retained only for deserializing 0.4.0 records; automatic inference ignores this field.
    public string Category {get;set;}="auto";
    public int ManualAxis {get;set;}
    public double ManualSize {get;set;}=600;
    public int ManualUnit {get;set;}
    public float Percent {get;set;}=100;
    public FaceReference? Reference {get;set;}
}
public sealed class SizingDecision
{
    public SizeMode Mode {get;set;}
    public string Category {get;set;}="";
    public string Basis {get;set;}="";
    public int Axis {get;set;}
    public double TargetMeters {get;set;}
    public double[]? PhysicalMeters {get;set;}
    public string DocumentUnit {get;set;}="";
}
// Product starter presets, not survey averages or a measurement of the photographed object.
public sealed record SizePreset(string Id,string Label,bool Height,double Typical,double Min,double Max,double RoomFraction,bool LongReference,string[] Aliases);
public static class Sizing
{
    public static readonly double[] UnitMeters=[.001,.01,1,.0254,.3048];
    public static readonly IReadOnlyList<SizePreset> Presets=new SizePreset[]{
        new("chair","椅子",true,.85,.70,1.10,.20,false,["椅子","单椅","餐椅","办公椅","椅","chair","chairs"]),
        new("armchair","扶手椅 / 休闲椅",false,.85,.65,1.15,.22,false,["扶手椅","休闲椅","沙发椅","armchair","lounge chair"]),
        new("stool","凳子",true,.45,.30,.65,.12,false,["凳子","圆凳","stool"]),
        new("sofa","沙发",false,2.10,1.40,3.20,.40,true,["沙发","sofa","couch"]),
        new("dining","餐桌",false,1.60,1.10,2.40,.32,true,["餐桌","dining table"]),
        new("coffee","茶几",false,.90,.55,1.40,.25,false,["茶几","咖啡桌","coffee table"]),
        new("desk","书桌 / 办公桌",false,1.20,.90,1.80,.28,true,["书桌","办公桌","desk"]),
        new("bed","床",false,2.10,1.90,2.30,.50,true,["床","双人床","单人床","bed","bed frame"]),
        new("cabinet","矮柜 / 床头柜",true,.70,.40,1.10,.20,false,["矮柜","床头柜","sideboard","nightstand"]),
        new("wardrobe","衣柜",true,2.00,1.80,2.40,.50,false,["衣柜","wardrobe"]),
        new("plant","落地绿植",true,1.20,.60,2.00,.30,false,["落地绿植","室内树","大盆栽","indoor tree","floor plant"]),
        new("smallplant","小盆栽",true,.35,.15,.80,.10,false,["盆栽","绿植","小盆栽","potted plant","plant"]),
        new("floorlamp","落地灯",true,1.60,1.20,2.00,.40,false,["落地灯","floor lamp"]),
        new("tablelamp","台灯",true,.45,.25,.75,.12,false,["台灯","table lamp","desk lamp"]),
        new("vase","花瓶",true,.30,.12,.80,.10,false,["花瓶","vase"]),
        new("bench","长凳",false,1.20,.80,2.00,.30,true,["长凳","长椅","bench"]),
        new("shelf","书架",true,1.80,.80,2.40,.45,false,["书架","置物架","bookshelf","shelving unit"]),
        new("rug","地毯",false,2.00,.60,3.50,.55,true,["地毯","rug","carpet"]),
        new("mug","杯子",true,.10,.06,.18,.03,false,["杯子","马克杯","茶杯","咖啡杯","mug","cup"]),
        new("bottle","瓶子",true,.25,.10,.40,.07,false,["瓶子","水瓶","酒瓶","bottle"]),
        new("book","书本",false,.24,.10,.40,.06,false,["书本","书籍","book"]),
        new("door","门",true,2.10,1.80,2.50,.50,false,["房门","木门","门扇","door"]),
        new("sink","洗手盆",false,.60,.35,1.20,.15,false,["洗手盆","洗脸盆","洗手池","sink","washbasin"]),
        new("toilet","马桶",true,.75,.60,.90,.20,false,["马桶","坐便器","toilet"]),
        new("bathtub","浴缸",false,1.70,1.30,2.20,.40,true,["浴缸","bathtub"]),
        new("bicycle","自行车",false,1.75,1.20,2.10,.40,true,["自行车","单车","bicycle","bike"]),
        new("car","汽车",false,4.50,3.00,5.50,.70,true,["汽车","轿车","car","automobile"]),
        new("laptop","笔记本电脑",false,.34,.25,.43,.09,false,["笔记本电脑","laptop"]),
        new("monitor","显示器",false,.60,.35,1.00,.16,false,["显示器","电脑屏幕","computer monitor"]),
        new("fridge","冰箱",true,1.75,.80,2.10,.45,false,["冰箱","refrigerator","fridge"]),
        new("pendant","吊灯",false,.50,.20,1.20,.13,false,["吊灯","pendant light","chandelier"]),
        new("pillow","靠垫",false,.45,.25,.80,.12,false,["靠垫","抱枕","枕头","pillow","cushion"]),
        new("clock","挂钟",false,.30,.15,.70,.08,false,["挂钟","时钟","wall clock"]),
        new("bin","垃圾桶",true,.45,.20,1.10,.12,false,["垃圾桶","trash can","waste bin"]),
        new("basket","篮子",false,.35,.15,.70,.10,false,["篮子","basket"])
    };
    public static string[] Matches(string text)
    {
        var spans=new List<(string Id,int Start,int End)>();
        foreach(var preset in Presets)foreach(var alias in preset.Aliases)
        {
            string pattern=Regex.Escape(alias);
            if(alias.All(c=>c<128))pattern=@"\b"+pattern+@"\b";
            foreach(Match match in Regex.Matches(text,pattern,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))spans.Add((preset.Id,match.Index,match.Index+match.Length));
        }
        // A specific phrase such as 'floor plant' wins over its contained generic word 'plant'.
        return spans.Where(a=>!spans.Any(b=>a.Id!=b.Id&&b.Start<=a.Start&&b.End>=a.End&&(b.Start<a.Start||b.End>a.End))).Select(a=>a.Id).Distinct().ToArray();
    }
    static void Positive(double value,string name){if(!Compat.IsFinite(value)||value<=0)throw new ArgumentException(name+"必须是有效正数。");}
    public static SizingDecision Resolve(SizingSettings settings,string text,string fileName,double[]? span,double documentMeters,FaceReference? reference,SizeEstimate? inferred=null)
    {
        Positive(documentMeters,"文档单位");
        if(span!=null&&(span.Length!=3||span.Any(x=>!Compat.IsFinite(x)||x<0)))throw new ArgumentException("模型没有有效的三维尺寸。");
        if(!Enum.IsDefined(typeof(SizeMode),settings.Mode))throw new ArgumentException("请选择有效尺寸方式。");
        var result=new SizingDecision{Mode=settings.Mode};
        if(settings.Mode==SizeMode.Manual)
        {
            if(settings.ManualAxis<0||settings.ManualAxis>2||settings.ManualUnit<0||settings.ManualUnit>=UnitMeters.Length)throw new ArgumentException("请选择尺寸方向与单位。");
            Positive(settings.ManualSize,"尺寸");Positive(settings.Percent,"比例");
            if(settings.ManualSize>1e9||settings.Percent>10000)throw new ArgumentException("尺寸或比例超出可用范围。");
            result.Axis=settings.ManualAxis;result.TargetMeters=settings.ManualSize*UnitMeters[settings.ManualUnit]*settings.Percent/100;
            result.Basis="用户输入尺寸 × 等比缩放";
        }
        else
        {
            inferred??=SizeInferenceRules.Text(text);
            if(inferred==null)throw new ArgumentException("请添加图片或描述目标物体，以自动估算尺寸。");
            if(inferred.Axis < -1 || inferred.Axis > 2)throw new ArgumentException("尺寸估算方向无效。");
            if(inferred.Axis>=0)Positive(inferred.Meters,"描述尺寸");
            var preset=Presets.FirstOrDefault(p=>p.Id==inferred.Category);
            if(preset==null&&inferred.Axis<0)throw new ArgumentException("暂时无法估算该物体，请补充描述或改用手动尺寸。");
            preset??=new SizePreset("specified","描述中的物体",inferred.Axis==2,inferred.Meters,.001,1000,.2,false,[]);
            string source=inferred.Source;
            result.Category=preset.Id;
            result.Axis=preset.Height?2:span!=null&&span[1]>span[0]?1:0;
            result.TargetMeters=preset.Typical;
            if(inferred?.Axis>=0){result.Axis=inferred.Axis;result.TargetMeters=inferred.Meters;}
            result.Basis=inferred?.Axis>=0?$"使用描述中的尺寸 · {inferred.Evidence}；保持模型原始比例":$"{source} · {preset.Label}；按常见{(preset.Height?"高度":"水平长边")}估算，非实测";
            if(settings.Mode==SizeMode.ReferenceFace)
            {
                if(reference==null)throw new ArgumentException("请先在 Rhino 选择一个平面作为尺寸参照。");
                Positive(reference.ShortMeters,"参考面短边");Positive(reference.LongMeters,"参考面长边");Positive(reference.AreaSquareMeters,"参考面面积");
                double edge=preset.LongReference?reference.LongMeters:reference.ShortMeters;
                if(inferred?.Axis>=0)result.TargetMeters=inferred.Meters;else result.TargetMeters=Compat.Clamp(edge*preset.RoomFraction,preset.Min,preset.Max);
                bool limited=false;
                if(span!=null)
                {
                    double factor=result.TargetMeters/span[result.Axis],longSide=Math.Max(span[0],span[1])*factor,shortSide=Math.Min(span[0],span[1])*factor;
                    double fit=Math.Min(1,Math.Min(reference.LongMeters*.8/longSide,Math.Min(reference.ShortMeters*.8/shortSide,Math.Sqrt(reference.AreaSquareMeters*.6/(longSide*shortSide)))));
                    if(fit<1){if(inferred?.Axis>=0)throw new ArgumentException("描述中的尺寸超出参考面可用范围，请调整描述或改用手动尺寸。");limited=true;result.TargetMeters*=fit;}
                    if(result.TargetMeters<preset.Min-1e-6)throw new ArgumentException("参考面过小，无法同时满足常见尺寸与空间比例；请换一个面，或切换手动尺寸。");
                }
                result.Basis=inferred?.Axis>=0?$"使用描述中的尺寸 · {inferred.Evidence}；已检查参考面可用范围。":$"{source} · {preset.Label}；参考{(preset.LongReference?"长":"短")}边 × {preset.RoomFraction:P0}，限制在 {preset.Min:0.##}–{preset.Max:0.##} m"+(limited?"；已按包围尺寸/面积缩小":"")+"。仅作比例建议，不保证贴合异形面的轮廓。";
            }
        }
        Positive(result.TargetMeters,"输出尺寸");
        if(span!=null&&span[result.Axis]<=1e-8)throw new ArgumentException("所选尺寸方向为零，请切换手动尺寸并选择其他方向。");
        if(span!=null)result.PhysicalMeters=span.Select(x=>x*result.TargetMeters/span[result.Axis]).ToArray();
        return result;
    }
    public static double[] RhinoSpan(AssetData asset){var b=asset.Bounds();var s=b.Max-b.Min;return[(double)s.X,s.Z,s.Y];}
}
