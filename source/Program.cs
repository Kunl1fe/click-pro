using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace LuClickPro
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    internal sealed class MainForm : Form
    {
        private const int HOTKEY_CAPTURE = 9002;
        private const int HOTKEY_STOP = 9003;
        private const int HOTKEY_PROFILE_BASE = 9100;
        private const int WM_HOTKEY = 0x0312;
        private const int WM_NCHITTEST = 0x0084;
        private const int HTCLIENT = 1;
        private const int HTCAPTION = 2;

        private readonly Color Bg = Color.FromArgb(16, 17, 19);
        private readonly Color Card = Color.FromArgb(25, 27, 30);
        private readonly Color Card2 = Color.FromArgb(39, 41, 45);
        private readonly Color TextMain = Color.FromArgb(245, 242, 234);
        private readonly Color TextMuted = Color.FromArgb(180, 180, 176);
        private readonly Color Purple = Color.FromArgb(113, 88, 48);
        private readonly Color Cyan = Color.FromArgb(226, 198, 141);
        private readonly Color Green = Color.FromArgb(52, 211, 153);
        private readonly Color Red = Color.FromArgb(251, 113, 133);

        private ComboBox buttonBox;
        private ComboBox clickTypeBox;
        private NumericUpDown intervalBox;
        private NumericUpDown cpsBox;
        private RadioButton intervalRadio;
        private RadioButton cpsRadio;
        private RadioButton untilStoppedRadio;
        private RadioButton repeatRadio;
        private NumericUpDown repeatBox;
        private RadioButton cursorRadio;
        private RadioButton fixedRadio;
        private NumericUpDown xBox;
        private NumericUpDown yBox;
        private NumericUpDown jitterBox;
        private NumericUpDown varianceBox;
        private CheckBox topMostCheck;
        private Label statusLabel;
        private Label editingProfileLabel;
        private Label countLabel;
        private Label rateLabel;
        private Label uptimeLabel;
        private Button startButton;
        private Button captureButton;
        private Button lockButton;
        private Panel statusDot;
        private Button[] profileButtons;
        private ProfileConfig[] profiles;
        private int selectedProfile;
        private NotifyIcon trayIcon;
        private System.Windows.Forms.Timer uiTimer;
        private System.Threading.Timer clickTimer;
        private readonly object timerLock = new object();
        private readonly Random random = new Random();
        private Stopwatch stopwatch = new Stopwatch();
        private long totalClicks;
        private bool running;
        private bool exiting;
        private int runGeneration;
        private int activeButton;
        private bool activeDoubleClick;
        private bool activeFixedPosition;
        private int activeX;
        private int activeY;
        private int activeJitter;
        private int activeBaseInterval;
        private int activeVariance;
        private long activeRepeatLimit;

        private string SettingsPath
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LuClickPro");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "settings.ini");
            }
        }

        public MainForm()
        {
            Text = "LU CLICK PRO • Champagne Edition";
            ClientSize = new Size(920, 715);
            AutoScaleMode = AutoScaleMode.None;
            MinimumSize = new Size(640, 460);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Bg;
            ForeColor = TextMain;
            Font = new Font("Segoe UI", 9.5f);
            DoubleBuffered = true;
            Padding = new Padding(1);
            Icon = AppIcon.Create();

            profiles = new ProfileConfig[6];
            for (int i = 0; i < profiles.Length; i++) profiles[i] = ProfileConfig.Default();
            BuildUi();
            Rectangle available = Screen.FromControl(this).WorkingArea;
            ClientSize = new Size(Math.Min(920, available.Width - 32), Math.Min(715, available.Height - 32));
            LoadSettings();
            RegisterConfiguredHotkeys();

            uiTimer = new System.Windows.Forms.Timer();
            uiTimer.Interval = 200;
            uiTimer.Tick += UiTimerTick;
            uiTimer.Start();

            FormClosing += OnFormClosing;
            Resize += OnResize;
        }

        private void BuildUi()
        {
            Panel titleBar = new Panel();
            titleBar.Dock = DockStyle.Top;
            titleBar.Width = ClientSize.Width - 2;
            titleBar.Height = 64;
            titleBar.BackColor = Bg;
            titleBar.MouseDown += DragWindow;

            Label logo = new Label();
            logo.Text = "LU";
            logo.Font = new Font("Segoe UI", 14f, FontStyle.Bold);
            logo.ForeColor = Color.White;
            logo.BackColor = Purple;
            logo.TextAlign = ContentAlignment.MiddleCenter;
            logo.SetBounds(22, 14, 42, 36);
            titleBar.Controls.Add(logo);

            Label brand = new Label();
            brand.Text = "CLICK PRO";
            brand.Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold);
            brand.ForeColor = TextMain;
            brand.AutoSize = true;
            brand.Location = new Point(76, 12);
            brand.MouseDown += DragWindow;
            titleBar.Controls.Add(brand);

            Label subtitle = new Label();
            subtitle.Text = "P R E C I S I O N   /   C H A M P A G N E";
            subtitle.Font = new Font("Segoe UI", 7f);
            subtitle.ForeColor = TextMuted;
            subtitle.AutoSize = true;
            subtitle.Location = new Point(78, 37);
            subtitle.MouseDown += DragWindow;
            titleBar.Controls.Add(subtitle);

            Button close = WindowButton("×", Red);
            close.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            close.Location = new Point(ClientSize.Width - 51, 14);
            close.Click += delegate { exiting = true; Close(); };
            titleBar.Controls.Add(close);

            Button minimize = WindowButton("—", TextMuted);
            minimize.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            minimize.Location = new Point(ClientSize.Width - 96, 14);
            minimize.Click += delegate { WindowState = FormWindowState.Minimized; };
            titleBar.Controls.Add(minimize);
            Controls.Add(titleBar);

            Panel hero = MakeCard(22, 82, 876, 102);
            hero.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            statusDot = new Panel();
            statusDot.BackColor = TextMuted;
            statusDot.SetBounds(24, 23, 12, 12);
            hero.Controls.Add(statusDot);

            statusLabel = NewLabel("SẴN SÀNG", 45, 16, 180, 28, 12, FontStyle.Bold, TextMain);
            hero.Controls.Add(statusLabel);
            hero.Controls.Add(NewLabel("F1–F6 chạy profile  •  F7 lấy vị trí  •  F8 dừng khẩn cấp", 24, 52, 430, 26, 9.5f, FontStyle.Regular, TextMuted));

            countLabel = NewLabel("0", 490, 17, 100, 30, 17, FontStyle.Bold, TextMain);
            countLabel.TextAlign = ContentAlignment.MiddleCenter;
            hero.Controls.Add(countLabel);
            Label countCaption = NewLabel("TỔNG CLICK", 490, 52, 100, 20, 8, FontStyle.Bold, TextMuted);
            countCaption.TextAlign = ContentAlignment.MiddleCenter;
            hero.Controls.Add(countCaption);

            rateLabel = NewLabel("0.0", 610, 17, 100, 30, 17, FontStyle.Bold, Cyan);
            rateLabel.TextAlign = ContentAlignment.MiddleCenter;
            hero.Controls.Add(rateLabel);
            Label rateCaption = NewLabel("CLICK/GIÂY", 610, 52, 100, 20, 8, FontStyle.Bold, TextMuted);
            rateCaption.TextAlign = ContentAlignment.MiddleCenter;
            hero.Controls.Add(rateCaption);

            uptimeLabel = NewLabel("00:00", 730, 17, 110, 30, 17, FontStyle.Bold, TextMain);
            uptimeLabel.TextAlign = ContentAlignment.MiddleCenter;
            hero.Controls.Add(uptimeLabel);
            Label uptimeCaption = NewLabel("THỜI GIAN", 730, 52, 110, 20, 8, FontStyle.Bold, TextMuted);
            uptimeCaption.TextAlign = ContentAlignment.MiddleCenter;
            hero.Controls.Add(uptimeCaption);
            Controls.Add(hero);

            Panel profileBar = MakeCard(22, 198, 876, 52);
            profileBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            profileBar.Controls.Add(NewLabel("PROFILE", 18, 15, 72, 24, 8, FontStyle.Bold, Cyan));
            profileButtons = new Button[6];
            for (int i = 0; i < profileButtons.Length; i++)
            {
                int profileIndex = i;
                Button button = MakeSmallButton("F" + (i + 1), 93 + i * 126, 8, 112, 36, i == 0 ? Purple : Card2);
                button.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
                button.Click += delegate { SelectProfile(profileIndex, false); };
                profileButtons[i] = button;
                profileBar.Controls.Add(button);
            }
            Controls.Add(profileBar);

            Panel left = MakeCard(22, 266, 428, 362);
            left.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            Controls.Add(left);
            AddSectionTitle(left, "THIẾT LẬP CLICK", "Kiểm soát tốc độ và kiểu click", 22);

            left.Controls.Add(NewLabel("Nút chuột", 24, 83, 110, 22, 9, FontStyle.Bold, TextMuted));
            buttonBox = MakeCombo(new string[] { "Chuột trái", "Chuột phải", "Chuột giữa" }, 24, 107, 178);
            left.Controls.Add(buttonBox);
            left.Controls.Add(NewLabel("Kiểu click", 220, 83, 110, 22, 9, FontStyle.Bold, TextMuted));
            clickTypeBox = MakeCombo(new string[] { "Click đơn", "Click đúp" }, 220, 107, 178);
            left.Controls.Add(clickTypeBox);

            left.Controls.Add(Divider(24, 157, 374));
            left.Controls.Add(NewLabel("TỐC ĐỘ", 24, 171, 120, 22, 9, FontStyle.Bold, Cyan));
            intervalRadio = MakeRadio("Khoảng nghỉ", 24, 204, true);
            cpsRadio = MakeRadio("CPS", 220, 204, false);
            left.Controls.Add(intervalRadio);
            left.Controls.Add(cpsRadio);
            GroupRadios(left, intervalRadio, cpsRadio, 24, 204, 374);
            intervalBox = MakeNumber(24, 235, 178, 1, 3600000, 100);
            cpsBox = MakeNumber(220, 235, 178, 1, 1000, 10);
            left.Controls.Add(intervalBox);
            left.Controls.Add(cpsBox);
            left.Controls.Add(NewLabel("milliseconds", 30, 273, 120, 20, 8.5f, FontStyle.Regular, TextMuted));
            left.Controls.Add(NewLabel("click mỗi giây", 226, 273, 120, 20, 8.5f, FontStyle.Regular, TextMuted));

            intervalRadio.CheckedChanged += delegate { UpdateSpeedControls(); };
            cpsRadio.CheckedChanged += delegate { UpdateSpeedControls(); };

            left.Controls.Add(Divider(24, 304, 374));
            untilStoppedRadio = MakeRadio("Chạy đến khi dừng", 24, 321, true);
            repeatRadio = MakeRadio("Giới hạn", 220, 321, false);
            repeatRadio.Width = 76;
            left.Controls.Add(untilStoppedRadio);
            left.Controls.Add(repeatRadio);
            GroupRadios(left, untilStoppedRadio, repeatRadio, 24, 321, 272);
            repeatBox = MakeNumber(300, 316, 98, 1, 100000000, 100);
            left.Controls.Add(repeatBox);
            repeatRadio.CheckedChanged += delegate { repeatBox.Enabled = repeatRadio.Checked; };

            Panel right = MakeCard(470, 266, 428, 362);
            right.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(right);
            right.Controls.Add(NewLabel("VỊ TRÍ & NÂNG CAO", 24, 22, 350, 26, 11, FontStyle.Bold, TextMain));
            editingProfileLabel = NewLabel("ĐANG CHỈNH F1 · CURRENT", 24, 51, 380, 22, 9, FontStyle.Bold, Cyan);
            right.Controls.Add(editingProfileLabel);
            editingProfileLabel.BringToFront();

            cursorRadio = MakeRadio("Current · Theo con trỏ", 24, 84, true);
            fixedRadio = MakeRadio("Point · Vị trí cố định", 220, 84, false);
            right.Controls.Add(cursorRadio);
            right.Controls.Add(fixedRadio);

            right.Controls.Add(NewLabel("X", 24, 122, 20, 34, 10, FontStyle.Bold, TextMuted));
            xBox = MakeNumber(46, 120, 112, -32768, 32767, 0);
            right.Controls.Add(xBox);
            right.Controls.Add(NewLabel("Y", 174, 122, 20, 34, 10, FontStyle.Bold, TextMuted));
            yBox = MakeNumber(196, 120, 112, -32768, 32767, 0);
            right.Controls.Add(yBox);
            captureButton = MakeSmallButton("LẤY VỊ TRÍ", 320, 120, 84, 34, Purple);
            captureButton.Click += delegate { CapturePosition(); };
            right.Controls.Add(captureButton);
            fixedRadio.CheckedChanged += delegate { UpdatePositionControls(); };

            right.Controls.Add(Divider(24, 172, 380));
            right.Controls.Add(NewLabel("Độ lệch vị trí ngẫu nhiên", 24, 190, 230, 22, 9, FontStyle.Bold, TextMuted));
            jitterBox = MakeNumber(276, 184, 96, 0, 100, 0);
            right.Controls.Add(jitterBox);
            right.Controls.Add(NewLabel("px", 379, 188, 26, 22, 9, FontStyle.Regular, TextMuted));

            right.Controls.Add(NewLabel("Biến thiên thời gian", 24, 234, 230, 22, 9, FontStyle.Bold, TextMuted));
            varianceBox = MakeNumber(276, 228, 96, 0, 80, 0);
            right.Controls.Add(varianceBox);
            right.Controls.Add(NewLabel("%", 379, 232, 26, 22, 9, FontStyle.Regular, TextMuted));

            right.Controls.Add(Divider(24, 282, 380));
            topMostCheck = new CheckBox();
            topMostCheck.Text = "Luôn nổi trên cùng";
            topMostCheck.ForeColor = TextMain;
            topMostCheck.BackColor = Color.Transparent;
            topMostCheck.FlatStyle = FlatStyle.Standard;
            topMostCheck.SetBounds(24, 301, 180, 28);
            topMostCheck.CheckedChanged += delegate { TopMost = topMostCheck.Checked; };
            right.Controls.Add(topMostCheck);

            lockButton = MakeSmallButton("KHÓA CHẠY PROFILE", 214, 298, 190, 38, Purple);
            lockButton.Click += delegate { ToggleProfileLock(); };
            right.Controls.Add(lockButton);

            startButton = new Button();
            startButton.Text = "▶   BẮT ĐẦU   ·   F6";
            startButton.Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold);
            startButton.ForeColor = TextMain;
            startButton.BackColor = Purple;
            startButton.FlatStyle = FlatStyle.Flat;
            startButton.FlatAppearance.BorderSize = 0;
            startButton.FlatAppearance.BorderSize = 1;
            startButton.FlatAppearance.BorderColor = Cyan;
            startButton.Cursor = Cursors.Hand;
            startButton.SetBounds(22, 648, 876, 48);
            startButton.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            startButton.Click += delegate { ToggleClicking(); };
            Controls.Add(startButton);

            // Keep the title controls visible, and allow the content to scroll on small screens.
            Panel viewport = new Panel();
            viewport.SetBounds(1, 65, ClientSize.Width - 2, ClientSize.Height - 66);
            viewport.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            viewport.AutoScroll = true;
            Panel canvas = new Panel();
            canvas.Size = new Size(900, 635);
            foreach (Control section in new Control[] { hero, profileBar, left, right, startButton })
            {
                Point position = section.Location;
                section.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                canvas.Controls.Add(section);
                section.Location = new Point(position.X - 1, position.Y - 65);
            }
            viewport.Controls.Add(canvas);
            Controls.Add(viewport);
            viewport.BringToFront();
            titleBar.BringToFront();

            trayIcon = new NotifyIcon();
            trayIcon.Text = "LU Click Pro";
            trayIcon.Icon = Icon;
            trayIcon.Visible = true;
            ContextMenuStrip trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Bật / Tắt profile đang chọn", null, delegate { ToggleClicking(); });
            trayMenu.Items.Add("Hiện cửa sổ", null, delegate { ShowFromTray(); });
            trayMenu.Items.Add("Thoát", null, delegate { exiting = true; Close(); });
            trayIcon.ContextMenuStrip = trayMenu;
            trayIcon.DoubleClick += delegate { ShowFromTray(); };

            UpdateSpeedControls();
            UpdatePositionControls();
            UpdateProfileButtons();
            UpdateLockControls();
        }

        private Panel MakeCard(int x, int y, int w, int h)
        {
            Panel p = new Panel();
            p.BackColor = Card;
            p.SetBounds(x, y, w, h);
            p.Paint += delegate(object sender, PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = new GraphicsPath())
                using (Pen pen = new Pen(Color.FromArgb(64, 61, 55)))
                {
                    int d = 16;
                    path.AddArc(0, 0, d, d, 180, 90);
                    path.AddArc(p.Width - d - 1, 0, d, d, 270, 90);
                    path.AddArc(p.Width - d - 1, p.Height - d - 1, d, d, 0, 90);
                    path.AddArc(0, p.Height - d - 1, d, d, 90, 90);
                    path.CloseFigure();
                    e.Graphics.DrawPath(pen, path);
                }
            };
            return p;
        }

        private void AddSectionTitle(Control parent, string title, string subtitle, int y)
        {
            parent.Controls.Add(NewLabel(title, 24, y, 300, 26, 11, FontStyle.Bold, TextMain));
            parent.Controls.Add(NewLabel(subtitle, 24, y + 29, 350, 22, 9, FontStyle.Regular, TextMuted));
        }

        private Label NewLabel(string text, int x, int y, int w, int h, float size, FontStyle style, Color color)
        {
            Label l = new Label();
            l.Text = text;
            l.ForeColor = color;
            l.BackColor = Color.Transparent;
            l.Font = new Font("Segoe UI", size, style);
            l.SetBounds(x, y, w, h);
            return l;
        }

        private ComboBox MakeCombo(string[] items, int x, int y, int width)
        {
            ComboBox box = new ComboBox();
            box.DropDownStyle = ComboBoxStyle.DropDownList;
            box.FlatStyle = FlatStyle.Standard;
            box.BackColor = Card2;
            box.ForeColor = TextMain;
            box.Font = new Font("Segoe UI", 10f);
            box.Items.AddRange(items);
            box.SelectedIndex = 0;
            box.SetBounds(x, y, width, 36);
            return box;
        }

        private NumericUpDown MakeNumber(int x, int y, int width, decimal min, decimal max, decimal value)
        {
            NumericUpDown n = new NumericUpDown();
            n.Minimum = min;
            n.Maximum = max;
            n.Value = value;
            n.BackColor = Card2;
            n.ForeColor = TextMain;
            n.BorderStyle = BorderStyle.FixedSingle;
            n.Font = new Font("Segoe UI Semibold", 10f);
            n.TextAlign = HorizontalAlignment.Center;
            n.SetBounds(x, y, width, 36);
            return n;
        }

        private RadioButton MakeRadio(string text, int x, int y, bool check)
        {
            RadioButton r = new RadioButton();
            r.Text = text;
            r.Checked = check;
            r.ForeColor = TextMain;
            r.BackColor = Color.Transparent;
            r.FlatStyle = FlatStyle.Flat;
            r.FlatAppearance.CheckedBackColor = Color.FromArgb(83, 67, 43);
            r.FlatAppearance.BorderSize = 2;
            r.FlatAppearance.BorderColor = check ? Cyan : Color.FromArgb(99, 96, 87);
            r.Appearance = Appearance.Button;
            r.TextAlign = ContentAlignment.MiddleCenter;
            r.UseVisualStyleBackColor = false;
            r.BackColor = check ? Purple : Card2;
            r.Cursor = Cursors.Hand;
            r.CheckedChanged += delegate
            {
                r.BackColor = r.Checked ? Purple : Card2;
                r.ForeColor = r.Checked ? Color.White : TextMuted;
                r.FlatAppearance.BorderColor = r.Checked ? Cyan : Color.FromArgb(99, 96, 87);
            };
            r.SetBounds(x, y, 178, 28);
            return r;
        }

        private void GroupRadios(Control parent, RadioButton first, RadioButton second, int x, int y, int width)
        {
            Panel group = new Panel();
            group.SetBounds(x, y, width, 28);
            first.Location = new Point(0, 0);
            second.Location = new Point(196, 0);
            group.Controls.Add(first);
            group.Controls.Add(second);
            parent.Controls.Add(group);
        }

        private Button MakeSmallButton(string text, int x, int y, int w, int h, Color color)
        {
            Button b = new Button();
            b.Text = text;
            b.Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
            b.ForeColor = Color.White;
            b.BackColor = color;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Color.FromArgb(99, 96, 87);
            b.Cursor = Cursors.Hand;
            b.SetBounds(x, y, w, h);
            return b;
        }

        private Button WindowButton(string text, Color hover)
        {
            Button b = new Button();
            b.Text = text;
            b.Font = new Font("Segoe UI", 12f);
            b.ForeColor = TextMuted;
            b.BackColor = Color.Transparent;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.Size = new Size(38, 34);
            b.MouseEnter += delegate { b.ForeColor = hover; b.BackColor = Color.FromArgb(32, 38, 56); };
            b.MouseLeave += delegate { b.ForeColor = TextMuted; b.BackColor = Color.Transparent; };
            return b;
        }

        private Panel Divider(int x, int y, int width)
        {
            Panel p = new Panel();
            p.BackColor = Color.FromArgb(56, 54, 49);
            p.SetBounds(x, y, width, 1);
            return p;
        }

        private void UpdateSpeedControls()
        {
            if (intervalBox == null) return;
            intervalBox.Enabled = intervalRadio.Checked;
            cpsBox.Enabled = cpsRadio.Checked;
        }

        private void UpdatePositionControls()
        {
            if (xBox == null) return;
            bool enabled = fixedRadio.Checked;
            xBox.Enabled = enabled;
            yBox.Enabled = enabled;
            captureButton.Enabled = enabled;
            if (editingProfileLabel != null)
                editingProfileLabel.Text = (profiles[selectedProfile].Locked ? "ĐÃ KHÓA F" : "ĐANG CHỈNH F") + (selectedProfile + 1) + (fixedRadio.Checked ? " · POINT" : " · CURRENT");
        }

        private void ToggleProfileLock()
        {
            SaveCurrentToProfile();
            profiles[selectedProfile].Locked = !profiles[selectedProfile].Locked;
            if (profiles[selectedProfile].Locked && running) StopClicking(false);
            UpdateLockControls();
            SaveSettings();
        }

        private void UpdateLockControls()
        {
            if (lockButton == null) return;
            bool locked = profiles[selectedProfile].Locked;
            foreach (Control c in new Control[] { buttonBox, clickTypeBox, intervalRadio, cpsRadio,
                untilStoppedRadio, repeatRadio, cursorRadio, fixedRadio, jitterBox, varianceBox })
                c.Enabled = true;
            repeatBox.Enabled = repeatRadio.Checked;
            UpdateSpeedControls();
            UpdatePositionControls();
            lockButton.Text = locked ? "ĐÃ KHÓA · MỞ KHÓA" : "KHÓA CHẠY PROFILE";
            lockButton.BackColor = locked ? Color.FromArgb(83, 67, 43) : Card2;
            lockButton.FlatAppearance.BorderColor = locked ? Cyan : TextMuted;
            UpdateRunningUi();
            UpdateProfileButtons();
        }

        private void CapturePosition()
        {
            Point p = Cursor.Position;
            SetNumeric(xBox, p.X);
            SetNumeric(yBox, p.Y);
            fixedRadio.Checked = true;
            trayIcon.ShowBalloonTip(900, "LU Click Pro", "Đã lấy vị trí X=" + p.X + ", Y=" + p.Y, ToolTipIcon.Info);
        }

        private void ToggleClicking()
        {
            if (running) StopClicking(false); else StartClicking();
        }

        private void StartClicking()
        {
            if (profiles[selectedProfile].Locked) return;
            if (running) return;
            activeButton = buttonBox.SelectedIndex;
            activeDoubleClick = clickTypeBox.SelectedIndex == 1;
            activeFixedPosition = fixedRadio.Checked;
            activeX = (int)xBox.Value;
            activeY = (int)yBox.Value;
            activeJitter = (int)jitterBox.Value;
            activeBaseInterval = GetBaseInterval();
            activeVariance = (int)varianceBox.Value;
            activeRepeatLimit = repeatRadio.Checked ? (long)repeatBox.Value : 0;
            SaveCurrentToProfile();
            Interlocked.Increment(ref runGeneration);
            running = true;
            totalClicks = 0;
            stopwatch.Restart();
            UpdateRunningUi();
            ScheduleNext(20);
        }

        private void StopClicking(bool completed)
        {
            lock (timerLock)
            {
                running = false;
                Interlocked.Increment(ref runGeneration);
                if (clickTimer != null)
                {
                    clickTimer.Dispose();
                    clickTimer = null;
                }
            }
            stopwatch.Stop();
            if (!IsDisposed && IsHandleCreated)
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    UpdateRunningUi();
                    if (completed)
                        trayIcon.ShowBalloonTip(1200, "LU Click Pro", "Đã hoàn thành " + totalClicks + " click.", ToolTipIcon.Info);
                });
            }
        }

        private void ScheduleNext(int dueMs)
        {
            lock (timerLock)
            {
                if (!running) return;
                if (clickTimer != null) clickTimer.Dispose();
                int generation = Thread.VolatileRead(ref runGeneration);
                clickTimer = new System.Threading.Timer(ClickTick, generation, Math.Max(1, dueMs), Timeout.Infinite);
            }
        }

        private void ClickTick(object state)
        {
            int generation = (int)state;
            if (!running || generation != Thread.VolatileRead(ref runGeneration)) return;
            try
            {
                if (activeFixedPosition)
                {
                    int px = activeX;
                    int py = activeY;
                    int radius = activeJitter;
                    if (radius > 0)
                    {
                        lock (random)
                        {
                            px += random.Next(-radius, radius + 1);
                            py += random.Next(-radius, radius + 1);
                        }
                    }
                    Native.SetCursorPos(px, py);
                }

                PerformMouseClick(activeButton);
                Interlocked.Increment(ref totalClicks);
                if (activeDoubleClick && (activeRepeatLimit == 0 || totalClicks < activeRepeatLimit))
                {
                    Thread.Sleep(Math.Min(80, Math.Max(20, activeBaseInterval / 3)));
                    if (!running || generation != Thread.VolatileRead(ref runGeneration)) return;
                    PerformMouseClick(activeButton);
                    Interlocked.Increment(ref totalClicks);
                }

                if (activeRepeatLimit > 0 && totalClicks >= activeRepeatLimit)
                {
                    StopClicking(true);
                    return;
                }

                if (generation == Thread.VolatileRead(ref runGeneration))
                    ScheduleNext(GetNextInterval());
            }
            catch
            {
                if (generation == Thread.VolatileRead(ref runGeneration))
                    StopClicking(false);
            }
        }

        private int GetBaseInterval()
        {
            if (cpsRadio.Checked)
                return Math.Max(1, (int)Math.Round(1000.0 / (double)cpsBox.Value));
            return Math.Max(1, (int)intervalBox.Value);
        }

        private int GetNextInterval()
        {
            int interval = activeBaseInterval;
            int variance = activeVariance;
            if (variance == 0) return interval;
            int range = Math.Max(1, interval * variance / 100);
            lock (random) return Math.Max(1, interval + random.Next(-range, range + 1));
        }

        private void PerformMouseClick(int button)
        {
            Native.INPUT[] inputs = new Native.INPUT[2];
            inputs[0].type = Native.INPUT_MOUSE;
            inputs[1].type = Native.INPUT_MOUSE;
            uint down;
            uint up;
            if (button == 1)
            {
                down = Native.MOUSEEVENTF_RIGHTDOWN;
                up = Native.MOUSEEVENTF_RIGHTUP;
            }
            else if (button == 2)
            {
                down = Native.MOUSEEVENTF_MIDDLEDOWN;
                up = Native.MOUSEEVENTF_MIDDLEUP;
            }
            else
            {
                down = Native.MOUSEEVENTF_LEFTDOWN;
                up = Native.MOUSEEVENTF_LEFTUP;
            }
            inputs[0].mi.dwFlags = down;
            inputs[1].mi.dwFlags = up;
            Native.SendInput(2, inputs, Marshal.SizeOf(typeof(Native.INPUT)));
        }

        private void UiTimerTick(object sender, EventArgs e)
        {
            countLabel.Text = Interlocked.Read(ref totalClicks).ToString("N0");
            double secs = stopwatch.Elapsed.TotalSeconds;
            rateLabel.Text = secs > 0.2 ? (totalClicks / secs).ToString("0.0") : "0.0";
            uptimeLabel.Text = stopwatch.Elapsed.TotalHours >= 1
                ? stopwatch.Elapsed.ToString(@"hh\:mm\:ss")
                : stopwatch.Elapsed.ToString(@"mm\:ss");
        }

        private void UpdateRunningUi()
        {
            bool locked = profiles[selectedProfile].Locked;
            statusLabel.Text = running ? "ĐANG CHẠY" : (locked ? "PROFILE ĐÃ KHÓA" : "SẴN SÀNG");
            statusDot.BackColor = running ? Green : TextMuted;
            startButton.Text = running ? "■   DỪNG PROFILE F" + (selectedProfile + 1) : "▶   CHẠY PROFILE F" + (selectedProfile + 1);
            startButton.BackColor = running ? Red : Purple;
            startButton.Enabled = !locked || running;
            if (locked && !running) startButton.Text = "PROFILE F" + (selectedProfile + 1) + " ĐÃ KHÓA CHẠY";
            UpdateProfileButtons();
        }

        private void SelectProfile(int index, bool runFromHotkey)
        {
            if (index < 0 || index >= profiles.Length) return;
            if (runFromHotkey && profiles[index].Locked) return;
            if (runFromHotkey && running && selectedProfile == index)
            {
                StopClicking(false);
                return;
            }
            if (running) StopClicking(false);
            SaveCurrentToProfile();
            selectedProfile = index;
            ApplyProfile(profiles[index]);
            UpdateProfileButtons();
            UpdateRunningUi();
            if (runFromHotkey) StartClicking();
        }

        private void UpdateProfileButtons()
        {
            if (profileButtons == null) return;
            for (int i = 0; i < profileButtons.Length; i++)
            {
                bool active = i == selectedProfile;
                profileButtons[i].BackColor = active ? (running ? Green : Purple) : Card2;
                profileButtons[i].ForeColor = active ? Color.White : TextMuted;
                profileButtons[i].FlatAppearance.BorderSize = active ? 2 : 1;
                profileButtons[i].FlatAppearance.BorderColor = active ? Cyan : Color.FromArgb(79, 77, 70);
                profileButtons[i].Text = "F" + (i + 1) + (profiles[i].Locked ? " · KHÓA" : (active ? "  •" : ""));
            }
        }

        private void RegisterConfiguredHotkeys()
        {
            if (!IsHandleCreated) CreateHandle();
            Native.UnregisterHotKey(Handle, HOTKEY_CAPTURE);
            Native.UnregisterHotKey(Handle, HOTKEY_STOP);
            for (int i = 0; i < profiles.Length; i++)
            {
                Native.UnregisterHotKey(Handle, HOTKEY_PROFILE_BASE + i);
                Native.RegisterHotKey(Handle, HOTKEY_PROFILE_BASE + i, 0, (uint)((int)Keys.F1 + i));
            }
            Native.RegisterHotKey(Handle, HOTKEY_CAPTURE, 0, (uint)Keys.F7);
            Native.RegisterHotKey(Handle, HOTKEY_STOP, 0, (uint)Keys.F8);
            UpdateRunningUi();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id >= HOTKEY_PROFILE_BASE && id < HOTKEY_PROFILE_BASE + profiles.Length)
                    SelectProfile(id - HOTKEY_PROFILE_BASE, true);
                else if (id == HOTKEY_CAPTURE) CapturePosition();
                else if (id == HOTKEY_STOP) StopClicking(false);
                return;
            }
            if (m.Msg == WM_NCHITTEST)
            {
                base.WndProc(ref m);
                if ((int)m.Result == HTCLIENT) m.Result = (IntPtr)HTCAPTION;
                return;
            }
            base.WndProc(ref m);
        }

        private void DragWindow(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Native.ReleaseCapture();
                Native.SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
            }
        }

        private void OnResize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
                trayIcon.ShowBalloonTip(800, "LU Click Pro", "Ứng dụng vẫn chạy dưới khay hệ thống.", ToolTipIcon.Info);
            }
        }

        private void ShowFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!exiting)
            {
                e.Cancel = true;
                WindowState = FormWindowState.Minimized;
                return;
            }
            StopClicking(false);
            SaveSettings();
            for (int i = 0; i < profiles.Length; i++)
                Native.UnregisterHotKey(Handle, HOTKEY_PROFILE_BASE + i);
            Native.UnregisterHotKey(Handle, HOTKEY_CAPTURE);
            Native.UnregisterHotKey(Handle, HOTKEY_STOP);
            trayIcon.Visible = false;
            trayIcon.Dispose();
        }

        private void SaveSettings()
        {
            try
            {
                SaveCurrentToProfile();
                StringBuilder s = new StringBuilder();
                s.AppendLine("SelectedProfile=" + selectedProfile);
                s.AppendLine("TopMost=" + topMostCheck.Checked);
                for (int i = 0; i < profiles.Length; i++)
                {
                    string p = "P" + (i + 1) + ".";
                    ProfileConfig c = profiles[i];
                    s.AppendLine(p + "Button=" + c.Button);
                    s.AppendLine(p + "ClickType=" + c.ClickType);
                    s.AppendLine(p + "UseCps=" + c.UseCps);
                    s.AppendLine(p + "Interval=" + c.Interval);
                    s.AppendLine(p + "CPS=" + c.CPS);
                    s.AppendLine(p + "Repeat=" + c.Repeat);
                    s.AppendLine(p + "RepeatCount=" + c.RepeatCount);
                    s.AppendLine(p + "Fixed=" + c.Fixed);
                    s.AppendLine(p + "X=" + c.X);
                    s.AppendLine(p + "Y=" + c.Y);
                    s.AppendLine(p + "Jitter=" + c.Jitter);
                    s.AppendLine(p + "Variance=" + c.Variance);
                    s.AppendLine(p + "Locked=" + c.Locked);
                }
                File.WriteAllText(SettingsPath, s.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return;
                Dictionary<string, string> cfg = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string line in File.ReadAllLines(SettingsPath))
                {
                    int pos = line.IndexOf('=');
                    if (pos > 0) cfg[line.Substring(0, pos)] = line.Substring(pos + 1);
                }
                topMostCheck.Checked = Get(cfg, "TopMost", "False") == "True";
                for (int i = 0; i < profiles.Length; i++)
                {
                    string p = "P" + (i + 1) + ".";
                    ProfileConfig c = ProfileConfig.Default();
                    c.Button = GetInt(cfg, p + "Button", c.Button);
                    c.ClickType = GetInt(cfg, p + "ClickType", c.ClickType);
                    c.UseCps = GetBool(cfg, p + "UseCps", c.UseCps);
                    c.Interval = GetInt(cfg, p + "Interval", c.Interval);
                    c.CPS = GetInt(cfg, p + "CPS", c.CPS);
                    c.Repeat = GetBool(cfg, p + "Repeat", c.Repeat);
                    c.RepeatCount = GetInt(cfg, p + "RepeatCount", c.RepeatCount);
                    c.Fixed = GetBool(cfg, p + "Fixed", c.Fixed);
                    c.X = GetInt(cfg, p + "X", c.X);
                    c.Y = GetInt(cfg, p + "Y", c.Y);
                    c.Jitter = GetInt(cfg, p + "Jitter", c.Jitter);
                    c.Variance = GetInt(cfg, p + "Variance", c.Variance);
                    c.Locked = GetBool(cfg, p + "Locked", false);
                    profiles[i] = c;
                }
                selectedProfile = Math.Max(0, Math.Min(profiles.Length - 1, GetInt(cfg, "SelectedProfile", 0)));
                ApplyProfile(profiles[selectedProfile]);
                UpdateProfileButtons();
            }
            catch { }
        }

        private void SaveCurrentToProfile()
        {
            if (profiles == null || buttonBox == null) return;
            ProfileConfig c = profiles[selectedProfile];
            c.Button = buttonBox.SelectedIndex;
            c.ClickType = clickTypeBox.SelectedIndex;
            c.UseCps = cpsRadio.Checked;
            c.Interval = (int)intervalBox.Value;
            c.CPS = (int)cpsBox.Value;
            c.Repeat = repeatRadio.Checked;
            c.RepeatCount = (int)repeatBox.Value;
            c.Fixed = fixedRadio.Checked;
            c.X = (int)xBox.Value;
            c.Y = (int)yBox.Value;
            c.Jitter = (int)jitterBox.Value;
            c.Variance = (int)varianceBox.Value;
        }

        private void ApplyProfile(ProfileConfig c)
        {
            SetIndex(buttonBox, c.Button);
            SetIndex(clickTypeBox, c.ClickType);
            cpsRadio.Checked = c.UseCps;
            intervalRadio.Checked = !c.UseCps;
            SetNumeric(intervalBox, c.Interval);
            SetNumeric(cpsBox, c.CPS);
            repeatRadio.Checked = c.Repeat;
            untilStoppedRadio.Checked = !c.Repeat;
            SetNumeric(repeatBox, c.RepeatCount);
            fixedRadio.Checked = c.Fixed;
            cursorRadio.Checked = !c.Fixed;
            SetNumeric(xBox, c.X);
            SetNumeric(yBox, c.Y);
            SetNumeric(jitterBox, c.Jitter);
            SetNumeric(varianceBox, c.Variance);
            UpdateSpeedControls();
            UpdatePositionControls();
            UpdateLockControls();
        }

        private static string Get(Dictionary<string, string> cfg, string key, string fallback)
        {
            string value;
            return cfg.TryGetValue(key, out value) ? value : fallback;
        }

        private static int GetInt(Dictionary<string, string> cfg, string key, int fallback)
        {
            int value;
            return int.TryParse(Get(cfg, key, fallback.ToString()), out value) ? value : fallback;
        }

        private static bool GetBool(Dictionary<string, string> cfg, string key, bool fallback)
        {
            bool value;
            return bool.TryParse(Get(cfg, key, fallback.ToString()), out value) ? value : fallback;
        }

        private static void SetIndex(ComboBox box, int index)
        {
            if (index >= 0 && index < box.Items.Count) box.SelectedIndex = index;
        }

        private static void SetNumeric(NumericUpDown box, decimal value)
        {
            box.Value = Math.Min(box.Maximum, Math.Max(box.Minimum, value));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen pen = new Pen(Color.FromArgb(52, 62, 88)))
                e.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }
    }

    internal sealed class ProfileConfig
    {
        internal bool Locked;
        internal int Button;
        internal int ClickType;
        internal bool UseCps;
        internal int Interval;
        internal int CPS;
        internal bool Repeat;
        internal int RepeatCount;
        internal bool Fixed;
        internal int X;
        internal int Y;
        internal int Jitter;
        internal int Variance;

        internal static ProfileConfig Default()
        {
            ProfileConfig c = new ProfileConfig();
            c.Button = 0;
            c.ClickType = 0;
            c.UseCps = false;
            c.Interval = 100;
            c.CPS = 10;
            c.Repeat = false;
            c.RepeatCount = 100;
            c.Fixed = false;
            c.X = 0;
            c.Y = 0;
            c.Jitter = 0;
            c.Variance = 0;
            return c;
        }
    }

    internal static class AppIcon
    {
        internal static Icon Create()
        {
            Bitmap bitmap = new Bitmap(64, 64);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (LinearGradientBrush gradient = new LinearGradientBrush(
                    new Rectangle(0, 0, 64, 64),
                    Color.FromArgb(113, 88, 48),
                    Color.FromArgb(226, 198, 141),
                    45f))
                {
                    g.FillRoundedRectangle(gradient, new Rectangle(3, 3, 58, 58), 15);
                }
                using (Font font = new Font("Segoe UI", 22f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (StringFormat format = new StringFormat())
                {
                    format.Alignment = StringAlignment.Center;
                    format.LineAlignment = StringAlignment.Center;
                    g.DrawString("LU", font, Brushes.White, new RectangleF(2, 1, 60, 60), format);
                }
            }
            IntPtr handle = bitmap.GetHicon();
            Icon icon = (Icon)Icon.FromHandle(handle).Clone();
            Native.DestroyIcon(handle);
            bitmap.Dispose();
            return icon;
        }

        private static void FillRoundedRectangle(this Graphics g, Brush brush, Rectangle rect, int radius)
        {
            using (GraphicsPath path = new GraphicsPath())
            {
                int d = radius * 2;
                path.AddArc(rect.X, rect.Y, d, d, 180, 90);
                path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
                path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
                path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                g.FillPath(brush, path);
            }
        }
    }

    internal static class Native
    {
        internal const uint INPUT_MOUSE = 0;
        internal const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        internal const uint MOUSEEVENTF_LEFTUP = 0x0004;
        internal const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        internal const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        internal const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        internal const uint MOUSEEVENTF_MIDDLEUP = 0x0040;

        [StructLayout(LayoutKind.Sequential)]
        internal struct INPUT
        {
            internal uint type;
            internal MOUSEINPUT mi;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MOUSEINPUT
        {
            internal int dx;
            internal int dy;
            internal uint mouseData;
            internal uint dwFlags;
            internal uint time;
            internal IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint SendInput(uint count, INPUT[] inputs, int size);

        [DllImport("user32.dll")]
        internal static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint key);

        [DllImport("user32.dll")]
        internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        internal static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        internal static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        internal static extern bool DestroyIcon(IntPtr handle);
    }
}
