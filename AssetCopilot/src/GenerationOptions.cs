namespace AssetCopilot;
public sealed record GenerationOptions(string Model, int Faces)
{
    public const string P2 = "P2-20260801";
    public const string V31 = "v3.1-20260211";
    public void Validate()
    {
        if (Model != P2 && Model != V31) throw new ArgumentException("Choose P2.0 or V3.1.");
        if (Faces < 500 || Faces > (Model == P2 ? 25000 : 2000000)) throw new ArgumentException(Model == P2 ? "P2.0 supports 500–25,000 triangles." : "V3.1 supports 500–2,000,000 triangles.");
    }
    public Dictionary<string, object> Payload(string input, bool text)
    {
        Validate();
        if (string.IsNullOrWhiteSpace(input) || text && input.Length > 1024) throw new ArgumentException("Enter a model description of 1–1,024 characters.");
        var body = new Dictionary<string, object> { [text ? "prompt" : "input"] = input.Trim(), ["model"] = Model, ["face_limit"] = Faces, ["texture"] = true, ["pbr"] = true, ["quad"] = false };
        // P2 does not accept H-series geometry_quality. GLB is the triangle output default.
        if (Model == V31) body["geometry_quality"] = Faces > 1500000 ? "detailed" : "standard";
        return body;
    }
}

