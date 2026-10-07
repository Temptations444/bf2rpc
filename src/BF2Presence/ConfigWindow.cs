using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Text.Json;
using System.Text.Json.Nodes;
using BF2Presence.Config;
using BF2Presence.Discord;
using BF2Presence.Game;

namespace BF2Presence;

internal sealed class ConfigWindow : Form
{
    private const string RepoUrl = "https://github.com/Temptations444/bf2rpc";

    private static readonly Color Accent = Color.FromArgb(124, 200, 106);
    private static readonly Color Muted = Color.FromArgb(175, 175, 175);
    private static readonly Color PanelFill = Color.FromArgb(185, 14, 14, 18);
    private static readonly Color Line = Color.FromArgb(80, 255, 255, 255);

    private static readonly (string? Prop, string Text, string? Hint)[] Rows =
    [
        (null, "Show on Discord", null),
        ("Map", "Map", "Map name and picture"),
        ("Mode", "Game mode", "Online modes only (not co-op or arcade)"),
        ("Kills", "Kills", null),
        ("Assists", "Assists", null),
        ("Deaths", "Deaths", null),
        ("Score", "Score", null),
        ("Spe", "SPE", "Score per elimination: score / (kills + assists)"),
        ("ElapsedTime", "Elapsed time", "How long you've been in the match"),
        ("InMenus", "While in menus", "Off hides your status until a match starts"),
        (null, "Style", null),
        ("Abbreviate", "Short stats", "10K/4A/5D instead of 10 Kills | 4 Assists | 5 Deaths"),
    ];

    private readonly string _settingsPath;
    private readonly ShowSettings _show;
    private readonly Image _background;
    private readonly Rectangle _left, _right;
    private readonly List<(Rectangle Panel, int Y)> _headerLines = [];
    private readonly Label _preview;
    private readonly Toggle _autoStart;

    public ConfigWindow(string settingsPath)
    {
        _settingsPath = settingsPath;
        _show = AppSettings.Load(settingsPath).Show;
        _background = Image.FromStream(typeof(ConfigWindow).Assembly.GetManifestResourceStream("background.jpg")!);

        float s = DeviceDpi / 96f;
        int P(int v) => (int)(v * s);

        Text = "BF2RPC settings";
        ClientSize = new Size(P(780), P(600));
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;
        BackColor = Color.Black;
        ForeColor = Color.White;
        Icon = Tray.AppIcon(SystemInformation.IconSize);

        var title = Text_("BF2RPC", 22, FontStyle.Bold | FontStyle.Italic, P(22), P(14));
        Controls.Add(title);
        var github = new PictureBox
        {
            Image = Image.FromStream(typeof(ConfigWindow).Assembly.GetManifestResourceStream("github.png")!),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
            Size = new Size(P(28), P(28)),
            Location = new Point(title.Right + P(10), title.Top + (title.PreferredHeight - P(28)) / 2),
            Cursor = Cursors.Hand,
            AccessibleName = "Open BF2RPC on GitHub",
            AccessibleRole = AccessibleRole.Link,
        };
        github.Click += (_, _) => Process.Start(new ProcessStartInfo(RepoUrl) { UseShellExecute = true });
        new ToolTip().SetToolTip(github, "Open on GitHub");
        Controls.Add(github);
        var subtitle = Text_("Select your Settings", 16, FontStyle.Bold | FontStyle.Italic, 0, P(20));
        subtitle.Left = ClientSize.Width - P(22) - subtitle.PreferredWidth;
        Controls.Add(subtitle);

        _left = new Rectangle(P(20), P(70), P(450), P(510));
        _right = new Rectangle(P(484), P(70), P(276), P(510));

        int y = _left.Top + P(8);
        foreach (var (prop, text, hint) in Rows)
        {
            if (prop is null)
            {
                Controls.Add(Text_(text, 10, FontStyle.Italic, _left.Left + P(16), y + P(6)));
                _headerLines.Add((_left, y + P(30)));
                y += P(38);
                continue;
            }
            var property = typeof(ShowSettings).GetProperty(prop)!;
            Controls.Add(Text_(text + ":", 12, FontStyle.Bold | FontStyle.Italic, _left.Left + P(16), y));
            if (hint is not null)
                Controls.Add(Text_(hint, 7.5f, FontStyle.Bold | FontStyle.Italic, _left.Left + P(18), y + P(23), Muted));

            var toggle = new Toggle(s)
            {
                On = (bool)property.GetValue(_show)!,
                AccessibleName = text,
                AccessibleDescription = hint,
                Location = new Point(_left.Right - P(16) - P(46), y + P(3)),
            };
            toggle.Changed += (_, _) =>
            {
                property.SetValue(_show, toggle.On);
                UpdatePreview();
            };
            Controls.Add(toggle);
            y += hint is null ? P(34) : P(46);
        }

        int x = _right.Left + P(16);
        Controls.Add(Text_("Preview", 10, FontStyle.Italic, x, _right.Top + P(14)));
        _headerLines.Add((_right, _right.Top + P(38)));
        _preview = Text_("", 10, FontStyle.Regular, x, _right.Top + P(50));
        _preview.MaximumSize = new Size(_right.Width - P(32), 0);
        Controls.Add(_preview);

        Controls.Add(Text_("General", 10, FontStyle.Italic, x, _right.Top + P(300)));
        _headerLines.Add((_right, _right.Top + P(324)));
        Controls.Add(Text_("Start with Windows:", 12, FontStyle.Bold | FontStyle.Italic, x, _right.Top + P(336)));
        _autoStart = new Toggle(s) { On = Tray.AutoStart, AccessibleName = "Start with Windows", Location = new Point(_right.Right - P(16) - P(46), _right.Top + P(339)) };
        Controls.Add(_autoStart);

        var save = new Button
        {
            Text = "Save",
            FlatStyle = FlatStyle.Flat,
            BackColor = Accent,
            ForeColor = Color.Black,
            Font = new Font("Segoe UI", 12, FontStyle.Bold | FontStyle.Italic),
            Bounds = new Rectangle(x, _right.Bottom - P(60), _right.Width - P(32), P(44)),
            Cursor = Cursors.Hand,
        };
        save.FlatAppearance.BorderSize = 0;
        save.Click += (_, _) => Save();
        Controls.Add(save);
        AcceptButton = save;

        UpdatePreview();
    }

    private void UpdatePreview()
    {
        var sample = new GameSnapshot { LevelName = "Levels/MP/Naboo_01/Naboo_01", GameMode = "Galactic Assault", Kills = 10, Assists = 4, Deaths = 5, Score = 1000 };
        var start = DateTime.UtcNow.AddSeconds(-634);
        var m = PresenceBuilder.Build(sample, new AppSettings { Show = _show }, start, start)!;

        var lines = new List<string?> { "Star Wars Battlefront II", m.Details, m.State, m.StartTimeUtc is null ? null : "10:34 elapsed" };
        _preview.Text = string.Join(Environment.NewLine, lines.Where(l => !string.IsNullOrEmpty(l)));
    }

    private void Save()
    {
        var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        var root = (File.Exists(_settingsPath) ? JsonNode.Parse(File.ReadAllText(_settingsPath), documentOptions: options) as JsonObject : null) ?? new JsonObject();
        var show = new JsonObject();
        foreach (var p in typeof(ShowSettings).GetProperties())
            show[char.ToLowerInvariant(p.Name[0]) + p.Name[1..]] = (bool)p.GetValue(_show)!;
        root["show"] = show;
        root["setupDone"] = true;
        File.WriteAllText(_settingsPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        if (_autoStart.On != Tray.AutoStart) Tray.AutoStart = _autoStart.On;
        Close();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var g = e.Graphics;
        float scale = Math.Max((float)ClientSize.Width / _background.Width, (float)ClientSize.Height / _background.Height);
        float w = _background.Width * scale, h = _background.Height * scale;
        g.DrawImage(_background, (ClientSize.Width - w) / 2, (ClientSize.Height - h) / 2, w, h);
        using var dim = new SolidBrush(Color.FromArgb(110, 0, 0, 0));
        g.FillRectangle(dim, ClientRectangle);
        g.FillRectangle(dim, 0, 0, ClientSize.Width, _left.Top - 6);

        using var panel = new SolidBrush(PanelFill);
        using var pen = new Pen(Line);
        foreach (var r in new[] { _left, _right })
        {
            g.FillRectangle(panel, r);
            g.DrawRectangle(pen, r);
        }
        foreach (var (r, y) in _headerLines)
            g.DrawLine(pen, r.Left + 14, y, r.Right - 14, y);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _background.Dispose();
        base.Dispose(disposing);
    }

    private static Label Text_(string text, float size, FontStyle style, int x, int y, Color? color = null) => new()
    {
        Text = text,
        AutoSize = true,
        BackColor = Color.Transparent,
        ForeColor = color ?? Color.White,
        Font = new Font("Segoe UI", size, style),
        Location = new Point(x, y),
    };

    private sealed class Toggle : Control
    {
        private bool _on;
        public event EventHandler? Changed;

        public bool On
        {
            get => _on;
            set { _on = value; Invalidate(); }
        }

        public Toggle(float scale)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
            BackColor = Color.Transparent;
            Size = new Size((int)(46 * scale), (int)(22 * scale));
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleRole = AccessibleRole.CheckButton;
        }

        protected override void OnClick(EventArgs e)
        {
            Focus();
            On = !On;
            Changed?.Invoke(this, e);
            base.OnClick(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space) OnClick(e);
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int h = Height - 1, w = Width - 1;
            using var track = new GraphicsPath();
            track.AddArc(0, 0, h, h, 90, 180);
            track.AddArc(w - h, 0, h, h, 270, 180);
            track.CloseFigure();
            using var fill = new SolidBrush(On ? Accent : Color.FromArgb(75, 75, 80));
            g.FillPath(fill, track);
            if (Focused)
            {
                using var focus = new Pen(Color.White, 1.5f);
                g.DrawPath(focus, track);
            }

            int knob = h - 4;
            int x = On ? w - knob - 2 : 2;
            using var knobFill = new SolidBrush(Color.FromArgb(55, 55, 60));
            using var knobEdge = new Pen(Color.FromArgb(200, 200, 200), 1.5f);
            g.FillEllipse(knobFill, x, 2, knob, knob);
            g.DrawEllipse(knobEdge, x, 2, knob, knob);
        }
    }
}
