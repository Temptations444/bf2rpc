using Microsoft.Win32;

namespace BF2Presence;

internal sealed class Tray : IDisposable
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "BF2Presence";

    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _status = new() { Enabled = false };
    private readonly string _settingsPath;
    private Form? _settings;

    public Tray(string settingsPath)
    {
        _settingsPath = settingsPath;
        if (AutoStart) AutoStart = true;

        var autoStart = new ToolStripMenuItem("Start with Windows") { Checked = AutoStart, CheckOnClick = true };
        autoStart.CheckedChanged += (_, _) => AutoStart = autoStart.Checked;

        var menu = new ContextMenuStrip();
        menu.Items.Add(_status);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Settings...", null, (_, _) => OpenSettings());
        menu.Items.Add(autoStart);
        menu.Items.Add("Quit", null, (_, _) => Application.Exit());

        _icon = new NotifyIcon { Icon = AppIcon(SystemInformation.SmallIconSize), Text = "BF2Presence", ContextMenuStrip = menu, Visible = true };
        _icon.DoubleClick += (_, _) => OpenSettings();
    }

    internal static Icon AppIcon(Size size) =>
        new(typeof(Tray).Assembly.GetManifestResourceStream("app.ico")!, size);

    public void SetStatus(string text)
    {
        _status.Text = text;
        _icon.Text = text.Length <= 63 ? text : text[..60] + "...";
    }

    public void OpenSettings()
    {
        if (_settings is { IsDisposed: false })
        {
            _settings.Activate();
            return;
        }
        _settings = new ConfigWindow(_settingsPath);
        _settings.Show();
    }

    internal static bool AutoStart
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(RunValue) is string;
        }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value) key.SetValue(RunValue, $"\"{Environment.ProcessPath}\" --background");
            else key.DeleteValue(RunValue, throwOnMissingValue: false);
        }
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
