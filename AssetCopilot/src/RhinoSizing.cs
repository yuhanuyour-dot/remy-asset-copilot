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
        using var get=new GetObject();get.SetCommandPrompt("Select a planar reference; use Ctrl+Shift for a polysurface face, Esc to cancel");
        get.GeometryFilter=ObjectType.Surface|ObjectType.Brep|ObjectType.Mesh;get.SubObjectSelect=true;get.GroupSelect=false;
        get.EnablePreSelect(false,true);get.EnableUnselectObjectsOnExit(false);
        if(get.Get()!=GetResult.Object)return null;
        if(RhinoDoc.ActiveDoc?.RuntimeSerialNumber!=doc.RuntimeSerialNumber)throw new InvalidOperationException("The active document changed. Select the reference face again.");
        var picked=get.Object(0);var owner=picked.Object();
        if(owner==null||owner is InstanceObject)throw new ArgumentException("Select a planar face in the document. Extract faces inside blocks into separate objects first.");
        if(owner.Geometry is Mesh)
        {
            var component=picked.GeometryComponentIndex;
            if(component.ComponentIndexType==ComponentIndexType.MeshNgon)throw new ArgumentException("Select one triangular or quad mesh face, or an entire planar mesh.");
            return Measure(doc,owner.Id,component.ComponentIndexType==ComponentIndexType.MeshFace?component.Index:-1);
        }
        var face=picked.Face();
        if(face==null)throw new ArgumentException("Select one planar face. Use Ctrl+Shift to select a face within a polysurface.");
        return Measure(doc,owner.Id,face.FaceIndex);
    }

    public static FaceReference Measure(RhinoDoc doc,Guid objectId,int faceIndex)
    {
        var obj=doc.Objects.FindId(objectId)??throw new InvalidOperationException("The reference face was deleted. Select another face.");
        if(obj.Geometry is Mesh mesh)return MeasureMesh(doc,objectId,mesh,faceIndex);
        Brep? owned=null;
        try
        {
            var brep=obj.Geometry as Brep;
            if(brep==null&&obj.Geometry is Surface surface)brep=owned=surface.ToBrep();
            if(brep==null||faceIndex<0||faceIndex>=brep.Faces.Count)throw new InvalidOperationException("The reference face topology changed. Select it again.");
            var face=brep.Faces[faceIndex];
            if(!face.TryGetPlane(out var plane,doc.ModelAbsoluteTolerance))throw new ArgumentException("Select a planar reference such as a floor, tabletop, or wall.");
            using var trimmed=face.DuplicateFace(false);
            var bounds=trimmed.GetBoundingBox(plane);
            using var properties=AreaMassProperties.Compute(trimmed);
            if(properties==null||!bounds.IsValid)throw new ArgumentException("The selected reference face could not be measured.");
            double meters=RhinoPlacement.Meters(doc),w=bounds.Max.X-bounds.Min.X,h=bounds.Max.Y-bounds.Min.Y;
            if(!Compat.IsFinite(properties.Area)||properties.Area<=doc.ModelAbsoluteTolerance*doc.ModelAbsoluteTolerance||w<=doc.ModelAbsoluteTolerance||h<=doc.ModelAbsoluteTolerance)throw new ArgumentException("The reference face is too small or invalid. Select another face.");
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
                if(index>=original.Faces.Count)throw new InvalidOperationException("The reference mesh face changed. Select it again.");
                var f=original.Faces[index];selected=new Mesh();
                foreach(int v in new[]{f.A,f.B,f.C,f.D})selected.Vertices.Add(original.Vertices[v]);
                if(f.IsTriangle)selected.Faces.AddFace(0,1,2);else selected.Faces.AddFace(0,1,2,3);mesh=selected;
            }
            if(mesh.Faces.Count==0)throw new ArgumentException("The mesh has no measurable faces.");
            Plane plane=Plane.Unset;
            foreach(var f in mesh.Faces){var candidate=new Plane(mesh.Vertices.Point3dAt(f.A),mesh.Vertices.Point3dAt(f.B),mesh.Vertices.Point3dAt(f.C));if(candidate.IsValid){plane=candidate;break;}}
            if(!plane.IsValid||Enumerable.Range(0,mesh.Vertices.Count).Any(i=>Math.Abs(plane.DistanceTo(mesh.Vertices.Point3dAt(i)))>doc.ModelAbsoluteTolerance))throw new ArgumentException("Select an entire planar mesh, or use Ctrl+Shift to select one planar face.");
            var bounds=mesh.GetBoundingBox(plane);using var area=AreaMassProperties.Compute(mesh);
            double meters=RhinoPlacement.Meters(doc),w=bounds.Max.X-bounds.Min.X,h=bounds.Max.Y-bounds.Min.Y;
            if(area==null||!Compat.IsFinite(area.Area)||area.Area<=doc.ModelAbsoluteTolerance*doc.ModelAbsoluteTolerance||w<=doc.ModelAbsoluteTolerance||h<=doc.ModelAbsoluteTolerance)throw new ArgumentException("The mesh face has invalid dimensions or area.");
            return new FaceReference{Session=session,DocumentSerial=doc.RuntimeSerialNumber,ObjectId=id,FaceIndex=index,FaceCount=original.Faces.Count,GeometryKind="Mesh",ShortMeters=Math.Min(w,h)*meters,LongMeters=Math.Max(w,h)*meters,AreaSquareMeters=area.Area*meters*meters};
        }
        finally{selected?.Dispose();}
    }
    public static FaceReference Refresh(RhinoDoc doc,FaceReference? reference)
    {
        if(reference==null)throw new ArgumentException("Select a planar face in Rhino as the size reference first.");
        if(reference.Session!=session||reference.DocumentSerial!=doc.RuntimeSerialNumber)throw new InvalidOperationException("The reference belongs to another document or an earlier session. Select it again in the current document.");
        var refreshed=Measure(doc,reference.ObjectId,reference.FaceIndex);
        if(reference.FaceCount!=refreshed.FaceCount||reference.GeometryKind!=refreshed.GeometryKind)throw new InvalidOperationException("The reference object's face topology changed. Select it again.");
        return refreshed;
    }
}
