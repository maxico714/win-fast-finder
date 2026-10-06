using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Wff.Engine;

namespace Wff.App;

// Tabbed production settings: General / Hotkeys / Search / Index / Advanced.
// Each section is independent: one bad section never kills the window.
public partial class SettingsWindow : Window
{
    private readonly string _path;
    private readonly Settings _settings;

    public SettingsWindow(IReadOnlyList<string>? indexedPaths, string statsLine)
    {
        InitializeComponent();
        _path = Settings.DefaultPath;
        _settings = Settings.Load(_path);
        LoadGeneral();
        LoadHotkeys();
        LoadSearch();
        LoadIndex(indexedPaths, statsLine);
        LoadAdvanced();
    }

    // ---- General ----
    private void LoadGeneral()
    {
        LaunchBox.IsChecked = _settings.LaunchAtLogin || Autostart.IsEnabled();
        SelectBox(ThemeBox, _settings.Theme);
        FullscreenBox.IsChecked = _settings.IgnoreInFullscreen;
    }

    // ---- Hotkeys ----
    private void LoadHotkeys()
    {
        HotkeyBox.Text = _settings.MainHotkey;
        DoubleTapBox.IsChecked = _settings.CtrlDoubleTap;
    }

    // ---- Search ----
    private void LoadSearch()
    {
        SelectBox(MaxBox, _settings.MaxResults.ToString());
        PathBox.IsChecked = _settings.MatchPath;
        SelectBox(SortBox, _settings.SortMode);
        HiddenBox.IsChecked = _settings.HideHiddenAndSystem;
    }

    private static void SelectBox(ComboBox box, string value)
    {
        foreach (ComboBoxItem item in box.Items)
        {
            if ((item.Content as string)?.Equals(value, StringComparison.OrdinalIgnoreCase) == true)
            {
                box.SelectedItem = item;
                return;
            }
        }
        box.SelectedIndex = 0;
    }

    // ---- Index ----
    private void LoadIndex(IReadOnlyList<string>? indexedPaths, string statsLine)
    {
        StatsLine.Text = statsLine;
        try { BuildDriveBoxes(); }
        catch (Exception ex) { Logger.Info("drive list failed: " + ex.Message); }
        FolderList.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<string>(
            _settings.Blacklist);
        WhiteList.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<string>(
            _settings.WhitelistRoots);
        try { BuildExtensionBoxes(indexedPaths); }
        catch (Exception ex) { Logger.Info("extension list failed: " + ex.Message); }
    }

    private void BuildDriveBoxes()
    {
        DrivePanel.Children.Clear();
        foreach (var d in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed))
        {
            string root, label;
            try
            {
                if (!d.IsReady)
                    continue;
                root = d.RootDirectory.FullName;
                label = $"{root}  ({d.VolumeLabel} {d.TotalSize / 1073741824} GB)";
            }
            catch
            {
                continue;
            }
            var box = new CheckBox
            {
                Content = label,
                Tag = root,
                IsChecked = _settings.IsDriveIgnored(root),
                Margin = new Thickness(0, 2, 0, 2),
            };
            DrivePanel.Children.Add(box);
        }
    }

    private void BuildExtensionBoxes(IReadOnlyList<string>? indexedPaths)
    {
        ExtPanel.Children.Clear();
        if (indexedPaths == null || indexedPaths.Count == 0)
        {
            ExtEmpty.Visibility = Visibility.Visible;
            return;
        }
        var ignored = new HashSet<string>(
            _settings.IgnoredExtensions.Select(e => e.StartsWith('.') ? e.ToLowerInvariant() : "." + e.ToLowerInvariant()));
        foreach (var (ext, count) in ExtensionScan.TopExtensions(indexedPaths, 200))
        {
            var box = new CheckBox
            {
                Content = $"{ext}  ({count:N0} files)",
                Tag = ext,
                IsChecked = ignored.Contains(ext),
                Margin = new Thickness(0, 1, 0, 1),
            };
            ExtPanel.Children.Add(box);
        }
    }

    private void FolderAdd_Click(object sender, RoutedEventArgs e) =>
        AddValidated(FolderBox, FolderError, (System.Collections.ObjectModel.ObservableCollection<string>)FolderList.ItemsSource);

    private void RootAdd_Click(object sender, RoutedEventArgs e) =>
        AddValidated(RootBox, RootError, (System.Collections.ObjectModel.ObservableCollection<string>)WhiteList.ItemsSource);

    private static void AddValidated(TextBox box, TextBlock error, System.Collections.ObjectModel.ObservableCollection<string> list)
    {
        string v = box.Text.Trim().Trim('"');
        error.Visibility = Visibility.Collapsed;
        if (v.Length == 0)
            return;
        if (!Directory.Exists(v))
        {
            error.Text = "Folder not found — check the full address and try again.";
            error.Visibility = Visibility.Visible;
            return;
        }
        if (!list.Contains(v, StringComparer.OrdinalIgnoreCase))
            list.Add(v);
        box.Clear();
    }

    private void FolderRemove_Click(object sender, RoutedEventArgs e)
    {
        if (FolderList.SelectedItem is string s)
            ((System.Collections.ObjectModel.ObservableCollection<string>)FolderList.ItemsSource).Remove(s);
    }

    private void RootRemove_Click(object sender, RoutedEventArgs e)
    {
        if (WhiteList.SelectedItem is string s)
            ((System.Collections.ObjectModel.ObservableCollection<string>)WhiteList.ItemsSource).Remove(s);
    }

    // ---- Advanced ----
    private void LoadAdvanced()
    {
        VersionLine.Text = "win-fast-finder 0.8.0 — MIT — zero dependencies";
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            FileName = "win-fast-finder-backup.json",
            Filter = "JSON (*.json)|*.json",
        };
        if (dlg.ShowDialog() == true)
        {
            try { Settings.Export(Collect(), dlg.FileName); }
            catch (Exception ex) { ShowImportError(ex.Message); }
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "JSON (*.json)|*.json" };
        if (dlg.ShowDialog() != true)
            return;
        try
        {
            var s = Settings.Import(dlg.FileName);
            Settings.Save(s, _path);
            ImportError.Visibility = Visibility.Collapsed;
            Close(); // reopen to see imported values
        }
        catch (Exception ex)
        {
            ShowImportError(ex.Message);
        }
    }

    private void ShowImportError(string msg)
    {
        ImportError.Text = msg;
        ImportError.Visibility = Visibility.Visible;
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        Settings.Save(new Settings(), _path);
        Close();
    }

    private void DataFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            Process.Start(new ProcessStartInfo(Path.GetDirectoryName(_path)!) { UseShellExecute = true });
        }
        catch { }
    }

    private void ViewLog_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var log = Logger.DefaultPath;
            if (File.Exists(log))
                Process.Start(new ProcessStartInfo("notepad.exe", $"\"{log}\"") { UseShellExecute = false });
        }
        catch { }
    }

    // ---- Save ----
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!Hotkey.TryParse(HotkeyBox.Text, out _, out _))
        {
            HotkeyError.Text = "Hotkey needs a modifier, e.g. Ctrl+Shift+F.";
            HotkeyError.Visibility = Visibility.Visible;
            return;
        }
        var s = Collect();
        Settings.Save(s, _path);
        Autostart.SetEnabled(s.LaunchAtLogin, Autostart.ExePath);
        Close();
    }

    private Settings Collect()
    {
        var drives = new List<string>();
        foreach (CheckBox box in DrivePanel.Children)
            if (box.IsChecked == true && box.Tag is string root)
                drives.Add(root);
        var exts = new List<string>();
        foreach (CheckBox box in ExtPanel.Children)
            if (box.IsChecked == true && box.Tag is string ext)
                exts.Add(ext);
        int max = 50;
        if (MaxBox.SelectedItem is ComboBoxItem mi && int.TryParse(mi.Content as string, out int m))
            max = m;
        string sort = (SortBox.SelectedItem as ComboBoxItem)?.Content as string ?? "rank";
        string theme = (ThemeBox.SelectedItem as ComboBoxItem)?.Content as string ?? "System";
        return new Settings
        {
            LaunchAtLogin = LaunchBox.IsChecked == true,
            Theme = theme,
            IgnoreInFullscreen = FullscreenBox.IsChecked == true,
            MainHotkey = HotkeyBox.Text.Trim(),
            CtrlDoubleTap = DoubleTapBox.IsChecked == true,
            MaxResults = max,
            MatchPath = PathBox.IsChecked == true,
            SortMode = sort,
            HideHiddenAndSystem = HiddenBox.IsChecked == true,
            Blacklist = ((System.Collections.ObjectModel.ObservableCollection<string>)FolderList.ItemsSource).ToList(),
            WhitelistRoots = ((System.Collections.ObjectModel.ObservableCollection<string>)WhiteList.ItemsSource).ToList(),
            IgnoredDrives = drives,
            IgnoredExtensions = exts,
        };
    }
}
