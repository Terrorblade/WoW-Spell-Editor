using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace SpellEditor.Sources.TrinityCore
{
    public enum TrinityColumnKind
    {
        UInt,
        Int,
        Float,
        Text
    }

    /// <summary>What a column gets to look at when the user adds a row.</summary>
    public class TrinityNewRowContext
    {
        public TrinityDatabase Database;
        public TrinityRankChain Chain;
        /// <summary>Rows already on screen, unsaved ones included.</summary>
        public IReadOnlyList<DataRow> Rows;

        public uint SpellId => Chain?.SpellId ?? 0;
    }

    public class TrinityColumn
    {
        public string Name;
        public TrinityColumnKind Kind = TrinityColumnKind.UInt;
        /// <summary>Part of the row identity, used in the WHERE clause on update and delete.</summary>
        public bool IsKey;
        /// <summary>Holds the spell being edited. Filled in from the selection, shown read only.</summary>
        public bool IsPrimarySpellKey;
        /// <summary>Offer the spell picker dialog on this column.</summary>
        public bool ShowSpellPicker;
        public bool AllowNull;
        public string DefaultValue = "0";
        public TrinityFlagSet Flags;
        public TrinityEnumSet Enum;
        public bool IsMultiline;
        /// <summary>From the table label list, see ApplyLocalisedText.</summary>
        public string Label;
        /// <summary>From the table tooltip list, see ApplyLocalisedText.</summary>
        public string Tooltip;
        /// <summary>Set when a negative id means something here, turning the sign into a tick box.</summary>
        public string NegativeOptionKey;
        public string FallbackNegativeOption;
        /// <summary>Override used when adding a row, e.g. the next free group id.</summary>
        public Func<TrinityNewRowContext, string> NewRowValue;

        public bool IsNumeric => Kind != TrinityColumnKind.Text;

        public bool HasNegativeOption => FallbackNegativeOption != null;

        public string NegativeOption
        {
            get
            {
                var resource = System.Windows.Application.Current?.TryFindResource(NegativeOptionKey) as string;
                return string.IsNullOrWhiteSpace(resource) ? FallbackNegativeOption : resource.Trim();
            }
        }
    }

    public class TrinityTable
    {
        public string Name;
        /// <summary>Language file key for the sub tab header.</summary>
        public string HeaderResourceKey;
        /// <summary>Language file key for the one line explanation shown above the fields.</summary>
        public string DescriptionResourceKey;
        /// <summary>Language file key, one pipe separated label per column in column order.</summary>
        public string LabelsResourceKey;
        /// <summary>Language file key, one pipe separated tooltip per column in column order.</summary>
        public string TooltipsResourceKey;
        /// <summary>Language file key for the line shown when the spell has no row.</summary>
        public string EmptyResourceKey;
        public string FallbackHeader;
        public string FallbackDescription;
        public string FallbackEmpty;
        public string[] FallbackLabels;
        public string[] FallbackTooltips;
        public IReadOnlyList<TrinityColumn> Columns;
        /// <summary>WHERE clause finding every row the core would apply to the spell.</summary>
        public Func<TrinityRankChain, string> SpellFilter;
        public string OrderBy;
        /// <summary>A row on the first rank covers every rank, so later ranks show it too.</summary>
        public bool InheritsRankChain;
        /// <summary>One row per spell at most, so the editor shows fields rather than a list.</summary>
        public bool IsSingleRow;
        /// <summary>Extra context line per row, worked out on the loading thread.</summary>
        public Func<TrinityDatabase, DataTable, IReadOnlyList<string>> DescribeRows;

        public IEnumerable<TrinityColumn> KeyColumns => Columns.Where(column => column.IsKey);

        public TrinityColumn PrimarySpellColumn => Columns.FirstOrDefault(column => column.IsPrimarySpellKey);

        public TrinityColumn FindColumn(string name) =>
            Columns.FirstOrDefault(column => column.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        public string Header => Resource(HeaderResourceKey, FallbackHeader);

        public string Description => Resource(DescriptionResourceKey, FallbackDescription);

        public string EmptyMessage => Resource(EmptyResourceKey, FallbackEmpty);

        private static string Resource(string key, string fallback)
        {
            var resource = System.Windows.Application.Current?.TryFindResource(key) as string;
            return string.IsNullOrWhiteSpace(resource) ? fallback : resource.Trim();
        }

        /// <summary>Falls back to English when a translation is missing or out of sync.</summary>
        public void ApplyLocalisedText()
        {
            var labels = TrinityEnums.ReadStringList(LabelsResourceKey, Columns.Count) ?? FallbackLabels;
            var tooltips = TrinityEnums.ReadStringList(TooltipsResourceKey, Columns.Count) ?? FallbackTooltips;
            for (var i = 0; i < Columns.Count; ++i)
            {
                Columns[i].Label = labels[i];
                Columns[i].Tooltip = tooltips[i];
            }
        }
    }

    /// <summary>
    /// The world tables the editor can edit. Columns match the TDB schema, meanings come from
    /// SpellMgr.cpp and ObjectMgr.cpp.
    /// </summary>
    public static class TrinityTables
    {
        public static readonly TrinityTable SpellProc = new TrinityTable
        {
            Name = "spell_proc",
            HeaderResourceKey = "trinity_tab_spell_proc",
            DescriptionResourceKey = "trinity_desc_spell_proc",
            LabelsResourceKey = "trinity_labels_spell_proc",
            TooltipsResourceKey = "trinity_tips_spell_proc",
            EmptyResourceKey = "trinity_empty_spell_proc",
            FallbackHeader = "Proc",
            FallbackDescription = "Controls when and how often the aura on this spell procs.",
            FallbackEmpty = "This spell has no proc data. The core falls back to the proc chance and flags in Spell.dbc.",
            IsSingleRow = true,
            InheritsRankChain = true,
            OrderBy = "`SpellId`",
            // Negative id means every rank, always written against the first, see LoadSpellProcs
            SpellFilter = chain => In("SpellId", chain.SpellId, -chain.SpellId, -chain.FirstSpellId),
            Columns = new List<TrinityColumn>
            {
                new TrinityColumn
                {
                    Name = "SpellId",
                    Kind = TrinityColumnKind.Int,
                    IsKey = true,
                    IsPrimarySpellKey = true,
                    NegativeOptionKey = "trinity_option_all_ranks",
                    FallbackNegativeOption = "Apply to every rank of this spell"
                },
                new TrinityColumn { Name = "SchoolMask", Flags = TrinityEnums.SchoolMask },
                new TrinityColumn { Name = "SpellFamilyName", Enum = TrinityEnums.SpellFamilyNames },
                new TrinityColumn { Name = "SpellFamilyMask0" },
                new TrinityColumn { Name = "SpellFamilyMask1" },
                new TrinityColumn { Name = "SpellFamilyMask2" },
                new TrinityColumn { Name = "ProcFlags", Flags = TrinityEnums.ProcFlags },
                new TrinityColumn { Name = "SpellTypeMask", Flags = TrinityEnums.ProcSpellTypeMask },
                new TrinityColumn { Name = "SpellPhaseMask", Flags = TrinityEnums.ProcSpellPhaseMask },
                new TrinityColumn { Name = "HitMask", Flags = TrinityEnums.ProcHitMask },
                new TrinityColumn { Name = "AttributesMask", Flags = TrinityEnums.ProcAttributesMask },
                new TrinityColumn { Name = "DisableEffectsMask", Flags = TrinityEnums.EffectIndexMask },
                new TrinityColumn { Name = "ProcsPerMinute", Kind = TrinityColumnKind.Float },
                new TrinityColumn { Name = "Chance", Kind = TrinityColumnKind.Float },
                new TrinityColumn { Name = "Cooldown" },
                new TrinityColumn { Name = "Charges" }
            },
            FallbackLabels = new[]
            {
                "Spell", "Required school", "Required spell family", "Family mask 1", "Family mask 2",
                "Family mask 3", "Proc flags", "Required spell type", "Proc phase", "Required hit result",
                "Proc attributes", "Effects that do not proc", "Procs per minute", "Proc chance",
                "Internal cooldown (ms)", "Charges"
            },
            FallbackTooltips = new[]
            {
                "Spell id the proc data belongs to. A negative id applies it to every rank of the spell.",
                "If not zero, the triggering spell must be of one of these schools.",
                "If not zero, the triggering spell must have this SpellFamilyName.",
                "If not zero, the triggering spell must match these SpellFamilyFlags (first 32 bits).",
                "If not zero, the triggering spell must match these SpellFamilyFlags (second 32 bits).",
                "If not zero, the triggering spell must match these SpellFamilyFlags (third 32 bits).",
                "If not zero, overrides the ProcTypeMask from Spell.dbc and decides which events can proc.",
                "If not zero, the triggering spell must do damage, healing, or neither.",
                "If not zero, the phase of the cast the proc happens on.",
                "If not zero, the hit result required for the proc. Defaults to normal and critical hits.",
                "Extra proc requirements, see ProcAttributes in SpellMgr.h.",
                "Spell effects that should not proc this aura.",
                "If not zero, proc chance is this value multiplied by the caster weapon speed over 60.",
                "If not zero, overrides the proc chance from Spell.dbc. Ignored when procs per minute is set.",
                "Internal cooldown in milliseconds between procs.",
                "If not zero, overrides the proc charges from Spell.dbc. Zero means unlimited."
            }
        };

        public static readonly TrinityTable SpellLinkedSpell = new TrinityTable
        {
            Name = "spell_linked_spell",
            HeaderResourceKey = "trinity_tab_spell_linked_spell",
            DescriptionResourceKey = "trinity_desc_spell_linked_spell",
            LabelsResourceKey = "trinity_labels_spell_linked_spell",
            TooltipsResourceKey = "trinity_tips_spell_linked_spell",
            EmptyResourceKey = "trinity_empty_spell_linked_spell",
            FallbackHeader = "Links",
            FallbackDescription = "Casts, applies, or removes another spell when this one is cast, hits, or applies its aura.",
            FallbackEmpty = "This spell is not linked to any other spell.",
            OrderBy = "`type`, `spell_effect`",
            // LoadSpellLinked keys on the exact id, nothing is inherited by a rank
            SpellFilter = chain => $"ABS(`spell_trigger`) = {chain.SpellId}",
            Columns = new List<TrinityColumn>
            {
                new TrinityColumn
                {
                    Name = "spell_trigger",
                    Kind = TrinityColumnKind.Int,
                    IsKey = true,
                    IsPrimarySpellKey = true,
                    NegativeOptionKey = "trinity_option_on_aura_removed",
                    FallbackNegativeOption = "Fire when the aura is removed"
                },
                new TrinityColumn
                {
                    Name = "spell_effect",
                    Kind = TrinityColumnKind.Int,
                    IsKey = true,
                    ShowSpellPicker = true,
                    NegativeOptionKey = "trinity_option_remove_instead",
                    FallbackNegativeOption = "Remove it, or grant immunity, instead of applying it"
                },
                new TrinityColumn { Name = "type", IsKey = true, Enum = TrinityEnums.SpellLinkedTypes },
                new TrinityColumn { Name = "comment", Kind = TrinityColumnKind.Text, DefaultValue = "", IsMultiline = true }
            },
            FallbackLabels = new[] { "Trigger spell", "Linked spell", "Fires on", "Comment" },
            FallbackTooltips = new[]
            {
                "Spell that triggers the link. A negative id means the link fires when the aura is removed.",
                "Spell that gets applied. A negative id removes that spell instead, or grants immunity for aura links.",
                "When the link fires. Cast, hit, or aura applied.",
                "Free text note, shown only in the database."
            }
        };

        public static readonly TrinityTable SpellScriptNames = new TrinityTable
        {
            Name = "spell_script_names",
            HeaderResourceKey = "trinity_tab_spell_script_names",
            DescriptionResourceKey = "trinity_desc_spell_script_names",
            LabelsResourceKey = "trinity_labels_spell_script_names",
            TooltipsResourceKey = "trinity_tips_spell_script_names",
            EmptyResourceKey = "trinity_empty_spell_script_names",
            FallbackHeader = "Scripts",
            FallbackDescription = "Attaches a C++ SpellScript or AuraScript from the server binary to this spell.",
            FallbackEmpty = "No scripts are attached to this spell.",
            InheritsRankChain = true,
            OrderBy = "`ScriptName`",
            // Same negative id convention as spell_proc, see LoadSpellScriptNames
            SpellFilter = chain => In("spell_id", chain.SpellId, -chain.SpellId, -chain.FirstSpellId),
            Columns = new List<TrinityColumn>
            {
                new TrinityColumn
                {
                    Name = "spell_id",
                    Kind = TrinityColumnKind.Int,
                    IsKey = true,
                    IsPrimarySpellKey = true,
                    NegativeOptionKey = "trinity_option_all_ranks",
                    FallbackNegativeOption = "Apply to every rank of this spell"
                },
                new TrinityColumn { Name = "ScriptName", Kind = TrinityColumnKind.Text, IsKey = true, DefaultValue = "" }
            },
            FallbackLabels = new[] { "Spell", "Script name" },
            FallbackTooltips = new[]
            {
                "Spell id the script is attached to. A negative id applies it to every rank of the spell.",
                "Name the script registers itself with in the server source."
            }
        };

        public static readonly TrinityTable SpellBonusData = new TrinityTable
        {
            Name = "spell_bonus_data",
            HeaderResourceKey = "trinity_tab_spell_bonus_data",
            DescriptionResourceKey = "trinity_desc_spell_bonus_data",
            LabelsResourceKey = "trinity_labels_spell_bonus_data",
            TooltipsResourceKey = "trinity_tips_spell_bonus_data",
            EmptyResourceKey = "trinity_empty_spell_bonus_data",
            FallbackHeader = "Bonus",
            FallbackDescription = "Overrides the spell power and attack power coefficients the core would otherwise calculate.",
            FallbackEmpty = "This spell has no coefficient override. The core works them out from the cast time and effects.",
            IsSingleRow = true,
            InheritsRankChain = true,
            OrderBy = "`entry`",
            // GetSpellBonusData falls back to the first spell in the chain
            SpellFilter = chain => In("entry", chain.SpellId, chain.FirstSpellId),
            Columns = new List<TrinityColumn>
            {
                new TrinityColumn { Name = "entry", IsKey = true, IsPrimarySpellKey = true },
                new TrinityColumn { Name = "direct_bonus", Kind = TrinityColumnKind.Float },
                new TrinityColumn { Name = "dot_bonus", Kind = TrinityColumnKind.Float },
                new TrinityColumn { Name = "ap_bonus", Kind = TrinityColumnKind.Float },
                new TrinityColumn { Name = "ap_dot_bonus", Kind = TrinityColumnKind.Float },
                new TrinityColumn { Name = "comments", Kind = TrinityColumnKind.Text, AllowNull = true, DefaultValue = "", IsMultiline = true }
            },
            FallbackLabels = new[]
            {
                "Spell", "Direct spell power", "Periodic spell power", "Direct attack power",
                "Periodic attack power", "Comment"
            },
            FallbackTooltips = new[]
            {
                "Spell id the coefficients apply to.",
                "Spell power coefficient for the direct damage or healing part.",
                "Spell power coefficient for the periodic part.",
                "Attack power coefficient for the direct damage or healing part.",
                "Attack power coefficient for the periodic part.",
                "Free text note, shown only in the database."
            }
        };

        public static readonly TrinityTable SpellGroup = new TrinityTable
        {
            Name = "spell_group",
            HeaderResourceKey = "trinity_tab_spell_group",
            DescriptionResourceKey = "trinity_desc_spell_group",
            LabelsResourceKey = "trinity_labels_spell_group",
            TooltipsResourceKey = "trinity_tips_spell_group",
            EmptyResourceKey = "trinity_empty_spell_group",
            FallbackHeader = "Groups",
            FallbackDescription = "Groups this spell belongs to. spell_group_stack_rules then decides how the members stack.",
            FallbackEmpty = "This spell is not in any group.",
            InheritsRankChain = true,
            OrderBy = "`id`",
            // GetSpellSpellGroupMapBounds looks up the first spell in the chain
            SpellFilter = chain => In("spell_id", chain.SpellId, chain.FirstSpellId),
            DescribeRows = DescribeSpellGroups,
            Columns = new List<TrinityColumn>
            {
                new TrinityColumn { Name = "id", IsKey = true, NewRowValue = NextSpellGroupId },
                new TrinityColumn { Name = "spell_id", Kind = TrinityColumnKind.Int, IsKey = true, IsPrimarySpellKey = true }
            },
            FallbackLabels = new[] { "Group id", "Spell" },
            FallbackTooltips = new[]
            {
                "Group id. Ids 1 to 4 are defined by the core, custom groups must be 1000 or greater.",
                "Spell in the group."
            }
        };

        public static readonly TrinityTable SpellThreat = new TrinityTable
        {
            Name = "spell_threat",
            HeaderResourceKey = "trinity_tab_spell_threat",
            DescriptionResourceKey = "trinity_desc_spell_threat",
            LabelsResourceKey = "trinity_labels_spell_threat",
            TooltipsResourceKey = "trinity_tips_spell_threat",
            EmptyResourceKey = "trinity_empty_spell_threat",
            FallbackHeader = "Threat",
            FallbackDescription = "Overrides how much threat this spell generates.",
            FallbackEmpty = "This spell has no threat override. Threat comes from the damage or healing done.",
            IsSingleRow = true,
            InheritsRankChain = true,
            OrderBy = "`entry`",
            // GetSpellThreatEntry falls back to the first spell in the chain
            SpellFilter = chain => In("entry", chain.SpellId, chain.FirstSpellId),
            Columns = new List<TrinityColumn>
            {
                new TrinityColumn { Name = "entry", IsKey = true, IsPrimarySpellKey = true },
                new TrinityColumn { Name = "flatMod", Kind = TrinityColumnKind.Int, AllowNull = true, DefaultValue = "" },
                new TrinityColumn { Name = "pctMod", Kind = TrinityColumnKind.Float, DefaultValue = "1" },
                new TrinityColumn { Name = "apPctMod", Kind = TrinityColumnKind.Float }
            },
            FallbackLabels = new[] { "Spell", "Flat threat", "Threat multiplier", "Attack power share" },
            FallbackTooltips = new[]
            {
                "Spell id the threat data applies to.",
                "Flat threat added on top of the damage or healing done. Leave empty for none.",
                "Threat multiplier for the damage or healing done.",
                "Extra threat taken from a share of the caster attack power."
            }
        };

        public static readonly TrinityTable SpellRanks = new TrinityTable
        {
            Name = "spell_ranks",
            HeaderResourceKey = "trinity_tab_spell_ranks",
            DescriptionResourceKey = "trinity_desc_spell_ranks",
            LabelsResourceKey = "trinity_labels_spell_ranks",
            TooltipsResourceKey = "trinity_tips_spell_ranks",
            EmptyResourceKey = "trinity_empty_spell_ranks",
            FallbackHeader = "Ranks",
            FallbackDescription = "The rank chain this spell is part of. Proc data, coefficients, threat, groups and scripts written against rank 1 apply to the whole chain.",
            FallbackEmpty = "This spell is not part of a rank chain. Talent ranks come from Talent.dbc rather than this table, so they are never listed here.",
            OrderBy = "`rank`",
            // The whole chain, a chain only makes sense read end to end
            SpellFilter = chain => $"`first_spell_id` = {chain.FirstSpellId}",
            Columns = new List<TrinityColumn>
            {
                new TrinityColumn
                {
                    Name = "first_spell_id",
                    IsKey = true,
                    ShowSpellPicker = true,
                    NewRowValue = context => context.Chain.FirstSpellId.ToString()
                },
                new TrinityColumn
                {
                    Name = "spell_id",
                    ShowSpellPicker = true,
                    NewRowValue = context => context.Chain.SpellId.ToString()
                },
                new TrinityColumn { Name = "rank", DefaultValue = "1", NewRowValue = NextRank }
            },
            FallbackLabels = new[] { "First rank", "Spell", "Rank" },
            FallbackTooltips = new[]
            {
                "Rank 1 of the chain. Every row in the same chain repeats this id.",
                "Spell that sits at this rank. Each spell can only be in one chain.",
                "Position in the chain, starting at 1. The core drops the chain if the ranks are not 1 to n with no gaps."
            }
        };

        public static readonly IReadOnlyList<TrinityTable> All = new List<TrinityTable>
        {
            SpellProc,
            SpellLinkedSpell,
            SpellScriptNames,
            SpellBonusData,
            SpellGroup,
            SpellThreat,
            SpellRanks
        };

        /// <summary>Duplicates dropped, a spell that is its own first rank must not repeat.</summary>
        private static string In(string column, params long[] ids) =>
            $"`{column}` IN ({string.Join(", ", ids.Distinct())})";

        /// <summary>Custom groups start at 1000, below that is reserved for the core.</summary>
        private static string NextSpellGroupId(TrinityNewRowContext context)
        {
            try
            {
                var existing = context.Database.QuerySingleValue("SELECT MAX(`id`) FROM `spell_group`");
                var max = existing == null ? 0u : Convert.ToUInt32(existing);
                return Math.Max(TrinityEnums.SpellGroupDbRangeMin, max + 1).ToString();
            }
            catch (Exception)
            {
                return TrinityEnums.SpellGroupDbRangeMin.ToString();
            }
        }

        /// <summary>Counts from what is on screen so adding several rows does not repeat a rank.</summary>
        private static string NextRank(TrinityNewRowContext context)
        {
            var highest = 0u;
            foreach (var row in context.Rows)
            {
                if (uint.TryParse((row["rank"] as string ?? string.Empty).Trim(), out var rank) && rank > highest)
                    highest = rank;
            }
            return (highest + 1).ToString();
        }

        /// <summary>Annotates each membership with what else is in the group, one query per group.</summary>
        private static IReadOnlyList<string> DescribeSpellGroups(TrinityDatabase database, DataTable rows)
        {
            var descriptions = new List<string>();
            foreach (DataRow row in rows.Rows)
            {
                var groupId = row["id"] as string;
                if (string.IsNullOrWhiteSpace(groupId) || !uint.TryParse(groupId, out var id))
                {
                    descriptions.Add(string.Empty);
                    continue;
                }

                try
                {
                    var members = database.Query($"SELECT `spell_id` FROM `spell_group` WHERE `id` = {id} LIMIT 200");
                    var ids = members.Rows.Cast<DataRow>().Select(member => member[0].ToString()).ToList();
                    var others = ids.Where(value => value != (row["spell_id"] as string)).ToList();
                    descriptions.Add(others.Count == 0
                        ? "This spell is the only member of the group."
                        : $"{others.Count} other member(s): {string.Join(", ", others.Take(12))}" +
                          (others.Count > 12 ? ", ..." : string.Empty));
                }
                catch (Exception)
                {
                    descriptions.Add(string.Empty);
                }
            }
            return descriptions;
        }
    }
}
