using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Wff.Engine;

namespace Wff.App;

// Borderless popup: global hotkey -> debounced background search -> top 50.
public partial class MainWindow : Window
{
    private FileIndex? _index;
    private readonly DispatcherTimer _debounce;
    private int _queryId;
    private const int HOTKEY_ID = 0xB001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint VK_F = 0x46;
    private KeyboardHook? _ctrlHook;
    private bool _indexStarted;
    private bool _wndHooked;
    private bool _dialogOpen; // a modal dialog owns focus: don't auto-hide
    private List<string>? _indexedPaths;
    private Settings _settings = new();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public MainWindow()
    {
        InitializeComponent();
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _debounce.Tick += (_, _) => { _debounce.Stop(); RunSearch(QueryBox.Text, ++_queryId); };
        Loaded += (_, _) => EnsureReady();
        // Click outside dismisses the popup (but not while a dialog is open).
        Deactivated += (_, _) =>
        {
            if (!_dialogOpen && IsVisible)
                Hide();
        };
        Closed += (_, _) => UnregisterHotKey();
    }

    public void SetIndex(FileIndex index) => _index = index;

    // Idempotent startup: callable from Loaded AND from App.OnStartup.
    // (A never-shown Hidden window cannot be trusted to raise Loaded.)
    public void EnsureReady()
    {
        if (_indexStarted)
            return;
        _indexStarted = true;
        try
        {
            _settings = Settings.Load();
            Theme.Apply(_settings.Theme);
            Logger.Info("app started");
            // A never-shown Hidden window cannot own an HWND ("Hwnd of zero"):
            // show it once far off-screen (no flash), wire up, then hide.
            Left = -32000;
            Top = -32000;
            Show();
            RegisterHotKey();
            bool firstRun = !File.Exists(Path.Combine(AppDir(), "cache.bin"));
            StartBackgroundIndex();
            if (firstRun)
                Summon(); // first run: show progress; afterwards stay hidden until hotkey
            else
                Hide();
        }
        catch (Exception ex)
        {
            Logger.Info("startup failed: " + ex.ToString().Replace(Environment.NewLine, " | "));
        }
    }

    // Every summon path lands here: upper-center placement, visible +
    // foreground + caret in box. 22% from top: above center, clear of edge.
    public void Summon()
    {
        try
        {
            Logger.Info("summon enter");
            if (_settings.IgnoreInFullscreen && Fullscreen.IsForegroundFullscreen())
            {
                Logger.Info("summon skipped: fullscreen foreground");
                return;
            }
            var work = SystemParameters.WorkArea;
            Left = work.Left + (work.Width - Width) / 2;
            Top = work.Top + work.Height * 0.22;
            Logger.Info($"summon pre-show title='{Title}' visible={IsVisible} L={Left} T={Top}");
            if (!IsVisible)
                Show();
            Logger.Info($"summon post-show visible={IsVisible} handle={new WindowInteropHelper(this).Handle}");
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;
            Activate();
            Topmost = true; // re-assert: another topmost window may have covered us
            QueryBox.Clear(); // always start empty: only the search box shows
            Dispatcher.BeginInvoke(() =>
            {
                QueryBox.Focus();
                System.Windows.Input.Keyboard.Focus(QueryBox);
            }, DispatcherPriority.ApplicationIdle);
        }
        catch (Exception ex)
        {
            Logger.Info("summon failed: " + ex.Message);
        }
    }

    public void StartBackgroundIndex(bool forceFull = false)
    {
        Task.Run(() =>
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var settings = Settings.Load();
                var roots = settings.StartupRoots();
                Logger.Info($"startup roots=[{string.Join(";", roots)}] blacklist=[{string.Join(",", settings.Blacklist)}] forceFull={forceFull}");
                var dir = AppDir();
                var cachePath = Path.Combine(dir, "cache.bin");
                var snapPath = Path.Combine(dir, "cache.mtimes");
                List<string> files;
                if (!forceFull && File.Exists(cachePath) && File.Exists(snapPath))
                {
                    try
                    {
                        var old = Cache.Load(cachePath);
                        var snap = SnapshotStore.Load(snapPath);
                        ShowStatus("checking for new files…");
                        files = Incremental.Refresh(old, snap, roots, settings.Blacklist,
                            d => ShowStatus($"refreshing {d}…"));
                        files = files.Where(f => ExtensionFilter.Keep(f, settings.IgnoredExtensions)).ToList();
                        var newSnap = Incremental.SnapshotRoots(roots, settings.Blacklist);
                        var index = FileIndex.Build(files);
                        try { Cache.Save(files, cachePath); SnapshotStore.Save(newSnap, snapPath); }
                        catch (Exception ex) { Logger.Info("cache save failed: " + ex.Message); }
                sw.Stop();
                Logger.Info($"refreshed {files.Count} files in {sw.ElapsedMilliseconds} ms");
                Dispatcher.BeginInvoke(() =>
                {
                    SetIndex(index);
                    _indexedPaths = files;
                    Status.Text = $"{files.Count:N0} files in {sw.ElapsedMilliseconds:N0} ms";
                });
                        return;
                    }
                    catch (Exception ex)
                    {
                        Logger.Info("incremental failed, full walk: " + ex.Message);
                    }
                }
                files = WalkAll(settings, count => ShowStatus($"indexing… {count:N0} files"));
                var freshSnap = Incremental.SnapshotRoots(roots, settings.Blacklist);
                var freshIndex = FileIndex.Build(files);
                try { Cache.Save(files, cachePath); SnapshotStore.Save(freshSnap, snapPath); }
                catch (Exception ex) { Logger.Info("cache save failed: " + ex.Message); }
                sw.Stop();
                Logger.Info($"indexed {files.Count} files in {sw.ElapsedMilliseconds} ms");
                Dispatcher.BeginInvoke(() =>
                {
                    SetIndex(freshIndex);
                    _indexedPaths = files;
                    Status.Text = $"{files.Count:N0} files in {sw.ElapsedMilliseconds:N0} ms";
                });
            }
            catch (Exception ex)
            {
                Logger.Info("index failed: " + ex);
                ShowStatus("index failed: " + ex.Message);
            }
        });
    }

    private void ShowStatus(string text) =>
        Dispatcher.BeginInvoke(() => Status.Text = text);

    private List<string> WalkAll(Settings settings, Action<int> onProgress)
    {
        var prog = new InlineCounter(onProgress);
        var all = new List<string>();
        foreach (var root in settings.StartupRoots())
            foreach (var f in Walker.EnumerateParallel(root, settings.Blacklist, prog))
                if (ExtensionFilter.Keep(f, settings.IgnoredExtensions))
                    all.Add(f);
        prog.Finish(all.Count);
        return all;
    }

    // Batches progress posts so the UI thread isn't flooded.
    private sealed class InlineCounter : IProgress<int>
    {
        private readonly Action<int> _onProgress;
        private int _last;
        public InlineCounter(Action<int> onProgress) => _onProgress = onProgress;
        public void Report(int value)
        {
            int last = Volatile.Read(ref _last);
            if (value - last >= 5000 || value == 0)
            {
                _last = value;
                _onProgress(value);
            }
        }
        public void Finish(int total) => _onProgress(total);
    }

    private static string CachePath() => AppDir();

    private static string AppDir() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "win-fast-finder");

    private void GearButton_Click(object sender, RoutedEventArgs e) => OpenSettings();

    private void Settings_Click(object sender, RoutedEventArgs e) => OpenSettings();

    private void OpenSettings()
    {
        _dialogOpen = true;
        try
        {
            string stats = _indexedPaths == null
                ? "Index not ready yet."
                : $"{_indexedPaths.Count:N0} files indexed. Last refresh: {LastRefresh()}.";
            new SettingsWindow(_indexedPaths, stats) { Owner = this }.ShowDialog();
        }
        finally
        {
            _dialogOpen = false;
        }
        // Settings may have changed: reload, re-theme, re-register, rebuild.
        _settings = Settings.Load();
        Theme.Apply(_settings.Theme);
        ReregisterHotKey();
        StartBackgroundIndex();
    }

    private static string LastRefresh()
    {
        try
        {
            var cache = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "win-fast-finder", "cache.bin");
            if (File.Exists(cache))
                return File.GetLastWriteTime(cache).ToString("g");
        }
        catch { }
        return "never";
    }

    private void ReregisterHotKey()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            UnregisterHotKey(hwnd, HOTKEY_ID);
        }
        catch { }
        _ctrlHook?.Dispose();
        _ctrlHook = null;
        RegisterHotKey();
    }

    private void Resync_Click(object sender, RoutedEventArgs e)
    {
        ShowStatus("resyncing…");
        StartBackgroundIndex(forceFull: true);
    }

    private void RegisterHotKey()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (!_wndHooked)
        {
            var src = HwndSource.FromHwnd(hwnd);
            src?.AddHook(WndProc);
            _wndHooked = true;
        }
        if (!Wff.Engine.Hotkey.TryParse(_settings.MainHotkey, out uint mods, out uint vk))
        {
            Logger.Info("bad hotkey in settings, using Ctrl+Shift+F");
            mods = MOD_CONTROL | MOD_SHIFT;
            vk = VK_F;
        }
        bool ok = RegisterHotKey(hwnd, HOTKEY_ID, mods, vk);
        Logger.Info($"hotkey {_settings.MainHotkey} registered={ok} err={Marshal.GetLastWin32Error()}");
        if (_settings.CtrlDoubleTap)
        {
            try
            {
                _ctrlHook = KeyboardHook.Start(new DoubleTapDetector(),
                    () => Dispatcher.BeginInvoke(() => Summon()));
            }
            catch
            {
                // Hook blocked (policy/AV): main hotkey keeps working.
            }
        }
        Hide();
    }

    private void UnregisterHotKey()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            UnregisterHotKey(hwnd, HOTKEY_ID);
        }
        catch { }
        _ctrlHook?.Dispose();
        _ctrlHook = null;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            Logger.Info("wm_hotkey received");
            Summon();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void QueryBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        bool hasText = QueryBox.Text.Length > 0;
        Placeholder.Visibility = hasText ? Visibility.Collapsed : Visibility.Visible;
        var vis = hasText ? Visibility.Visible : Visibility.Collapsed;
        HintBar.Visibility = vis;
        Hits.Visibility = vis;
        BottomBar.Visibility = vis;
        if (!hasText)
            Hits.Items.Clear();
        _debounce.Stop();
        if (hasText)
            _debounce.Start();
    }

    private void RunSearch(string q, int id)
    {
        var index = _index;
        if (index == null || string.IsNullOrWhiteSpace(q))
            return;
        int max = Math.Clamp(_settings.MaxResults, 1, 200);
        var query = QueryParser.Parse(q);
        if (_settings.MatchPath && !query.MatchPath)
            query = query with { MatchPath = true };
        if (query.Sort == "rank" && _settings.SortMode != "rank")
            query = query with { Sort = _settings.SortMode };
        Task.Run(() =>
        {
            var hits = index.Search(query, max);
            Dispatcher.BeginInvoke(() =>
            {
                if (id != _queryId)
                    return; // stale: a newer keystroke already queued
                RenderHits(hits, q);
            });
        });
    }

    private sealed class HitItem
    {
        public string Path { get; init; } = "";
        public string Name { get; init; } = "";
        public bool IsExact { get; init; }
        public HashSet<int> Spans { get; init; } = new();
    }

    private void RenderHits(List<string> hits, string query)
    {
        var words = HighlightWords(query);
        Hits.Items.Clear();
        foreach (var h in hits)
        {
            var name = System.IO.Path.GetFileName(h);
            var spans = new HashSet<int>();
            foreach (var w in words)
                foreach (var i in MatchSpans.Find(name, w))
                    spans.Add(i);
            bool exact = name.Equals(query.Trim(), StringComparison.OrdinalIgnoreCase);
            var line1 = new System.Windows.Controls.TextBlock { FontSize = 14 };
            var accent = SystemColors.HighlightBrush;
            var normal = SystemColors.WindowTextBrush;
            if (exact)
                line1.Inlines.Add(new System.Windows.Documents.Run("★ ")
                {
                    Foreground = accent,
                    FontWeight = FontWeights.Bold,
                });
            for (int i = 0; i < name.Length; i++)
            {
                bool hit = spans.Contains(i);
                line1.Inlines.Add(new System.Windows.Documents.Run(name[i].ToString())
                {
                    Foreground = hit ? accent : normal,
                    FontWeight = hit ? FontWeights.Bold : FontWeights.Normal,
                });
            }
            var line2 = new System.Windows.Controls.TextBlock
            {
                Text = h,
                FontSize = 11,
                Foreground = SystemColors.GrayTextBrush,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            var panel = new System.Windows.Controls.StackPanel();
            panel.Children.Add(line1);
            panel.Children.Add(line2);
            var item = new System.Windows.Controls.ListBoxItem
            {
                Content = panel,
                Tag = new HitItem { Path = h, Name = name, IsExact = exact, Spans = spans },
                Padding = new Thickness(4, 3, 4, 3),
            };
            Hits.Items.Add(item);
        }
        if (Hits.Items.Count > 0)
            ((System.Windows.Controls.ListBoxItem)Hits.Items[0]).IsSelected = true;
    }

    private static List<string> HighlightWords(string query)
    {
        var words = new List<string>();
        foreach (var tok in query.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var t = tok.ToLowerInvariant();
            if (t.StartsWith("ext:") || t.StartsWith("size:") || t.StartsWith("dm:"))
            {
                int c = t.IndexOf(':');
                if (c + 1 < t.Length) words.Add(t[(c + 1)..].TrimStart('.', '<', '>', '='));
            }
            else if (t.Contains('*') || t.Contains('?'))
            {
                words.Add(t.Replace("*", "").Replace("?", ""));
            }
            else
            {
                words.Add(t);
            }
        }
        return words.Where(w => w.Length > 0).ToList();
    }

    private void QueryBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Tab completes directive prefixes: ex→ext:, si→size:, dm→dm:
        if (e.Key == Key.Tab && QueryBox.CaretIndex == QueryBox.Text.Length)
        {
            var text = QueryBox.Text;
            int last = text.LastIndexOf(' ');
            string head = last < 0 ? "" : text[..(last + 1)];
            string tail = (last < 0 ? text : text[(last + 1)..]).ToLowerInvariant();
            string[] dirs = { "ext:", "size:", "dm:" };
            foreach (var d in dirs)
            {
                if (d.StartsWith(tail) && tail.Length > 0 && tail.Length < d.Length)
                {
                    QueryBox.Text = head + d;
                    QueryBox.CaretIndex = QueryBox.Text.Length;
                    e.Handled = true;
                    return;
                }
            }
        }
    }

    private void QueryBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            OpenSelected();
        else if (e.Key == Key.Escape)
            Hide();
    }

    private void Hits_DoubleClick(object sender, MouseButtonEventArgs e) => OpenSelected();

    private void OpenSelected()
    {
        string? path = null;
        if (Hits.SelectedItem is System.Windows.Controls.ListBoxItem sel && sel.Tag is HitItem hit)
            path = hit.Path;
        else if (Hits.Items.Count > 0 && Hits.Items[0] is System.Windows.Controls.ListBoxItem first
            && first.Tag is HitItem fhit)
            path = fhit.Path;
        if (path != null)
        {
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch { }
            Hide();
        }
    }
}
