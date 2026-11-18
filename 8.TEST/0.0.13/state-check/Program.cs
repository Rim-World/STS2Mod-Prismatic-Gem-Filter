using System.Reflection;

var assembly = Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, "PrismaticGemFilter.dll"));

var groupsType = assembly.GetType("PrismaticGemFilter.CardGroups", throwOnError: true)!;
var exclusionType = assembly.GetType("PrismaticGemFilter.ExclusionTable", throwOnError: true)!;
var stateType = assembly.GetType("PrismaticGemFilter.FilterState", throwOnError: true)!;

var build = groupsType.GetMethod("Build", BindingFlags.Static | BindingFlags.NonPublic)!;
var defaultKeys = ((IEnumerable<string>)exclusionType
    .GetField("DefaultExcludedKeys", BindingFlags.Static | BindingFlags.Public)!
    .GetValue(null)!).ToList();
var explicitKeys = ((IEnumerable<string>)exclusionType
    .GetProperty("ExplicitExemptionKeys", BindingFlags.Static | BindingFlags.Public)!
    .GetValue(null)!).ToList();

var isFilteredMethod = stateType.GetMethod("IsFiltered", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
var isGroupFilteredMethod = stateType.GetMethod("IsGroupFiltered", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
var countFilteredMethod = stateType.GetMethod("CountFiltered", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
var setExemptMethod = stateType.GetMethod("SetExempt", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
var setGroupMethod = stateType.GetMethod("SetGroup", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;

bool IsFiltered(IReadOnlyCollection<string> list, string entry) =>
    (bool)isFilteredMethod.Invoke(null, [list, entry])!;
bool IsGroupFiltered(IReadOnlyCollection<string> list, object group) =>
    (bool)isGroupFilteredMethod.Invoke(null, [list, group])!;
int CountFiltered(IReadOnlyCollection<string> list, object group) =>
    (int)countFilteredMethod.Invoke(null, [list, group])!;
List<string> SetExempt(IReadOnlyCollection<string> list, string entry, bool exempted) =>
    (List<string>)setExemptMethod.Invoke(null, [list, entry, exempted])!;
List<string> SetGroup(IReadOnlyCollection<string> list, object group, bool filtered) =>
    (List<string>)setGroupMethod.Invoke(null, [list, group, filtered])!;

object[] BuildGroups(bool hasFool) =>
    ((Array)build.Invoke(null, [hasFool])!).Cast<object>().ToArray();

string Id(object group) => (string)group.GetType().GetProperty("Id")!.GetValue(group)!;
string[] Members(object group) => (string[])group.GetType().GetProperty("Members")!.GetValue(group)!;

var groups111 = BuildGroups(false);
var groups113 = BuildGroups(true);
var visible111 = groups111.Where(group => Members(group).Length > 0).ToArray();
var visible113 = groups113.Where(group => Members(group).Length > 0).ToArray();

var union111 = visible111.SelectMany(Members).ToHashSet(StringComparer.Ordinal);
var union113 = visible113.SelectMany(Members).ToHashSet(StringComparer.Ordinal);
var excluded111 = union111.Count(defaultKeys.Contains);
var excluded113 = union113.Count(defaultKeys.Contains);
var active111 = visible111.Count(group => IsGroupFiltered(defaultKeys, group));
var active113 = visible113.Count(group => IsGroupFiltered(defaultKeys, group));

Console.WriteLine($"defaultKeys={defaultKeys.Count}");
Console.WriteLine($"visible111={visible111.Length} entries111={union111.Count} excluded111={excluded111} active111={active111}");
Console.WriteLine($"visible113={visible113.Length} entries113={union113.Count} excluded113={excluded113} active113={active113}");
Console.WriteLine($"staleGroupPrefix={defaultKeys.Any(key => key.Contains(':'))}");
Console.WriteLine($"defaultsContainHidden={defaultKeys.Contains("BLOT_OUT_THE_SKY")}");
Console.WriteLine($"defaultsExcludeHiddenOff={!defaultKeys.Contains("ARROW_STAB")}");

foreach (var key in explicitKeys)
    Console.WriteLine($"explicitKept={key}:{!IsFiltered(defaultKeys, key)}");

var forge = groups113.Single(group => Id(group) == "forge");
var star = groups113.Single(group => Id(group) == "star");

var shared = SetExempt(defaultKeys, "THE_SMITH", true);
var sourceUnchanged = IsFiltered(defaultKeys, "THE_SMITH");
Console.WriteLine($"exemptShared={!IsFiltered(shared, "THE_SMITH")}:{!sourceUnchanged}");
Console.WriteLine($"forgeCount={CountFiltered(shared, forge)}/{Members(forge).Length}");
Console.WriteLine($"starCount={CountFiltered(shared, star)}/{Members(star).Length}");

var forgeOff = SetGroup(shared, forge, false);
var forgeOffAllKept = Members(forge).All(entry => !IsFiltered(forgeOff, entry));
var sharedKeptFromStarView = !IsFiltered(forgeOff, "THE_SMITH") && !IsFiltered(forgeOff, "BIG_BANG");
Console.WriteLine($"forgeOffAllKept={forgeOffAllKept} sharedKept={sharedKeptFromStarView}");
Console.WriteLine($"forgeOffGroupRead={IsGroupFiltered(forgeOff, forge)}");

var starOn = SetGroup(forgeOff, star, true);
var starOnAllFiltered = Members(star).All(entry => IsFiltered(starOn, entry));
var sharedFilteredFromForgeView = IsFiltered(starOn, "THE_SMITH") && IsFiltered(starOn, "BIG_BANG");
Console.WriteLine($"starOnAllFiltered={starOnAllFiltered} sharedFiltered={sharedFilteredFromForgeView}");
Console.WriteLine($"sortedUnique={starOn.SequenceEqual(starOn.Distinct(StringComparer.Ordinal).OrderBy(key => key, StringComparer.Ordinal))}");

var cardOn = SetGroup(starOn, star, false);
Console.WriteLine($"starOffAllKept={Members(star).All(entry => !IsFiltered(cardOn, entry))}");

var failures = 0;
void Check(string name, bool condition)
{
    if (condition)
    {
        Console.WriteLine($"PASS {name}");
        return;
    }

    failures++;
    Console.WriteLine($"FAIL {name}");
}

Check("default stored excludes 121 cards", defaultKeys.Count == 121);
Check("0.111 visible excludes 99 cards", excluded111 == 99);
Check("0.113 visible excludes 121 cards", excluded113 == 121);
Check("0.111 active groups 8", active111 == 8);
Check("0.113 active groups 11", active113 == 11);
Check("no group-prefixed keys", !defaultKeys.Any(key => key.Contains(':')));
Check("hidden default card present", defaultKeys.Contains("BLOT_OUT_THE_SKY"));
Check("hidden off-group card absent", !defaultKeys.Contains("ARROW_STAB"));
Check("explicit defaults kept", explicitKeys.All(key => !IsFiltered(defaultKeys, key)));
Check("source list not mutated", sourceUnchanged);
Check("shared card exempt from both views", !IsFiltered(shared, "THE_SMITH"));
Check("forge group off keeps all", forgeOffAllKept && !IsGroupFiltered(forgeOff, forge));
Check("star group on filters all", starOnAllFiltered && IsGroupFiltered(starOn, star));
Check("shared card filtered from both views", sharedFilteredFromForgeView);
Check("group write sorted unique", starOn.SequenceEqual(starOn.Distinct(StringComparer.Ordinal).OrderBy(key => key, StringComparer.Ordinal)));
Check("star group off keeps all", Members(star).All(entry => !IsFiltered(cardOn, entry)));

Environment.ExitCode = failures == 0 ? 0 : 1;
Console.WriteLine($"failures={failures}");
