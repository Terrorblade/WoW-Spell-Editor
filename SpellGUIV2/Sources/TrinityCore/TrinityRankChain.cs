using NLog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;

namespace SpellEditor.Sources.TrinityCore
{
    /// <summary>One row of spell_ranks.</summary>
    public class TrinityRank
    {
        public uint SpellId;
        public uint Rank;
    }

    /// <summary>
    /// The rank chain a spell belongs to, from spell_ranks. Most server side tables are only
    /// filled in against the first rank and the core applies the row to the whole chain, see
    /// SpellMgr::GetFirstSpellInChain. Talent ranks come from Talent.dbc instead.
    /// </summary>
    public class TrinityRankChain
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly IReadOnlyList<TrinityRank> NoRanks = new List<TrinityRank>();

        public uint SpellId { get; }
        /// <summary>First rank, or the spell itself when it is not in a chain.</summary>
        public uint FirstSpellId { get; }
        /// <summary>Zero when the spell is not in a chain.</summary>
        public uint Rank { get; }
        /// <summary>Every rank in order, empty when the spell is not in a chain.</summary>
        public IReadOnlyList<TrinityRank> Ranks { get; }

        /// <summary>The core throws away chains shorter than two ranks, see LoadSpellRanks.</summary>
        public bool HasChain => Ranks.Count > 1;

        public bool IsFirstRank => FirstSpellId == SpellId;

        public TrinityRankChain(uint spellId, IEnumerable<TrinityRank> ranks = null)
        {
            var ordered = (ranks ?? Enumerable.Empty<TrinityRank>())
                .OrderBy(entry => entry.Rank)
                .ToList();
            if (ordered.Count < 2)
                ordered.Clear();

            SpellId = spellId;
            Ranks = ordered.Count == 0 ? NoRanks : ordered;
            FirstSpellId = ordered.Count == 0 ? spellId : ordered[0].SpellId;
            Rank = ordered.FirstOrDefault(entry => entry.SpellId == spellId)?.Rank ?? 0;
        }

        public uint RankOf(uint spellId) =>
            Ranks.FirstOrDefault(entry => entry.SpellId == spellId)?.Rank ?? 0;

        public static TrinityRankChain Load(TrinityDatabase database, uint spellId)
        {
            if (database == null || spellId == 0)
                return new TrinityRankChain(spellId);

            try
            {
                var rows = database.Query(
                    "SELECT other.`spell_id`, other.`rank` FROM `spell_ranks` AS mine " +
                    "JOIN `spell_ranks` AS other ON other.`first_spell_id` = mine.`first_spell_id` " +
                    $"WHERE mine.`spell_id` = {spellId} ORDER BY other.`rank`");

                var ranks = new List<TrinityRank>();
                foreach (DataRow row in rows.Rows)
                {
                    ranks.Add(new TrinityRank
                    {
                        SpellId = Convert.ToUInt32(row[0], CultureInfo.InvariantCulture),
                        Rank = Convert.ToUInt32(row[1], CultureInfo.InvariantCulture)
                    });
                }
                return new TrinityRankChain(spellId, ranks);
            }
            catch (Exception exception)
            {
                Logger.Warn(exception, $"Could not read the rank chain for spell {spellId}");
                return new TrinityRankChain(spellId);
            }
        }
    }
}
