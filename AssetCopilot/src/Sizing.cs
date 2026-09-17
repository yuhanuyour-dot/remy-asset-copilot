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
        new("chair","Chair",true,.85,.70,1.10,.20,false,["椅子","单椅","餐椅","办公椅","椅","chair","chairs"]),
        new("armchair","Armchair / lounge chair",false,.85,.65,1.15,.22,false,["扶手椅","休闲椅","沙发椅","armchair","lounge chair"]),
        new("stool","Stool",true,.45,.30,.65,.12,false,["凳子","圆凳","stool"]),
        new("sofa","Sofa",false,2.10,1.40,3.20,.40,true,["沙发","sofa","couch"]),
        new("dining","Dining table",false,1.60,1.10,2.40,.32,true,["餐桌","dining table"]),
        new("coffee","Coffee table",false,.90,.55,1.40,.25,false,["茶几","咖啡桌","coffee table"]),
        new("desk","Desk",false,1.20,.90,1.80,.28,true,["书桌","办公桌","desk"]),
        new("bed","Bed",false,2.10,1.90,2.30,.50,true,["床","双人床","单人床","bed","bed frame"]),
        new("cabinet","Sideboard / nightstand",true,.70,.40,1.10,.20,false,["矮柜","床头柜","sideboard","nightstand"]),
        new("wardrobe","Wardrobe",true,2.00,1.80,2.40,.50,false,["衣柜","wardrobe"]),
        new("plant","Floor plant",true,1.20,.60,2.00,.30,false,["落地绿植","室内树","大盆栽","indoor tree","floor plant"]),
        new("smallplant","Potted plant",true,.35,.15,.80,.10,false,["盆栽","绿植","小盆栽","potted plant","plant"]),
        new("floorlamp","Floor lamp",true,1.60,1.20,2.00,.40,false,["落地灯","floor lamp"]),
        new("tablelamp","Table lamp",true,.45,.25,.75,.12,false,["台灯","table lamp","desk lamp"]),
        new("vase","Vase",true,.30,.12,.80,.10,false,["花瓶","vase"]),
        new("bench","Bench",false,1.20,.80,2.00,.30,true,["长凳","长椅","bench"]),
        new("shelf","Bookshelf",true,1.80,.80,2.40,.45,false,["书架","置物架","bookshelf","shelving unit"]),
        new("rug","Rug",false,2.00,.60,3.50,.55,true,["地毯","rug","carpet"]),
        new("mug","Cup / mug",true,.10,.06,.18,.03,false,["杯子","马克杯","茶杯","咖啡杯","mug","cup"]),
        new("bottle","Bottle",true,.25,.10,.40,.07,false,["瓶子","水瓶","酒瓶","bottle"]),
        new("book","Book",false,.24,.10,.40,.06,false,["书本","书籍","book"]),
        new("door","Door",true,2.10,1.80,2.50,.50,false,["房门","木门","门扇","door"]),
        new("sink","Sink",false,.60,.35,1.20,.15,false,["洗手盆","洗脸盆","洗手池","sink","washbasin"]),
        new("toilet","Toilet",true,.75,.60,.90,.20,false,["马桶","坐便器","toilet"]),
        new("bathtub","Bathtub",false,1.70,1.30,2.20,.40,true,["浴缸","bathtub"]),
        new("bicycle","Bicycle",false,1.75,1.20,2.10,.40,true,["自行车","单车","bicycle","bike"]),
        new("car","Car",false,4.50,3.00,5.50,.70,true,["汽车","轿车","car","automobile"]),
        new("laptop","Laptop",false,.34,.25,.43,.09,false,["笔记本电脑","laptop"]),
        new("monitor","Monitor",false,.60,.35,1.00,.16,false,["显示器","电脑屏幕","computer monitor"]),
        new("fridge","Refrigerator",true,1.75,.80,2.10,.45,false,["冰箱","refrigerator","fridge"]),
        new("pendant","Pendant light",false,.50,.20,1.20,.13,false,["吊灯","pendant light","chandelier"]),
        new("pillow","Pillow / cushion",false,.45,.25,.80,.12,false,["靠垫","抱枕","枕头","pillow","cushion"]),
        new("clock","Wall clock",false,.30,.15,.70,.08,false,["挂钟","时钟","wall clock"]),
        new("bin","Waste bin",true,.45,.20,1.10,.12,false,["垃圾桶","trash can","waste bin"]),
        new("basket","Basket",false,.35,.15,.70,.10,false,["篮子","basket"])
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
    static void Positive(double value,string name){if(!Compat.IsFinite(value)||value<=0)throw new ArgumentException(name+" must be a valid positive number.");}
    public static SizingDecision Resolve(SizingSettings settings,string text,string fileName,double[]? span,double documentMeters,FaceReference? reference,SizeEstimate? inferred=null)
    {
        Positive(documentMeters,"Document unit");
        if(span!=null&&(span.Length!=3||span.Any(x=>!Compat.IsFinite(x)||x<0)))throw new ArgumentException("The model has no valid 3D dimensions.");
        if(!Enum.IsDefined(typeof(SizeMode),settings.Mode))throw new ArgumentException("Choose a valid size mode.");
        var result=new SizingDecision{Mode=settings.Mode};
        if(settings.Mode==SizeMode.Manual)
        {
            if(settings.ManualAxis<0||settings.ManualAxis>2||settings.ManualUnit<0||settings.ManualUnit>=UnitMeters.Length)throw new ArgumentException("Choose a size axis and unit.");
            Positive(settings.ManualSize,"Size");Positive(settings.Percent,"Scale");
            if(settings.ManualSize>1e9||settings.Percent>10000)throw new ArgumentException("Size or scale is outside the supported range.");
            result.Axis=settings.ManualAxis;result.TargetMeters=settings.ManualSize*UnitMeters[settings.ManualUnit]*settings.Percent/100;
            result.Basis="User-defined size × uniform scale";
        }
        else
        {
            inferred??=SizeInferenceRules.Text(text);
            if(inferred==null)throw new ArgumentException("Add an image or describe the target to estimate its size.");
            if(inferred.Axis < -1 || inferred.Axis > 2)throw new ArgumentException("The estimated size axis is invalid.");
            if(inferred.Axis>=0)Positive(inferred.Meters,"Described size");
            var preset=Presets.FirstOrDefault(p=>p.Id==inferred.Category);
            if(preset==null&&inferred.Axis<0)throw new ArgumentException("This object's size could not be estimated. Add more detail or use Manual size.");
            preset??=new SizePreset("specified","Described object",inferred.Axis==2,inferred.Meters,.001,1000,.2,false,[]);
            string source=inferred.Source;
            result.Category=preset.Id;
            result.Axis=preset.Height?2:span!=null&&span[1]>span[0]?1:0;
            result.TargetMeters=preset.Typical;
            if(inferred?.Axis>=0){result.Axis=inferred.Axis;result.TargetMeters=inferred.Meters;}
            result.Basis=inferred?.Axis>=0?$"Dimension from description · {inferred.Evidence}; original proportions preserved":$"{source} · {preset.Label}; estimated from typical {(preset.Height?"height":"longest horizontal side")}, not a measurement";
            if(settings.Mode==SizeMode.ReferenceFace)
            {
                if(reference==null)throw new ArgumentException("Select a planar face in Rhino as the size reference first.");
                Positive(reference.ShortMeters,"Reference short edge");Positive(reference.LongMeters,"Reference long edge");Positive(reference.AreaSquareMeters,"Reference area");
                double edge=preset.LongReference?reference.LongMeters:reference.ShortMeters;
                if(inferred?.Axis>=0)result.TargetMeters=inferred.Meters;else result.TargetMeters=Compat.Clamp(edge*preset.RoomFraction,preset.Min,preset.Max);
                bool limited=false;
                if(span!=null)
                {
                    double factor=result.TargetMeters/span[result.Axis],longSide=Math.Max(span[0],span[1])*factor,shortSide=Math.Min(span[0],span[1])*factor;
                    double fit=Math.Min(1,Math.Min(reference.LongMeters*.8/longSide,Math.Min(reference.ShortMeters*.8/shortSide,Math.Sqrt(reference.AreaSquareMeters*.6/(longSide*shortSide)))));
                    if(fit<1){if(inferred?.Axis>=0)throw new ArgumentException("The described size exceeds the reference face. Adjust the description or use Manual size.");limited=true;result.TargetMeters*=fit;}
                    if(result.TargetMeters<preset.Min-1e-6)throw new ArgumentException("The reference face is too small for a typical object at this scale. Choose another face or use Manual size.");
                }
                result.Basis=inferred?.Axis>=0?$"Dimension from description · {inferred.Evidence}; checked against reference bounds.":$"{source} · {preset.Label}; reference {(preset.LongReference?"long":"short")} edge × {preset.RoomFraction:P0}, within {preset.Min:0.##}–{preset.Max:0.##} m"+(limited?"; reduced to fit bounds/area":"")+". Scale suggestion only; irregular face boundaries may not fit.";
            }
        }
        Positive(result.TargetMeters,"Output size");
        if(span!=null&&span[result.Axis]<=1e-8)throw new ArgumentException("The selected dimension is zero. Use Manual size and choose another axis.");
        if(span!=null)result.PhysicalMeters=span.Select(x=>x*result.TargetMeters/span[result.Axis]).ToArray();
        return result;
    }
    public static double[] RhinoSpan(AssetData asset){var b=asset.Bounds();var s=b.Max-b.Min;return[(double)s.X,s.Z,s.Y];}
}
