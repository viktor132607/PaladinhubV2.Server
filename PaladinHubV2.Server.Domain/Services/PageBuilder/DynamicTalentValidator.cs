using System.Text.Json;

namespace PaladinHubV2.Server.Domain.Services.PageBuilder;

public static class DynamicTalentValidator
{
    public static void Validate(JsonElement root, List<string> errors)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in root.EnumerateArray()) Validate(element, errors);
            return;
        }
        if (root.ValueKind != JsonValueKind.Object) return;
        if (root.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String && type.GetString() == "talenttree.dynamic")
            ValidateTree(root, errors);
        foreach (var property in root.EnumerateObject())
            if (property.Name is "Blocks" or "blocks" or "props" or "Tabs" or "tabs") Validate(property.Value, errors);
    }
    private static bool Text(JsonElement obj, string name, out string value)
    {
        value = "";
        if (!obj.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.String) return false;
        value = element.GetString() ?? "";
        return true;
    }
    private static bool Number(JsonElement obj, string name, int max, out int value)
    {
        value = 0;
        return obj.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.Number
            && element.TryGetInt32(out value) && value >= 1 && value <= max;
    }
    private static void ValidateTree(JsonElement tree, List<string> errors)
    {
        if (!Text(tree, "id", out var treeId) || string.IsNullOrWhiteSpace(treeId) || !Text(tree,"title",out _)
            || !Number(tree,"rows",20,out var rows) || !Number(tree,"columns",12,out var columns)
            || !Number(tree,"points",100,out _) || !tree.TryGetProperty("nodes",out var nodes) || nodes.ValueKind != JsonValueKind.Array)
        { errors.Add("Invalid dynamic talent tree settings."); return; }
        var graph = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var cells = new HashSet<(int,int)>();
        foreach (var node in nodes.EnumerateArray())
        {
            if (node.ValueKind != JsonValueKind.Object || !Text(node,"id",out var id) || string.IsNullOrWhiteSpace(id)
                || !Text(node,"name",out var name) || string.IsNullOrWhiteSpace(name) || !Text(node,"description",out _) || !Text(node,"icon",out _)
                || !Number(node,"row",rows,out var row) || !Number(node,"column",columns,out var column)
                || !Number(node,"maxRank",10,out _) || !node.TryGetProperty("requires",out var requires) || requires.ValueKind != JsonValueKind.Array)
            { errors.Add("Invalid talent fields or grid position."); return; }
            if (!cells.Add((row,column)) || graph.ContainsKey(id)) { errors.Add("Talent IDs and grid positions must be unique."); return; }
            var parents = new List<string>();
            foreach (var parent in requires.EnumerateArray())
            {
                if (parent.ValueKind != JsonValueKind.String) { errors.Add("Invalid talent prerequisite."); return; }
                parents.Add(parent.GetString()!);
            }
            graph.Add(id,parents);
        }
        var visiting = new HashSet<string>(); var done = new HashSet<string>();
        bool Visit(string id)
        {
            if (done.Contains(id)) return true;
            if (!graph.TryGetValue(id,out var parents) || !visiting.Add(id)) return false;
            foreach (var parent in parents) if (!Visit(parent)) return false;
            visiting.Remove(id); done.Add(id); return true;
        }
        if (graph.Keys.Any(id => !Visit(id))) errors.Add("Talent prerequisites must exist and cannot form cycles.");
    }
}
