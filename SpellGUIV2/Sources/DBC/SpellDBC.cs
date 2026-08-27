using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data;
using SpellEditor.Sources.Database;

namespace SpellEditor.Sources.DBC
{
    class SpellDBC : AbstractDBC
    {
        // Description parsing resolves $12345s1 references on every keystroke, without this each
        // character typed costs a query per reference. Cleared when a spell is loaded or saved.
        private static readonly Dictionary<uint, DataRow> RecordCache = new Dictionary<uint, DataRow>();

        public Task ImportToSql(IDatabaseAdapter adapter, MainWindow.UpdateProgressFunc UpdateProgress, string bindingName, ImportExportType _type)
        {
            return ImportTo(adapter, UpdateProgress, "ID", bindingName, _type);
        }

        public static void ClearRecordCache()
        {
            lock (RecordCache)
                RecordCache.Clear();
        }

        public static DataRow GetRecordById(uint id, MainWindow mainWindows)
        {
            lock (RecordCache)
            {
                if (RecordCache.TryGetValue(id, out var cached))
                    return cached;
            }
            DataRowCollection Result = mainWindows.GetDBAdapter().Query(string.Format("SELECT * FROM `spell` WHERE `ID` = '{0}'", id)).Rows;
            var row = Result != null && Result.Count == 1 ? Result[0] : null;
            lock (RecordCache)
                RecordCache[id] = row;
            return row;
        }

        public Task Export(IDatabaseAdapter adapter, MainWindow.UpdateProgressFunc updateProgress, ImportExportType _type)
        {
            return ExportTo(adapter, updateProgress, "ID", "Spell", _type);
        }
    }
}
