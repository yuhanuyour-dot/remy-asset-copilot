using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
namespace AssetCopilot;

public static class RhinoSizing
{
    static readonly string session=Guid.NewGuid().ToString("N");
    public static FaceReference? Pick(RhinoDoc doc)
    {
        using var get=new GetObject();get.SetCommandPrompt("选择平面参照；多重曲面请 Ctrl+Shift 选单个面，Esc 取消");
        get.GeometryFilter=ObjectType.Surface|ObjectType.Brep|ObjectType.Mesh;get.SubObjectSelect=true;get.GroupSelect=false;
        get.EnablePreSelect(false,true);get.EnableUnselectObjectsOnExit(false);
        if(get.Get()!=GetResult.Object)return null;
        if(RhinoDoc.ActiveDoc?.RuntimeSerialNumber!=doc.RuntimeSerialNumber)throw new InvalidOperationException("当前文档已切换，请重新选择参考面。");
        var picked=get.Object(0);var owner=picked.Object();
        if(owner==null||owner is InstanceObject)throw new ArgumentException("请选直接位于文档中的平面，块内的面请先提取为独立对象。");
        if(owner.Geometry is Mesh)
        {
            var component=picked.GeometryComponentIndex;
            if(component.ComponentIndexType==ComponentIndexType.MeshNgon)throw new ArgumentException("请选择网格的单个三角/四边面，或整个平面网格。");
            return Measure(doc,owner.Id,component.ComponentIndexType==ComponentIndexType.MeshFace?component.Index:-1);
        }
        var face=picked.Face();
        if(face==null)throw new ArgumentException("请选择单个平面；多重曲面请用 Ctrl+Shift 选择其中一个面。");
        return Measure(doc,owner.Id,face.FaceIndex);
    }

    public static FaceReference Measure(RhinoDoc doc,Guid objectId,int faceIndex)
    {
        var obj=doc.Objects.FindId(objectId)??throw new InvalidOperationException("参考面已删除，请重新选择。");
        if(obj.Geometry is Mesh mesh)return MeasureMesh(doc,objectId,mesh,faceIndex);
        Brep? owned=null;
        try
        {
            var brep=obj.Geometry as Brep;
            if(brep==null&&obj.Geometry is Surface surface)brep=owned=surface.ToBrep();
            if(brep==null||faceIndex<0||faceIndex>=brep.Faces.Count)throw new InvalidOperationException("参考面结构已变化，请重新选择。");
            var face=brep.Faces[faceIndex];
            if(!face.TryGetPlane(out var plane,doc.ModelAbsoluteTolerance))throw new ArgumentException("当前仅支持平面参考，如地面、桌面或墙面；请选择平面。");
            using var trimmed=face.DuplicateFace(false);
            var bounds=trimmed.GetBoundingBox(plane);
            using var properties=AreaMassProperties.Compute(trimmed);
            if(properties==null||!bounds.IsValid)throw new ArgumentException("无法测量所选参考面。");
            double meters=RhinoPlacement.Meters(doc),w=bounds.Max.X-bounds.Min.X,h=bounds.Max.Y-bounds.Min.Y;
            if(!Compat.IsFinite(properties.Area)||properties.Area<=doc.ModelAbsoluteTolerance*doc.ModelAbsoluteTolerance||w<=doc.ModelAbsoluteTolerance||h<=doc.ModelAbsoluteTolerance)throw new ArgumentException("参考面面积过小或无效，请重新选择。");
            return new FaceReference{Session=session,DocumentSerial=doc.RuntimeSerialNumber,ObjectId=objectId,FaceIndex=faceIndex,FaceCount=brep.Faces.Count,ShortMeters=Math.Min(w,h)*meters,LongMeters=Math.Max(w,h)*meters,AreaSquareMeters=properties.Area*meters*meters};
        }
        finally{owned?.Dispose();}
    }
    static FaceReference MeasureMesh(RhinoDoc doc,Guid id,Mesh original,int index)
    {
        Mesh? selected=null;
        try
        {
            var mesh=original;
            if(index>=0)
            {
                if(index>=original.Faces.Count)throw new InvalidOperationException("参考网格面已变化，请重新选择。");
                var f=original.Faces[index];selected=new Mesh();
                foreach(int v in new[]{f.A,f.B,f.C,f.D})selected.Vertices.Add(original.Vertices[v]);
                if(f.IsTriangle)selected.Faces.AddFace(0,1,2);else selected.Faces.AddFace(0,1,2,3);mesh=selected;
            }
            if(mesh.Faces.Count==0)throw new ArgumentException("网格中没有可测量的面。");
            Plane plane=Plane.Unset;
            foreach(var f in mesh.Faces){var candidate=new Plane(mesh.Vertices.Point3dAt(f.A),mesh.Vertices.Point3dAt(f.B),mesh.Vertices.Point3dAt(f.C));if(candidate.IsValid){plane=candidate;break;}}
            if(!plane.IsValid||Enumerable.Range(0,mesh.Vertices.Count).Any(i=>Math.Abs(plane.DistanceTo(mesh.Vertices.Point3dAt(i)))>doc.ModelAbsoluteTolerance))throw new ArgumentException("请选择整个平面网格，或 Ctrl+Shift 选择其中的单个平面。");
            var bounds=mesh.GetBoundingBox(plane);using var area=AreaMassProperties.Compute(mesh);
            double meters=RhinoPlacement.Meters(doc),w=bounds.Max.X-bounds.Min.X,h=bounds.Max.Y-bounds.Min.Y;
            if(area==null||!Compat.IsFinite(area.Area)||area.Area<=doc.ModelAbsoluteTolerance*doc.ModelAbsoluteTolerance||w<=doc.ModelAbsoluteTolerance||h<=doc.ModelAbsoluteTolerance)throw new ArgumentException("网格面的尺寸或面积无效。");
            return new FaceReference{Session=session,DocumentSerial=doc.RuntimeSerialNumber,ObjectId=id,FaceIndex=index,FaceCount=original.Faces.Count,GeometryKind="Mesh",ShortMeters=Math.Min(w,h)*meters,LongMeters=Math.Max(w,h)*meters,AreaSquareMeters=area.Area*meters*meters};
        }
        finally{selected?.Dispose();}
    }
    public static FaceReference Refresh(RhinoDoc doc,FaceReference? reference)
    {
        if(reference==null)throw new ArgumentException("请先在 Rhino 选择一个平面作为尺寸参照。");
        if(reference.Session!=session||reference.DocumentSerial!=doc.RuntimeSerialNumber)throw new InvalidOperationException("参考面属于其他文档或上次会话，请在当前文档重新选择。");
        var refreshed=Measure(doc,reference.ObjectId,reference.FaceIndex);
        if(reference.FaceCount!=refreshed.FaceCount||reference.GeometryKind!=refreshed.GeometryKind)throw new InvalidOperationException("参考对象的面结构已变化，请重新选择。");
        return refreshed;
    }
}
