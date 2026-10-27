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
    private System.Windows.Controls.ScrollViewer? _boxScroller; // TextBox's own scroller
    private List<(string Ext, int Count)> _extCache = new(); // top extensions, rebuilt per index
    private static readonly System.Windows.Media.SolidColorBrush AmberBrush = MakeBrush(0xFF, 0xB9, 0x00);
    private static readonly System.Windows.Media.SolidColorBrush CyanBrush = MakeBrush(0x22, 0xD3, 0xEE);

    private static System.Windows.Media.SolidColorBrush MakeBrush(byte r, byte g, byte b)
    {
        var br = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
        br.Freeze();
        return br;
    }

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

    private void RebuildExtCache()
    {
        // Distinct-extension suggestions must not re-walk a huge index per
        // keystroke: snapshot the top 300 once per index build.
        try
        {
            _extCache = _indexedPaths == null
                ? new List<(string Ext, int Count)>()
                : QueryHint.TopExtensions(_indexedPaths).Take(300).ToList();
        }
        catch { _extCache = new List<(string Ext, int Count)>(); }
    }

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
            // Deep mode skips the incremental shortcut: full rebuild.
            StartBackgroundIndex(forceFull: _settings.RefreshMode == "Deep");
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
                        var cached = Cache.Load(cachePath);
                        var old = cached.Paths;
                        var snap = SnapshotStore.Load(snapPath);
                        ShowStatus("checking for new files…");
                        files = Incremental.Refresh(old, snap, roots, settings.Blacklist,
                            d => ShowStatus($"refreshing {d}…"), settings.HideHiddenAndSystem);
                        files = files.Where(f => ExtensionFilter.Keep(f, settings.IgnoredExtensions)).ToList();
                        var newSnap = Incremental.SnapshotRoots(roots, settings.Blacklist,
                            settings.HideHiddenAndSystem);
                        var index = FileIndex.Build(files);
                        try { Cache.Save(index, cachePath); SnapshotStore.Save(newSnap, snapPath); }
                        catch (Exception ex) { Logger.Info("cache save failed: " + ex.Message); }
                sw.Stop();
                Logger.Info($"refreshed {files.Count} files in {sw.ElapsedMilliseconds} ms");
                Dispatcher.BeginInvoke(() =>
                {
                    SetIndex(index);
                    _indexedPaths = files;
                    RebuildExtCache();
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
                var freshSnap = Incremental.SnapshotRoots(roots, settings.Blacklist,
                    settings.HideHiddenAndSystem);
                var freshIndex = FileIndex.Build(files);
                try { Cache.Save(freshIndex, cachePath); SnapshotStore.Save(freshSnap, snapPath); }
                catch (Exception ex) { Logger.Info("cache save failed: " + ex.Message); }
                sw.Stop();
                Logger.Info($"indexed {files.Count} files in {sw.ElapsedMilliseconds} ms");
                Dispatcher.BeginInvoke(() =>
                {
                    SetIndex(freshIndex);
                    _indexedPaths = files;
                    RebuildExtCache();
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
            foreach (var f in Walker.EnumerateParallel(root, settings.Blacklist, prog,
                skipHiddenSystem: settings.HideHiddenAndSystem))
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
        StartBackgroundIndex(forceFull: _settings.RefreshMode == "Deep");
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
        {
            Hits.Items.Clear();
            CloseSuggestions();
        }
        SyncOverlay();
        if (hasText)
            UpdateSuggestions();
        _debounce.Stop();
        if (hasText)
        {
            // Directive queries scan more index: give them a longer
            // keystroke rest than plain text (Settings, default 250 ms).
            int delay = QueryBox.Text.Contains(':')
                ? Math.Clamp(_settings.FilterDebounceMs, 50, 2000)
                : 50;
            _debounce.Interval = TimeSpan.FromMilliseconds(delay);
            _debounce.Start();
        }
    }

    private void HookBoxScroller()
    {
        if (_boxScroller != null)
            return;
        try
        {
            QueryBox.ApplyTemplate();
            _boxScroller = QueryBox.Template.FindName("PART_ContentHost", QueryBox)
                as System.Windows.Controls.ScrollViewer;
            if (_boxScroller != null)
                _boxScroller.ScrollChanged += (_, _) => SyncOverlayOffset();
        }
        catch { }
    }

    private void SyncOverlayOffset()
    {
        if (_boxScroller != null)
            OverlayScroll.ScrollToHorizontalOffset(_boxScroller.HorizontalOffset);
    }

    // Keyword highlighting inside the search box: directive names amber,
    // values and globs cyan, free text normal. The overlay is a read-only
    // twin; the transparent TextBox on top keeps typing/caret/selection.
    private void SyncOverlay()
    {
        HookBoxScroller();
        QueryOverlay.Inlines.Clear();
        var text = QueryBox.Text;
        var normal = SystemColors.WindowTextBrush;
        var bad = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var inv in QueryParser.Parse(text, CurrentQueryOptions()).Invalid)
                bad.Add(inv.Token);
        }
        catch { }
        int i = 0;
        while (i < text.Length)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                int j = i + 1;
                while (j < text.Length && char.IsWhiteSpace(text[j])) j++;
                QueryOverlay.Inlines.Add(new System.Windows.Documents.Run(text[i..j]) { Foreground = normal });
                i = j;
            }
            else
            {
                int j = i + 1;
                while (j < text.Length && !char.IsWhiteSpace(text[j])) j++;
                AppendTokenRuns(text[i..j], normal, bad);
                i = j;
            }
        }
        SyncOverlayOffset();
    }

    private static readonly System.Windows.Media.SolidColorBrush RedBrush = MakeBrush(0xF8, 0x71, 0x71);

    private void AppendTokenRuns(string token, System.Windows.Media.Brush normal,
        HashSet<string> invalidTokens)
    {
        var lower = token.ToLowerInvariant();
        if (invalidTokens.Contains(token))
        {
            // Looks like a directive but does not parse: red, not amber.
            // Amber keeps its promise — it always means "understood".
            QueryOverlay.Inlines.Add(new System.Windows.Documents.Run(token)
            {
                Foreground = RedBrush,
                FontWeight = FontWeights.Bold,
            });
            return;
        }
        var split = QueryHint.SplitDirective(lower);
        if (split != null)
        {
            QueryOverlay.Inlines.Add(new System.Windows.Documents.Run(token[..split.Value.Name.Length])
            {
                Foreground = AmberBrush,
                FontWeight = FontWeights.Bold,
            });
            if (split.Value.Value.Length > 0)
                QueryOverlay.Inlines.Add(new System.Windows.Documents.Run(token[split.Value.Name.Length..])
                {
                    Foreground = CyanBrush,
                });
            return;
        }
        if (lower is "[and]" or "[or]")
        {
            // Logical operators glow amber like directive names.
            QueryOverlay.Inlines.Add(new System.Windows.Documents.Run(token)
            {
                Foreground = AmberBrush,
                FontWeight = FontWeights.Bold,
            });
            return;
        }
        if (Glob.IsGlob(lower))
        {
            QueryOverlay.Inlines.Add(new System.Windows.Documents.Run(token) { Foreground = CyanBrush });
            return;
        }
        QueryOverlay.Inlines.Add(new System.Windows.Documents.Run(token) { Foreground = normal });
    }

    // Autocomplete: directive names for a strict prefix ("ex" -> ext:),
    // index-backed ext: values afterwards. Enter always opens the file;
    // only Tab (or click) accepts a suggestion.
    private void UpdateSuggestions()
    {
        var text = QueryBox.Text;
        int caret = Math.Clamp(QueryBox.CaretIndex, 0, text.Length);
        int start = caret;
        while (start > 0 && !char.IsWhiteSpace(text[start - 1])) start--;
        string tail = text[start..caret].ToLowerInvariant();
        var list = new List<Suggestion>();
        if (tail.Length > 0)
        {
            if (tail.StartsWith("[", StringComparison.Ordinal))
            {
                list.AddRange(QueryHint.SuggestOperators(tail));
            }
            else
            {
                var split = QueryHint.SplitDirective(tail);
                if (split != null && split.Value.Name == "ext:")
                    list.AddRange(QueryHint.FormatExtSuggestions(_extCache, split.Value.Value));
                else if (split == null)
                    list.AddRange(QueryHint.SuggestNames(tail));
            }
        }
        if (list.Count == 0)
        {
            CloseSuggestions();
            return;
        }
        SuggestBox.ItemsSource = list;
        SuggestBox.SelectedIndex = 0;
        SuggestBox.ScrollIntoView(SuggestBox.SelectedItem);
        SuggestBox.Visibility = Visibility.Visible;
    }

    private void CloseSuggestions()
    {
        SuggestBox.Visibility = Visibility.Collapsed;
        SuggestBox.ItemsSource = null;
    }

    private void MoveSuggestion(int dir)
    {
        int n = SuggestBox.Items.Count;
        if (n == 0)
            return;
        int i = (SuggestBox.SelectedIndex + dir + n) % n;
        SuggestBox.SelectedIndex = i;
        SuggestBox.ScrollIntoView(SuggestBox.SelectedItem);
    }

    private void AcceptSelectedSuggestion()
    {
        if (SuggestBox.SelectedItem is Suggestion s)
            AcceptSuggestion(s);
        else if (SuggestBox.Items.Count > 0 && SuggestBox.Items[0] is Suggestion first)
            AcceptSuggestion(first);
    }

    private void AcceptSuggestion(Suggestion s)
    {
        var text = QueryBox.Text;
        int caret = Math.Clamp(QueryBox.CaretIndex, 0, text.Length);
        int start = caret;
        while (start > 0 && !char.IsWhiteSpace(text[start - 1])) start--;
        int end = caret;
        while (end < text.Length && !char.IsWhiteSpace(text[end])) end++;
        QueryBox.Text = text[..start] + s.Insert + text[end..];
        QueryBox.CaretIndex = start + s.Insert.Length;
        QueryBox.Focus();
        // TextChanged refreshes overlay + suggestions (typing "ext:"
        // immediately offers that extension's values next).
    }

    private void SuggestBox_Click(object sender, MouseButtonEventArgs e)
    {
        // Accept only real item clicks, not empty-area clicks.
        DependencyObject? d = e.OriginalSource as DependencyObject;
        while (d != null && d is not System.Windows.Controls.ListBoxItem)
            d = System.Windows.Media.VisualTreeHelper.GetParent(d);
        if (d is System.Windows.Controls.ListBoxItem item && item.DataContext is Suggestion s)
            AcceptSuggestion(s);
    }

    private CancellationTokenSource? _searchCts;

    private QueryOptions CurrentQueryOptions() => new()
    {
        ExactSizeMode = _settings.ExactSizeMode,
        SizeDecimalUnits = _settings.SizeDecimalUnits,
        DateUseLocalTime = _settings.DateUseLocalTime,
    };

    private void RunSearch(string q, int id)
    {
        var index = _index;
        if (index == null || string.IsNullOrWhiteSpace(q))
            return;
        // A new keystroke cancels the previous scan: stale full-index work
        // dies instead of piling up behind the latest query.
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;
        int max = Math.Clamp(_settings.MaxResults, 1, 200);
        var query = QueryParser.Parse(q, CurrentQueryOptions());
        if (_settings.MatchPath && !query.MatchPath)
            query = query with { MatchPath = true };
        if (query.Sort == "rank" && _settings.SortMode != "rank")
            query = query with { Sort = _settings.SortMode };
        Task.Run(() =>
        {
            try
            {
                var sw = Stopwatch.StartNew();
                var (rows, total) = index.SearchDetailed(query, max, token);
                sw.Stop();
                if (_settings.LogSlowQueries && sw.ElapsedMilliseconds > 500)
                    Logger.Info($"slow query ({sw.ElapsedMilliseconds} ms): {q}");
                bool sizeActive = query.MinSizeBytes != null || query.MaxSizeBytes != null
                    || query.SizeDisplayTarget != null;
                Dispatcher.BeginInvoke(() =>
                {
                    if (id != _queryId)
                        return; // stale: a newer keystroke already queued
                    RenderHits(rows, q, sizeActive, _settings.ShowFileSize);
                    UpdateResultStatus(rows.Count, total, query);
                });
            }
            catch (OperationCanceledException)
            {
                // Superseded by a newer keystroke: nothing to render.
            }
        });
    }

    private void UpdateResultStatus(int shown, int total, Query query)
    {
        // Invalid directives take precedence: the user must see WHY a
        // filter did nothing instead of guessing.
        if (query.Invalid.Count > 0)
        {
            Status.Text = "⚠ " + query.Invalid[0].Message;
            return;
        }
        if (_settings.ShowTotalMatches)
        {
            Status.Text = total == shown
                ? $"{shown:N0} matches"
                : $"{shown:N0} of {total:N0} matches";
        }
        // Otherwise the index-stats line stays untouched.
    }

    private sealed class HitItem
    {
        public string Path { get; init; } = "";
        public string Name { get; init; } = "";
        public bool IsExact { get; init; }
        public HashSet<int> Spans { get; init; } = new();
    }

    private void RenderHits(List<ResultRow> rows, string query, bool highlightSize, bool showSize)
    {
        var words = HighlightWords(query);
        Hits.Items.Clear();
        foreach (var row in rows)
        {
            string h = row.Path;
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
                FontSize = 11,
                Foreground = SystemColors.GrayTextBrush,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            // Every row shows the file size up front (from index metadata,
            // no syscalls); with a size: filter active it glows amber like
            // an exact match. Unknown sizes (file vanished) show no badge.
            string? sizeText = showSize && row.SizeBytes >= 0
                ? SizeFormat.FormatBytes(row.SizeBytes, _settings.SizeDecimalUnits)
                : null;
            if (sizeText != null)
            {
                line2.Inlines.Add(new System.Windows.Documents.Run(sizeText)
                {
                    Foreground = highlightSize ? accent : SystemColors.GrayTextBrush,
                    FontWeight = highlightSize ? FontWeights.Bold : FontWeights.Normal,
                });
                line2.Inlines.Add(new System.Windows.Documents.Run(" · ")
                {
                    Foreground = SystemColors.GrayTextBrush,
                });
            }
            line2.Inlines.Add(new System.Windows.Documents.Run(h)
            {
                Foreground = SystemColors.GrayTextBrush,
            });
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
            else if (t == "path:")
            {
                // Directive only: nothing to highlight.
            }
            else if (t.StartsWith("path:") && t.Length > 5)
            {
                words.Add(t[5..]); // path:value: highlight the value
            }
            else if (t.Contains('*') || t.Contains('?'))
            {
                words.Add(t.Replace("*", "").Replace("?", ""));
            }
            else if (t is "[and]" or "[or]")
            {
                // Logical operators: keywords, nothing to highlight.
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
        if (SuggestBox.Visibility == Visibility.Visible && SuggestBox.HasItems)
        {
            if (e.Key == Key.Down) { MoveSuggestion(1); e.Handled = true; return; }
            if (e.Key == Key.Up) { MoveSuggestion(-1); e.Handled = true; return; }
            // Tab accepts; Enter still opens the file. Esc dismisses here
            // (handled) so KeyDown never hides the popup for it.
            if (e.Key == Key.Tab) { AcceptSelectedSuggestion(); e.Handled = true; return; }
            if (e.Key == Key.Escape) { CloseSuggestions(); e.Handled = true; return; }
        }
        // Fallback Tab: complete directive prefixes incl. path:
        // ex→ext:, si→size:, dm→dm:, p→path:
        if (e.Key == Key.Tab && QueryBox.CaretIndex == QueryBox.Text.Length)
        {
            var text = QueryBox.Text;
            int last = text.LastIndexOf(' ');
            string head = last < 0 ? "" : text[..(last + 1)];
            string tail = (last < 0 ? text : text[(last + 1)..]).ToLowerInvariant();
            string? done = QueryHint.CompleteName(tail);
            if (done != null)
            {
                QueryBox.Text = head + done;
                QueryBox.CaretIndex = QueryBox.Text.Length;
                e.Handled = true;
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
