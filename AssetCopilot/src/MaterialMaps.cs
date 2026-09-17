using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Security.Cryptography;
using System.Numerics;
using Rhino.DocObjects;
using Rhino.Geometry;
using Transform = Rhino.Geometry.Transform;
using Rhino.Display;
namespace AssetCopilot;

public static class MaterialMaps
{
    // RhinoCommon added this optional texture flag in 8.7. Keep the 8.0 API baseline.
    // Old Rhino uses its native texture-channel defaults; 8.7+ retains the explicit flags.
    static readonly System.Reflection.PropertyInfo? LinearTextureFlag = typeof(Texture).GetProperty("TreatAsLinear");
    public static byte[] Pixels(byte[] bytes, out int width, out int height)
    {
        using var stream=new MemoryStream(bytes);
        var frame=BitmapFrame.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
        width=frame.PixelWidth;height=frame.PixelHeight;
        if(width>8192 || height>8192 || (long)width*height>67108864)throw new InvalidDataException("贴图超过 8192 像素上限。");
        var converted=new FormatConvertedBitmap(frame,PixelFormats.Bgra32,null,0);
        var data=new byte[checked(width*height*4)];converted.CopyPixels(data,width*4,0);return data;
    }
    public static byte[] Encode(byte[] data,int width,int height)
    {
        var bmp=BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,data,width*4);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));
        using var output=new MemoryStream();encoder.Save(output);return output.ToArray();
    }
    static byte Byte(double value)=>(byte)Math.Clamp(Math.Round(value*255),0,255);
    static double Srgb(double v)=>v<=.0031308?v*12.92:1.055*Math.Pow(v,1/2.4)-.055;
    static double Linear(double v)=>v<=.04045?v/12.92:Math.Pow((v+.055)/1.055,2.4);
    public static byte[] ConvertMap(TextureMap map,Part p,string kind)
    {
        var pixels=Pixels(map.Bytes,out int width,out int height);
        for(int i=0;i<pixels.Length;i+=4)
        {
            if(kind=="base")
            {
                for(int c=0;c<3;c++)pixels[i+c]=Byte(Srgb(Linear(pixels[i+c]/255.0)*p.Color[2-c]));
                double alpha=pixels[i+3]/255.0*p.Color[3];
                pixels[i+3]=p.AlphaMode=="OPAQUE"?(byte)255:p.AlphaMode=="MASK"?(alpha>=p.AlphaCutoff?(byte)255:(byte)0):Byte(alpha);
            }
            else if(kind=="normal")
            {
                // V is inverted when writing Rhino UVs, so invert the tangent-space Y component too.
                var n=Vector3.Normalize(new Vector3((pixels[i+2]/255f*2-1)*p.NormalStrength,-(pixels[i+1]/255f*2-1)*p.NormalStrength,pixels[i]/255f*2-1));
                pixels[i]=Byte(n.Z*.5+.5);pixels[i+1]=Byte(n.Y*.5+.5);pixels[i+2]=Byte(n.X*.5+.5);pixels[i+3]=255;
            }
            else if(kind=="alpha")
            {
                var alpha=pixels[i+3]/255.0*p.Color[3];
                byte value=p.AlphaMode=="MASK"?(alpha>=p.AlphaCutoff?(byte)255:(byte)0):Byte(alpha);
                pixels[i]=pixels[i+1]=pixels[i+2]=value;pixels[i+3]=255;
            }
            else
            {
                double value=kind=="metallic"?pixels[i]/255.0*p.Metallic:kind=="roughness"?pixels[i+1]/255.0*p.Roughness:1+p.OcclusionStrength*(pixels[i+2]/255.0-1);
                pixels[i]=pixels[i+1]=pixels[i+2]=Byte(value);pixels[i+3]=255;
            }
        }
        return Encode(pixels,width,height);
    }
    public static string SaveMap(string folder,TextureMap map,Part part,string kind)
    {
        Directory.CreateDirectory(folder);
        byte[] data=kind=="emission"?map.Bytes:ConvertMap(map,part,kind);
        if(kind=="emission"){var pixels=Pixels(data,out var w,out var h);data=Encode(pixels,w,h);}
        var path=Path.Combine(folder,kind+"_"+Convert.ToHexString(SHA256.HashData(data))[..16]+".png");
        if(!File.Exists(path))File.WriteAllBytes(path,data);
        return path;
    }
    public static void Export(AssetData data,string folder)
    {
        foreach(var p in data.Parts)
        {
            if(p.BaseColorMap!=null){SaveMap(folder,p.BaseColorMap,p,"base");if(p.AlphaMode!="OPAQUE")SaveMap(folder,p.BaseColorMap,p,"alpha");}
            if(p.MetallicRoughnessMap!=null){SaveMap(folder,p.MetallicRoughnessMap,p,"metallic");SaveMap(folder,p.MetallicRoughnessMap,p,"roughness");}
            if(p.NormalMap!=null)SaveMap(folder,p.NormalMap,p,"normal");
            if(p.OcclusionMap!=null)SaveMap(folder,p.OcclusionMap,p,"occlusion");
            if(p.EmissionMap!=null)SaveMap(folder,p.EmissionMap,p,"emission");
        }
    }
    public static Transform Uvw(TextureMap map)
    {
        var tr=Transform.Identity;double c=Math.Cos(map.Rotation),s=Math.Sin(map.Rotation);
        tr.M00=c*map.Scale.X;tr.M01=-s*map.Scale.Y;tr.M03=map.Offset.X;
        tr.M10=s*map.Scale.X;tr.M11=c*map.Scale.Y;tr.M13=map.Offset.Y;
        var flip=Transform.Identity;flip.M11=-1;flip.M13=1;return flip*tr*flip;
    }
    public static Material Create(Part p,string folder,string name)
    {
        var m=new Material {Name=name};m.ToPhysicallyBased();var pb=m.PhysicallyBased;
        pb.BaseColor=new Color4f(p.Color[0],p.Color[1],p.Color[2],1);
        pb.Metallic=p.Metallic;pb.Roughness=p.Roughness;
        pb.Alpha=p.AlphaMode=="OPAQUE"?1:p.AlphaMode=="MASK"?(p.Color[3]>=p.AlphaCutoff?1:0):p.Color[3];
        pb.Opacity=1;
        pb.Emission=new Color4f(p.Emission[0],p.Emission[1],p.Emission[2],1);
        void Set(TextureMap? map,string kind,TextureType type,bool linear)
        {
            if(map==null)return;
            using var texture=new Texture {FileName=SaveMap(folder,map,p,kind),Enabled=true,TextureType=type,ApplyUvwTransform=true,UvwTransform=Uvw(map),WrapU=map.WrapS==33071?TextureUvwWrapping.Clamp:TextureUvwWrapping.Repeat,WrapV=map.WrapT==33071?TextureUvwWrapping.Clamp:TextureUvwWrapping.Repeat};
            if(LinearTextureFlag?.CanWrite==true)LinearTextureFlag.SetValue(texture,linear);
            if(!pb.SetTexture(texture,type))throw new InvalidOperationException("无法设置 PBR 贴图："+kind);
        }
        Set(p.BaseColorMap,"base",TextureType.PBR_BaseColor,false);
        if(p.BaseColorMap!=null)pb.BaseColor=new Color4f(1,1,1,1);
        Set(p.MetallicRoughnessMap,"metallic",TextureType.PBR_Metallic,true);
        Set(p.MetallicRoughnessMap,"roughness",TextureType.PBR_Roughness,true);
        if(p.MetallicRoughnessMap!=null){pb.Metallic=1;pb.Roughness=1;}
        Set(p.NormalMap,"normal",TextureType.Bump,true);
        Set(p.OcclusionMap,"occlusion",TextureType.PBR_AmbientOcclusion,true);
        Set(p.EmissionMap,"emission",TextureType.PBR_Emission,false);
        if(p.AlphaMode!="OPAQUE" && p.BaseColorMap!=null){Set(p.BaseColorMap,"alpha",TextureType.PBR_Alpha,true);pb.Alpha=1;pb.UseBaseColorTextureAlphaForObjectAlphaTransparencyTexture=false;}
        pb.SynchronizeLegacyMaterial();return m;
    }
}

