using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SpellEditor.Sources.TrinityCore
{
    /// <summary>
    /// Puts the TrinityCore world tables into the main tab strip and owns the connection.
    /// </summary>
    public class TrinityIntegration
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly MainWindow _mainWindow;
        private readonly TabControl _host;
        private readonly List<TabItem> _tabs = new List<TabItem>();
        private readonly List<TrinityTableEditor> _editors = new List<TrinityTableEditor>();

        private readonly object _connectLock = new object();

        private TrinityDatabase _database;
        private List<string> _missingTables = new List<string>();
        private uint _spellId;
        private bool _spellSelected;
        private bool _connecting;
        private bool _probedTables;
        private string _lastConnectError;

        public TrinityIntegration(MainWindow mainWindow, TabControl host)
        {
            _mainWindow = mainWindow;
            _host = host;
        }

        public bool Owns(object tab) => tab is TabItem item && _tabs.Contains(item);

        public void UpdateTabs(bool spellSelected)
        {
            _spellSelected = spellSelected;
            UpdateTabs();
        }

        private void UpdateTabs()
        {
            if (!TrinityDatabase.IsConfigured || !_spellSelected)
            {
                foreach (var tab in _tabs)
                    tab.Visibility = Visibility.Collapsed;
                if (_host.SelectedItem is TabItem selected && _tabs.Contains(selected))
                    _host.SelectedIndex = 0;
                return;
            }

            if (_tabs.Count == 0)
                BuildTabs();

            foreach (var tab in _tabs)
                tab.Visibility = IsAvailable(tab) ? Visibility.Visible : Visibility.Collapsed;
        }

        private bool IsAvailable(TabItem tab) =>
            !_missingTables.Contains(((TrinityTable)tab.Tag).Name, StringComparer.OrdinalIgnoreCase);

        private void BuildTabs()
        {
            foreach (var table in TrinityTables.All)
            {
                var editor = new TrinityTableEditor(table, _mainWindow);
                editor.SetSpell(_spellId);
                editor.Saved += () => InvalidateOthers(editor);
                _editors.Add(editor);

                var tab = new TabItem
                {
                    Header = table.Header,
                    Content = editor,
                    Tag = table
                };
                _tabs.Add(tab);
                _host.Items.Add(tab);
            }
        }

        public async void Activate(object tab)
        {
            if (!(tab is TabItem item) || !(item.Content is TrinityTableEditor editor))
                return;

            if (!TrinityDatabase.IsConfigured)
            {
                editor.ShowMessage(Localise("trinity_disabled",
                    "The TrinityCore integration is turned off. Turn it on and enter your world " +
                    "database details in the settings."), false);
                return;
            }

            if (_database == null || !_probedTables)
            {
                await ConnectAsync(editor);
                if (_database == null)
                    return;
            }

            editor.Refresh(_spellId);
        }

        /// <summary>Connects on first use. Null means the world database is off or unreachable.</summary>
        public TrinityDatabase EnsureDatabase()
        {
            if (_database != null)
                return _database;
            if (!TrinityDatabase.IsConfigured)
                return null;

            lock (_connectLock)
            {
                if (_database != null)
                    return _database;
                try
                {
                    var database = TrinityDatabase.FromConfig();
                    database.TestConnection();
                    _database = database;
                    foreach (var current in _editors)
                        current.SetDatabase(database);
                }
                catch (Exception exception)
                {
                    Logger.Error(exception, "Failed to connect to the TrinityCore world database");
                    _lastConnectError = exception.Message;
                }
                return _database;
            }
        }

        /// <summary>Editing spell_ranks changes which rows the other tabs inherit.</summary>
        private void InvalidateOthers(TrinityTableEditor source)
        {
            foreach (var editor in _editors)
            {
                if (editor != source)
                    editor.Invalidate();
            }
        }

        public void SetSpell(uint spellId)
        {
            _spellId = spellId;
            foreach (var editor in _editors)
                editor.SetSpell(spellId);

            // The rest catch up when they are opened
            if (_database != null && _host.SelectedItem is TabItem selected && _tabs.Contains(selected) &&
                selected.Content is TrinityTableEditor editor2)
                editor2.Refresh(spellId);
        }

        public void RefreshLocalisation()
        {
            foreach (var tab in _tabs)
                tab.Header = ((TrinityTable)tab.Tag).Header;
            foreach (var editor in _editors)
                editor.RefreshLocalisation();
        }

        private async Task ConnectAsync(TrinityTableEditor editor)
        {
            if (_connecting)
                return;

            _connecting = true;
            editor.ShowMessage(Localise("trinity_connecting", "Connecting..."), false);
            try
            {
                _missingTables = await Task.Run(() =>
                {
                    var database = EnsureDatabase();
                    if (database == null)
                        throw new Exception(_lastConnectError ?? "the world database could not be reached");
                    return database.FindMissingTables(TrinityTables.All.Select(table => table.Name));
                });
                _probedTables = true;

                    UpdateTabs();
                if (_missingTables.Count > 0)
                    Logger.Info($"TrinityCore tables not present in the world database: {string.Join(", ", _missingTables)}");
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "Failed to connect to the TrinityCore world database");
                _database = null;
                foreach (var current in _editors)
                    current.SetDatabase(null);
                editor.ShowMessage(string.Format(Localise("trinity_connect_failed", "Could not connect: {0}"),
                    exception.Message), true);
            }
            finally
            {
                _connecting = false;
            }
        }

        private static string Localise(string key, string fallback)
        {
            var resource = Application.Current?.TryFindResource(key) as string;
            return string.IsNullOrWhiteSpace(resource) ? fallback : resource.Trim();
        }
    }
}
