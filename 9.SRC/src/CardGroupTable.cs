// 本文件由 TOOLS/scripts/build-card-group-table.py 生成，不要手改。
// 输入：TMP/0.0.1/卡牌名单/卡牌分组名单.csv、多人卡牌组.csv

namespace PrismaticGemFilter;

/// <summary>
///     分组与卡牌标识的对照表。每行是「分组标识、卡牌标识、卡池类名」。
///     <para>
///         卡池类名只用于启动自检与文档核对，运行期的归属判定按玩家卡池的实际成员集合进行。
///     </para>
/// </summary>
internal static class CardGroupTable
{
    /// <summary>全部行。顺序与 <see cref="CardGroups.All" /> 的分组顺序一致。</summary>
    public static readonly (string Group, string Entry, string Pool)[] Rows =
    [
        // osty：10 张
        ("osty", "BONE_SHARDS", "NecrobinderCardPool"),
        ("osty", "FETCH", "NecrobinderCardPool"),
        ("osty", "FLATTEN", "NecrobinderCardPool"),
        ("osty", "HIGH_FIVE", "NecrobinderCardPool"),
        ("osty", "POKE", "NecrobinderCardPool"),
        ("osty", "RATTLE", "NecrobinderCardPool"),
        ("osty", "RIGHT_HAND_HAND", "NecrobinderCardPool"),
        ("osty", "SIC_EM", "NecrobinderCardPool"),
        ("osty", "SNAP", "NecrobinderCardPool"),
        ("osty", "SQUEEZE", "NecrobinderCardPool"),

        // summon：8 张
        ("summon", "AFTERLIFE", "NecrobinderCardPool"),
        ("summon", "CLEANSE", "NecrobinderCardPool"),
        ("summon", "DIRGE", "NecrobinderCardPool"),
        ("summon", "INVOKE", "NecrobinderCardPool"),
        ("summon", "NECRO_MASTERY", "NecrobinderCardPool"),
        ("summon", "PULL_AGGRO", "NecrobinderCardPool"),
        ("summon", "REANIMATE", "NecrobinderCardPool"),
        ("summon", "SPUR", "NecrobinderCardPool"),

        // doom：13 张
        ("doom", "BLIGHT_STRIKE", "NecrobinderCardPool"),
        ("doom", "COUNTDOWN", "NecrobinderCardPool"),
        ("doom", "DEATHBRINGER", "NecrobinderCardPool"),
        ("doom", "DEATHS_DOOR", "NecrobinderCardPool"),
        ("doom", "END_OF_DAYS", "NecrobinderCardPool"),
        ("doom", "NEGATIVE_PULSE", "NecrobinderCardPool"),
        ("doom", "NEUROSURGE", "NecrobinderCardPool"),
        ("doom", "NO_ESCAPE", "NecrobinderCardPool"),
        ("doom", "OBLIVION", "NecrobinderCardPool"),
        ("doom", "REAPER_FORM", "NecrobinderCardPool"),
        ("doom", "SCOURGE", "NecrobinderCardPool"),
        ("doom", "SHROUD", "NecrobinderCardPool"),
        ("doom", "TIMES_UP", "NecrobinderCardPool"),

        // orb：25 张
        ("orb", "BALL_LIGHTNING", "DefectCardPool"),
        ("orb", "BULK_UP", "DefectCardPool"),
        ("orb", "CAPACITOR", "DefectCardPool"),
        ("orb", "CHAOS", "DefectCardPool"),
        ("orb", "CHILL", "DefectCardPool"),
        ("orb", "COLD_SNAP", "DefectCardPool"),
        ("orb", "CONSUMING_SHADOW", "DefectCardPool"),
        ("orb", "COOLHEADED", "DefectCardPool"),
        ("orb", "DARKNESS", "DefectCardPool"),
        ("orb", "FUSION", "DefectCardPool"),
        ("orb", "GLACIER", "DefectCardPool"),
        ("orb", "GLASSWORK", "DefectCardPool"),
        ("orb", "ICE_LANCE", "DefectCardPool"),
        ("orb", "METEOR_STRIKE", "DefectCardPool"),
        ("orb", "MODDED", "DefectCardPool"),
        ("orb", "MULTI_CAST", "DefectCardPool"),
        ("orb", "NULL", "DefectCardPool"),
        ("orb", "RAINBOW", "DefectCardPool"),
        ("orb", "REFRACT", "DefectCardPool"),
        ("orb", "SHADOW_SHIELD", "DefectCardPool"),
        ("orb", "SHATTER", "DefectCardPool"),
        ("orb", "SPINNER", "DefectCardPool"),
        ("orb", "TEMPEST", "DefectCardPool"),
        ("orb", "TESLA_COIL", "DefectCardPool"),
        ("orb", "VOLTAIC", "DefectCardPool"),

        // focus：5 张
        ("focus", "DEFRAGMENT", "DefectCardPool"),
        ("focus", "FOCUSED_STRIKE", "DefectCardPool"),
        ("focus", "HOTFIX", "DefectCardPool"),
        ("focus", "HYPERBEAM", "DefectCardPool"),
        ("focus", "SYNCHRONIZE", "DefectCardPool"),

        // sly：9 张
        ("sly", "ABRASIVE", "SilentCardPool"),
        ("sly", "FLICK_FLACK", "SilentCardPool"),
        ("sly", "HAND_TRICK", "SilentCardPool"),
        ("sly", "MASTER_PLANNER", "SilentCardPool"),
        ("sly", "MEMENTO_MORI", "SilentCardPool"),
        ("sly", "REFLEX", "SilentCardPool"),
        ("sly", "RICOCHET", "SilentCardPool"),
        ("sly", "TACTICIAN", "SilentCardPool"),
        ("sly", "UNTOUCHABLE", "SilentCardPool"),

        // shiv：12 张
        ("shiv", "ACCURACY", "SilentCardPool"),
        ("shiv", "BLADE_DANCE", "SilentCardPool"),
        ("shiv", "BLADE_OF_INK", "SilentCardPool"),
        ("shiv", "CLOAK_AND_DAGGER", "SilentCardPool"),
        ("shiv", "FAN_OF_KNIVES", "SilentCardPool"),
        ("shiv", "HIDDEN_DAGGERS", "SilentCardPool"),
        ("shiv", "INFINITE_BLADES", "SilentCardPool"),
        ("shiv", "KNIFE_TRAP", "SilentCardPool"),
        ("shiv", "LEADING_STRIKE", "SilentCardPool"),
        ("shiv", "PHANTOM_BLADES", "SilentCardPool"),
        ("shiv", "STORM_OF_STEEL", "SilentCardPool"),
        ("shiv", "UP_MY_SLEEVE", "SilentCardPool"),

        // poison：12 张
        ("poison", "ACCELERANT", "SilentCardPool"),
        ("poison", "BOUNCING_FLASK", "SilentCardPool"),
        ("poison", "BUBBLE_BUBBLE", "SilentCardPool"),
        ("poison", "CORROSIVE_WAVE", "SilentCardPool"),
        ("poison", "DEADLY_POISON", "SilentCardPool"),
        ("poison", "ENVENOM", "SilentCardPool"),
        ("poison", "HAZE", "SilentCardPool"),
        ("poison", "MIRAGE", "SilentCardPool"),
        ("poison", "NOXIOUS_FUMES", "SilentCardPool"),
        ("poison", "OUTBREAK", "SilentCardPool"),
        ("poison", "POISONED_STAB", "SilentCardPool"),
        ("poison", "SNAKEBITE", "SilentCardPool"),

        // forge：11 张
        ("forge", "BEAT_INTO_SHAPE", "RegentCardPool"),
        ("forge", "BIG_BANG", "RegentCardPool"),
        ("forge", "BULWARK", "RegentCardPool"),
        ("forge", "CONQUEROR", "RegentCardPool"),
        ("forge", "FURNACE", "RegentCardPool"),
        ("forge", "REFINE_BLADE", "RegentCardPool"),
        ("forge", "SEEKING_EDGE", "RegentCardPool"),
        ("forge", "SPOILS_OF_BATTLE", "RegentCardPool"),
        ("forge", "SUMMON_FORTH", "RegentCardPool"),
        ("forge", "THE_SMITH", "RegentCardPool"),
        ("forge", "WROUGHT_IN_WAR", "RegentCardPool"),

        // star：27 张
        ("star", "ALIGNMENT", "RegentCardPool"),
        ("star", "ASTRAL_PULSE", "RegentCardPool"),
        ("star", "BIG_BANG", "RegentCardPool"),
        ("star", "CLOAK_OF_STARS", "RegentCardPool"),
        ("star", "COMET", "RegentCardPool"),
        ("star", "CONVERGENCE", "RegentCardPool"),
        ("star", "CRESCENT_SPEAR", "RegentCardPool"),
        ("star", "DECISIONS_DECISIONS", "RegentCardPool"),
        ("star", "DEVASTATE", "RegentCardPool"),
        ("star", "DYING_STAR", "RegentCardPool"),
        ("star", "GAMMA_BLAST", "RegentCardPool"),
        ("star", "GATHER_LIGHT", "RegentCardPool"),
        ("star", "GLOW", "RegentCardPool"),
        ("star", "GUIDING_STAR", "RegentCardPool"),
        ("star", "HIDDEN_CACHE", "RegentCardPool"),
        ("star", "KNOCKOUT_BLOW", "RegentCardPool"),
        ("star", "NEUTRON_AEGIS", "RegentCardPool"),
        ("star", "PARTICLE_WALL", "RegentCardPool"),
        ("star", "QUASAR", "RegentCardPool"),
        ("star", "RADIATE", "RegentCardPool"),
        ("star", "REFLECT", "RegentCardPool"),
        ("star", "RESONANCE", "RegentCardPool"),
        ("star", "ROYAL_GAMBLE", "RegentCardPool"),
        ("star", "SEVEN_STARS", "RegentCardPool"),
        ("star", "SHINING_STRIKE", "RegentCardPool"),
        ("star", "SOLAR_STRIKE", "RegentCardPool"),
        ("star", "THE_SMITH", "RegentCardPool"),

        // arrow：13 张
        ("arrow", "ARROW_STAB", "FoolCardPool"),
        ("arrow", "BLOSSOM_SHOT", "FoolCardPool"),
        ("arrow", "FLORESCENCE", "FoolCardPool"),
        ("arrow", "HEAVY_SHOT", "FoolCardPool"),
        ("arrow", "HOBBLING_SHOT", "FoolCardPool"),
        ("arrow", "LOCKED_IN", "FoolCardPool"),
        ("arrow", "MELODIC_SHOT", "FoolCardPool"),
        ("arrow", "QUICK_SHOT", "FoolCardPool"),
        ("arrow", "RAIN_SHOT", "FoolCardPool"),
        ("arrow", "RAPID_SHOT", "FoolCardPool"),
        ("arrow", "REPEAT_SHOT", "FoolCardPool"),
        ("arrow", "SKY_SHOT", "FoolCardPool"),
        ("arrow", "TRICK_SHOT", "FoolCardPool"),

        // impale：10 张
        ("impale", "BOW_SMACK", "FoolCardPool"),
        ("impale", "EPIC_RETRIEVAL", "FoolCardPool"),
        ("impale", "EXPLOSIVE_ARROWS", "FoolCardPool"),
        ("impale", "KI_HOP", "FoolCardPool"),
        ("impale", "NAIL", "FoolCardPool"),
        ("impale", "RECALL", "FoolCardPool"),
        ("impale", "SERRATED_ARROWS", "FoolCardPool"),
        ("impale", "UPROOT", "FoolCardPool"),
        ("impale", "WAH_HAMMER", "FoolCardPool"),
        ("impale", "WREST", "FoolCardPool"),

        // multiplayer：10 张
        ("multiplayer", "LEGION_OF_BONE", "NecrobinderCardPool"),
        ("multiplayer", "UNDERWORLD", "NecrobinderCardPool"),
        ("multiplayer", "HIBERNATE", "DefectCardPool"),
        ("multiplayer", "IGNITION", "DefectCardPool"),
        ("multiplayer", "BLADE_SYMPHONY", "SilentCardPool"),
        ("multiplayer", "CONCOCT", "SilentCardPool"),
        ("multiplayer", "SNEAKY", "SilentCardPool"),
        ("multiplayer", "CONSTELLATION", "RegentCardPool"),
        ("multiplayer", "HAMMER_TIME", "RegentCardPool"),
        ("multiplayer", "COVERING_FIRE", "FoolCardPool"),

        // bugged：1 张
        ("bugged", "BLOT_OUT_THE_SKY", "FoolCardPool"),
    ];
}
