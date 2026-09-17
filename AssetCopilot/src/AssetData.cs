using System.Numerics;

namespace AssetCopilot;

public sealed class TextureMap
{
    public byte[] Bytes { get; set; } = [];
    public Vector2 Offset { get; set; } = Vector2.Zero;
    public Vector2 Scale { get; set; } = Vector2.One;
    public float Rotation { get; set; }
    public int WrapS { get; set; } = 10497;
    public int WrapT { get; set; } = 10497;
    public Vector2 Transform(Vector2 uv)
    {
        var v=uv*Scale; float c=MathF.Cos(Rotation),s=MathF.Sin(Rotation);
        return Offset+new Vector2(c*v.X-s*v.Y,s*v.X+c*v.Y);
    }
}
public sealed class Part
{
    public List<Vector3> Vertices { get; } = new();
    public List<int> Indices { get; } = new();
    public List<Vector2> UV { get; } = new();
    public float[] Color { get; set; } = [0.72f, 0.69f, 0.64f, 1];
    public TextureMap? BaseColorMap { get; set; }
    public TextureMap? MetallicRoughnessMap { get; set; }
    public TextureMap? NormalMap { get; set; }
    public TextureMap? OcclusionMap { get; set; }
    public TextureMap? EmissionMap { get; set; }
    public float[] Emission { get; set; } = [0,0,0];
    public float Metallic { get; set; } = 1;
    public float Roughness { get; set; } = 1;
    public float NormalStrength { get; set; } = 1;
    public float OcclusionStrength { get; set; } = 1;
    public string AlphaMode { get; set; } = "OPAQUE";
    public float AlphaCutoff { get; set; } = .5f;
    public bool DoubleSided { get; set; }
    public byte[]? Texture { get => BaseColorMap?.Bytes; set => BaseColorMap=value==null?null:new TextureMap {Bytes=value}; }
    public Part Copy()
    {
        var p=new Part {Color=Color,BaseColorMap=BaseColorMap,MetallicRoughnessMap=MetallicRoughnessMap,NormalMap=NormalMap,OcclusionMap=OcclusionMap,EmissionMap=EmissionMap,Emission=Emission,Metallic=Metallic,Roughness=Roughness,NormalStrength=NormalStrength,OcclusionStrength=OcclusionStrength,AlphaMode=AlphaMode,AlphaCutoff=AlphaCutoff,DoubleSided=DoubleSided};
        p.Vertices.AddRange(Vertices);p.Indices.AddRange(Indices);p.UV.AddRange(UV);return p;
    }
}
public sealed class AssetData
{
    public List<Part> Parts { get; } = new();
    public List<string> Warnings { get; } = new();
    public int FaceCount => Parts.Sum(p => p.Indices.Count / 3);
    public (Vector3 Min, Vector3 Max) Bounds()
    {
        var min = new Vector3(float.MaxValue); var max = new Vector3(float.MinValue);
        foreach (var v in Parts.SelectMany(p => p.Vertices)) { min = Vector3.Min(min, v); max = Vector3.Max(max, v); }
        if (Parts.Count == 0 || !float.IsFinite(min.X)) throw new InvalidDataException("模型没有有效几何。");
        return (min, max);
    }
    public AssetData Prepare(int axis, double size, double unitMeters, double documentMeters, float scalePercent, int pitch, int yaw)
    {
        if (axis < 0 || axis > 2 || !double.IsFinite(size) || size <= 0 || size > 1e9 || unitMeters <= 0 || documentMeters <= 0 || !float.IsFinite(scalePercent) || scalePercent <= 0 || scalePercent > 10000)
            throw new ArgumentException("请输入有效的正尺寸、比例和文档单位。");
        var result = new AssetData();
        // GLB is Y-up; Rhino is Z-up. User correction precedes dimension fitting.
        var rotation = Matrix4x4.CreateRotationX((90 + pitch) * MathF.PI / 180) * Matrix4x4.CreateRotationZ(yaw * MathF.PI / 180);
        foreach (var part in Parts) { var copy = part.Copy(); for (int i = 0; i < copy.Vertices.Count; i++) copy.Vertices[i] = Vector3.Transform(copy.Vertices[i], rotation); result.Parts.Add(copy); }
        var (min, max) = result.Bounds(); var span = max - min;
        var measured = axis == 0 ? span.X : axis == 1 ? span.Y : span.Z;
        if (measured < 1e-8) throw new ArgumentException("所选尺寸方向为零，请切换宽／深／高。");
        var factor = size * unitMeters / documentMeters / measured * scalePercent / 100;
        if (!double.IsFinite(factor) || factor > 1e12) throw new ArgumentException("缩放过大，请检查尺寸。");
        var origin = new Vector3((min.X + max.X) / 2, (min.Y + max.Y) / 2, min.Z);
        foreach (var part in result.Parts) for (int i = 0; i < part.Vertices.Count; i++) part.Vertices[i] = (part.Vertices[i] - origin) * (float)factor;
        return result;
    }
}
