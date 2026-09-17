using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json;

namespace AssetCopilot;

public static class GlbReader
{
    public static AssetData Read(string path) => GlbDecoder.Read(path);
    public static AssetData Read(byte[] bytes)
    {
        if (bytes.Length < 20 || bytes.Length > 150 * 1024 * 1024 || U(bytes, 0) != 0x46546C67 || U(bytes, 4) != 2 || U(bytes, 8) != bytes.Length)
            throw new InvalidDataException("需要有效的 GLB 2.0 文件，大小不超过 150 MB。");
        byte[]? json = null, bin = null;
        for (int pos = 12; pos < bytes.Length;)
        {
            if (pos + 8 > bytes.Length) throw new InvalidDataException("GLB 分块损坏。");
            int len = checked((int)U(bytes, pos)); uint type = U(bytes, pos + 4); pos += 8;
            if (len < 0 || len > bytes.Length - pos) throw new InvalidDataException("GLB 长度错误。");
            if (type == 0x4E4F534A) json = bytes.AsSpan(pos,len).ToArray();
            if (type == 0x004E4942) bin = bytes.AsSpan(pos,len).ToArray();
            pos += len;
        }
        if (json == null || bin == null) throw new InvalidDataException("GLB 缺少数据。");
        using var doc = JsonDocument.Parse(json); var root = doc.RootElement; var data = new AssetData();
        if (root.TryGetProperty("extensionsRequired", out var required))
            foreach (var extension in required.EnumerateArray())
                if (extension.GetString() is not ("KHR_mesh_quantization" or "KHR_texture_transform"))
                    throw new InvalidDataException("不支持的 GLB 扩展：" + extension.GetString());
        if (root.TryGetProperty("animations", out _)) data.Warnings.Add("仅导入静态几何，不导入动画。");
        void AddMesh(int index, Matrix4x4 transform)
        {
            foreach (var primitive in root.GetProperty("meshes")[index].GetProperty("primitives").EnumerateArray())
            {
                if (I(primitive, "mode", 4) != 4) throw new InvalidDataException("仅支持三角网格。");
                var attr = primitive.GetProperty("attributes"); var p = new Part();
                var positions = Access(root, bin, attr.GetProperty("POSITION").GetInt32(), 3, false);
                for (int i = 0; i < positions.Length; i += 3) p.Vertices.Add(Vector3.Transform(new((float)positions[i], (float)positions[i + 1], (float)positions[i + 2]), transform));
                if (primitive.TryGetProperty("indices", out var indices)) p.Indices.AddRange(Access(root, bin, indices.GetInt32(), 1, true).Select(n => checked((int)n)));
                else p.Indices.AddRange(Enumerable.Range(0, p.Vertices.Count));
                if (p.Indices.Count % 3 != 0 || p.Indices.Any(i => i < 0 || i >= p.Vertices.Count)) throw new InvalidDataException("三角面索引无效。");
                if (transform.GetDeterminant() < 0) for (int i = 0; i < p.Indices.Count; i += 3) (p.Indices[i+1], p.Indices[i+2]) = (p.Indices[i+2], p.Indices[i+1]);
                if (attr.TryGetProperty("TEXCOORD_0", out var uv))
                {
                    var coords = Access(root, bin, uv.GetInt32(), 2, false);
                    if (coords.Length / 2 != p.Vertices.Count) throw new InvalidDataException("UV 数量错误。");
                    for (int i = 0; i < coords.Length; i += 2) p.UV.Add(new((float)coords[i], (float)coords[i+1]));
                }
                if (primitive.TryGetProperty("material", out var material))
                {
                    var m=root.GetProperty("materials")[material.GetInt32()];
                    if(m.TryGetProperty("pbrMetallicRoughness",out var pbr))
                    {
                        if(pbr.TryGetProperty("baseColorFactor",out var color))p.Color=color.EnumerateArray().Select(v=>v.GetSingle()).ToArray();
                        p.Metallic=F(pbr,"metallicFactor",1);p.Roughness=F(pbr,"roughnessFactor",1);
                        p.BaseColorMap=Map(pbr,"baseColorTexture",root,bin,p,data);
                        p.MetallicRoughnessMap=Map(pbr,"metallicRoughnessTexture",root,bin,p,data);
                    }
                    p.NormalMap=Map(m,"normalTexture",root,bin,p,data);
                    if(m.TryGetProperty("normalTexture",out var normal))p.NormalStrength=F(normal,"scale",1);
                    p.OcclusionMap=Map(m,"occlusionTexture",root,bin,p,data);
                    if(m.TryGetProperty("occlusionTexture",out var occ))p.OcclusionStrength=F(occ,"strength",1);
                    p.EmissionMap=Map(m,"emissiveTexture",root,bin,p,data);
                    if(m.TryGetProperty("emissiveFactor",out var emissive))p.Emission=emissive.EnumerateArray().Select(v=>v.GetSingle()).ToArray();
                    p.AlphaMode=m.TryGetProperty("alphaMode",out var alpha)?alpha.GetString()!:"OPAQUE";
                    p.AlphaCutoff=F(m,"alphaCutoff",.5f);p.DoubleSided=m.TryGetProperty("doubleSided",out var ds)&&ds.GetBoolean();
                }
                if (p.Vertices.Any(v => !Compat.IsFinite(v.X) || !Compat.IsFinite(v.Y) || !Compat.IsFinite(v.Z))) throw new InvalidDataException("几何含无效坐标。");
                data.Parts.Add(p);
                if (data.FaceCount > 2000000 || data.Parts.Sum(x => x.Vertices.Count) > 6000000) throw new InvalidDataException("超过 200 万三角面／600 万顶点上限，请先减面。");
            }
        }
        var visiting = new HashSet<int>();
        void Node(int n, Matrix4x4 parent, int depth)
        {
            if (depth > 128 || !visiting.Add(n)) throw new InvalidDataException("节点层级无效。");
            var node = root.GetProperty("nodes")[n]; if (node.TryGetProperty("skin", out _)) throw new InvalidDataException("首版不支持骨骼网格。");
            Matrix4x4 local;
            if (node.TryGetProperty("matrix", out var mat)) { var v = mat.EnumerateArray().Select(x => x.GetSingle()).ToArray(); local = new(v[0],v[1],v[2],v[3],v[4],v[5],v[6],v[7],v[8],v[9],v[10],v[11],v[12],v[13],v[14],v[15]); }
            else
            {
                var t = Vec(node,"translation",Vector3.Zero); var s = Vec(node,"scale",Vector3.One); var r = Quaternion.Identity;
                if (node.TryGetProperty("rotation",out var q)) r = new(q[0].GetSingle(),q[1].GetSingle(),q[2].GetSingle(),q[3].GetSingle());
                local = Matrix4x4.CreateScale(s) * Matrix4x4.CreateFromQuaternion(r) * Matrix4x4.CreateTranslation(t);
            }
            var world = local * parent;
            if (node.TryGetProperty("mesh",out var mesh)) AddMesh(mesh.GetInt32(),world);
            if (node.TryGetProperty("children",out var children)) foreach(var child in children.EnumerateArray()) Node(child.GetInt32(),world,depth+1);
            visiting.Remove(n);
        }
        if (root.TryGetProperty("scenes",out var scenes)) foreach(var n in scenes[I(root,"scene",0)].GetProperty("nodes").EnumerateArray()) Node(n.GetInt32(),Matrix4x4.Identity,0);
        else throw new InvalidDataException("GLB 缺少默认场景。");
        if (data.FaceCount == 0) throw new InvalidDataException("没有可导入的三角面。");
        if (data.FaceCount > 500000) data.Warnings.Add("高面数模型：预览和放置可能较慢，当前保留原始几何，未自动减面。");
        return data;
    }
    static float F(JsonElement e,string key,float fallback)=>e.TryGetProperty(key,out var v)?v.GetSingle():fallback;
    static TextureMap? Map(JsonElement owner,string name,JsonElement root,byte[] bin,Part part,AssetData data)
    {
        if(!owner.TryGetProperty(name,out var info))return null;
        if(part.UV.Count==0) {data.Warnings.Add("模型缺少 UV，部分贴图无法映射。");return null;}
        var map=new TextureMap();int uv=I(info,"texCoord",0);
        if(info.TryGetProperty("extensions",out var ex)&&ex.TryGetProperty("KHR_texture_transform",out var tr))
        {
            uv=I(tr,"texCoord",uv);
            if(tr.TryGetProperty("offset",out var v))map.Offset=new(v[0].GetSingle(),v[1].GetSingle());
            if(tr.TryGetProperty("scale",out var sc))map.Scale=new(sc[0].GetSingle(),sc[1].GetSingle());
            map.Rotation=F(tr,"rotation",0);
        }
        if(uv!=0)throw new InvalidDataException("该 GLB 使用了第二套 UV；当前支持 Tripo 输出的 TEXCOORD_0。");
        var tex=root.GetProperty("textures")[info.GetProperty("index").GetInt32()];
        if(tex.TryGetProperty("sampler",out var sampler))
        {var sa=root.GetProperty("samplers")[sampler.GetInt32()];map.WrapS=I(sa,"wrapS",10497);map.WrapT=I(sa,"wrapT",10497);}
        var img=root.GetProperty("images")[tex.GetProperty("source").GetInt32()];
        if(!img.TryGetProperty("bufferView",out var view)){data.Warnings.Add("外部贴图未读取，请使用内嵌 GLB。");return null;}
        map.Bytes=View(root,bin,view.GetInt32());return map;
    }
    static uint U(byte[] b,int i) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(i,4));
    static int I(JsonElement e,string key,int fallback) => e.TryGetProperty(key,out var v) ? v.GetInt32() : fallback;
    static Vector3 Vec(JsonElement e,string key,Vector3 fallback) => e.TryGetProperty(key,out var v) ? new(v[0].GetSingle(),v[1].GetSingle(),v[2].GetSingle()) : fallback;
    static byte[] View(JsonElement root,byte[] bin,int index)
    {
        var v = root.GetProperty("bufferViews")[index]; if(I(v,"buffer",0)!=0) throw new InvalidDataException("不支持外部 Buffer。");
        int start=I(v,"byteOffset",0),len=v.GetProperty("byteLength").GetInt32();
        if(start<0 || len<0 || start>bin.Length-len) throw new InvalidDataException("Buffer 越界。");
        return bin.AsSpan(start,len).ToArray();
    }
    static double[] Access(JsonElement root,byte[] bin,int index,int count,bool integer)
    {
        var a=root.GetProperty("accessors")[index];
        if(a.TryGetProperty("sparse",out _)) throw new InvalidDataException("暂不支持 sparse accessor。");
        if(a.GetProperty("type").GetString() != (count==3?"VEC3":count==2?"VEC2":"SCALAR")) throw new InvalidDataException("Accessor 类型错误。");
        var view=root.GetProperty("bufferViews")[a.GetProperty("bufferView").GetInt32()];
        var b=View(root,bin,a.GetProperty("bufferView").GetInt32()); int component=a.GetProperty("componentType").GetInt32();
        int size=component switch {5120 or 5121=>1,5122 or 5123=>2,5125=>4,5126=>4,_=>throw new InvalidDataException("不支持的组件类型。")};
        if(integer && component is not (5121 or 5123 or 5125)) throw new InvalidDataException("面索引必须是无符号整数。");
        int n=a.GetProperty("count").GetInt32(),offset=I(a,"byteOffset",0),stride=I(view,"byteStride",count*size);
        if(n<0 || n>(integer?6000000:6000000) || offset<0 || stride<count*size || (long)offset+(long)Math.Max(0,n-1)*stride+count*size>b.Length) throw new InvalidDataException("Accessor 越界或过大。");
        bool normalized=a.TryGetProperty("normalized",out var norm)&&norm.GetBoolean(); var result=new double[n*count];
        for(int i=0;i<n;i++) for(int j=0;j<count;j++)
        {
            int pos=offset+i*stride+j*size;
            double value=component switch {5120=>(sbyte)b[pos],5121=>b[pos],5122=>BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan(pos,2)),5123=>BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(pos,2)),5125=>U(b,pos),_=>BitConverter.ToSingle(b,pos)};
            if(normalized && component!=5126) value=component switch {5120=>Math.Max(value/127,-1),5122=>Math.Max(value/32767,-1),5121=>value/255,5123=>value/65535,_=>value/uint.MaxValue};
            result[i*count+j]=value;
        }
        return result;
    }
}
