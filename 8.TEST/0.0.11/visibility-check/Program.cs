using System.Reflection;

var assembly = Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, "PrismaticGemFilter.dll"));
var type = assembly.GetType("PrismaticGemFilter.CardGroups", throwOnError: true)!;

var all = (Array)type.GetField("All", BindingFlags.Static | BindingFlags.Public)!.GetValue(null)!;
var visible = (Array)type.GetField("Visible", BindingFlags.Static | BindingFlags.Public)!.GetValue(null)!;

var features = assembly.GetType("PrismaticGemFilter.GameFeatures", throwOnError: true)!;
var hasFool = features.GetProperty("HasFool", BindingFlags.Static | BindingFlags.Public)!.GetValue(null);
Console.WriteLine($"hasFool={hasFool} all={all.Length} visible={visible.Length}");

var ids = new List<string>();
foreach (var group in visible)
    ids.Add((string)group.GetType().GetProperty("Id")!.GetValue(group)!);
Console.WriteLine("visibleGroups=" + string.Join(",", ids));

var multiplayer = visible.Cast<object>().First(group =>
    (string)group.GetType().GetProperty("Id")!.GetValue(group)! == "multiplayer");
var members = (Array)multiplayer.GetType().GetProperty("Members")!.GetValue(multiplayer)!;
Console.WriteLine($"multiplayer={members.Length}");

foreach (var id in new[] { "arrow", "impale", "sadness", "bugged" })
    Console.WriteLine($"{id}Visible={ids.Contains(id)}");

var allMembers = visible.Cast<object>()
    .SelectMany(group => ((Array)group.GetType().GetProperty("Members")!.GetValue(group)!).Cast<string>())
    .ToHashSet(StringComparer.Ordinal);
Console.WriteLine("hasCoveringFire=" + allMembers.Contains("COVERING_FIRE"));
Console.WriteLine("hasBlotOutTheSky=" + allMembers.Contains("BLOT_OUT_THE_SKY"));
Console.WriteLine("hasCalcify=" + allMembers.Contains("CALCIFY"));
Console.WriteLine("hasSleightOfFlesh=" + allMembers.Contains("SLEIGHT_OF_FLESH"));

var build = type.GetMethod("Build", BindingFlags.Static | BindingFlags.NonPublic)!;
var exclusionType = assembly.GetType("PrismaticGemFilter.ExclusionTable", throwOnError: true)!;
var isDefaultExempt = exclusionType.GetMethod(
    "IsDefaultExempt", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
var defaultKeys = ((IEnumerable<string>)exclusionType
    .GetProperty("DefaultExemptionKeys", BindingFlags.Static | BindingFlags.Public)!
    .GetValue(null)!).ToArray();
Console.WriteLine("defaultKeys=" + defaultKeys.Length);
Console.WriteLine("staleOrbBulkUp=" + defaultKeys.Contains("orb:BULK_UP"));

foreach (var flag in new[] { true, false })
{
    var groups = (Array)build.Invoke(null, [flag])!;
    var excluded = new HashSet<string>(StringComparer.Ordinal);
    var rows = 0;

    foreach (var group in groups)
    {
        var groupType = group.GetType();
        if (!(bool)groupType.GetProperty("DefaultEnabled")!.GetValue(group)!)
            continue;

        var groupId = (string)groupType.GetProperty("Id")!.GetValue(group)!;
        foreach (var entry in (string[])groupType.GetProperty("Members")!.GetValue(group)!)
        {
            rows++;
            if (!(bool)isDefaultExempt.Invoke(null, [groupId, entry])!)
                excluded.Add(entry);
        }
    }

    Console.WriteLine($"defaults hasFool={flag} rows={rows} excluded={excluded.Count}");
}

foreach (var key in defaultKeys)
{
    var parts = key.Split(':', 2);
    var groups = (Array)build.Invoke(null, [true])!;
    var match = groups.Cast<object>().FirstOrDefault(group =>
        (string)group.GetType().GetProperty("Id")!.GetValue(group)! == parts[0]);
    var member = match is not null &&
        ((string[])match.GetType().GetProperty("Members")!.GetValue(match)!).Contains(parts[1]);
    Console.WriteLine($"defaultKeyValid={key}:{member}");
}
