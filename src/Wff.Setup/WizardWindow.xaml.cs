using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace Wff.Setup;

public partial class WizardWindow : Window
{
    private int _page; // 0 welcome, 1 license, 2 directory, 3 progress, 4 finish

    public WizardWindow()
    {
        InitializeComponent();
        LicenseBox.Text = Installer.LicenseText();
        DirBox.Text = Installer.DefaultDir;
        ShowPage(0);
    }

    private void ShowPage(int page)
    {
        _page = page;
        PageWelcome.Visibility = page == 0 ? Visibility.Visible : Visibility.Collapsed;
        PageLicense.Visibility = page == 1 ? Visibility.Visible : Visibility.Collapsed;
        PageDirectory.Visibility = page == 2 ? Visibility.Visible : Visibility.Collapsed;
        PageProgress.Visibility = page == 3 ? Visibility.Visible : Visibility.Collapsed;
        PageFinish.Visibility = page == 4 ? Visibility.Visible : Visibility.Collapsed;
        PageBlocked.Visibility = page == 5 ? Visibility.Visible : Visibility.Collapsed;
        BackBtn.IsEnabled = page is 1 or 2;
        NextBtn.Content = page switch
        {
            1 => "Next ›",
            2 => "Install",
            4 => "Finish",
            5 => "Recheck",
            _ => "Next ›",
        };
        NextBtn.IsEnabled = page switch
        {
            1 => AcceptBox.IsChecked == true,
            2 => DirError.Visibility != Visibility.Visible && DirBox.Text.Length > 0,
            3 => false,
            _ => true,
        };
    }

    private void Download_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(Wff.Engine.RuntimeCheck.DownloadUrl)
            { UseShellExecute = true });
        }
        catch { }
    }

    private void Recheck_Click(object sender, RoutedEventArgs e) => Next_Click(sender, e);

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_page > 0) ShowPage(_page - 1);
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_page == 4) { Close(); return; }
        if (_page == 2) { StartInstall(); return; }
        if (_page == 0 || _page == 5)
        {
            // Compatibility gate: no runtime, no install.
            if (!Wff.Engine.RuntimeCheck.IsDotNet8DesktopPresent())
            {
                ShowPage(5);
                return;
            }
        }
        ShowPage(_page == 5 ? 0 : _page + 1);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void Accept_Changed(object sender, RoutedEventArgs e)
    {
        if (_page == 1)
            NextBtn.IsEnabled = AcceptBox.IsChecked == true;
    }

    private void DirBox_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        var err = Installer.ValidateDirectory(DirBox.Text);
        DirError.Text = err ?? "";
        DirError.Visibility = err == null ? Visibility.Collapsed : Visibility.Visible;
        if (_page == 2)
            NextBtn.IsEnabled = err == null;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Install win-fast-finder to…",
            SelectedPath = DirBox.Text,
            ShowNewFolderButton = true,
        };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            DirBox.Text = Path.Combine(dlg.SelectedPath, "win-fast-finder");
    }

    private void StartInstall()
    {
        ShowPage(3);
        string dir = DirBox.Text;
        Task.Run(() =>
        {
            try
            {
                byte[] payload = Installer.ReadPayload(Environment.ProcessPath!);
                Installer.InstallTo(dir, payload, m =>
                    Dispatcher.BeginInvoke(() => ProgressLog.Items.Add(m)));
                Dispatcher.BeginInvoke(() =>
                {
                    FinishText.Text = "Installed. Press Ctrl+Shift+F anywhere to search.";
                    ShowPage(4);
                    if (LaunchBox.IsChecked == true)
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo(
                                Path.Combine(dir, "Wff.App.exe"))
                            { UseShellExecute = true });
                        }
                        catch { }
                    }
                });
            }
            catch (Exception ex)
            {
                string hint = ex is InvalidDataException
                    ? "This installer copy looks damaged or incomplete. Please download it again. "
                    : "";
                Dispatcher.BeginInvoke(() =>
                {
                    ProgressLabel.Text = "Install failed.";
                    ProgressLog.Items.Add("ERROR: " + hint + ex.Message);
                    NextBtn.IsEnabled = false;
                    BackBtn.IsEnabled = true;
                });
            }
        });
    }
}
