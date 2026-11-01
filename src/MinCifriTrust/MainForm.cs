using MinCifriTrust.Services;

namespace MinCifriTrust;

public sealed class MainForm : Form
{
    private readonly CertificateTrustService _trustService = new();
    private readonly Button _toggleButton;
    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _enableItem;
    private readonly ToolStripMenuItem _disableItem;
    private Icon? _currentTrayIcon;
    private bool _enabled;
    private bool _busy;
    private bool _exitRequested;

    public MainForm()
    {
        Text = "MinCifri Trust";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(280, 100);
        Font = new Font("Segoe UI", 10f);
        ShowInTaskbar = true;

        _toggleButton = new Button
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(16),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 12f, FontStyle.Bold)
        };
        _toggleButton.FlatAppearance.BorderSize = 0;
        _toggleButton.Click += async (_, _) => await ToggleAsync().ConfigureAwait(true);

        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16)
        };
        panel.Controls.Add(_toggleButton);
        Controls.Add(panel);

        _enableItem = new ToolStripMenuItem("Включить", null, async (_, _) =>
            await SetEnabledAsync(true).ConfigureAwait(true));
        _disableItem = new ToolStripMenuItem("Выключить", null, async (_, _) =>
            await SetEnabledAsync(false).ConfigureAwait(true));
        var closeItem = new ToolStripMenuItem("Закрыть", null, (_, _) => ExitApp());

        var menu = new ContextMenuStrip();
        menu.Items.Add(_enableItem);
        menu.Items.Add(_disableItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(closeItem);

        _trayIcon = new NotifyIcon
        {
            Visible = true,
            Text = "MinCifri Trust",
            ContextMenuStrip = menu
        };
        _trayIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                ShowMainWindow();
        };

        Load += (_, _) => RefreshUi();
        Resize += (_, _) =>
        {
            if (WindowState == FormWindowState.Minimized)
                HideToTray();
        };
        FormClosing += (_, e) =>
        {
            if (_exitRequested)
                return;

            e.Cancel = true;
            HideToTray();
        };
        FormClosed += (_, _) =>
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _currentTrayIcon?.Dispose();
        };
    }

    private void HideToTray()
    {
        Hide();
        ShowInTaskbar = false;
    }

    private void ShowMainWindow()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void ExitApp()
    {
        _exitRequested = true;
        Close();
    }

    private async Task ToggleAsync()
        => await SetEnabledAsync(!_enabled).ConfigureAwait(true);

    private async Task SetEnabledAsync(bool enable)
    {
        if (_busy)
            return;

        _busy = true;
        SetControlsEnabled(false);

        try
        {
            if (enable)
            {
                var download = await _trustService.EnsureDownloadedAsync().ConfigureAwait(true);
                if (!download.Success)
                {
                    ShowError(download.Message);
                    return;
                }

                var result = _trustService.Enable();
                if (!result.Success)
                {
                    ShowError(result.Message);
                    return;
                }
            }
            else
            {
                if (!_trustService.AreAllTrusted() && !HasCachedCerts())
                {
                    var download = await _trustService.EnsureDownloadedAsync().ConfigureAwait(true);
                    if (!download.Success)
                    {
                        ShowError(download.Message);
                        return;
                    }
                }

                var result = _trustService.Disable();
                if (!result.Success)
                {
                    ShowError(result.Message);
                    return;
                }
            }

            RefreshUi();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            _busy = false;
            SetControlsEnabled(true);
            RefreshUi();
        }
    }

    private bool HasCachedCerts()
        => Directory.Exists(_trustService.CertsDirectory)
           && Directory.EnumerateFiles(_trustService.CertsDirectory, "*.cer").Any();

    private void RefreshUi()
    {
        _enabled = _trustService.AreAllTrusted();

        _toggleButton.Text = _enabled ? "Выключить" : "Включить";
        _toggleButton.BackColor = _enabled
            ? Color.FromArgb(34, 139, 34)
            : Color.FromArgb(180, 40, 40);
        _toggleButton.ForeColor = Color.White;

        _enableItem.Enabled = !_busy && !_enabled;
        _disableItem.Enabled = !_busy && _enabled;

        var previous = _currentTrayIcon;
        _currentTrayIcon = TrayIconFactory.Create(_enabled);
        _trayIcon.Icon = _currentTrayIcon;
        _trayIcon.Text = _enabled
            ? "MinCifri Trust: включено"
            : "MinCifri Trust: выключено";
        previous?.Dispose();
    }

    private void SetControlsEnabled(bool enabled)
    {
        _toggleButton.Enabled = enabled;
        _enableItem.Enabled = enabled && !_enabled;
        _disableItem.Enabled = enabled && _enabled;
    }

    private static void ShowError(string message)
        => MessageBox.Show(message, "MinCifri Trust", MessageBoxButtons.OK, MessageBoxIcon.Error);
}
