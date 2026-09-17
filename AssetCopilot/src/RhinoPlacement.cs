using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
namespace AssetCopilot;

public static class RhinoPlacement
{
    public static double Meters(RhinoDoc doc)
    {
        if(doc.ModelUnitSystem is UnitSystem.None or UnitSystem.CustomUnits)throw new InvalidOperationException("Set standard document units in Rhino's document properties first.");
        return RhinoMath.UnitScale(doc.ModelUnitSystem,UnitSystem.Meters);
    }
    static List<Mesh> Meshes(AssetData asset)
    {
        var list=new List<Mesh>();
        try
        {
            foreach(var part in asset.Parts)
            {
                var mesh=new Mesh();list.Add(mesh);
                foreach(var v in part.Vertices)mesh.Vertices.Add(v.X,v.Y,v.Z);
                for(int i=0;i<part.Indices.Count;i+=3)mesh.Faces.AddFace(part.Indices[i],part.Indices[i+1],part.Indices[i+2]);
                foreach(var uv in part.UV)mesh.TextureCoordinates.Add(uv.X,1-uv.Y);
                mesh.Normals.ComputeNormals();mesh.Compact();
                if(!mesh.IsValid)throw new InvalidDataException("Placement was blocked because the model contains invalid geometry.");
            }
            return list;
        }
        catch {foreach(var mesh in list)mesh.Dispose();throw;}
    }
    public static bool Place(RhinoDoc doc,AssetData asset,string textures,string taskId)
    {
        if(RhinoDoc.ActiveDoc?.RuntimeSerialNumber!=doc.RuntimeSerialNumber)throw new InvalidOperationException("The active document changed. Start placement again.");
        var plane=(doc.Views.ActiveView??throw new InvalidOperationException("There is no active viewport.")).ActiveViewport.ConstructionPlane();
        var meshes=Meshes(asset);
        try
        {
            using var get=new GetPoint();get.SetCommandPrompt("Pick a placement point on the current construction plane; Esc to cancel");get.Constrain(plane,false);
            get.DynamicDraw+=(_,e)=>
            {
                e.Display.PushModelTransform(Transform.PlaneToPlane(Plane.WorldXY,new Plane(e.CurrentPoint,plane.XAxis,plane.YAxis)));
                try{foreach(var mesh in meshes){if(asset.FaceCount>100000)e.Display.DrawBox(mesh.GetBoundingBox(false),System.Drawing.Color.DimGray);else e.Display.DrawMeshWires(mesh,System.Drawing.Color.DimGray);}}
                finally{e.Display.PopModelTransform();}
            };
            if(get.Get()!=GetResult.Point)return false;
            if(RhinoDoc.ActiveDoc?.RuntimeSerialNumber!=doc.RuntimeSerialNumber)throw new InvalidOperationException("Placement was cancelled because the active document changed.");
            var id=InsertMeshes(doc,asset,meshes,textures,taskId,new Plane(get.Point(),plane.XAxis,plane.YAxis));
            doc.Objects.UnselectAll();doc.Objects.Select(id);doc.Views.Redraw();return true;
        }
        finally{foreach(var mesh in meshes)mesh.Dispose();}
    }
    public static Guid InsertAt(RhinoDoc doc,AssetData asset,string textures,string taskId,Plane target)
    {
        var meshes=Meshes(asset);
        try{return InsertMeshes(doc,asset,meshes,textures,taskId,target);}
        finally{foreach(var mesh in meshes)mesh.Dispose();}
    }
    static Guid InsertMeshes(RhinoDoc doc,AssetData asset,List<Mesh> meshes,string textures,string taskId,Plane target)
    {
        uint undo=doc.BeginUndoRecord("Asset Copilot: Place asset");
        int definition=-1;Guid objectId=Guid.Empty;var materials=new List<int>();var layers=new List<int>();
        var folder=Path.GetDirectoryName(textures)!;var assetName=AssetStore.Slug(Path.GetFileName(folder));int assetLayer=-1;
        try
        {
            int parent=doc.Layers.FindByFullPath("AssetCopilot",-1);
            if(parent<0){parent=doc.Layers.Add(new Layer{Name="AssetCopilot",Color=System.Drawing.Color.SlateGray});if(parent<0)throw new InvalidOperationException("Could not create the asset layer.");layers.Add(parent);}
            if(doc.Layers[parent].IsLocked||!doc.Layers[parent].IsVisible)throw new InvalidOperationException("Unlock and show the AssetCopilot layer first.");
            string name=assetName+"_"+Guid.NewGuid().ToString("N")[..6];
            assetLayer=doc.Layers.Add(new Layer{Name=name,ParentLayerId=doc.Layers[parent].Id});
            if(assetLayer<0)throw new InvalidOperationException("Could not create the asset sublayer.");layers.Add(assetLayer);
            var attrs=new List<ObjectAttributes>();
            for(int i=0;i<meshes.Count;i++)
            {
                using var mat=MaterialMaps.Create(asset.Parts[i],textures,"Copilot_"+name+"_"+i);
                int material=doc.Materials.Add(mat);if(material<0)throw new InvalidOperationException("Could not create the PBR material.");materials.Add(material);
                var at=new ObjectAttributes{LayerIndex=assetLayer,MaterialIndex=material,MaterialSource=ObjectMaterialSource.MaterialFromObject};
                at.SetUserString("AssetCopilot.Task",taskId);at.SetUserString("AssetCopilot.Folder",folder);attrs.Add(at);
            }
            definition=doc.InstanceDefinitions.Add("AI_"+name,"Asset Copilot 0.2 · PBR asset",Point3d.Origin,meshes.Cast<GeometryBase>(),attrs);
            if(definition<0)throw new InvalidOperationException("Could not create the asset block.");
            var instanceAttributes=new ObjectAttributes{Name=name,LayerIndex=assetLayer};
            instanceAttributes.SetUserString("AssetCopilot.Folder",folder);instanceAttributes.SetUserString("AssetCopilot.Task",taskId);
            objectId=doc.Objects.AddInstanceObject(definition,Transform.PlaneToPlane(Plane.WorldXY,target),instanceAttributes);
            if(objectId==Guid.Empty)throw new InvalidOperationException("Could not place the asset block.");
            return objectId;
        }
        catch
        {
            if(objectId!=Guid.Empty)doc.Objects.Delete(objectId,true);
            if(definition>=0)doc.InstanceDefinitions.Delete(definition,true,true);
            foreach(var m in materials)doc.Materials.DeleteAt(m);
            foreach(var l in layers.AsEnumerable().Reverse())doc.Layers.Delete(l,true);
            throw;
        }
        finally{doc.EndUndoRecord(undo);}
    }
}

