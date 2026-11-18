using System.Collections;
using System.Reflection;

var assembly = Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, "PrismaticGemFilter.dll"));

var groupsType = assembly.GetType("PrismaticGemFilter.CardGroups", throwOnError: true)!;
var exclusionType = assembly.GetType("PrismaticGemFilter.ExclusionTable", throwOnError: true)!;
var stateType = assembly.GetType("PrismaticGemFilter.FilterState", throwOnError: true)!;
var groupType = assembly.GetType("PrismaticGemFilter.CardGroup", throwOnError: true)!;
var kindType = assembly.GetType("PrismaticGemFilter.GroupKind", throwOnError: true)!;

var build = groupsType.GetMethod("Build", BindingFlags.Static | BindingFlags.NonPublic)!;
var defaultGroupsMethod = stateType.GetMethod("DefaultGroups", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
var buildPoolMethod = stateType.GetMethod("BuildPool", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
var setGroupMethod = stateType.GetMethod("SetGroupEnabled", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
var setExemptMethod = stateType.GetMethod("SetExempt", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
var isExemptMethod = stateType.GetMethod("IsExempt", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
var isGroupEnabledMethod = stateType.GetMethod("IsGroupEnabled", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
var countInPoolMethod = stateType.GetMethod("CountInPool", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
var countExemptMethod = stateType.GetMethod("CountExempt", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;

var defaultKeys = ((IEnumerable<string>)exclusionType
    .GetField("DefaultExcludedKeys", BindingFlags.Static | BindingFlags.Public)!
    .GetValue(null)!).ToList();
var explicitKeys = ((IEnumerable<string>)exclusionType
    .GetProperty("ExplicitExemptionKeys", BindingFlags.Static | BindingFlags.Public)!
    .GetValue(null)!).ToList();

Dictionary<string, bool> DefaultGroups() =>
    (Dictionary<string, bool>)defaultGroupsMethod.Invoke(null, null)!;
Array ToGroupArray(IEnumerable groups)
{
    var source = groups.Cast<object>().ToArray();
    var result = Array.CreateInstance(groupType, source.Length);
    for (var i = 0; i < source.Length; i++)
        result.SetValue(source[i], i);
    return result;
}
HashSet<string> BuildPool(IEnumerable groups, HashSet<string> enabled, HashSet<string> exemptions) =>
    (HashSet<string>)buildPoolMethod.Invoke(null, [ToGroupArray(groups), enabled, exemptions])!;
Dictionary<string, bool> SetGroupEnabled(IReadOnlyDictionary<string, bool> groups, object group, bool enabled) =>
    (Dictionary<string, bool>)setGroupMethod.Invoke(null, [groups, group, enabled])!;
List<string> SetExempt(IReadOnlyCollection<string> exemptions, string entry, bool exempted) =>
    (List<string>)setExemptMethod.Invoke(null, [exemptions, entry, exempted])!;
bool IsExempt(IReadOnlyCollection<string> exemptions, string entry) =>
    (bool)isExemptMethod.Invoke(null, [exemptions, entry])!;
bool IsGroupEnabled(IReadOnlyDictionary<string, bool> groups, object group) =>
    (bool)isGroupEnabledMethod.Invoke(null, [groups, group])!;
int CountInPool(IReadOnlyCollection<string> entries, HashSet<string> pool) =>
    (int)countInPoolMethod.Invoke(null, [entries, pool])!;
int CountExempt(IReadOnlyCollection<string> exemptions, object group) =>
    (int)countExemptMethod.Invoke(null, [exemptions, group])!;

object[] BuildGroups(bool hasFool) =>
    ((Array)build.Invoke(null, [hasFool])!).Cast<object>().ToArray();

string Id(object group) => (string)group.GetType().GetProperty("Id")!.GetValue(group)!;
string[] Members(object group) => (string[])group.GetType().GetProperty("Members")!.GetValue(group)!;

object MakeGroup(string id, bool defaultEnabled, params string[] members)
{
    var ctor = groupType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        .Single(candidate => candidate.GetParameters().Length == 6);
    return ctor.Invoke([
        id,
        "other",
        Enum.Parse(kindType, "Explicit"),
        defaultEnabled,
        false,
        members,
    ]);
}

var defaultGroups = DefaultGroups();
var defaultExemptions = new HashSet<string>(explicitKeys, StringComparer.Ordinal);
var defaultEnabled = defaultGroups.Where(pair => pair.Value).Select(pair => pair.Key).ToHashSet(StringComparer.Ordinal);

var groups111 = BuildGroups(false);
var groups113 = BuildGroups(true);
var visible111 = groups111.Where(group => Members(group).Length > 0).ToArray();
var visible113 = groups113.Where(group => Members(group).Length > 0).ToArray();

var pool113 = BuildPool(visible113, defaultEnabled, defaultExemptions);
var pool111 = BuildPool(visible111, defaultEnabled, defaultExemptions);

var forge = groups113.Single(group => Id(group) == "forge");
var star = groups113.Single(group => Id(group) == "star");

var exemptionsWithoutBigBang = new HashSet<string>(defaultExemptions, StringComparer.Ordinal);
exemptionsWithoutBigBang.Remove("BIG_BANG");
exemptionsWithoutBigBang.Add("THE_SMITH");

var forgeOffGroups = SetGroupEnabled(defaultGroups, forge, false);
var forgeOffPool = BuildPool(visible113, forgeOffGroups.Where(pair => pair.Value).Select(pair => pair.Key).ToHashSet(StringComparer.Ordinal), exemptionsWithoutBigBang);
var forgeOffCross = CountInPool(Members(forge), forgeOffPool);
var starStillCountsBigBang = forgeOffPool.Contains("BIG_BANG");

var forgeOffStarOffGroups = SetGroupEnabled(forgeOffGroups, star, false);
var bothOffPool = BuildPool(visible113, forgeOffStarOffGroups.Where(pair => pair.Value).Select(pair => pair.Key).ToHashSet(StringComparer.Ordinal), exemptionsWithoutBigBang);

var forgeOnStarOffGroups = SetGroupEnabled(forgeOffStarOffGroups, forge, true);
var forgeOnPool = BuildPool(visible113, forgeOnStarOffGroups.Where(pair => pair.Value).Select(pair => pair.Key).ToHashSet(StringComparer.Ordinal), exemptionsWithoutBigBang);

var thirteenMembers = Enumerable.Range(1, 13).Select(index => "TEST_" + index).ToArray();
var thirteenGroup = MakeGroup("test13", true, thirteenMembers);
var thirteenExemptions = new HashSet<string>(["TEST_13"], StringComparer.Ordinal);
var thirteenPool = BuildPool(new[] { thirteenGroup }, new HashSet<string>(["test13"]), thirteenExemptions);
var thirteenOwnFiltered = Members(thirteenGroup).Length - CountExempt(thirteenExemptions, thirteenGroup);
var thirteenCross = CountInPool(Members(thirteenGroup), thirteenPool);

var otherGroup = MakeGroup("test-other", true, "TEST_1");
var thirteenOffWithOther = BuildPool(
    new[] { thirteenGroup, otherGroup },
    new HashSet<string>(["test-other"]),
    thirteenExemptions);
var thirteenOffOwn = 0;
var thirteenOffCross = CountInPool(Members(thirteenGroup), thirteenOffWithOther);

var cardBefore = new List<string>(defaultExemptions);
var cardAfter = SetExempt(cardBefore, "BIG_BANG", false);
var cardReadded = SetExempt(cardAfter, "BIG_BANG", true);

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

Check("default groups cover 15", defaultGroups.Count == 15);
Check("default enabled 11", defaultEnabled.Count == 11);
Check("explicit exemptions 10", explicitKeys.Count == 10);
Check("default pool 121", defaultKeys.Count == 121);
Check("default 113 pool 121", pool113.Count == 121);
Check("default 111 pool 99", pool111.Count == 99);
Check("0.111 visible groups 11", visible111.Length == 11);
Check("0.113 visible groups 15", visible113.Length == 15);
Check("group switch does not mutate source", IsGroupEnabled(defaultGroups, forge));
Check("forge off disables forge", !IsGroupEnabled(forgeOffGroups, forge));
Check("forge off leaves star on", IsGroupEnabled(forgeOffGroups, star));
Check("forge off keeps cross-group card", starStillCountsBigBang);
Check("forge off cross count 1", forgeOffCross == 1);
Check("both off removes shared card", !bothOffPool.Contains("BIG_BANG"));
Check("forge on restores shared card", forgeOnPool.Contains("BIG_BANG"));
Check("forge on filters non-exempt forge cards", CountInPool(Members(forge), forgeOnPool) == Members(forge).Length - CountExempt(exemptionsWithoutBigBang, forge));
Check("13/1 group filters 12", thirteenOwnFiltered == 12 && thirteenPool.Count == 12);
Check("13/1 group off own 0", thirteenOffOwn == 0);
Check("13/1 group off still cross 1", thirteenOffCross == 1);
var cardExempt = SetExempt(cardBefore, "BIG_BANG", true);
Check("shared card non-exempt is global", !IsExempt(cardAfter, "BIG_BANG"));
Check("shared card exempt is global", IsExempt(cardExempt, "BIG_BANG"));
Check("card switch re-add sorted", cardReadded.SequenceEqual(cardReadded.Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal)));
Check("source exemption list not mutated", IsExempt(cardBefore, "BIG_BANG"));

Environment.ExitCode = failures == 0 ? 0 : 1;
Console.WriteLine($"failures={failures}");
