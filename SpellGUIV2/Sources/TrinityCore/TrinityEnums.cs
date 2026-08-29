using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace SpellEditor.Sources.TrinityCore
{
    public class TrinityFlag
    {
        public uint Value;
        public string Name;

        public TrinityFlag(uint value, string name)
        {
            Value = value;
            Name = name;
        }

        public override string ToString() => Name;
    }

    /// <summary>
    /// A bitmask column such as spell_proc.ProcFlags. Values from the TrinityCore headers,
    /// labels from the language files.
    /// </summary>
    public class TrinityFlagSet
    {
        private readonly uint[] _values;
        private readonly string[] _fallbackNames;
        private IReadOnlyList<TrinityFlag> _cached;
        private int _cachedGeneration = -1;

        public string Name { get; }
        public string ResourceKey { get; }

        /// <param name="values">Bit value of each flag, in the order the language file lists them.</param>
        public TrinityFlagSet(string name, string resourceKey, uint[] values, string[] fallbackNames)
        {
            if (values.Length != fallbackNames.Length)
                throw new ArgumentException($"Flag set {name} has {values.Length} values but {fallbackNames.Length} names");

            Name = name;
            ResourceKey = resourceKey;
            _values = values;
            _fallbackNames = fallbackNames;
        }

        public IReadOnlyList<TrinityFlag> Flags
        {
            get
            {
                if (_cached != null && _cachedGeneration == TrinityEnums.LocalisationGeneration)
                    return _cached;

                var names = TrinityEnums.ReadStringList(ResourceKey, _values.Length) ?? _fallbackNames;
                _cached = _values.Select((value, index) => new TrinityFlag(value, names[index])).ToList();
                _cachedGeneration = TrinityEnums.LocalisationGeneration;
                return _cached;
            }
        }

        public string Describe(uint mask)
        {
            if (mask == 0)
                return "0 - None";

            var builder = new StringBuilder();
            builder.Append(mask).Append(" - ");
            var first = builder.Length;
            var remaining = mask;
            foreach (var flag in Flags)
            {
                if (flag.Value == 0 || (mask & flag.Value) != flag.Value)
                    continue;
                if (builder.Length > first)
                    builder.Append(", ");
                builder.Append(flag.Name);
                remaining &= ~flag.Value;
            }
            if (remaining != 0)
            {
                if (builder.Length > first)
                    builder.Append(", ");
                builder.Append($"Unknown (0x{remaining:X})");
            }
            return builder.ToString();
        }
    }

    public class TrinityEnumValue
    {
        // String so it binds straight to the string backed table columns
        public string Value { get; }
        public string Label { get; }

        public TrinityEnumValue(long value, string label)
        {
            Value = value.ToString();
            Label = $"{value} - {label}";
        }

        public override string ToString() => Label;
    }

    /// <summary>A value list column. Same idea as TrinityFlagSet but exclusive, not a mask.</summary>
    public class TrinityEnumSet
    {
        private readonly long[] _values;
        private readonly string[] _fallbackNames;
        private IReadOnlyList<TrinityEnumValue> _cached;
        private int _cachedGeneration = -1;

        public string ResourceKey { get; }

        public TrinityEnumSet(string resourceKey, long[] values, string[] fallbackNames)
        {
            if (values.Length != fallbackNames.Length)
                throw new ArgumentException($"Enum set {resourceKey} has {values.Length} values but {fallbackNames.Length} names");

            ResourceKey = resourceKey;
            _values = values;
            _fallbackNames = fallbackNames;
        }

        public IReadOnlyList<TrinityEnumValue> Values
        {
            get
            {
                if (_cached != null && _cachedGeneration == TrinityEnums.LocalisationGeneration)
                    return _cached;

                var names = TrinityEnums.ReadStringList(ResourceKey, _values.Length) ?? _fallbackNames;
                _cached = _values.Select((value, index) => new TrinityEnumValue(value, names[index])).ToList();
                _cachedGeneration = TrinityEnums.LocalisationGeneration;
                return _cached;
            }
        }

        public string Describe(string rawValue)
        {
            var match = Values.FirstOrDefault(entry => entry.Value == rawValue);
            return match != null ? match.Label : rawValue;
        }
    }

    /// <summary>
    /// Mirrors the spell enums from TrinityCore 3.3.5 SpellMgr.h, SpellInfo.h and SharedDefines.h.
    /// </summary>
    public static class TrinityEnums
    {
        /// <summary>Bumped when the display language changes so cached labels are rebuilt.</summary>
        public static int LocalisationGeneration { get; private set; }

        public static void InvalidateLocalisation() => LocalisationGeneration++;

        /// <summary>
        /// Null when the key is missing or has the wrong number of entries, so an out of date
        /// translation falls back to English.
        /// </summary>
        public static string[] ReadStringList(string resourceKey, int expectedCount)
        {
            if (resourceKey == null || Application.Current == null)
                return null;

            var resource = Application.Current.TryFindResource(resourceKey) as string;
            if (string.IsNullOrWhiteSpace(resource))
                return null;

            var parts = resource.Split('|').Select(part => part.Trim()).ToArray();
            return parts.Length == expectedCount ? parts : null;
        }

        private static uint[] Bits(int count) => Enumerable.Range(0, count).Select(index => 1u << index).ToArray();

        public static readonly TrinityFlagSet SchoolMask = new TrinityFlagSet("SpellSchoolMask", "trinity_school_mask_strings",
            Bits(7),
            new[] { "Physical", "Holy", "Fire", "Nature", "Frost", "Shadow", "Arcane" });

        /// <summary>Reuses the aura editor list, which starts with a None entry before the 25 bits.</summary>
        public static readonly TrinityFlagSet ProcFlags = new TrinityFlagSet("ProcFlags", "proc_strings",
            new uint[] { 0 }.Concat(Bits(25)).ToArray(),
            new[]
            {
                "None",
                "Killed by aggressor", "Killed a target", "Melee auto attack done", "Melee auto attack taken",
                "Melee damage class spell done", "Melee damage class spell taken", "Ranged auto attack done",
                "Ranged auto attack taken", "Ranged damage class spell done", "Ranged damage class spell taken",
                "Positive none damage class spell done", "Positive none damage class spell taken",
                "Negative none damage class spell done", "Negative none damage class spell taken",
                "Positive magic damage class spell done", "Positive magic damage class spell taken",
                "Negative magic damage class spell done", "Negative magic damage class spell taken",
                "Periodic done", "Periodic taken", "Any damage taken", "Trap activation",
                "Main hand attack done", "Off hand attack done", "Death"
            });

        public static readonly TrinityFlagSet ProcSpellTypeMask = new TrinityFlagSet("ProcFlagsSpellType", "trinity_proc_spell_type_strings",
            Bits(3),
            new[] { "Damage", "Heal", "Neither damage nor heal" });

        public static readonly TrinityFlagSet ProcSpellPhaseMask = new TrinityFlagSet("ProcFlagsSpellPhase", "trinity_proc_spell_phase_strings",
            Bits(3),
            new[] { "Cast", "Hit", "Finish" });

        public static readonly TrinityFlagSet ProcHitMask = new TrinityFlagSet("ProcFlagsHit", "trinity_proc_hit_strings",
            Bits(14),
            new[]
            {
                "Normal (non critical) hit", "Critical", "Miss", "Full resist", "Dodge", "Parry",
                "Block (partial or full)", "Evade", "Immune", "Deflect", "Absorb (partial or full)",
                "Reflect", "Interrupt", "Full block"
            });

        public static readonly TrinityFlagSet ProcAttributesMask = new TrinityFlagSet("ProcAttributes", "trinity_proc_attributes_strings",
            new uint[] { 0x001, 0x002, 0x004, 0x008, 0x080, 0x100 },
            new[]
            {
                "Proc target must give exp or honor", "Can proc from triggered spells",
                "Triggering spell must have a mana cost", "Triggering spell must be affected by this aura",
                "Reduced proc chance above level 60", "Cannot proc from a spell cast by an item"
            });

        public static readonly TrinityFlagSet EffectIndexMask = new TrinityFlagSet("EffectIndexMask", "trinity_effect_mask_strings",
            Bits(3),
            new[] { "Effect 1", "Effect 2", "Effect 3" });

        public static readonly TrinityFlagSet SpellCustomAttributes = new TrinityFlagSet("SpellCustomAttributes", "trinity_custom_attr_strings",
            Bits(26),
            new[]
            {
                "Enchant proc", "Cone back", "Cone line", "Share damage", "No initial threat", "Aura is crowd control",
                "Does not break stealth", "Can crit", "Direct damage", "Charge", "Pickpocket", "Rolling periodic",
                "Negative effect 1", "Negative effect 2", "Negative effect 3", "Ignore armor",
                "Target must face caster", "Caster must be behind target", "Allow in flight target",
                "Needs ammo data", "Binary spell", "Physical school counts as magic", "Deprecated liquid aura (do not reuse)",
                "Is talent (master branch only)", "Aura cannot be saved", "Can target any private object (master branch only)"
            });

        /// <summary>Reuses the editor's spell family list, where the index is the family id.</summary>
        public static readonly TrinityEnumSet SpellFamilyNames = new TrinityEnumSet("spell_family_strings",
            Enumerable.Range(0, 19).Select(index => (long)index).ToArray(),
            new[]
            {
                "None/Generic", "Events/Holidays", "Unused", "Mage", "Warrior", "Warlock", "Priest", "Druid",
                "Rogue", "Hunter", "Paladin", "Shaman", "Unused", "Potion", "Unused", "Death Knight", "Unused",
                "Pet", "Custom"
            });

        public static readonly TrinityEnumSet SpellLinkedTypes = new TrinityEnumSet("trinity_linked_type_strings",
            new long[] { 0, 1, 2 },
            new[]
            {
                "Cast (negative effect removes instead)",
                "Hit",
                "Aura (negative effect applies immunity instead)"
            });

        /// <summary>Core defined group ids. Anything else must be 1000 or greater.</summary>
        public static readonly TrinityEnumSet CoreSpellGroups = new TrinityEnumSet("trinity_spell_group_strings",
            new long[] { 1, 2, 3, 4 },
            new[] { "Elixir Battle", "Elixir Guardian", "Elixir Unstable", "Elixir Shattrath" });

        public const uint SpellGroupCoreRangeMax = 5;
        public const uint SpellGroupDbRangeMin = 1000;

        public static bool TryParseUInt(string text, out uint value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text))
                return true;
            text = text.Trim();
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return uint.TryParse(text.Substring(2), System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out value);
            return uint.TryParse(text, out value);
        }
    }
}
