using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using Microsoft.Win32;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Web.Script.Serialization;

namespace SK150CControl
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

    public sealed class MainForm : Form
    {
        private enum ToggleKind
        {
            Normal,
            Danger,
            Current,
            Voltage
        }

        private const string VersionName = "SK150C_Control_v49";
        private const int MaxLogLines = 2000;
        private const string RegistryBasePath = @"Software\SK150C_Control";
        private static readonly TimeSpan FullChargeStartDelay = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan FullChargeLowCurrentDelay = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan FullChargeVoltageOffSettle = TimeSpan.FromSeconds(5);
        private static readonly Color VoltageColor = Color.FromArgb(36, 150, 96);
        private static readonly Color CurrentColor = Color.FromArgb(184, 134, 11);
        private static readonly Color PowerColor = Color.FromArgb(196, 68, 62);

        private readonly ComboBox portBox = new ComboBox();
        private readonly ComboBox baudBox = new ComboBox();
        private readonly ComboBox languageBox = new ComboBox();
        private readonly NumericUpDown slaveBox = new NumericUpDown();
        private readonly Button refreshButton = new Button();
        private readonly Button connectButton = new Button();
        private readonly Button disconnectButton = new Button();
        private readonly CheckBox autoPollBox = new CheckBox();
        private readonly Button interval100Button = new Button();
        private readonly Button interval300Button = new Button();
        private readonly Button interval1000Button = new Button();

        private readonly InlineValuePanel voutTile = new InlineValuePanel("輸出電壓", "V", VoltageColor, 16F);
        private readonly InlineValuePanel setVoltageTile = new InlineValuePanel("設定電壓", "V", VoltageColor, 16F);
        private readonly InlineValuePanel ioutTile = new InlineValuePanel("輸出電流", "A", CurrentColor, 16F);
        private readonly InlineValuePanel setCurrentTile = new InlineValuePanel("設定電流", "A", CurrentColor, 16F);
        private readonly InlineValuePanel powerTile = new InlineValuePanel("輸出功率", "W", PowerColor, 22F);
        private readonly InlineValuePanel ovpTile = new InlineValuePanel("OVP", "V", VoltageColor, 16F);
        private readonly InlineValuePanel ocpTile = new InlineValuePanel("OCP", "A", CurrentColor, 16F);
        private readonly InlineValuePanel temperatureTile = new InlineValuePanel("系統溫度", "C", Color.FromArgb(31, 38, 46), 15F);
        private readonly InlineValuePanel modeTile = new InlineValuePanel("輸出模式", "", Color.FromArgb(31, 38, 46), 15F);
        private readonly InlineValuePanel outputStateTile = new InlineValuePanel("輸出狀態", "", Color.FromArgb(31, 38, 46), 15F);
        private readonly InlineValuePanel capacityTile = new InlineValuePanel("容量 AH", "Ah", CurrentColor, 15F);
        private readonly InlineValuePanel energyTile = new InlineValuePanel("能量 WH", "Wh", PowerColor, 15F);
        private readonly InlineValuePanel timerTile = new InlineValuePanel("計時器", "", Color.FromArgb(31, 38, 46), 15F);
        private readonly InlineValuePanel protectTile = new InlineValuePanel("保護狀態", "", Color.FromArgb(31, 38, 46), 15F);
        private readonly TrendChart trendChart = new TrendChart();

        private readonly StepEditor setVoltageBox = new StepEditor(0.50M, 40.00M, 12.00M, 2, "V", 1.00M, 0.10M, VoltageColor);
        private readonly StepEditor setCurrentBox = new StepEditor(0.001M, 8.000M, 1.000M, 3, "A", 1.000M, 0.100M, CurrentColor);
        private readonly StepEditor setLvpBox = new StepEditor(0.00M, 40.00M, 0.00M, 2, "V", 1.00M, 0.10M, VoltageColor);
        private readonly StepEditor setOvpBox = new StepEditor(0.50M, 42.00M, 42.00M, 2, "V", 1.00M, 0.10M, VoltageColor);
        private readonly StepEditor setOcpBox = new StepEditor(0.001M, 8.200M, 8.200M, 3, "A", 1.000M, 0.100M, CurrentColor);
        private readonly StepEditor setOppBox = new StepEditor(0.0M, 160.0M, 160.0M, 1, "W", 10.0M, 1.0M, PowerColor);
        private readonly StepEditor fullChargeCurrentBox = new StepEditor(0.001M, 8.000M, 0.100M, 3, "A", 0.100M, 0.010M, CurrentColor);
        private readonly StepEditor fullChargeVoltageTargetBox = new StepEditor(0.50M, 40.00M, 12.00M, 2, "V", 1.00M, 0.10M, VoltageColor);
        private readonly StepEditor fullChargeVoltagePollBox = new StepEditor(5M, 3600M, 60M, 0, "s", 60M, 10M, Color.FromArgb(31, 38, 46));
        private readonly ComboBox backlightBox = new ComboBox();
        private readonly NumericUpDown sleepBox = new NumericUpDown();
        private readonly CheckBox buzzerBox = new CheckBox();
        private readonly CheckBox keyLockBox = new CheckBox();
        private readonly CheckBox quickInputBox = new CheckBox();
        private readonly Button restartButton = new Button();
        private readonly Button factoryButton = new Button();
        private readonly Button calibrationButton = new Button();
        private readonly Button exportGroupsButton = new Button();
        private readonly Button importGroupsButton = new Button();
        private readonly Button writeVoltageButton = new Button();
        private readonly Button writeCurrentButton = new Button();
        private readonly Button writeLvpButton = new Button();
        private readonly Button writeOvpButton = new Button();
        private readonly Button writeOcpButton = new Button();
        private readonly Button writeOppButton = new Button();
        private readonly Button applyFullChargeCurrentButton = new Button();
        private readonly Button applyFullChargeVoltageButton = new Button();
        private readonly Button applyFullChargeVoltagePollButton = new Button();
        private readonly Button fullChargeCurrentToggleButton = new Button();
        private readonly Button fullChargeVoltageToggleButton = new Button();
        private readonly Button siniToggleButton = new Button();
        private readonly Button outputOnButton = new Button();
        private readonly Button outputOffButton = new Button();
        private readonly Button clearLogButton = new Button();
        private readonly Button refreshQuickGroupsButton = new Button();
        private readonly CheckBox fullChargeCurrentEnableBox = new CheckBox();
        private readonly CheckBox fullChargeVoltageEnableBox = new CheckBox();
        private readonly Label fullChargeCurrentStatusLabel = new Label();
        private readonly Label fullChargeVoltageStatusLabel = new Label();
        private readonly Label editGroupLabel = new Label();
        private readonly Dictionary<StepEditor, Panel> parameterBlocks = new Dictionary<StepEditor, Panel>();
        private readonly Dictionary<int, QuickGroupButton> quickGroupButtons = new Dictionary<int, QuickGroupButton>();
        private readonly double?[] quickGroupVoltages = new double?[11];
        private readonly double?[] quickGroupCurrents = new double?[11];
        private readonly bool?[] quickGroupSini = new bool?[11];
        private readonly bool?[] quickGroupCurrentCutoff = new bool?[11];
        private readonly bool?[] quickGroupVoltageCutoff = new bool?[11];

        private readonly Label statusLabel = new Label();
        private readonly Label successLabel = new Label();
        private readonly Label failLabel = new Label();
        private readonly Label crcLabel = new Label();
        private readonly Label retryLabel = new Label();
        private readonly Label productModelLabel = new Label();
        private readonly Label firmwareVersionLabel = new Label();
        private readonly Label stateLabel = new Label();
        private readonly TextBox logBox = new TextBox();
        private readonly System.Windows.Forms.Timer pollTimer = new System.Windows.Forms.Timer();

        private SerialPort serial;
        private bool busy;
        private int successCount;
        private int failCount;
        private int crcErrorCount;
        private int retryCount;
        private int currentGroup;
        private int activeQuickGroup = -1;
        private int pollCounter;
        private int editGroup = -1;
        private int pollIntervalMs = 1000;
        private int consecutivePollFailures;
        private bool pollBackoffActive;
        private int logLineCount;
        private int currentProtectCode;
        private bool syncingOptions;
        private bool syncingFullChargeSettings;
        private bool startupAutoConnectAttempted;
        private double latestVout;
        private double latestIout;
        private bool latestOutputOn;
        private DateTime? outputOnSince;
        private DateTime? lowCurrentSince;
        private DateTime? nextVoltageCheckAt;
        private DateTime? voltageOffConfirmedAt;
        private bool autoOffInProgress;
        private bool voltageCheckInProgress;
        private bool voltageOffCommandPending;
        private bool voltageReadPending;
        private bool voltageRestoreOnPending;
        private decimal voltageRestorePollSeconds;

        public MainForm()
        {
            Text = "SK150C Modbus 控制台";
            Width = 1180;
            Height = 760;
            MinimumSize = new Size(1060, 680);
            Font = new Font("Microsoft JhengHei UI", 9F);
            BackColor = Color.FromArgb(244, 246, 248);
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;

            BuildUi();
            ApplyLanguage();
            RefreshPorts();
            pollTimer.Tick += PollTimer_Tick;
            autoPollBox.Checked = true;
            SetPollInterval(1000);
            UpdateCounters();
            UpdateDeviceInfo(null, null);
            Shown += delegate { TryStartupAutoConnect(); };
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(12);
            root.ColumnCount = 3;
            root.RowCount = 3;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 310));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
            Controls.Add(root);

            var header = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(35, 38, 43), ColumnCount = 2, RowCount = 1, Padding = new Padding(16, 0, 16, 0) };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360F));
            root.SetColumnSpan(header, 3);
            root.Controls.Add(header, 0, 0);
            var title = new Label
            {
                Text = "SK150C Modbus 控制台",
                ForeColor = Color.White,
                Font = new Font(Font.FontFamily, 15F, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            statusLabel.Text = Ui("未連線");
            statusLabel.ForeColor = Color.FromArgb(255, 214, 102);
            statusLabel.Font = new Font(Font.FontFamily, 10F, FontStyle.Bold);
            statusLabel.AutoSize = false;
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.TextAlign = ContentAlignment.MiddleRight;
            header.Controls.Add(title, 0, 0);
            header.Controls.Add(statusLabel, 1, 0);

            root.Controls.Add(BuildLeftPanel(), 0, 1);
            root.Controls.Add(BuildCenterPanel(), 1, 1);
            root.Controls.Add(BuildRightPanel(), 2, 1);

            var logPanel = Box("TX / RX 通訊紀錄");
            root.SetColumnSpan(logPanel, 3);
            root.Controls.Add(logPanel, 0, 2);
            logBox.Dock = DockStyle.Fill;
            logBox.Multiline = true;
            logBox.ScrollBars = ScrollBars.Vertical;
            logBox.ReadOnly = true;
            logBox.BackColor = Color.FromArgb(22, 24, 27);
            logBox.ForeColor = Color.FromArgb(225, 232, 240);
            logBox.Font = new Font("Consolas", 9F);
            logPanel.Controls.Add(logBox);

            clearLogButton.Text = "清除";
            clearLogButton.Width = 64;
            clearLogButton.Height = 26;
            clearLogButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            clearLogButton.Location = new Point(logPanel.Width - 76, 8);
            clearLogButton.Click += delegate { logBox.Clear(); logLineCount = 0; };
            logPanel.Controls.Add(clearLogButton);
            logPanel.Resize += delegate { clearLogButton.Left = logPanel.Width - 76; };
        }

        private Control BuildLeftPanel()
        {
            var panel = Box("連線設定");
            var container = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            container.RowStyles.Add(new RowStyle(SizeType.Absolute, 160));
            panel.Controls.Add(container);

            var flow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 30, Padding = new Padding(12, 32, 12, 4) };
            flow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
            flow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            container.Controls.Add(flow, 0, 0);

            AddRow(flow, 0, "COM 埠", portBox);
            portBox.DropDownStyle = ComboBoxStyle.DropDownList;
            AddRow(flow, 1, "波特率", baudBox);
            baudBox.DropDownStyle = ComboBoxStyle.DropDownList;
            baudBox.Items.AddRange(new object[] { "9600", "19200", "38400", "57600", "115200" });
            baudBox.SelectedItem = "115200";
            slaveBox.Minimum = 1;
            slaveBox.Maximum = 247;
            slaveBox.Value = 1;
            AddRow(flow, 2, "站號", slaveBox);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            refreshButton.Text = "掃描";
            connectButton.Text = "連線";
            disconnectButton.Text = "斷線";
            refreshButton.Width = connectButton.Width = disconnectButton.Width = 62;
            refreshButton.Click += delegate { RefreshPorts(); };
            connectButton.Click += delegate { Connect(); };
            disconnectButton.Click += delegate { Disconnect(); };
            buttons.Controls.Add(refreshButton);
            buttons.Controls.Add(connectButton);
            buttons.Controls.Add(disconnectButton);
            flow.Controls.Add(buttons, 0, 3);
            flow.SetColumnSpan(buttons, 2);

            autoPollBox.Text = "自動輪詢";
            flow.Controls.Add(autoPollBox, 0, 4);
            flow.SetColumnSpan(autoPollBox, 2);
            flow.Controls.Add(new Label { Text = "間隔", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 5);
            flow.Controls.Add(BuildIntervalButtons(), 1, 5);

            var sep = new Label { Height = 18, Dock = DockStyle.Fill };
            flow.Controls.Add(sep, 0, 6);
            flow.SetColumnSpan(sep, 2);

            AddMetric(flow, 7, "成功", successLabel);
            AddMetric(flow, 8, "失敗", failLabel);
            AddMetric(flow, 9, "CRC 錯", crcLabel);
            AddMetric(flow, 10, "重試", retryLabel);

            var infoTitle = SectionText("裝置資訊");
            flow.Controls.Add(infoTitle, 0, 12);
            flow.SetColumnSpan(infoTitle, 2);
            AddMetric(flow, 13, "產品型號", productModelLabel);
            AddMetric(flow, 14, "固件版本", firmwareVersionLabel);

            var deviceTitle = SectionText("裝置選項");
            flow.Controls.Add(deviceTitle, 0, 16);
            flow.SetColumnSpan(deviceTitle, 2);

            backlightBox.DropDownStyle = ComboBoxStyle.DropDownList;
            backlightBox.Items.AddRange(new object[] { "0", "1", "2", "3", "4", "5" });
            backlightBox.SelectedIndexChanged += delegate
            {
                if (!syncingOptions && backlightBox.SelectedItem != null)
                    QueueWriteRegister(0x0014, int.Parse(backlightBox.SelectedItem.ToString()), "背光亮度");
            };
            AddRow(flow, 17, "背光亮度", backlightBox);

            sleepBox.Minimum = 0;
            sleepBox.Maximum = 99;
            sleepBox.ValueChanged += delegate
            {
                if (!syncingOptions) QueueWriteRegister(0x0015, (int)sleepBox.Value, "熄屏時間");
            };
            AddRow(flow, 18, "熄屏時間", sleepBox);

            buzzerBox.Text = "蜂鳴音";
            buzzerBox.CheckedChanged += delegate
            {
                if (!syncingOptions) QueueWriteRegister(0x001C, buzzerBox.Checked ? 1 : 0, "蜂鳴音");
            };
            flow.Controls.Add(buzzerBox, 0, 19);
            flow.SetColumnSpan(buzzerBox, 2);

            keyLockBox.Text = "按鍵鎖";
            keyLockBox.CheckedChanged += delegate
            {
                if (!syncingOptions) QueueWriteRegister(0x000F, keyLockBox.Checked ? 1 : 0, "按鍵鎖");
            };
            flow.Controls.Add(keyLockBox, 0, 20);
            flow.SetColumnSpan(keyLockBox, 2);

            var maintenanceTitle = SectionText("維護功能");
            flow.Controls.Add(maintenanceTitle, 0, 22);
            flow.SetColumnSpan(maintenanceTitle, 2);

            var maintenance = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
            restartButton.Text = "重啟";
            factoryButton.Text = "恢復出廠";
            calibrationButton.Text = "校準";
            restartButton.Width = 66;
            factoryButton.Width = 82;
            calibrationButton.Width = 66;
            restartButton.Height = factoryButton.Height = calibrationButton.Height = 30;
            restartButton.Click += delegate { ConfirmAndWrite("重啟裝置", "確定要送出重啟命令？輸出可能會中斷。", 0x002F, "重啟"); };
            factoryButton.Click += delegate { ConfirmAndWrite("恢復出廠設定", "確定要恢復出廠設定？目前參數可能會被清除。", 0x0020, "恢復出廠設定"); };
            calibrationButton.Click += delegate { OpenCalibrationWindow(); };
            maintenance.Controls.Add(restartButton);
            maintenance.Controls.Add(factoryButton);
            maintenance.Controls.Add(calibrationButton);
            flow.Controls.Add(maintenance, 0, 23);
            flow.SetColumnSpan(maintenance, 2);

            var groupIoTitle = SectionText("M組參數");
            flow.Controls.Add(groupIoTitle, 0, 24);
            flow.SetColumnSpan(groupIoTitle, 2);

            var groupIo = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
            exportGroupsButton.Text = "匯出 M組";
            importGroupsButton.Text = "匯入 M組";
            exportGroupsButton.Width = importGroupsButton.Width = 86;
            exportGroupsButton.Height = importGroupsButton.Height = 30;
            exportGroupsButton.Click += delegate { ExportGroupSettings(); };
            importGroupsButton.Click += delegate { ImportGroupSettings(); };
            groupIo.Controls.Add(exportGroupsButton);
            groupIo.Controls.Add(importGroupsButton);
            flow.Controls.Add(groupIo, 0, 25);
            flow.SetColumnSpan(groupIo, 2);

            languageBox.DropDownStyle = ComboBoxStyle.DropDownList;
            languageBox.Items.AddRange(new object[] { "中文", "English" });
            languageBox.SelectedItem = InitialLanguageSelection();
            languageBox.SelectedIndexChanged += delegate { ApplyLanguage(); };
            AddRow(flow, 27, "語言", languageBox);

            SetStatusText("待命");
            container.Controls.Add(BuildSignaturePanel(), 0, 1);
            return panel;
        }

        private Control BuildCenterPanel()
        {
            var panel = Box("即時監控與歷史曲線");
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(12, 32, 12, 12) };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 270));
            panel.Controls.Add(layout);

            var tiles = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 5 };
            powerTile.ShowTrend = true;
            ApplyTileGroupColors();
            for (int i = 0; i < 4; i++) tiles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            for (int i = 0; i < 5; i++) tiles.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
            tiles.Controls.Add(setVoltageTile, 0, 0);
            tiles.Controls.Add(capacityTile, 1, 0);
            tiles.Controls.Add(powerTile, 2, 0);
            tiles.SetRowSpan(powerTile, 3);
            tiles.SetColumnSpan(powerTile, 2);
            tiles.Controls.Add(setCurrentTile, 0, 1);
            tiles.Controls.Add(energyTile, 1, 1);
            tiles.Controls.Add(ovpTile, 0, 2);
            tiles.Controls.Add(timerTile, 1, 2);
            tiles.Controls.Add(ocpTile, 0, 3);
            tiles.Controls.Add(modeTile, 1, 3);
            tiles.Controls.Add(voutTile, 2, 3);
            tiles.Controls.Add(temperatureTile, 0, 4);
            tiles.Controls.Add(outputStateTile, 1, 4);
            tiles.Controls.Add(ioutTile, 2, 4);
            SetupStatusBadge(stateLabel, StatusText("待命"), Color.FromArgb(35, 96, 73), 16F);
            tiles.Controls.Add(stateLabel, 3, 3);
            tiles.SetRowSpan(stateLabel, 2);
            layout.Controls.Add(trendChart, 0, 0);
            layout.Controls.Add(tiles, 0, 1);
            return panel;
        }

        private void ApplyTileGroupColors()
        {
            Color target = Color.FromArgb(239, 248, 243);
            Color running = Color.FromArgb(239, 245, 250);
            Color actual = Color.FromArgb(252, 241, 241);

            setVoltageTile.SetBaseBackColor(target);
            setCurrentTile.SetBaseBackColor(target);
            ovpTile.SetBaseBackColor(target);
            ocpTile.SetBaseBackColor(target);
            temperatureTile.SetBaseBackColor(target);

            capacityTile.SetBaseBackColor(running);
            energyTile.SetBaseBackColor(running);
            timerTile.SetBaseBackColor(running);
            modeTile.SetBaseBackColor(running);
            outputStateTile.SetBaseBackColor(running);

            powerTile.SetBaseBackColor(actual);
            voutTile.SetBaseBackColor(actual);
            ioutTile.SetBaseBackColor(actual);
            protectTile.SetBaseBackColor(actual);
        }

        private Control BuildIntervalButtons()
        {
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = new Padding(0) };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
            SetupIntervalButton(interval100Button, "100", 100);
            SetupIntervalButton(interval300Button, "300", 300);
            SetupIntervalButton(interval1000Button, "1000", 1000);
            panel.Controls.Add(interval100Button, 0, 0);
            panel.Controls.Add(interval300Button, 1, 0);
            panel.Controls.Add(interval1000Button, 2, 0);
            return panel;
        }

        private void SetupIntervalButton(Button button, string text, int interval)
        {
            button.Text = text;
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(1);
            button.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            button.Tag = interval;
            button.Click += delegate { SetPollInterval((int)button.Tag); };
        }

        private Control BuildSignaturePanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 4, 12, 10), BackColor = Color.White };
            var picture = new PictureBox
            {
                Dock = DockStyle.Top,
                Height = 96,
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = LoadSignatureImage(),
                BackColor = Color.White
            };
            var version = new Label
            {
                Text = VersionName,
                Dock = DockStyle.Top,
                Height = 22,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(42, 48, 56)
            };
            var author = new Label
            {
                Text = "作者：鄭large",
                Dock = DockStyle.Top,
                Height = 22,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Microsoft JhengHei UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(76, 86, 96)
            };
            panel.Controls.Add(author);
            panel.Controls.Add(version);
            panel.Controls.Add(picture);
            return panel;
        }

        private static Image LoadSignatureImage()
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream("SK150CControl.signature.jpg"))
            {
                if (stream != null)
                {
                    using (var image = Image.FromStream(stream))
                    {
                        return new Bitmap(image);
                    }
                }
            }
            return null;
        }

        private Control BuildRightPanel()
        {
            var panel = Box("輸出控制");
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(0, 34, 0, 0) };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
            panel.Controls.Add(layout);
            quickInputBox.Text = "快速輸入";
            quickInputBox.Checked = true;
            quickInputBox.AutoSize = true;
            quickInputBox.Font = new Font("Microsoft JhengHei UI", 8.5F, FontStyle.Bold);
            quickInputBox.ForeColor = Color.FromArgb(42, 48, 56);
            quickInputBox.BackColor = Color.White;
            quickInputBox.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            quickInputBox.Location = new Point(Math.Max(120, panel.Width - 96), 9);
            quickInputBox.CheckedChanged += delegate { ApplyQuickInputMode(); };
            panel.Resize += delegate { quickInputBox.Location = new Point(Math.Max(120, panel.Width - quickInputBox.Width - 12), 9); };
            panel.Controls.Add(quickInputBox);
            quickInputBox.BringToFront();
            ApplyQuickInputMode();

            var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(8, 4) };
            var commonTab = new TabPage("常用") { BackColor = Color.White, Padding = new Padding(0) };
            var advancedTab = new TabPage("進階") { BackColor = Color.White, Padding = new Padding(0) };
            tabs.TabPages.Add(commonTab);
            tabs.TabPages.Add(advancedTab);
            layout.Controls.Add(tabs, 0, 0);

            var commonFlow = BuildRightFlow();
            var advancedFlow = BuildRightFlow();
            commonTab.Controls.Add(BuildScrollHost(commonFlow));
            advancedTab.Controls.Add(BuildScrollHost(advancedFlow));

            writeVoltageButton.Text = "寫入電壓";
            writeVoltageButton.Click += delegate
            {
                QueueVerifiedWrite(0x0000, (int)Math.Round((double)setVoltageBox.Value * 100.0), "設定電壓", setVoltageBox,
                    delegate
                    {
                        UpdateCurrentGroupVoltage((double)setVoltageBox.Value);
                        UpdateTrendTargetsFromEditors();
                    });
            };
            commonFlow.Controls.Add(BuildParameterBlock("設定電壓", setVoltageBox, writeVoltageButton));

            writeCurrentButton.Text = "寫入電流";
            writeCurrentButton.Click += delegate
            {
                QueueVerifiedWrite(0x0001, (int)Math.Round((double)setCurrentBox.Value * 1000.0), "設定電流", setCurrentBox,
                    delegate
                    {
                        UpdateCurrentGroupCurrent((double)setCurrentBox.Value);
                        UpdateTrendTargetsFromEditors();
                    });
            };
            commonFlow.Controls.Add(BuildParameterBlock("設定電流", setCurrentBox, writeCurrentButton));

            writeOvpButton.Text = "寫入 OVP";
            writeOvpButton.Click += delegate
            {
                QueueVerifiedWrite(CurrentGroupBase() + 3, (int)Math.Round((double)setOvpBox.Value * 100.0), "設定 OVP M" + currentGroup, setOvpBox);
            };
            commonFlow.Controls.Add(BuildParameterBlock("OVP 過壓保護", setOvpBox, writeOvpButton));

            writeOcpButton.Text = "寫入 OCP";
            writeOcpButton.Click += delegate
            {
                QueueVerifiedWrite(CurrentGroupBase() + 4, (int)Math.Round((double)setOcpBox.Value * 1000.0), "設定 OCP M" + currentGroup, setOcpBox);
            };
            commonFlow.Controls.Add(BuildParameterBlock("OCP 過流保護", setOcpBox, writeOcpButton));

            commonFlow.Controls.Add(BuildFullChargePanel());

            writeLvpButton.Text = "寫入 LVP";
            writeLvpButton.Click += delegate
            {
                QueueVerifiedWrite(CurrentGroupBase() + 2, (int)Math.Round((double)setLvpBox.Value * 100.0), "設定 LVP M" + currentGroup, setLvpBox);
            };
            advancedFlow.Controls.Add(BuildParameterBlock("LVP 低壓保護", setLvpBox, writeLvpButton));

            writeOppButton.Text = "寫入 OPP";
            writeOppButton.Click += delegate
            {
                QueueVerifiedWrite(CurrentGroupBase() + 5, (int)Math.Round((double)setOppBox.Value * 10.0), "設定 OPP M" + currentGroup, setOppBox);
            };
            advancedFlow.Controls.Add(BuildParameterBlock("OPP 過功率保護", setOppBox, writeOppButton));
            advancedFlow.Controls.Add(BuildSiniPanel());
            BindEnterToWriteButtons();

            var quick = new TableLayoutPanel { Width = 260, Height = 228, ColumnCount = 2, RowCount = 6, Margin = new Padding(0, 0, 0, 4) };
            quick.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            quick.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            for (int r = 0; r < 6; r++) quick.RowStyles.Add(new RowStyle(SizeType.Percent, 16.666F));
            commonFlow.Controls.Add(SectionText("快捷組"));
            for (int i = 0; i <= 10; i++)
            {
                int column = i <= 5 ? 0 : 1;
                int row = i <= 5 ? i : i - 6;
                quick.Controls.Add(BuildQuickGroupPair(i), column, row);
            }
            commonFlow.Controls.Add(quick);

            layout.Controls.Add(BuildRightFooterPanel(), 0, 1);
            LoadFullChargeSettings(currentGroup);
            return panel;
        }

        private Control BuildSiniPanel()
        {
            var block = new Panel { Width = 260, Height = 52, Margin = new Padding(0, 0, 0, 4), BackColor = Color.FromArgb(250, 251, 252) };
            var title = SectionText("S-INI 調用後輸出");
            title.SetBounds(0, 0, 170, 28);
            title.Dock = DockStyle.None;

            siniToggleButton.SetBounds(190, 4, 70, 34);
            StyleOnOffToggle(siniToggleButton, false, ToggleKind.Danger);
            siniToggleButton.Click += delegate
            {
                bool next = !(currentGroup >= 0 && currentGroup < quickGroupSini.Length && quickGroupSini[currentGroup].GetValueOrDefault(false));
                QueueWriteRegister(CurrentGroupBase() + 13, next ? 1 : 0, "S-INI M" + currentGroup);
                SetQuickGroupSini(currentGroup, next);
                SetStatusText("S-INI M" + currentGroup + " " + (next ? "ON" : "OFF"));
            };

            block.Controls.Add(title);
            block.Controls.Add(siniToggleButton);
            return block;
        }

        private static FlowLayoutPanel BuildRightFlow()
        {
            return new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(8, 8, 8, 8)
            };
        }

        private static Control BuildScrollHost(Control content)
        {
            var scrollHost = new Panel { Dock = DockStyle.Fill, AutoScroll = false, Padding = new Padding(0), BackColor = Color.White };
            scrollHost.Controls.Add(content);
            return scrollHost;
        }

        private Control BuildFullChargePanel()
        {
            var flow = new FlowLayoutPanel
            {
                Width = 260,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 4)
            };

            fullChargeCurrentBox.UseCompactInput();
            fullChargeVoltageTargetBox.UseCompactInput();
            fullChargeVoltagePollBox.UseCompactInput();

            fullChargeCurrentEnableBox.CheckedChanged += delegate
            {
                if (!syncingFullChargeSettings) SaveFullChargeSettings();
                StyleOnOffToggle(fullChargeCurrentToggleButton, fullChargeCurrentEnableBox.Checked, ToggleKind.Current);
                UpdateQuickGroupCutoffIndicators(currentGroup);
                UpdateFullChargeStatusLabels();
            };

            applyFullChargeCurrentButton.Text = "套用截止電流";
            applyFullChargeCurrentButton.Click += delegate { SaveFullChargeSettings(); ClearDirty(fullChargeCurrentBox); };
            fullChargeCurrentToggleButton.Click += delegate { fullChargeCurrentEnableBox.Checked = !fullChargeCurrentEnableBox.Checked; };
            flow.Controls.Add(BuildFullChargeParameterBlock("滿電截止電流", fullChargeCurrentBox, applyFullChargeCurrentButton, fullChargeCurrentToggleButton));
            flow.Controls.Add(BuildStatusLine(fullChargeCurrentStatusLabel, "滿電截止電流說明",
                "充電接近滿電時，輸出電流會逐漸下降。\r\n當電流低於指定目標後，程式會自動關閉輸出。"));

            fullChargeVoltageEnableBox.CheckedChanged += delegate
            {
                if (!syncingFullChargeSettings) SaveFullChargeSettings();
                StyleOnOffToggle(fullChargeVoltageToggleButton, fullChargeVoltageEnableBox.Checked, ToggleKind.Voltage);
                UpdateQuickGroupCutoffIndicators(currentGroup);
                UpdateFullChargeStatusLabels();
            };

            applyFullChargeVoltageButton.Text = "套用電壓";
            applyFullChargeVoltageButton.Click += delegate { SaveFullChargeSettings(); ClearDirty(fullChargeVoltageTargetBox); };
            fullChargeVoltageToggleButton.Click += delegate { fullChargeVoltageEnableBox.Checked = !fullChargeVoltageEnableBox.Checked; };

            applyFullChargeVoltagePollButton.Text = "套用";
            applyFullChargeVoltagePollButton.Click += delegate { SaveFullChargeSettings(); ClearDirty(fullChargeVoltagePollBox); };
            flow.Controls.Add(BuildFullChargeVoltageRow());
            flow.Controls.Add(BuildStatusLine(fullChargeVoltageStatusLabel, "滿電截止電壓說明",
                "使用前需將探頭接在電池輸出端，確保能量測到電池正負極電壓。\r\n此功能會依設定週期偵測電壓，當電壓高於設定目標時自動關閉輸出。"));
            return flow;
        }

        private Control BuildFullChargeParameterBlock(string title, StepEditor editor, Button applyButton, Button toggleButton)
        {
            var block = new Panel { Width = 260, Height = 78, Margin = new Padding(0, 0, 0, 4), BackColor = Color.FromArgb(250, 251, 252) };
            parameterBlocks[editor] = block;
            editor.UserChanged += delegate { UpdateParameterBlockDirty(editor); };

            var titleLabel = SectionText(title);
            titleLabel.SetBounds(0, 0, 260, 20);
            titleLabel.Dock = DockStyle.None;
            editor.Dock = DockStyle.None;
            editor.SetBounds(0, 21, 260, 26);

            applyButton.SetBounds(0, 49, 196, 26);
            toggleButton.SetBounds(202, 49, 58, 26);
            applyButton.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            toggleButton.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            StyleOnOffToggle(toggleButton, false, ToggleKind.Current);

            block.Controls.Add(titleLabel);
            block.Controls.Add(editor);
            block.Controls.Add(applyButton);
            block.Controls.Add(toggleButton);
            block.Resize += delegate
            {
                titleLabel.Width = block.Width;
                editor.Width = block.Width;
                applyButton.Width = Math.Max(110, block.Width - 64);
                toggleButton.Left = block.Width - toggleButton.Width;
            };
            UpdateParameterBlockDirty(editor);
            return block;
        }

        private Control BuildFullChargeVoltageRow()
        {
            var outer = new Panel { Width = 260, Height = 78, Margin = new Padding(0, 0, 0, 4), BackColor = Color.White };
            var voltageBlock = new Panel { BackColor = Color.FromArgb(250, 251, 252) };
            var pollBlock = new Panel { BackColor = Color.FromArgb(250, 251, 252) };
            voltageBlock.SetBounds(0, 0, 172, 78);
            pollBlock.SetBounds(180, 0, 80, 78);

            parameterBlocks[fullChargeVoltageTargetBox] = voltageBlock;
            parameterBlocks[fullChargeVoltagePollBox] = pollBlock;
            fullChargeVoltageTargetBox.UserChanged += delegate { UpdateParameterBlockDirty(fullChargeVoltageTargetBox); };
            fullChargeVoltagePollBox.UserChanged += delegate { UpdateParameterBlockDirty(fullChargeVoltagePollBox); };

            var voltageTitle = SectionText("滿電截止電壓");
            voltageTitle.SetBounds(0, 0, 172, 20);
            voltageTitle.Dock = DockStyle.None;
            fullChargeVoltageTargetBox.Dock = DockStyle.None;
            fullChargeVoltageTargetBox.SetBounds(0, 21, 172, 26);
            applyFullChargeVoltageButton.SetBounds(0, 49, 108, 26);
            fullChargeVoltageToggleButton.SetBounds(114, 49, 58, 26);
            StyleOnOffToggle(fullChargeVoltageToggleButton, false, ToggleKind.Voltage);

            var pollTitle = SectionText("輪詢時間");
            pollTitle.SetBounds(0, 0, 80, 20);
            pollTitle.Dock = DockStyle.None;
            fullChargeVoltagePollBox.Dock = DockStyle.None;
            fullChargeVoltagePollBox.SetBounds(0, 21, 80, 26);
            applyFullChargeVoltagePollButton.SetBounds(0, 49, 80, 26);

            voltageBlock.Controls.Add(voltageTitle);
            voltageBlock.Controls.Add(fullChargeVoltageTargetBox);
            voltageBlock.Controls.Add(applyFullChargeVoltageButton);
            voltageBlock.Controls.Add(fullChargeVoltageToggleButton);
            pollBlock.Controls.Add(pollTitle);
            pollBlock.Controls.Add(fullChargeVoltagePollBox);
            pollBlock.Controls.Add(applyFullChargeVoltagePollButton);
            outer.Controls.Add(voltageBlock);
            outer.Controls.Add(pollBlock);
            UpdateParameterBlockDirty(fullChargeVoltageTargetBox);
            UpdateParameterBlockDirty(fullChargeVoltagePollBox);
            return outer;
        }

        private Control BuildStatusLine(Label label, string title, string message)
        {
            var row = new Panel { Width = 260, Height = 22, Margin = new Padding(0, 0, 0, 4), BackColor = Color.FromArgb(245, 247, 249) };
            label.SetBounds(6, 0, 228, 22);
            label.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Font = new Font("Microsoft JhengHei UI", 8F, FontStyle.Bold);
            label.ForeColor = Color.FromArgb(76, 86, 96);
            label.BackColor = Color.Transparent;

            var helpButton = new Button
            {
                Text = "?",
                Width = 20,
                Height = 20,
                Left = 238,
                Top = 1,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(76, 86, 96),
                Font = new Font("Segoe UI", 7F, FontStyle.Bold),
                TabStop = false
            };
            helpButton.FlatAppearance.BorderColor = Color.FromArgb(176, 186, 196);
            helpButton.FlatAppearance.BorderSize = 1;
            helpButton.Click += delegate
            {
                MessageBox.Show(this, Ui(message), Ui(title), MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            row.Controls.Add(label);
            row.Controls.Add(helpButton);
            return row;
        }

        private string PresetRegistryPath(int group)
        {
            int safeGroup = Math.Max(0, Math.Min(10, group));
            return RegistryBasePath + @"\Presets\M" + safeGroup.ToString(CultureInfo.InvariantCulture);
        }

        private void LoadFullChargeSettings(int group)
        {
            syncingFullChargeSettings = true;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(PresetRegistryPath(group)))
                {
                    bool currentEnabled = ReadRegistryBool(key, "FullChargeCutoffCurrentEnabled", false);
                    decimal current = ReadRegistryDecimal(key, "FullChargeCutoffCurrent", 0.100M);
                    bool voltageEnabled = ReadRegistryBool(key, "FullChargeCutoffVoltageEnabled", false);
                    decimal voltage = ReadRegistryDecimal(key, "FullChargeTargetVoltage", setVoltageBox.Value);
                    decimal pollSeconds = ReadRegistryDecimal(key, "FullChargeVoltagePollSeconds", 60M);

                    fullChargeCurrentEnableBox.Checked = currentEnabled;
                    fullChargeVoltageEnableBox.Checked = voltageEnabled;
                    fullChargeCurrentBox.SetValue((double)current);
                    fullChargeVoltageTargetBox.SetValue((double)voltage);
                    fullChargeVoltagePollBox.SetValue((double)pollSeconds);
                    ClearDirty(fullChargeCurrentBox);
                    ClearDirty(fullChargeVoltageTargetBox);
                    ClearDirty(fullChargeVoltagePollBox);
                    SetQuickGroupCutoffIndicators(group, currentEnabled, voltageEnabled);
                }
            }
            finally
            {
                syncingFullChargeSettings = false;
            }
            ResetFullChargeState();
            UpdateFullChargeStatusLabels();
        }

        private void SaveFullChargeSettings()
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(PresetRegistryPath(currentGroup)))
            {
                key.SetValue("FullChargeCutoffCurrentEnabled", fullChargeCurrentEnableBox.Checked ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue("FullChargeCutoffCurrent", fullChargeCurrentBox.Value.ToString("0.000", CultureInfo.InvariantCulture), RegistryValueKind.String);
                key.SetValue("FullChargeCutoffVoltageEnabled", fullChargeVoltageEnableBox.Checked ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue("FullChargeTargetVoltage", fullChargeVoltageTargetBox.Value.ToString("0.00", CultureInfo.InvariantCulture), RegistryValueKind.String);
                key.SetValue("FullChargeVoltagePollSeconds", fullChargeVoltagePollBox.Value.ToString("0", CultureInfo.InvariantCulture), RegistryValueKind.String);
            }
            UpdateFullChargeStatusLabels();
            SetQuickGroupCutoffIndicators(currentGroup, fullChargeCurrentEnableBox.Checked, fullChargeVoltageEnableBox.Checked);
        }

        private void UpdatePresetBaselineAndCutoffs(int group, int rawVoltage, int rawCurrent)
        {
            bool changed = false;
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(PresetRegistryPath(group)))
            {
                object voltageValue = key.GetValue("LastPresetVoltageRaw");
                object currentValue = key.GetValue("LastPresetCurrentRaw");
                bool hasBaseline = voltageValue != null && currentValue != null;
                int oldVoltage;
                int oldCurrent;
                changed = hasBaseline &&
                    (!int.TryParse(voltageValue.ToString(), out oldVoltage) || oldVoltage != rawVoltage ||
                     !int.TryParse(currentValue.ToString(), out oldCurrent) || oldCurrent != rawCurrent);

                if (changed)
                {
                    key.SetValue("FullChargeCutoffCurrentEnabled", 0, RegistryValueKind.DWord);
                    key.SetValue("FullChargeCutoffVoltageEnabled", 0, RegistryValueKind.DWord);
                    SetQuickGroupCutoffIndicators(group, false, false);
                    Log("SYS", "M" + group.ToString(CultureInfo.InvariantCulture) + " preset V/I changed, full charge cutoffs disabled");
                }

                key.SetValue("LastPresetVoltageRaw", rawVoltage, RegistryValueKind.DWord);
                key.SetValue("LastPresetCurrentRaw", rawCurrent, RegistryValueKind.DWord);
            }

            if (changed && group == currentGroup)
            {
                bool oldSync = syncingFullChargeSettings;
                syncingFullChargeSettings = true;
                try
                {
                    fullChargeCurrentEnableBox.Checked = false;
                    fullChargeVoltageEnableBox.Checked = false;
                    StyleOnOffToggle(fullChargeCurrentToggleButton, false, ToggleKind.Current);
                    StyleOnOffToggle(fullChargeVoltageToggleButton, false, ToggleKind.Voltage);
                }
                finally
                {
                    syncingFullChargeSettings = oldSync;
                }
                ResetFullChargeState();
                UpdateFullChargeStatusLabels();
            }
        }

        private static bool ReadRegistryBool(RegistryKey key, string name, bool fallback)
        {
            object value = key.GetValue(name);
            if (value == null) return fallback;
            int intValue;
            if (int.TryParse(value.ToString(), out intValue)) return intValue != 0;
            bool boolValue;
            if (bool.TryParse(value.ToString(), out boolValue)) return boolValue;
            return fallback;
        }

        private string InitialLanguageSelection()
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryBasePath))
            {
                object value = key.GetValue("Language");
                if (value != null)
                {
                    string text = value.ToString();
                    if (string.Equals(text, "zh-TW", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(text, "zh", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(text, "中文", StringComparison.OrdinalIgnoreCase))
                        return "中文";
                    if (string.Equals(text, "en-US", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(text, "en", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(text, "English", StringComparison.OrdinalIgnoreCase))
                        return "English";
                }
            }

            string culture = CultureInfo.CurrentUICulture.Name;
            return culture.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "中文" : "English";
        }

        private void SaveLanguageSelection()
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryBasePath))
            {
                key.SetValue("Language", IsEnglishUi() ? "en-US" : "zh-TW", RegistryValueKind.String);
            }
        }

        private static decimal ReadRegistryDecimal(RegistryKey key, string name, decimal fallback)
        {
            object value = key.GetValue(name);
            if (value == null) return fallback;
            decimal parsed;
            if (decimal.TryParse(value.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out parsed)) return parsed;
            if (decimal.TryParse(value.ToString(), NumberStyles.Number, CultureInfo.CurrentCulture, out parsed)) return parsed;
            return fallback;
        }

        private void ResetFullChargeState()
        {
            outputOnSince = latestOutputOn ? (DateTime?)DateTime.Now : null;
            lowCurrentSince = null;
            nextVoltageCheckAt = latestOutputOn ? (DateTime?)DateTime.Now.AddSeconds((double)fullChargeVoltagePollBox.Value) : null;
            voltageOffConfirmedAt = null;
            autoOffInProgress = false;
            voltageCheckInProgress = false;
            voltageOffCommandPending = false;
            voltageReadPending = false;
            voltageRestoreOnPending = false;
            voltageRestorePollSeconds = 0M;
        }

        private void BindEnterToWriteButtons()
        {
            setVoltageBox.EnterPressed += delegate { PerformEnterActionIfDirty(setVoltageBox, writeVoltageButton); };
            setCurrentBox.EnterPressed += delegate { PerformEnterActionIfDirty(setCurrentBox, writeCurrentButton); };
            setLvpBox.EnterPressed += delegate { PerformEnterActionIfDirty(setLvpBox, writeLvpButton); };
            setOvpBox.EnterPressed += delegate { PerformEnterActionIfDirty(setOvpBox, writeOvpButton); };
            setOcpBox.EnterPressed += delegate { PerformEnterActionIfDirty(setOcpBox, writeOcpButton); };
            setOppBox.EnterPressed += delegate { PerformEnterActionIfDirty(setOppBox, writeOppButton); };
            fullChargeCurrentBox.EnterPressed += delegate { PerformEnterActionIfDirty(fullChargeCurrentBox, applyFullChargeCurrentButton); };
            fullChargeVoltageTargetBox.EnterPressed += delegate { PerformEnterActionIfDirty(fullChargeVoltageTargetBox, applyFullChargeVoltageButton); };
            fullChargeVoltagePollBox.EnterPressed += delegate { PerformEnterActionIfDirty(fullChargeVoltagePollBox, applyFullChargeVoltagePollButton); };
        }

        private void PerformEnterActionIfDirty(StepEditor editor, Button button)
        {
            if (!editor.IsDirty)
            {
                ActiveControl = null;
                return;
            }
            button.PerformClick();
            ActiveControl = button;
        }

        private Control BuildRightFooterPanel()
        {
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(12, 4, 20, 8), BackColor = Color.White };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var outputRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = new Padding(0, 0, 0, 6) };
            outputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78F));
            outputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            outputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            var label = new Label
            {
                Text = "輸出開關",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(62, 70, 80),
                Font = new Font("Microsoft JhengHei UI", 9F, FontStyle.Bold)
            };

            outputOnButton.Text = "ON";
            outputOffButton.Text = "OFF";
            outputOnButton.Dock = DockStyle.Fill;
            outputOffButton.Dock = DockStyle.Fill;
            outputOnButton.Margin = new Padding(0, 0, 4, 0);
            outputOffButton.Margin = new Padding(4, 0, 0, 0);
            outputOnButton.BackColor = Color.FromArgb(65, 145, 108);
            outputOnButton.ForeColor = Color.White;
            outputOffButton.BackColor = Color.FromArgb(172, 79, 82);
            outputOffButton.ForeColor = Color.White;
            outputOnButton.Font = outputOffButton.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            outputOnButton.Click += delegate { QueueWriteRegister(0x0012, 1, "輸出 ON"); };
            outputOffButton.Click += delegate { QueueWriteRegister(0x0012, 0, "輸出 OFF"); };
            outputRow.Controls.Add(label, 0, 0);
            outputRow.Controls.Add(outputOnButton, 1, 0);
            outputRow.Controls.Add(outputOffButton, 2, 0);
            panel.Controls.Add(outputRow, 0, 0);

            refreshQuickGroupsButton.Text = "刷新 M0-M10";
            refreshQuickGroupsButton.Dock = DockStyle.Fill;
            refreshQuickGroupsButton.Margin = new Padding(0);
            refreshQuickGroupsButton.Font = new Font("Microsoft JhengHei UI", 9.5F, FontStyle.Bold);
            refreshQuickGroupsButton.BackColor = Color.FromArgb(241, 245, 249);
            refreshQuickGroupsButton.ForeColor = Color.FromArgb(31, 38, 46);
            refreshQuickGroupsButton.Click += delegate { RefreshQuickGroupSummary(); };
            panel.Controls.Add(refreshQuickGroupsButton, 0, 1);
            return panel;
        }

        private static void StyleOnOffToggle(Button button, bool enabled)
        {
            StyleOnOffToggle(button, enabled, ToggleKind.Normal);
        }

        private static void StyleOnOffToggle(Button button, bool enabled, ToggleKind kind)
        {
            button.Text = enabled ? "ON" : "OFF";
            Color onBack = Color.FromArgb(35, 96, 73);
            Color onBorder = Color.FromArgb(23, 78, 57);
            if (kind == ToggleKind.Danger)
            {
                onBack = Color.FromArgb(185, 28, 28);
                onBorder = Color.FromArgb(127, 29, 29);
            }
            else if (kind == ToggleKind.Current)
            {
                onBack = Color.FromArgb(184, 134, 11);
                onBorder = Color.FromArgb(146, 101, 8);
            }
            else if (kind == ToggleKind.Voltage)
            {
                onBack = Color.FromArgb(35, 96, 73);
                onBorder = Color.FromArgb(23, 78, 57);
            }

            button.BackColor = enabled ? onBack : Color.FromArgb(226, 232, 240);
            button.ForeColor = enabled ? Color.White : Color.FromArgb(31, 38, 46);
            button.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = enabled ? onBorder : Color.FromArgb(190, 199, 208);
        }

        private static void SetupStatusBadge(Label label, string text, Color backColor, float fontSize)
        {
            label.Text = text;
            label.Dock = DockStyle.Fill;
            label.Margin = new Padding(3);
            label.BackColor = backColor;
            label.ForeColor = Color.White;
            label.Font = new Font("Microsoft JhengHei UI", fontSize, FontStyle.Bold);
            label.TextAlign = ContentAlignment.MiddleCenter;
        }

        private Control BuildParameterBlock(string title, StepEditor editor, Button writeButton)
        {
            var block = new Panel { Width = 260, Height = 80, Margin = new Padding(0, 0, 0, 4), BackColor = Color.FromArgb(250, 251, 252) };
            parameterBlocks[editor] = block;
            editor.UserChanged += delegate { UpdateParameterBlockDirty(editor); };
            var titleLabel = SectionText(title);
            titleLabel.SetBounds(0, 0, 260, 20);
            titleLabel.Dock = DockStyle.None;
            editor.Dock = DockStyle.None;
            editor.SetBounds(0, 21, 260, 32);
            writeButton.SetBounds(0, 55, 260, 24);
            writeButton.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            block.Controls.Add(titleLabel);
            block.Controls.Add(editor);
            block.Controls.Add(writeButton);
            block.Resize += delegate
            {
                titleLabel.Width = block.Width;
                editor.Width = block.Width;
                writeButton.Width = block.Width;
            };
            UpdateParameterBlockDirty(editor);
            return block;
        }

        private Control BuildQuickGroupPair(int group)
        {
            var pair = new Panel { Dock = DockStyle.Fill, Margin = new Padding(2) };
            var call = new QuickGroupButton
            {
                Dock = DockStyle.Fill,
                Tag = group
            };
            call.SetGroup(group);
            quickGroupButtons[group] = call;
            call.Click += delegate(object sender, EventArgs e) { QueueQuickGroup((int)((Control)sender).Tag); };
            pair.Controls.Add(call);
            return pair;
        }

        private void RefreshPorts()
        {
            var selected = portBox.SelectedItem as string;
            portBox.Items.Clear();
            foreach (var port in SerialPort.GetPortNames().OrderBy(p => PortNumber(p)).ThenBy(p => p)) portBox.Items.Add(port);
            if (selected != null && portBox.Items.Contains(selected)) portBox.SelectedItem = selected;
            else if (portBox.Items.Count > 0) portBox.SelectedIndex = portBox.Items.Count - 1;
        }

        private static int PortNumber(string port)
        {
            if (port == null) return -1;
            string digits = new string(port.Where(char.IsDigit).ToArray());
            int value;
            if (int.TryParse(digits, out value)) return value;
            return -1;
        }

        private void TryStartupAutoConnect()
        {
            if (startupAutoConnectAttempted) return;
            startupAutoConnectAttempted = true;
            if (serial != null && serial.IsOpen) return;
            if (portBox.SelectedItem == null)
            {
                Log("SYS", "startup auto connect skipped: no COM port");
                return;
            }

            Log("SYS", "startup auto connect " + portBox.SelectedItem + " @ " + baudBox.SelectedItem);
            Connect(true);
        }

        private void Connect()
        {
            Connect(false);
        }

        private void Connect(bool quiet)
        {
            if (portBox.SelectedItem == null)
            {
                if (!quiet) MessageBox.Show("請先選擇 COM 埠。", "SK150C", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                serial = new SerialPort(portBox.SelectedItem.ToString(), int.Parse(baudBox.SelectedItem.ToString()), Parity.None, 8, StopBits.One);
                serial.ReadTimeout = 300;
                serial.WriteTimeout = 800;
                serial.Open();
                consecutivePollFailures = 0;
                pollBackoffActive = false;
                statusLabel.Text = Ui("已連線") + "  " + serial.PortName + "  " + serial.BaudRate + "  8N1";
                statusLabel.ForeColor = Color.FromArgb(134, 216, 160);
                SetStatusText("已連線");
                pollTimer.Interval = PollIntervalMs();
                Log("SYS", "connected");
                QueueQuickGroupSummaryScan(true);
            }
            catch (Exception ex)
            {
                statusLabel.Text = Ui("連線失敗");
                Log("ERR", "connect failed: " + ex.Message);
                if (!quiet) MessageBox.Show(ex.Message, "連線失敗", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void Disconnect()
        {
            pollTimer.Stop();
            if (serial != null)
            {
                try { serial.Close(); } catch { }
                serial = null;
            }
            statusLabel.Text = Ui("未連線");
            statusLabel.ForeColor = Color.FromArgb(255, 214, 102);
            SetStatusText("待命");
            consecutivePollFailures = 0;
            pollBackoffActive = false;
            Log("SYS", "disconnected");
        }

        private void PollTimer_Tick(object sender, EventArgs e)
        {
            if (!autoPollBox.Checked) return;
            if (busy) return;
            if (serial == null || !serial.IsOpen) return;
            busy = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    var response = ExecuteWithRetry(BuildRead(0x0000, 19), ExpectedReadLength(19));
                    if (response != null)
                    {
                        int vset = Word(response, 3);
                        int iset = Word(response, 5);
                        int vout = Word(response, 7);
                        int iout = Word(response, 9);
                        int power = Word(response, 11);
                        int ahLow = Word(response, 15);
                        int ahHigh = Word(response, 17);
                        int whLow = Word(response, 19);
                        int whHigh = Word(response, 21);
                        int runHour = Word(response, 23);
                        int runMinute = Word(response, 25);
                        int runSecond = Word(response, 27);
                        int temperature = Word(response, 29);
                        int keyLock = Word(response, 33);
                        int protect = Word(response, 35);
                        int cvcc = Word(response, 37);
                        int onoff = Word(response, 39);
                        BeginInvoke((Action)(() =>
                        {
                            UpdateSetValues(vset / 100.0, iset / 1000.0);
                            UpdateValues(vout / 100.0, iout / 1000.0, power / 100.0);
                            UpdateStatusValues(temperature / 10.0, cvcc, onoff, protect, CombineWords(ahHigh, ahLow) / 1000.0, CombineWords(whHigh, whLow) / 1000.0, runHour, runMinute, runSecond, keyLock);
                        }));
                    }
                    BeginInvoke((Action)(() => NotePollResult(response != null)));

                    if (response != null)
                    {
                        ExecuteVoltageAutomationWritesAtPollEnd();
                        SyncActiveGroupFromDevice();
                        SyncProtectionFromGroup();
                        pollCounter++;
                        if (pollCounter % 5 == 0) SyncDeviceOptions();
                    }
                }
                finally
                {
                    busy = false;
                }
            });
        }

        private void QueueQuickGroup(int group)
        {
            if (serial == null || !serial.IsOpen)
            {
                MessageBox.Show("尚未連線。", "SK150C", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (busy)
            {
                SetStatusText("忙碌中");
                return;
            }
            SetStatusText("M" + group + " 讀取中");
            busy = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    var groupData = ExecuteWithRetry(BuildRead(GroupBase(group), 14), ExpectedReadLength(14));
                    if (groupData == null)
                    {
                        BeginInvoke((Action)(() => SetStatusText("M" + group + " 讀取失敗")));
                        return;
                    }

                    BeginInvoke((Action)(() =>
                    {
                        editGroup = -1;
                        UpdateGroupEditor(groupData, true);
                        UpdateQuickGroupButton(group, Word(groupData, 3) / 100.0, Word(groupData, 5) / 1000.0, Word(groupData, 29) != 0);
                        editGroupLabel.Text = "保護寫入：目前 M 組";
                        SetStatusText("M" + group + " 調用中");
                    }));

                    byte[] response = ExecuteWithRetry(BuildWriteSingle(0x001D, group), 8);
                    if (response != null)
                    {
                        currentGroup = group;
                        Thread.Sleep(300);
                        SyncSettingsFromDevice();
                        BeginInvoke((Action)(() =>
                        {
                            SetActiveQuickGroup(group);
                            LoadFullChargeSettings(group);
                            StyleOnOffToggle(siniToggleButton, quickGroupSini[group].GetValueOrDefault(false), ToggleKind.Danger);
                            SetStatusText("已調用 M" + group + " 並同步");
                        }));
                    }
                    else
                    {
                        BeginInvoke((Action)(() => SetStatusText("M" + group + " 失敗")));
                    }
                }
                finally
                {
                    busy = false;
                }
            });
        }

        private bool WriteSingleVerified(int address, int value, string label)
        {
            byte[] writeResponse = ExecuteWithRetry(BuildWriteSingle(address, value), 8);
            if (writeResponse == null)
            {
                Log("SYS", label + " write no response");
                return false;
            }

            Thread.Sleep(80);
            byte[] readResponse = ExecuteWithRetry(BuildRead(address, 1), ExpectedReadLength(1));
            if (readResponse == null)
            {
                Log("SYS", label + " verify no response");
                return false;
            }

            int readBack = Word(readResponse, 3);
            if (readBack != value)
            {
                Log("SYS", label + " verify mismatch write " + value.ToString(CultureInfo.InvariantCulture) + " read " + readBack.ToString(CultureInfo.InvariantCulture));
                return false;
            }

            return true;
        }
        private void QueueQuickGroupSummaryScan(bool startPollingAfter)
        {
            QueueQuickGroupSummaryScan(startPollingAfter, false);
        }

        private void QueueQuickGroupSummaryScan(bool startPollingAfter, bool manualRefresh)
        {
            if (serial == null || !serial.IsOpen) return;
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool claimedBusy = false;
                try
                {
                    int waited = 0;
                    while (busy && waited < 2500)
                    {
                        Thread.Sleep(50);
                        waited += 50;
                    }
                    if (busy) return;
                    busy = true;
                    claimedBusy = true;
                    BeginInvoke((Action)(() =>
                    {
                        SetStatusText("讀取快捷組摘要 M0-M10");
                        if (manualRefresh)
                        {
                            refreshQuickGroupsButton.Enabled = false;
                            refreshQuickGroupsButton.Text = Ui("刷新中...");
                        }
                    }));
                    for (int group = 0; group <= 10; group++)
                    {
                        var response = ExecuteWithRetry(BuildRead(GroupBase(group), 14), ExpectedReadLength(14));
                        int capturedGroup = group;
                        if (response != null)
                        {
                            int rawVoltage = Word(response, 3);
                            int rawCurrent = Word(response, 5);
                            bool siniOn = Word(response, 29) != 0;
                            BeginInvoke((Action)(() =>
                            {
                                UpdatePresetBaselineAndCutoffs(capturedGroup, rawVoltage, rawCurrent);
                                UpdateQuickGroupButton(capturedGroup, rawVoltage / 100.0, rawCurrent / 1000.0, siniOn);
                            }));
                        }
                        else
                        {
                            BeginInvoke((Action)(() => UpdateQuickGroupButton(capturedGroup, null, null, null)));
                        }
                        Thread.Sleep(35);
                    }
                    BeginInvoke((Action)(() =>
                    {
                        SetStatusText("快捷組摘要已更新");
                        if (startPollingAfter && autoPollBox.Checked)
                        {
                            pollTimer.Interval = PollIntervalMs();
                            pollTimer.Start();
                        }
                        if (manualRefresh)
                        {
                            refreshQuickGroupsButton.Enabled = true;
                            refreshQuickGroupsButton.Text = Ui("刷新 M0-M10");
                        }
                    }));
                }
                finally
                {
                    if (claimedBusy) busy = false;
                }
            });
        }

        private void RefreshQuickGroupSummary()
        {
            if (serial == null || !serial.IsOpen)
            {
                MessageBox.Show("尚未連線。", "SK150C", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (busy)
            {
                SetStatusText("忙碌，稍後再刷新 M0-M10");
                return;
            }
            bool restartPolling = pollTimer.Enabled && autoPollBox.Checked;
            pollTimer.Stop();
            QueueQuickGroupSummaryScan(restartPolling, true);
        }

        private void ExportGroupSettings()
        {
            if (serial == null || !serial.IsOpen)
            {
                MessageBox.Show(Ui("尚未連線。"), "SK150C", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (busy)
            {
                SetStatusText("忙碌中");
                return;
            }

            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = Ui("匯出 M組參數");
                dialog.Filter = "JSON (*.json)|*.json";
                dialog.FileName = "SK150C_M_Groups_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".json";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                QueueExportGroupSettings(dialog.FileName);
            }
        }

        private void QueueExportGroupSettings(string path)
        {
            busy = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool ok = false;
                try
                {
                    BeginInvoke((Action)(() => SetStatusText("匯出 M組參數")));
                    var groups = new List<Dictionary<string, object>>();
                    for (int group = 0; group <= 10; group++)
                    {
                        byte[] response = ExecuteWithRetry(BuildRead(GroupBase(group), 14), ExpectedReadLength(14));
                        if (response == null)
                        {
                            BeginInvoke((Action)(() => MessageBox.Show(this, Ui("讀取失敗") + " M" + group.ToString(CultureInfo.InvariantCulture), "SK150C", MessageBoxButtons.OK, MessageBoxIcon.Warning)));
                            return;
                        }

                        int[] raw = new int[14];
                        for (int i = 0; i < raw.Length; i++) raw[i] = Word(response, 3 + i * 2);
                        Dictionary<string, object> cutoff = ReadFullChargeExport(group);
                        groups.Add(new Dictionary<string, object>
                        {
                            { "group", group },
                            { "vSetRaw", raw[0] },
                            { "iSetRaw", raw[1] },
                            { "lvpRaw", raw[2] },
                            { "ovpRaw", raw[3] },
                            { "ocpRaw", raw[4] },
                            { "oppRaw", raw[5] },
                            { "sIni", raw[13] },
                            { "rawWords", raw },
                            { "fullCharge", cutoff }
                        });
                    }

                    var root = new Dictionary<string, object>
                    {
                        { "schema", "SK150C_M_GROUPS" },
                        { "version", 1 },
                        { "appVersion", VersionName },
                        { "exportedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) },
                        { "groups", groups }
                    };
                    string json = new JavaScriptSerializer().Serialize(root);
                    File.WriteAllText(path, PrettyJson(json), Encoding.UTF8);
                    ok = true;
                    BeginInvoke((Action)(() =>
                    {
                        SetStatusText("M組參數已匯出");
                        MessageBox.Show(this, Ui("匯出完成") + "\r\n" + path, "SK150C", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }));
                }
                catch (Exception ex)
                {
                    BeginInvoke((Action)(() => MessageBox.Show(this, ex.Message, Ui("匯出失敗"), MessageBoxButtons.OK, MessageBoxIcon.Warning)));
                }
                finally
                {
                    busy = false;
                    if (!ok) BeginInvoke((Action)(() => SetStatusText("匯出失敗")));
                }
            });
        }

        private Dictionary<string, object> ReadFullChargeExport(int group)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(PresetRegistryPath(group)))
            {
                return new Dictionary<string, object>
                {
                    { "currentEnabled", ReadRegistryBool(key, "FullChargeCutoffCurrentEnabled", false) },
                    { "currentA", ReadRegistryDecimal(key, "FullChargeCutoffCurrent", 0.100M).ToString("0.000", CultureInfo.InvariantCulture) },
                    { "voltageEnabled", ReadRegistryBool(key, "FullChargeCutoffVoltageEnabled", false) },
                    { "targetV", ReadRegistryDecimal(key, "FullChargeTargetVoltage", setVoltageBox.Value).ToString("0.00", CultureInfo.InvariantCulture) },
                    { "pollSeconds", ReadRegistryDecimal(key, "FullChargeVoltagePollSeconds", 60M).ToString("0", CultureInfo.InvariantCulture) }
                };
            }
        }

        private void ImportGroupSettings()
        {
            if (serial == null || !serial.IsOpen)
            {
                MessageBox.Show(Ui("尚未連線。"), "SK150C", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (busy)
            {
                SetStatusText("忙碌中");
                return;
            }

            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = Ui("匯入 M組參數");
                dialog.Filter = "JSON (*.json)|*.json";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                List<GroupImportData> groups;
                string error;
                if (!TryLoadGroupImportFile(dialog.FileName, out groups, out error))
                {
                    MessageBox.Show(this, error, Ui("匯入格式錯誤"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                QueueImportGroupSettings(groups);
            }
        }

        private bool TryLoadGroupImportFile(string path, out List<GroupImportData> groups, out string error)
        {
            groups = new List<GroupImportData>();
            error = null;
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                var root = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
                if (root == null)
                {
                    error = Ui("JSON 根格式錯誤");
                    return false;
                }
                object schema;
                if (!root.TryGetValue("schema", out schema) || Convert.ToString(schema, CultureInfo.InvariantCulture) != "SK150C_M_GROUPS")
                {
                    error = Ui("不是 SK150C M組參數檔");
                    return false;
                }
                object groupsObject;
                if (!root.TryGetValue("groups", out groupsObject))
                {
                    error = Ui("缺少 groups 欄位");
                    return false;
                }
                object[] groupArray = groupsObject as object[];
                if (groupArray == null || groupArray.Length != 11)
                {
                    error = Ui("M組數量必須為 11 組");
                    return false;
                }

                bool[] seen = new bool[11];
                foreach (object item in groupArray)
                {
                    var dict = item as Dictionary<string, object>;
                    if (dict == null)
                    {
                        error = Ui("M組資料格式錯誤");
                        return false;
                    }
                    GroupImportData data;
                    if (!TryParseGroupImport(dict, out data, out error)) return false;
                    if (seen[data.Group])
                    {
                        error = Ui("M組資料重複");
                        return false;
                    }
                    seen[data.Group] = true;
                    groups.Add(data);
                }
                groups.Sort((a, b) => a.Group.CompareTo(b.Group));
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private bool TryParseGroupImport(Dictionary<string, object> dict, out GroupImportData data, out string error)
        {
            data = new GroupImportData();
            error = null;
            data.Group = GetJsonInt(dict, "group", -1);
            if (data.Group < 0 || data.Group > 10)
            {
                error = Ui("M組編號錯誤");
                return false;
            }

            data.Values = new int[14];
            object rawObject;
            object[] rawArray = null;
            if (dict.TryGetValue("rawWords", out rawObject)) rawArray = rawObject as object[];
            if (rawArray != null && rawArray.Length >= 14)
            {
                for (int i = 0; i < 14; i++) data.Values[i] = Convert.ToInt32(rawArray[i], CultureInfo.InvariantCulture);
            }
            else
            {
                data.Values[0] = GetJsonInt(dict, "vSetRaw", -1);
                data.Values[1] = GetJsonInt(dict, "iSetRaw", -1);
                data.Values[2] = GetJsonInt(dict, "lvpRaw", -1);
                data.Values[3] = GetJsonInt(dict, "ovpRaw", -1);
                data.Values[4] = GetJsonInt(dict, "ocpRaw", -1);
                data.Values[5] = GetJsonInt(dict, "oppRaw", -1);
                data.Values[13] = GetJsonInt(dict, "sIni", 0);
            }

            int[] required = new int[] { 0, 1, 2, 3, 4, 5, 13 };
            foreach (int index in required)
            {
                if (data.Values[index] < 0 || data.Values[index] > 0xFFFF)
                {
                    error = "M" + data.Group.ToString(CultureInfo.InvariantCulture) + " " + Ui("參數範圍錯誤");
                    return false;
                }
            }

            object cutoffObject;
            var cutoff = dict.TryGetValue("fullCharge", out cutoffObject) ? cutoffObject as Dictionary<string, object> : null;
            if (cutoff != null)
            {
                data.HasFullCharge = true;
                data.CurrentCutoffEnabled = GetJsonBool(cutoff, "currentEnabled", false);
                data.VoltageCutoffEnabled = GetJsonBool(cutoff, "voltageEnabled", false);
                data.CurrentCutoff = GetJsonDecimal(cutoff, "currentA", 0.100M);
                data.TargetVoltage = GetJsonDecimal(cutoff, "targetV", 0M);
                data.PollSeconds = GetJsonDecimal(cutoff, "pollSeconds", 60M);
            }
            return true;
        }

        private void QueueImportGroupSettings(List<GroupImportData> groups)
        {
            busy = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                string failure = null;
                try
                {
                    BeginInvoke((Action)(() => SetStatusText("匯入 M組參數")));
                    foreach (GroupImportData group in groups)
                    {
                        int baseAddress = GroupBase(group.Group);
                        int[] offsets = new int[] { 0, 1, 2, 3, 4, 5, 13 };
                        foreach (int offset in offsets)
                        {
                            if (!WriteSingleVerified(baseAddress + offset, group.Values[offset], "M" + group.Group.ToString(CultureInfo.InvariantCulture) + " +" + offset.ToString(CultureInfo.InvariantCulture)))
                            {
                                failure = "M" + group.Group.ToString(CultureInfo.InvariantCulture) + " +" + offset.ToString(CultureInfo.InvariantCulture);
                                return;
                            }
                        }

                        byte[] readback = ExecuteWithRetry(BuildRead(baseAddress, 14), ExpectedReadLength(14));
                        if (readback == null)
                        {
                            failure = "M" + group.Group.ToString(CultureInfo.InvariantCulture) + " " + Ui("讀回失敗");
                            return;
                        }
                        foreach (int offset in offsets)
                        {
                            int readValue = Word(readback, 3 + offset * 2);
                            if (readValue != group.Values[offset])
                            {
                                failure = "M" + group.Group.ToString(CultureInfo.InvariantCulture) + " +" + offset.ToString(CultureInfo.InvariantCulture) + " " + Ui("讀回不一致");
                                return;
                            }
                        }

                        if (group.HasFullCharge) SaveImportedFullChargeSettings(group);
                        int captured = group.Group;
                        BeginInvoke((Action)(() =>
                        {
                            SavePresetBaseline(captured, group.Values[0], group.Values[1]);
                            UpdateQuickGroupButton(captured, group.Values[0] / 100.0, group.Values[1] / 1000.0, group.Values[13] != 0);
                        }));
                    }
                }
                catch (Exception ex)
                {
                    failure = ex.Message;
                }
                finally
                {
                    busy = false;
                    if (failure == null)
                    {
                        BeginInvoke((Action)(() =>
                        {
                            SetStatusText("M組參數已匯入");
                            MessageBox.Show(this, Ui("匯入完成"), "SK150C", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }));
                    }
                    else
                    {
                        string capturedFailure = failure;
                        BeginInvoke((Action)(() =>
                        {
                            SetStatusText("匯入失敗");
                            MessageBox.Show(this, capturedFailure, Ui("匯入失敗"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }));
                    }
                }
            });
        }

        private void SaveImportedFullChargeSettings(GroupImportData group)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(PresetRegistryPath(group.Group)))
            {
                key.SetValue("FullChargeCutoffCurrentEnabled", group.CurrentCutoffEnabled ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue("FullChargeCutoffCurrent", group.CurrentCutoff.ToString("0.000", CultureInfo.InvariantCulture), RegistryValueKind.String);
                key.SetValue("FullChargeCutoffVoltageEnabled", group.VoltageCutoffEnabled ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue("FullChargeTargetVoltage", group.TargetVoltage.ToString("0.00", CultureInfo.InvariantCulture), RegistryValueKind.String);
                key.SetValue("FullChargeVoltagePollSeconds", group.PollSeconds.ToString("0", CultureInfo.InvariantCulture), RegistryValueKind.String);
            }
            BeginInvoke((Action)(() => SetQuickGroupCutoffIndicators(group.Group, group.CurrentCutoffEnabled, group.VoltageCutoffEnabled)));
        }

        private void SavePresetBaseline(int group, int rawVoltage, int rawCurrent)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(PresetRegistryPath(group)))
            {
                key.SetValue("LastPresetVoltageRaw", rawVoltage, RegistryValueKind.DWord);
                key.SetValue("LastPresetCurrentRaw", rawCurrent, RegistryValueKind.DWord);
            }
        }

        private static int GetJsonInt(Dictionary<string, object> dict, string name, int fallback)
        {
            object value;
            if (!dict.TryGetValue(name, out value) || value == null) return fallback;
            int result;
            return int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out result) ? result : fallback;
        }

        private static bool GetJsonBool(Dictionary<string, object> dict, string name, bool fallback)
        {
            object value;
            if (!dict.TryGetValue(name, out value) || value == null) return fallback;
            bool result;
            if (bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out result)) return result;
            int intValue;
            if (int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out intValue)) return intValue != 0;
            return fallback;
        }

        private static decimal GetJsonDecimal(Dictionary<string, object> dict, string name, decimal fallback)
        {
            object value;
            if (!dict.TryGetValue(name, out value) || value == null) return fallback;
            decimal result;
            return decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out result) ? result : fallback;
        }

        private static string PrettyJson(string json)
        {
            var sb = new StringBuilder();
            int indent = 0;
            bool inString = false;
            for (int i = 0; i < json.Length; i++)
            {
                char ch = json[i];
                if (ch == '"' && (i == 0 || json[i - 1] != '\\')) inString = !inString;
                if (!inString && (ch == '{' || ch == '['))
                {
                    sb.Append(ch).AppendLine();
                    indent++;
                    sb.Append(new string(' ', indent * 2));
                }
                else if (!inString && (ch == '}' || ch == ']'))
                {
                    sb.AppendLine();
                    indent--;
                    sb.Append(new string(' ', indent * 2)).Append(ch);
                }
                else if (!inString && ch == ',')
                {
                    sb.Append(ch).AppendLine();
                    sb.Append(new string(' ', indent * 2));
                }
                else if (!inString && ch == ':')
                {
                    sb.Append(": ");
                }
                else
                {
                    sb.Append(ch);
                }
            }
            return sb.ToString();
        }

        private sealed class GroupImportData
        {
            public int Group;
            public int[] Values;
            public bool HasFullCharge;
            public bool CurrentCutoffEnabled;
            public bool VoltageCutoffEnabled;
            public decimal CurrentCutoff;
            public decimal TargetVoltage;
            public decimal PollSeconds;
        }

        private void QueueEditGroup(int group)
        {
            if (serial == null || !serial.IsOpen)
            {
                MessageBox.Show("尚未連線。", "SK150C", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (busy)
            {
                editGroupLabel.Text = "正在編輯：忙碌中";
                return;
            }
            editGroupLabel.Text = "正在編輯：M" + group + " 讀取中";
            busy = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    var groupData = ExecuteWithRetry(BuildRead(GroupBase(group), 14), ExpectedReadLength(14));
                    if (groupData != null)
                    {
                        editGroup = group;
                        BeginInvoke((Action)(() =>
                        {
                            UpdateGroupEditor(groupData, true);
                            UpdateQuickGroupButton(group, Word(groupData, 3) / 100.0, Word(groupData, 5) / 1000.0, Word(groupData, 29) != 0);
                            StyleOnOffToggle(siniToggleButton, Word(groupData, 29) != 0, ToggleKind.Danger);
                            editGroupLabel.Text = "正在編輯：M" + group;
                            SetStatusText("正在編輯 M" + group);
                        }));
                    }
                    else
                    {
                        BeginInvoke((Action)(() => editGroupLabel.Text = "正在編輯：M" + group + " 讀取失敗"));
                    }
                }
                finally
                {
                    busy = false;
                }
            });
        }

        private void SyncSettingsFromDevice()
        {
            var settings = ExecuteWithRetry(BuildRead(0x0000, 2), ExpectedReadLength(2));
            if (settings != null)
            {
                int vset = Word(settings, 3);
                int iset = Word(settings, 5);
                BeginInvoke((Action)(() =>
                {
                    UpdateSetValues(vset / 100.0, iset / 1000.0);
                }));
            }

            SyncProtectionFromGroup();
        }

        private void SyncActiveGroupFromDevice()
        {
            var response = ExecuteWithRetry(BuildRead(0x001D, 1), ExpectedReadLength(1));
            if (response == null) return;
            int group = Word(response, 3);
            if (group < 0 || group > 10) return;
            if (group == currentGroup && group == activeQuickGroup) return;

            currentGroup = group;
            BeginInvoke((Action)(() =>
            {
                SetActiveQuickGroup(group);
                LoadFullChargeSettings(group);
                StyleOnOffToggle(siniToggleButton, quickGroupSini[group].GetValueOrDefault(false), ToggleKind.Danger);
                editGroupLabel.Text = "保護寫入：目前 M 組";
                SetStatusText("已同步 M" + group.ToString(CultureInfo.InvariantCulture));
            }));
        }

        private void WriteSetOrGroup(int liveAddress, int groupOffset, int value, string label)
        {
            if (editGroup >= 0)
            {
                editGroupLabel.Text = "正在編輯：M" + editGroup + " 寫入中";
                QueueWriteRegister(GroupBase(editGroup) + groupOffset, value, label + " M" + editGroup);
                editGroupLabel.Text = "正在編輯：M" + editGroup;
            }
            else QueueWriteRegister(liveAddress, value, label);
        }

        private void WriteGroupOnly(int groupOffset, int value, string label)
        {
            if (editGroup < 0)
            {
                MessageBox.Show("請先按快捷組旁邊的「編」，選擇要編輯的 M 組。", "快捷組編輯", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            editGroupLabel.Text = "正在編輯：M" + editGroup + " 寫入中";
            QueueWriteRegister(GroupBase(editGroup) + groupOffset, value, label + " M" + editGroup);
            editGroupLabel.Text = "正在編輯：M" + editGroup;
        }

        private void QueueWriteRegister(int address, int value, string label)
        {
            if (serial == null || !serial.IsOpen)
            {
                MessageBox.Show("尚未連線。", "SK150C", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool claimedBusy = false;
                try
                {
                    int waited = 0;
                    while (busy && waited < 2000)
                    {
                        Thread.Sleep(50);
                        waited += 50;
                    }
                    if (busy)
                    {
                        BeginInvoke((Action)(() =>
                        {
                            SetStatusText("忙碌，寫入未送出");
                            Log("SYS", label + " skipped: busy");
                        }));
                        return;
                    }
                    busy = true;
                    claimedBusy = true;
                    ExecuteWithRetry(BuildWriteSingle(address, value), 8);
                }
                finally
                {
                    if (claimedBusy) busy = false;
                }
            });
        }

        private void QueueVerifiedWrite(int address, int value, string label, StepEditor editor)
        {
            QueueVerifiedWrite(address, value, label, editor, null);
        }

        private void QueueVerifiedWrite(int address, int value, string label, StepEditor editor, Action confirmedAction)
        {
            if (serial == null || !serial.IsOpen)
            {
                MessageBox.Show("尚未連線。", "SK150C", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            editor.SetWriting(true);
            UpdateParameterBlockDirty(editor);
            SetStatusText(label + " 寫入確認中");
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool claimedBusy = false;
                try
                {
                    int waited = 0;
                    while (busy && waited < 2000)
                    {
                        Thread.Sleep(50);
                        waited += 50;
                    }
                    if (busy)
                    {
                        BeginInvoke((Action)(() =>
                        {
                            editor.SetDirty(true);
                            UpdateParameterBlockDirty(editor);
                            SetStatusText("忙碌，" + label + " 未送出");
                        }));
                        return;
                    }

                    busy = true;
                    claimedBusy = true;
                    byte[] writeResponse = ExecuteWithRetry(BuildWriteSingle(address, value), 8);
                    if (writeResponse == null)
                    {
                        BeginInvoke((Action)(() =>
                        {
                            editor.SetDirty(true);
                            UpdateParameterBlockDirty(editor);
                            SetStatusText(label + " 寫入無回應");
                        }));
                        return;
                    }

                    Thread.Sleep(80);
                    byte[] readResponse = ExecuteWithRetry(BuildRead(address, 1), ExpectedReadLength(1));
                    if (readResponse != null)
                    {
                        int readBack = Word(readResponse, 3);
                        BeginInvoke((Action)(() =>
                        {
                            if (readBack == value)
                            {
                                ClearDirty(editor);
                                if (confirmedAction != null) confirmedAction();
                                SetStatusText(label + " 已確認");
                            }
                            else
                            {
                                editor.SetDirty(true);
                                UpdateParameterBlockDirty(editor);
                                SetStatusText(label + " 未確認，寫入 " + value.ToString(CultureInfo.InvariantCulture) + " 讀回 " + readBack.ToString(CultureInfo.InvariantCulture));
                            }
                        }));
                    }
                    else
                    {
                        BeginInvoke((Action)(() =>
                        {
                            editor.SetDirty(true);
                            UpdateParameterBlockDirty(editor);
                            SetStatusText(label + " 讀回失敗");
                        }));
                    }
                }
                finally
                {
                    if (claimedBusy) busy = false;
                }
            });
        }

        private void ConfirmAndWrite(string title, string message, int address, string label)
        {
            if (serial == null || !serial.IsOpen)
            {
                MessageBox.Show("尚未連線。", "SK150C", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = MessageBox.Show(this, message, title, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (result == DialogResult.OK) QueueWriteRegister(address, 1, label);
        }

        private void OpenCalibrationWindow()
        {
            using (var form = new CalibrationForm(this))
            {
                form.ShowDialog(this);
            }
        }

        public void QueueCalibrationVoltageSetup(decimal voltage)
        {
            int raw = (int)Math.Round((double)voltage * 100.0);
            QueueCalibrationWork("校準準備 " + voltage.ToString("0.00", CultureInfo.InvariantCulture) + "V", delegate
            {
                if (!WriteSingleVerified(CurrentGroupBase() + 3, 2500, "校準 OVP 25V")) return false;
                return WriteSingleVerified(0x0000, raw, "校準設定電壓");
            });
        }

        public void QueueCalibrationCurrentSetup(decimal current)
        {
            int rawCurrent = (int)Math.Round((double)current * 1000.0);
            QueueCalibrationWork("校準準備 " + current.ToString("0.000", CultureInfo.InvariantCulture) + "A", delegate
            {
                if (!WriteSingleVerified(CurrentGroupBase() + 4, 5000, "校準 OCP 5A")) return false;
                if (!WriteSingleVerified(0x0000, 500, "校準設定 5V")) return false;
                return WriteSingleVerified(0x0001, rawCurrent, "校準設定電流");
            });
        }

        public void QueueCalibrationWrite(int address, decimal measuredValue, int scale, string label)
        {
            int raw = (int)Math.Round((double)measuredValue * scale);
            QueueCalibrationWork(label + " 寫入", delegate
            {
                return WriteSingleVerified(address, raw, label);
            });
        }

        public void QueueCalibrationWriteAndReadStatus(int address, decimal measuredValue, int scale, string label, Action<int?> statusCallback)
        {
            int raw = (int)Math.Round((double)measuredValue * scale);
            QueueCalibrationWork(label + " 寫入並讀取狀態", delegate
            {
                if (!WriteSingleVerified(address, raw, label))
                {
                    BeginInvoke((Action)(() => statusCallback(-1)));
                    return false;
                }

                byte[] response = ExecuteWithRetry(BuildRead(0x0022, 1), ExpectedReadLength(1));
                if (response == null)
                {
                    BeginInvoke((Action)(() => statusCallback(null)));
                    return false;
                }

                int status = Word(response, 3);
                BeginInvoke((Action)(() =>
                {
                    Log("SYS", "calibration status 0022H = " + status.ToString(CultureInfo.InvariantCulture));
                    statusCallback(status);
                }));
                return status == 1;
            });
        }

        public void QueueCalibrationWriteThenVoltageSetup(int address, decimal measuredValue, int scale, string label, decimal nextVoltage)
        {
            int raw = (int)Math.Round((double)measuredValue * scale);
            int nextRaw = (int)Math.Round((double)nextVoltage * 100.0);
            QueueCalibrationWork(label + " 寫入，準備 " + nextVoltage.ToString("0.00", CultureInfo.InvariantCulture) + "V", delegate
            {
                if (!WriteSingleVerified(address, raw, label)) return false;
                return WriteSingleVerified(0x0000, nextRaw, "校準設定電壓");
            });
        }

        public void QueueCalibrationWriteThenCurrentSetup(int address, decimal measuredValue, int scale, string label, decimal nextCurrent)
        {
            int raw = (int)Math.Round((double)measuredValue * scale);
            int nextRaw = (int)Math.Round((double)nextCurrent * 1000.0);
            QueueCalibrationWork(label + " 寫入，準備 " + nextCurrent.ToString("0.000", CultureInfo.InvariantCulture) + "A", delegate
            {
                if (!WriteSingleVerified(address, raw, label)) return false;
                if (!WriteSingleVerified(0x0000, 500, "校準設定 5V")) return false;
                return WriteSingleVerified(0x0001, nextRaw, "校準設定電流");
            });
        }

        public void QueueCalibrationStatusRead()
        {
            QueueCalibrationWork("讀取校準狀態", delegate
            {
                byte[] response = ExecuteWithRetry(BuildRead(0x0022, 1), ExpectedReadLength(1));
                if (response == null) return false;
                int status = Word(response, 3);
                BeginInvoke((Action)(() =>
                {
                    SetStatusText("校準狀態 " + status.ToString(CultureInfo.InvariantCulture));
                    Log("SYS", "calibration status 0022H = " + status.ToString(CultureInfo.InvariantCulture));
                }));
                return true;
            });
        }

        private void QueueCalibrationWork(string label, Func<bool> work)
        {
            if (serial == null || !serial.IsOpen)
            {
                MessageBox.Show("尚未連線。", "SK150C", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SetStatusText(label);
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool claimedBusy = false;
                try
                {
                    int waited = 0;
                    while (busy && waited < 3000)
                    {
                        Thread.Sleep(50);
                        waited += 50;
                    }
                    if (busy)
                    {
                        BeginInvoke((Action)(() => SetStatusText("忙碌，校準命令未送出")));
                        return;
                    }

                    busy = true;
                    claimedBusy = true;
                    bool ok = work();
                    BeginInvoke((Action)(() => SetStatusText(ok ? label + " 已確認" : label + " 失敗")));
                }
                finally
                {
                    if (claimedBusy) busy = false;
                }
            });
        }

        private void SyncProtectionFromGroup()
        {
            var groupData = ExecuteWithRetry(BuildRead(CurrentGroupBase(), 6), ExpectedReadLength(6));
            if (groupData != null)
            {
                BeginInvoke((Action)(() =>
                {
                    UpdateGroupMonitor(groupData);
                    if (editGroup == currentGroup) UpdateGroupEditor(groupData, false);
                    Log("SYS", "M" + currentGroup + " group protection synced");
                }));
            }
            else
            {
                BeginInvoke((Action)(() => Log("SYS", "M" + currentGroup + " group protection sync failed")));
            }
        }

        private void SyncDeviceOptions()
        {
            var display = ExecuteWithRetry(BuildRead(0x0014, 4), ExpectedReadLength(4));
            int? backlight = null;
            int? sleep = null;
            int? productModel = null;
            int? firmwareVersion = null;
            if (display != null)
            {
                backlight = Word(display, 3);
                sleep = Word(display, 5);
                productModel = Word(display, 7);
                firmwareVersion = Word(display, 9);
            }

            var buzzer = ExecuteWithRetry(BuildRead(0x001C, 1), ExpectedReadLength(1));
            int? buzzerValue = null;
            if (buzzer != null) buzzerValue = Word(buzzer, 3);

            BeginInvoke((Action)(() =>
            {
                syncingOptions = true;
                try
                {
                    if (backlight.HasValue)
                    {
                        int level = Math.Max(0, Math.Min(5, backlight.Value));
                        backlightBox.SelectedItem = level.ToString(CultureInfo.InvariantCulture);
                    }
                    if (sleep.HasValue)
                    {
                        decimal next = sleep.Value;
                        if (next < sleepBox.Minimum) next = sleepBox.Minimum;
                        if (next > sleepBox.Maximum) next = sleepBox.Maximum;
                        sleepBox.Value = next;
                    }
                    if (buzzerValue.HasValue) buzzerBox.Checked = buzzerValue.Value != 0;
                    UpdateDeviceInfo(productModel, firmwareVersion);
                }
                finally
                {
                    syncingOptions = false;
                }
            }));
        }

        private byte[] ExecuteWithRetry(byte[] request, int expectedLength)
        {
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    byte[] response = Exchange(request, expectedLength);
                    if (response != null)
                    {
                        BeginInvoke((Action)(() =>
                        {
                            successCount++;
                            UpdateCounters();
                        }));
                        return response;
                    }
                }
                catch (CrcException)
                {
                    BeginInvoke((Action)(() =>
                    {
                        crcErrorCount++;
                        UpdateCounters();
                    }));
                }
                catch (Exception ex)
                {
                    BeginInvoke((Action)(() => Log("ERR", ex.Message)));
                }

                if (attempt < 3)
                {
                    BeginInvoke((Action)(() =>
                    {
                        retryCount++;
                        UpdateCounters();
                        Log("SYS", "retry " + attempt + "/3");
                    }));
                    Thread.Sleep(100);
                }
            }

            BeginInvoke((Action)(() =>
            {
                failCount++;
                UpdateCounters();
                SetStatusText("無回應");
            }));
            return null;
        }

        private byte[] Exchange(byte[] request, int expectedLength)
        {
            lock (this)
            {
                serial.DiscardInBuffer();
                Log("TX", Hex(request));
                serial.Write(request, 0, request.Length);

                var buffer = new List<byte>();
                DateTime deadline = DateTime.Now.AddMilliseconds(500);
                while (DateTime.Now < deadline && buffer.Count < expectedLength)
                {
                    int n = serial.BytesToRead;
                    if (n > 0)
                    {
                        byte[] chunk = new byte[n];
                        serial.Read(chunk, 0, n);
                        buffer.AddRange(chunk);
                    }
                    else
                    {
                        Thread.Sleep(5);
                    }
                }

                if (buffer.Count == 0)
                {
                    Log("RX", "timeout");
                    return null;
                }

                byte[] response = buffer.ToArray();
                Log("RX", Hex(response));
                if (!CheckCrc(response)) throw new CrcException();
                return response;
            }
        }

        private byte[] BuildRead(int startAddress, int quantity)
        {
            var frame = new byte[8];
            frame[0] = (byte)slaveBox.Value;
            frame[1] = 0x03;
            frame[2] = (byte)(startAddress >> 8);
            frame[3] = (byte)startAddress;
            frame[4] = (byte)(quantity >> 8);
            frame[5] = (byte)quantity;
            AppendCrc(frame);
            return frame;
        }

        private byte[] BuildWriteSingle(int address, int value)
        {
            var frame = new byte[8];
            frame[0] = (byte)slaveBox.Value;
            frame[1] = 0x06;
            frame[2] = (byte)(address >> 8);
            frame[3] = (byte)address;
            frame[4] = (byte)(value >> 8);
            frame[5] = (byte)value;
            AppendCrc(frame);
            return frame;
        }

        private static int ExpectedReadLength(int words)
        {
            return 5 + words * 2;
        }

        private static int Word(byte[] data, int offset)
        {
            return (data[offset] << 8) | data[offset + 1];
        }

        private static long CombineWords(int high, int low)
        {
            return (((long)high & 0xFFFFL) << 16) | ((long)low & 0xFFFFL);
        }

        private static void AppendCrc(byte[] frame)
        {
            ushort crc = Crc16(frame, frame.Length - 2);
            frame[frame.Length - 2] = (byte)(crc & 0xFF);
            frame[frame.Length - 1] = (byte)(crc >> 8);
        }

        private static bool CheckCrc(byte[] frame)
        {
            if (frame == null || frame.Length < 5) return false;
            ushort crc = Crc16(frame, frame.Length - 2);
            return frame[frame.Length - 2] == (byte)(crc & 0xFF) && frame[frame.Length - 1] == (byte)(crc >> 8);
        }

        private static ushort Crc16(byte[] data, int length)
        {
            ushort crc = 0xFFFF;
            for (int i = 0; i < length; i++)
            {
                crc ^= data[i];
                for (int bit = 0; bit < 8; bit++)
                {
                    bool lsb = (crc & 1) != 0;
                    crc >>= 1;
                    if (lsb) crc ^= 0xA001;
                }
            }
            return crc;
        }

        private void UpdateValues(double vout, double iout, double power)
        {
            latestVout = vout;
            latestIout = iout;
            voutTile.SetValue(vout, "0.00");
            ioutTile.SetValue(iout, "0.000");
            powerTile.SetValue(power, "0.00");
            powerTile.AddTrendPoint(power);
            trendChart.AddPoint(vout, iout, power);
        }

        private void UpdateSetValues(double voltage, double current)
        {
            if (editGroup < 0)
            {
                SetEditorFromDevice(setVoltageBox, voltage, false);
                SetEditorFromDevice(setCurrentBox, current, false);
            }
            setVoltageTile.SetValue(voltage, "0.00");
            setCurrentTile.SetValue(current, "0.000");
            trendChart.SetTargets(voltage, current);
        }

        private void UpdateTrendTargetsFromEditors()
        {
            trendChart.SetTargets((double)setVoltageBox.Value, (double)setCurrentBox.Value);
        }

        private void UpdateProtectionValues(double ovp, double ocp)
        {
            if (editGroup < 0)
            {
                SetEditorFromDevice(setOvpBox, ovp, false);
                SetEditorFromDevice(setOcpBox, ocp, false);
            }
            ovpTile.SetValue(ovp, "0.00");
            ocpTile.SetValue(ocp, "0.000");
        }

        private void UpdateGroupMonitor(byte[] groupData)
        {
            int ovp = Word(groupData, 9);
            int ocp = Word(groupData, 11);
            ovpTile.SetValue(ovp / 100.0, "0.00");
            ocpTile.SetValue(ocp / 1000.0, "0.000");
        }

        private void UpdateGroupEditor(byte[] groupData, bool force)
        {
            int vset = Word(groupData, 3);
            int iset = Word(groupData, 5);
            int lvp = Word(groupData, 7);
            int ovp = Word(groupData, 9);
            int ocp = Word(groupData, 11);
            int opp = Word(groupData, 13);

            SetEditorFromDevice(setVoltageBox, vset / 100.0, force);
            SetEditorFromDevice(setCurrentBox, iset / 1000.0, force);
            SetEditorFromDevice(setLvpBox, lvp / 100.0, force);
            SetEditorFromDevice(setOvpBox, ovp / 100.0, force);
            SetEditorFromDevice(setOcpBox, ocp / 1000.0, force);
            SetEditorFromDevice(setOppBox, opp / 10.0, force);
        }

        private void SetEditorFromDevice(StepEditor editor, double value, bool force)
        {
            if (force || !editor.IsDirty)
            {
                editor.SetValue(value);
                UpdateParameterBlockDirty(editor);
            }
        }

        private void ClearDirty(StepEditor editor)
        {
            editor.SetDirty(false);
            UpdateParameterBlockDirty(editor);
        }

        private void UpdateParameterBlockDirty(StepEditor editor)
        {
            Panel block;
            if (!parameterBlocks.TryGetValue(editor, out block)) return;
            if (editor.IsWriting) block.BackColor = Color.FromArgb(255, 205, 210);
            else block.BackColor = editor.IsDirty ? Color.FromArgb(255, 242, 153) : Color.FromArgb(250, 251, 252);
            block.Invalidate();
        }

        private void UpdateStatusValues(double temperature, int cvcc, int onoff, int protect, double ah, double wh, int runHour, int runMinute, int runSecond, int keyLock)
        {
            temperatureTile.SetValue(temperature, "0.0");
            modeTile.SetText(cvcc == 1 ? "CC" : "CV");
            outputStateTile.SetText(onoff == 1 ? "ON" : "OFF");
            bool wasOutputOn = latestOutputOn;
            latestOutputOn = onoff == 1;
            if (latestOutputOn && !wasOutputOn)
            {
                outputOnSince = DateTime.Now;
                lowCurrentSince = null;
                nextVoltageCheckAt = DateTime.Now.AddSeconds((double)fullChargeVoltagePollBox.Value);
            }
            else if (!latestOutputOn && wasOutputOn && !voltageCheckInProgress)
            {
                outputOnSince = null;
                lowCurrentSince = null;
                nextVoltageCheckAt = null;
            }
            int previousProtectCode = currentProtectCode;
            currentProtectCode = protect;
            protectTile.SetText(ProtectText(protect));
            protectTile.SetAlert(protect != 0);
            if (protect != 0) SetStatusText("保護：" + ProtectText(protect));
            else if (previousProtectCode != 0) SetStatusText("正常");
            capacityTile.SetValue(ah, "0.000");
            energyTile.SetValue(wh, "0.000");
            timerTile.SetText(FormatRunTime(runHour, runMinute, runSecond));

            syncingOptions = true;
            try
            {
                keyLockBox.Checked = keyLock != 0;
            }
            finally
            {
                syncingOptions = false;
            }
            EvaluateFullChargeAutomation();
        }

        private void EvaluateFullChargeAutomation()
        {
            DateTime now = DateTime.Now;
            if (voltageCheckInProgress)
            {
                ContinueVoltageCutoffFlow(now);
                if (fullChargeCurrentEnableBox.Checked) SetFullChargeCurrentStatus("電壓檢查中");
                else SetFullChargeCurrentStatus("停用");
                return;
            }

            if (!latestOutputOn)
            {
                if (fullChargeCurrentEnableBox.Checked) SetFullChargeCurrentStatus("等待輸出 ON");
                else SetFullChargeCurrentStatus("停用");
                if (fullChargeVoltageEnableBox.Checked) SetFullChargeVoltageStatus("等待輸出 ON");
                else SetFullChargeVoltageStatus("停用");
                return;
            }

            TimeSpan onElapsed = outputOnSince.HasValue ? now - outputOnSince.Value : TimeSpan.Zero;
            if (fullChargeCurrentEnableBox.Checked)
            {
                if (onElapsed < FullChargeStartDelay)
                {
                    SetFullChargeCurrentStatus("延遲中 " + Math.Max(0, (int)Math.Ceiling((FullChargeStartDelay - onElapsed).TotalSeconds)).ToString(CultureInfo.InvariantCulture) + "s");
                    lowCurrentSince = null;
                }
                else if (latestIout < (double)fullChargeCurrentBox.Value)
                {
                    if (!lowCurrentSince.HasValue) lowCurrentSince = now;
                    TimeSpan lowElapsed = now - lowCurrentSince.Value;
                    SetFullChargeCurrentStatus("低電流 " + lowElapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " / " + FullChargeLowCurrentDelay.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + "s");
                    if (lowElapsed >= FullChargeLowCurrentDelay) TriggerAutoOutputOff("滿電截止電流");
                }
                else
                {
                    lowCurrentSince = null;
                    SetFullChargeCurrentStatus("監控中 " + latestIout.ToString("0.000", CultureInfo.InvariantCulture) + "A");
                }
            }
            else
            {
                lowCurrentSince = null;
                SetFullChargeCurrentStatus("停用");
            }

            if (fullChargeVoltageEnableBox.Checked)
            {
                if (onElapsed < FullChargeStartDelay)
                {
                    SetFullChargeVoltageStatus("延遲中 " + Math.Max(0, (int)Math.Ceiling((FullChargeStartDelay - onElapsed).TotalSeconds)).ToString(CultureInfo.InvariantCulture) + "s");
                    return;
                }
                if (!nextVoltageCheckAt.HasValue) nextVoltageCheckAt = now.AddSeconds((double)fullChargeVoltagePollBox.Value);
                if (now >= nextVoltageCheckAt.Value && !voltageCheckInProgress && !autoOffInProgress)
                {
                    TriggerVoltageCutoffCheck();
                }
                else if (!voltageCheckInProgress)
                {
                    TimeSpan remain = nextVoltageCheckAt.Value - now;
                    SetFullChargeVoltageStatus("下次檢查 " + Math.Max(0, (int)Math.Ceiling(remain.TotalSeconds)).ToString(CultureInfo.InvariantCulture) + "s");
                }
            }
            else
            {
                nextVoltageCheckAt = null;
                SetFullChargeVoltageStatus("停用");
            }
        }

        private void TriggerAutoOutputOff(string reason)
        {
            if (autoOffInProgress || voltageCheckInProgress) return;
            autoOffInProgress = true;
            SetFullChargeCurrentStatus("自動關閉中");
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool claimed = false;
                try
                {
                    if (!ClaimSerialForAutomation(4000)) return;
                    claimed = true;
                    byte[] response = ExecuteWithRetry(BuildWriteSingle(0x0012, 0), 8);
                    BeginInvoke((Action)(() =>
                    {
                        if (response != null)
                        {
                            SetStatusText(reason + " 已停止輸出");
                            SetFullChargeCurrentStatus("已自動截止");
                        }
                        else SetFullChargeCurrentStatus("自動關閉失敗");
                    }));
                }
                finally
                {
                    if (claimed) busy = false;
                    autoOffInProgress = false;
                }
            });
        }

        private void TriggerVoltageCutoffCheck()
        {
            if (voltageCheckInProgress) return;
            voltageCheckInProgress = true;
            lowCurrentSince = null;
            voltageOffConfirmedAt = null;
            voltageOffCommandPending = false;
            voltageReadPending = false;
            voltageRestoreOnPending = false;
            nextVoltageCheckAt = null;
            SetFullChargeVoltageStatus("要求輸出 OFF");
            Log("SYS", "full charge voltage check: request OFF, polling continues");
            RequestVoltageCutoffOff();
        }

        private void RequestVoltageCutoffOff()
        {
            if (!voltageCheckInProgress || voltageOffCommandPending || voltageRestoreOnPending) return;
            voltageOffCommandPending = true;
            SetFullChargeVoltageStatus("等待輪巡送 OFF");
        }

        private void ExecuteVoltageAutomationWritesAtPollEnd()
        {
            if (voltageCheckInProgress && voltageOffCommandPending && !voltageRestoreOnPending)
            {
                byte[] offResponse = ExecuteWithRetry(BuildWriteSingle(0x0012, 0), 8);
                bool sent = offResponse != null;
                BeginInvoke((Action)(() =>
                {
                    if (sent)
                    {
                        voltageOffCommandPending = false;
                        SetFullChargeVoltageStatus("等待輪巡確認 OFF");
                        Log("SYS", "full charge voltage check: OFF command sent at poll end");
                    }
                    else
                    {
                        SetFullChargeVoltageStatus("OFF 寫入重試中");
                        Log("SYS", "full charge voltage check: OFF write failed at poll end, will retry");
                    }
                }));
            }

            if (voltageCheckInProgress && voltageRestoreOnPending)
            {
                byte[] onResponse = ExecuteWithRetry(BuildWriteSingle(0x0012, 1), 8);
                bool sent = onResponse != null;
                BeginInvoke((Action)(() =>
                {
                    if (sent)
                    {
                        voltageReadPending = false;
                        voltageRestoreOnPending = false;
                        voltageCheckInProgress = false;
                        voltageOffConfirmedAt = null;
                        nextVoltageCheckAt = DateTime.Now.AddSeconds((double)voltageRestorePollSeconds);
                        SetFullChargeVoltageStatus("恢復 ON，等待輪巡");
                        Log("SYS", "full charge voltage check: ON command sent at poll end");
                    }
                    else
                    {
                        SetFullChargeVoltageStatus("恢復 ON 重試中");
                        Log("SYS", "full charge voltage check: ON write failed at poll end, will retry");
                    }
                }));
            }
        }

        private void ContinueVoltageCutoffFlow(DateTime now)
        {
            if (!voltageCheckInProgress) return;
            lowCurrentSince = null;

            if (latestOutputOn)
            {
                voltageOffConfirmedAt = null;
                if (!voltageOffCommandPending) RequestVoltageCutoffOff();
                return;
            }

            if (!voltageOffConfirmedAt.HasValue)
            {
                voltageOffConfirmedAt = now;
                SetFullChargeVoltageStatus("OFF 已確認，等待 " + FullChargeVoltageOffSettle.TotalSeconds.ToString("0", CultureInfo.InvariantCulture) + "s");
                Log("SYS", "full charge voltage check: OFF confirmed by polling");
                return;
            }

            TimeSpan wait = now - voltageOffConfirmedAt.Value;
            if (wait < FullChargeVoltageOffSettle)
            {
                SetFullChargeVoltageStatus("OFF 穩定中 " + Math.Max(0, (int)Math.Ceiling((FullChargeVoltageOffSettle - wait).TotalSeconds)).ToString(CultureInfo.InvariantCulture) + "s");
                return;
            }

            if (!voltageReadPending && !voltageRestoreOnPending)
            {
                voltageReadPending = true;
                decimal target = fullChargeVoltageTargetBox.Value;
                decimal pollSeconds = fullChargeVoltagePollBox.Value;
                double measured = latestVout;
                if (measured >= (double)target)
                {
                    voltageReadPending = false;
                    voltageCheckInProgress = false;
                    voltageOffConfirmedAt = null;
                    SetStatusText("滿電截止電壓 已停止輸出");
                    SetFullChargeVoltageStatus("已截止 " + measured.ToString("0.00", CultureInfo.InvariantCulture) + "V");
                    Log("SYS", "full charge voltage check: stopped at " + measured.ToString("0.00", CultureInfo.InvariantCulture) + "V");
                }
                else
                {
                    SetFullChargeVoltageStatus("讀值 " + measured.ToString("0.00", CultureInfo.InvariantCulture) + "V，恢復 ON");
                    Log("SYS", "full charge voltage check: " + measured.ToString("0.00", CultureInfo.InvariantCulture) + "V < target " + target.ToString("0.00", CultureInfo.InvariantCulture));
                    RestoreOutputOnForVoltageCheck(pollSeconds);
                }
            }
        }

        private void RestoreOutputOnForVoltageCheck(decimal pollSeconds)
        {
            if (voltageRestoreOnPending) return;
            voltageRestoreOnPending = true;
            voltageRestorePollSeconds = pollSeconds;
            SetFullChargeVoltageStatus("等待輪巡恢復 ON");
        }

        private bool ClaimSerialForAutomation(int timeoutMs)
        {
            if (serial == null || !serial.IsOpen) return false;
            int waited = 0;
            while (busy && waited < timeoutMs)
            {
                Thread.Sleep(50);
                waited += 50;
            }
            if (busy || serial == null || !serial.IsOpen) return false;
            busy = true;
            return true;
        }

        private void UpdateFullChargeStatusLabels()
        {
            if (!fullChargeCurrentEnableBox.Checked) SetFullChargeCurrentStatus("停用");
            if (!fullChargeVoltageEnableBox.Checked) SetFullChargeVoltageStatus("停用");
            if (fullChargeCurrentEnableBox.Checked && !latestOutputOn) SetFullChargeCurrentStatus("等待輸出 ON");
            if (fullChargeVoltageEnableBox.Checked && !latestOutputOn) SetFullChargeVoltageStatus("等待輸出 ON");
        }

        private void SetFullChargeCurrentStatus(string text)
        {
            string prefix = IsEnglishUi() ? "Current cutoff: M" : "電流截止：M";
            fullChargeCurrentStatusLabel.Text = prefix + currentGroup.ToString(CultureInfo.InvariantCulture) + " " + FullChargeStatusText(text);
        }

        private void SetFullChargeVoltageStatus(string text)
        {
            string prefix = IsEnglishUi() ? "Voltage cutoff: M" : "電壓截止：M";
            fullChargeVoltageStatusLabel.Text = prefix + currentGroup.ToString(CultureInfo.InvariantCulture) + " " + FullChargeStatusText(text);
        }

        private string FullChargeStatusText(string text)
        {
            if (!IsEnglishUi()) return text;
            return text
                .Replace("停用", "Disabled")
                .Replace("等待輸出 ON", "Waiting ON")
                .Replace("延遲中", "Delay")
                .Replace("低電流", "Low current")
                .Replace("監控中", "Monitoring")
                .Replace("電壓檢查中", "Voltage check")
                .Replace("自動關閉中", "Auto OFF")
                .Replace("已自動截止", "Stopped")
                .Replace("自動關閉失敗", "OFF failed")
                .Replace("下次檢查", "Next check")
                .Replace("檢查中 OFF", "Checking OFF")
                .Replace("要求輸出 OFF", "Requesting OFF")
                .Replace("等待通訊空檔 OFF", "Waiting bus for OFF")
                .Replace("等待輪巡送 OFF", "Waiting poll OFF write")
                .Replace("等待輪巡確認 OFF", "Waiting poll OFF")
                .Replace("OFF 檢查失敗：通訊忙碌", "OFF check failed: busy")
                .Replace("OFF 寫入失敗", "OFF write failed")
                .Replace("OFF 寫入重試中", "OFF retrying")
                .Replace("OFF 確認失敗", "OFF verify failed")
                .Replace("OFF 已確認，等待", "OFF verified, wait")
                .Replace("OFF 穩定中", "OFF settling")
                .Replace("OFF 失敗", "OFF failed")
                .Replace("等待輪巡恢復 ON", "Waiting poll ON write")
                .Replace("恢復 ON，等待輪巡", "ON sent, waiting poll")
                .Replace("恢復 ON 重試中", "ON retrying")
                .Replace("讀值失敗，恢復 ON 失敗", "Read failed, ON restore failed")
                .Replace("讀值失敗，恢復 ON", "Read failed, ON restored")
                .Replace("已截止", "Stopped")
                .Replace("讀值", "Read")
                .Replace("恢復 ON 失敗", "ON restore failed")
                .Replace("，恢復 ON", ", ON restored");
        }

        private static string FormatRunTime(int hours, int minutes, int seconds)
        {
            if (minutes < 0) minutes = 0;
            if (seconds < 0) seconds = 0;
            minutes = Math.Min(99, minutes);
            seconds = Math.Min(99, seconds);
            return hours.ToString("00", CultureInfo.InvariantCulture) + ":" +
                minutes.ToString("00", CultureInfo.InvariantCulture) + ":" +
                seconds.ToString("00", CultureInfo.InvariantCulture);
        }

        private string ProtectText(int code)
        {
            bool english = IsEnglishUi();
            switch (code)
            {
                case 0: return english ? "Normal" : "正常";
                case 1: return english ? "OVP Over Voltage" : "OVP 過壓保護";
                case 2: return english ? "OCP Over Current" : "OCP 過流保護";
                case 3: return english ? "OPP Over Power" : "OPP 過功率保護";
                case 4: return english ? "LVP Low Voltage" : "LVP 低壓保護";
                case 5: return english ? "OAH Over Capacity" : "OAH 超容量保護";
                case 6: return english ? "OHP Over Time" : "OHP 超時保護";
                case 7: return english ? "OTP Over Temperature" : "OTP 過溫保護";
                case 8: return english ? "OEP Over Energy" : "OEP 超能量保護";
                case 9: return english ? "OWH Over Wh" : "OWH 超瓦時保護";
                case 10: return english ? "ICP Input Current Protection" : "ICP 輸入電流保護";
                case 11: return english ? "IVP Input Voltage Protection" : "IVP 輸入電壓保護";
                default: return english ? "Code " + code.ToString(CultureInfo.InvariantCulture) : "代碼 " + code.ToString(CultureInfo.InvariantCulture);
            }
        }

        private int CurrentGroupBase()
        {
            return GroupBase(currentGroup);
        }

        private static int GroupBase(int group)
        {
            return 0x0050 + group * 0x0010;
        }

        private void UpdateQuickGroupButton(int group, double? voltage, double? current)
        {
            bool? sini = group >= 0 && group < quickGroupSini.Length ? quickGroupSini[group] : null;
            UpdateQuickGroupButton(group, voltage, current, sini);
        }

        private void UpdateQuickGroupButton(int group, double? voltage, double? current, bool? siniOn)
        {
            QuickGroupButton button;
            if (!quickGroupButtons.TryGetValue(group, out button)) return;
            if (group >= 0 && group < quickGroupVoltages.Length)
            {
                quickGroupVoltages[group] = voltage;
                quickGroupCurrents[group] = current;
                quickGroupSini[group] = siniOn;
                LoadQuickGroupCutoffIndicators(group);
            }
            button.SetValues(voltage.HasValue ? FormatGroupVoltage(voltage.Value) : "--V",
                current.HasValue ? FormatGroupCurrent(current.Value) : "--A");
            button.HasSiniWarning = siniOn.GetValueOrDefault(false);
            button.HasCurrentCutoff = group >= 0 && group < quickGroupCurrentCutoff.Length && quickGroupCurrentCutoff[group].GetValueOrDefault(false);
            button.HasVoltageCutoff = group >= 0 && group < quickGroupVoltageCutoff.Length && quickGroupVoltageCutoff[group].GetValueOrDefault(false);
            button.IsActive = group == activeQuickGroup;
        }

        private void LoadQuickGroupCutoffIndicators(int group)
        {
            if (group < 0 || group >= quickGroupCurrentCutoff.Length) return;
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(PresetRegistryPath(group)))
            {
                quickGroupCurrentCutoff[group] = ReadRegistryBool(key, "FullChargeCutoffCurrentEnabled", false);
                quickGroupVoltageCutoff[group] = ReadRegistryBool(key, "FullChargeCutoffVoltageEnabled", false);
            }
        }

        private void SetQuickGroupCutoffIndicators(int group, bool currentEnabled, bool voltageEnabled)
        {
            if (group < 0 || group >= quickGroupCurrentCutoff.Length) return;
            quickGroupCurrentCutoff[group] = currentEnabled;
            quickGroupVoltageCutoff[group] = voltageEnabled;
            QuickGroupButton button;
            if (quickGroupButtons.TryGetValue(group, out button))
            {
                button.HasCurrentCutoff = currentEnabled;
                button.HasVoltageCutoff = voltageEnabled;
            }
        }

        private void UpdateQuickGroupCutoffIndicators(int group)
        {
            SetQuickGroupCutoffIndicators(group, fullChargeCurrentEnableBox.Checked, fullChargeVoltageEnableBox.Checked);
        }

        private void SetQuickGroupSini(int group, bool value)
        {
            if (group < 0 || group >= quickGroupSini.Length) return;
            quickGroupSini[group] = value;
            QuickGroupButton button;
            if (quickGroupButtons.TryGetValue(group, out button)) button.HasSiniWarning = value;
            if (group == currentGroup) StyleOnOffToggle(siniToggleButton, value, ToggleKind.Danger);
        }

        private void SetActiveQuickGroup(int group)
        {
            activeQuickGroup = group;
            foreach (var item in quickGroupButtons)
            {
                item.Value.IsActive = item.Key == group;
            }
        }

        private void UpdateCurrentGroupVoltage(double voltage)
        {
            if (currentGroup < 0 || currentGroup >= quickGroupVoltages.Length) return;
            quickGroupVoltages[currentGroup] = voltage;
            UpdateQuickGroupButton(currentGroup, quickGroupVoltages[currentGroup], quickGroupCurrents[currentGroup]);
        }

        private void UpdateCurrentGroupCurrent(double current)
        {
            if (currentGroup < 0 || currentGroup >= quickGroupCurrents.Length) return;
            quickGroupCurrents[currentGroup] = current;
            UpdateQuickGroupButton(currentGroup, quickGroupVoltages[currentGroup], quickGroupCurrents[currentGroup]);
        }

        private static string FormatGroupVoltage(double voltage)
        {
            double rounded = Math.Round(voltage, 2);
            if (Math.Abs(rounded - Math.Round(rounded)) < 0.005)
                return Math.Round(rounded).ToString("0", CultureInfo.InvariantCulture) + "V";
            return rounded.ToString("0.##", CultureInfo.InvariantCulture) + "V";
        }

        private static string FormatGroupCurrent(double current)
        {
            double rounded = Math.Round(current, 3);
            if (Math.Abs(rounded - Math.Round(rounded)) < 0.0005)
                return Math.Round(rounded).ToString("0", CultureInfo.InvariantCulture) + "A";
            if (Math.Abs(rounded * 10.0 - Math.Round(rounded * 10.0)) < 0.0005)
                return rounded.ToString("0.0", CultureInfo.InvariantCulture) + "A";
            return rounded.ToString("0.###", CultureInfo.InvariantCulture) + "A";
        }

        private int PollIntervalMs()
        {
            return pollBackoffActive ? Math.Max(2000, pollIntervalMs) : pollIntervalMs;
        }

        private void SetPollInterval(int interval)
        {
            pollIntervalMs = interval;
            pollTimer.Interval = PollIntervalMs();
            StyleIntervalButton(interval100Button, interval == 100);
            StyleIntervalButton(interval300Button, interval == 300);
            StyleIntervalButton(interval1000Button, interval == 1000);
        }

        private void NotePollResult(bool ok)
        {
            if (ok)
            {
                bool recovered = consecutivePollFailures > 0 || pollBackoffActive;
                consecutivePollFailures = 0;
                if (pollBackoffActive)
                {
                    pollBackoffActive = false;
                    pollTimer.Interval = PollIntervalMs();
                    Log("SYS", "poll interval restored " + pollIntervalMs.ToString(CultureInfo.InvariantCulture) + "ms");
                }
                if (recovered && currentProtectCode == 0) SetStatusText("正常");
                return;
            }

            consecutivePollFailures++;
            if (consecutivePollFailures >= 3 && !pollBackoffActive)
            {
                pollBackoffActive = true;
                pollTimer.Interval = PollIntervalMs();
                Log("SYS", "poll backoff " + pollTimer.Interval.ToString(CultureInfo.InvariantCulture) + "ms after consecutive failures");
            }
        }

        private void ApplyQuickInputMode()
        {
            bool enabled = quickInputBox.Checked;
            setVoltageBox.QuickSelectOnFocus = enabled;
            setCurrentBox.QuickSelectOnFocus = enabled;
            setLvpBox.QuickSelectOnFocus = enabled;
            setOvpBox.QuickSelectOnFocus = enabled;
            setOcpBox.QuickSelectOnFocus = enabled;
            setOppBox.QuickSelectOnFocus = enabled;
            fullChargeCurrentBox.QuickSelectOnFocus = enabled;
            fullChargeVoltageTargetBox.QuickSelectOnFocus = enabled;
            fullChargeVoltagePollBox.QuickSelectOnFocus = enabled;
        }

        private void ApplyLanguage()
        {
            bool english = languageBox.SelectedItem != null && languageBox.SelectedItem.ToString() == "English";
            Text = english ? "SK150C Modbus Control Console" : "SK150C Modbus 控制台";
            ApplyLanguageToControls(this, english);
            SaveLanguageSelection();
            protectTile.SetText(ProtectText(currentProtectCode));
            protectTile.SetAlert(currentProtectCode != 0);
            if (currentProtectCode != 0) SetStatusText("保護：" + ProtectText(currentProtectCode));
            UpdateFullChargeStatusLabels();
        }

        private void ApplyLanguageToControls(Control parent, bool english)
        {
            foreach (Control control in parent.Controls)
            {
                if (!object.ReferenceEquals(control, languageBox) && !string.IsNullOrEmpty(control.Text))
                {
                    control.Text = TranslateUiText(control.Text, english);
                }
                if (control.HasChildren) ApplyLanguageToControls(control, english);
            }
        }

        private static string TranslateUiText(string text, bool english)
        {
            if (english)
            {
                if (text.StartsWith("狀態：")) return "Status: " + TranslateUiText(text.Substring(3), true);
                if (text.StartsWith("保護：")) return "Protection: " + TranslateUiText(text.Substring(3), true);
                if (text.StartsWith("目前調用：")) return "Active Preset: " + TranslateUiText(text.Substring(5), true);
                if (text.StartsWith("正在編輯：")) return "Editing: " + TranslateUiText(text.Substring(5), true);
            }
            else
            {
                if (text.StartsWith("Status: ")) return "狀態：" + TranslateUiText(text.Substring(8), false);
                if (text.StartsWith("Protection: ")) return "保護：" + TranslateUiText(text.Substring(12), false);
                if (text.StartsWith("Active Preset: ")) return "目前調用：" + TranslateUiText(text.Substring(15), false);
                if (text.StartsWith("Editing: ")) return "正在編輯：" + TranslateUiText(text.Substring(9), false);
            }

            string translated;
            if (TryTranslateExact(text, english, out translated)) return translated;
            return TranslatePhrases(text, english);
        }

        private static string TranslatePhrases(string text, bool english)
        {
            string[,] pairs = new string[,]
            {
                { "讀取中", "Reading" },
                { "讀取失敗", "Read Failed" },
                { "調用中", "Calling" },
                { "失敗", "Failed" },
                { "忙碌中", "Busy" },
                { "忙碌", "Busy" },
                { "稍後再刷新 M0-M10", "refresh M0-M10 later" },
                { "讀取快捷組摘要 M0-M10", "Reading preset summary M0-M10" },
                { "快捷組摘要已更新", "Preset summary updated" },
                { "正在編輯", "Editing" },
                { "寫入確認中", "write verifying" },
                { "寫入無回應", "write no response" },
                { "未送出", "not sent" },
                { "已確認", "confirmed" },
                { "未確認", "not confirmed" },
                { "讀回失敗", "readback failed" },
                { "已調用", "Called" },
                { "並同步", "and synced" },
                { "寫入", "write" },
                { "讀回", "readback" },
                { "將寫入", "will write" },
                { "。請確認外部表具量測值就是目前輸入值。", ". Please confirm the external meter value matches the value entered." }
            };
            int from = english ? 0 : 1;
            int to = english ? 1 : 0;
            string result = text;
            for (int i = 0; i < pairs.GetLength(0); i++)
            {
                result = result.Replace(pairs[i, from], pairs[i, to]);
            }
            return result;
        }

        private static bool TryTranslateExact(string text, bool english, out string translated)
        {
            string[,] pairs = new string[,]
            {
                { "連線設定", "Connection" },
                { "COM 埠", "COM Port" },
                { "波特率", "Baud" },
                { "站號", "Slave ID" },
                { "掃描", "Scan" },
                { "連線", "Connect" },
                { "斷線", "Disconnect" },
                { "自動輪詢", "Auto Poll" },
                { "間隔", "Interval" },
                { "成功", "Success" },
                { "失敗", "Fail" },
                { "CRC 錯", "CRC Err" },
                { "重試", "Retry" },
                { "裝置資訊", "Device Info" },
                { "產品型號", "Model" },
                { "固件版本", "Firmware" },
                { "裝置選項", "Device Options" },
                { "背光亮度", "Backlight" },
                { "熄屏時間", "Sleep Time" },
                { "蜂鳴音", "Buzzer" },
                { "按鍵鎖", "Key Lock" },
                { "維護功能", "Maintenance" },
                { "重啟", "Restart" },
                { "恢復出廠", "Factory Reset" },
                { "校準", "Calibration" },
                { "M組參數", "M Group Params" },
                { "匯出 M組", "Export M" },
                { "匯入 M組", "Import M" },
                { "匯出 M組參數", "Export M Group Params" },
                { "匯入 M組參數", "Import M Group Params" },
                { "M組參數已匯出", "M group params exported" },
                { "M組參數已匯入", "M group params imported" },
                { "匯出完成", "Export complete" },
                { "匯出失敗", "Export failed" },
                { "匯入完成", "Import complete" },
                { "匯入失敗", "Import failed" },
                { "匯入格式錯誤", "Import format error" },
                { "JSON 根格式錯誤", "Invalid JSON root" },
                { "不是 SK150C M組參數檔", "Not an SK150C M group parameter file" },
                { "缺少 groups 欄位", "Missing groups field" },
                { "M組數量必須為 11 組", "M group count must be 11" },
                { "M組資料格式錯誤", "Invalid M group data format" },
                { "M組編號錯誤", "Invalid M group number" },
                { "M組資料重複", "Duplicate M group data" },
                { "參數範圍錯誤", "parameter out of range" },
                { "讀回不一致", "readback mismatch" },
                { "語言", "Language" },
                { "待命", "Standby" },
                { "未連線", "Disconnected" },
                { "尚未連線。", "Not connected." },
                { "已連線", "Connected" },
                { "連線失敗", "Connect Failed" },
                { "正常", "Normal" },
                { "無回應", "No Response" },
                { "清除", "Clear" },
                { "即時監控與歷史曲線", "Live Monitor & History" },
                { "輸出電壓", "Output Voltage" },
                { "設定電壓", "Set Voltage" },
                { "輸出電流", "Output Current" },
                { "設定電流", "Set Current" },
                { "輸出功率", "Output Power" },
                { "系統溫度", "Temperature" },
                { "輸出模式", "Mode" },
                { "輸出狀態", "Output State" },
                { "容量 AH", "Capacity AH" },
                { "能量 WH", "Energy WH" },
                { "計時器", "Timer" },
                { "保護狀態", "Protection" },
                { "輸出控制", "Output Control" },
                { "快速輸入", "Quick Input" },
                { "寫入電壓", "Write Voltage" },
                { "寫入電流", "Write Current" },
                { "寫入 LVP", "Write LVP" },
                { "寫入 OVP", "Write OVP" },
                { "寫入 OCP", "Write OCP" },
                { "寫入 OPP", "Write OPP" },
                { "LVP 低壓保護", "LVP Protection" },
                { "OVP 過壓保護", "OVP Protection" },
                { "OCP 過流保護", "OCP Protection" },
                { "OPP 過功率保護", "OPP Protection" },
                { "S-INI 調用後輸出", "S-INI Output After Call" },
                { "輸出開關", "Output Switch" },
                { "快捷組", "Presets" },
                { "刷新 M0-M10", "Refresh M0-M10" },
                { "刷新中...", "Refreshing..." },
                { "目前調用：--", "Active Preset: --" },
                { "忙碌中", "Busy" },
                { "讀取中", "Reading" },
                { "讀取失敗", "Read Failed" },
                { "調用中", "Calling" },
                { "校準", "Calibration" },
                { "TX / RX 通訊紀錄", "TX / RX Log" }
                ,
                { "常用", "Common" },
                { "進階", "Advanced" },
                { "滿電截止", "Full Charge Cutoff" },
                { "滿電截止電流", "Cutoff Current" },
                { "套用截止電流", "Apply Current" },
                { "滿電截止電壓", "Cutoff Voltage" },
                { "目標電壓", "Target Voltage" },
                { "套用目標電壓", "Apply Target" },
                { "套用電壓", "Apply V" },
                { "輪詢時間", "Poll Interval" },
                { "套用輪詢時間", "Apply Interval" },
                { "套用", "Apply" },
                { "SK150C 校準", "SK150C Calibration" },
                { "校準電壓", "Voltage Calibration" },
                { "校準電流", "Current Calibration" },
                { "請先選擇校準類別", "Select calibration type" },
                { "選擇後會顯示實測值輸入框，並立即送出第一段準備值。", "After selection, the measured-value input appears and the first preparation command is sent." },
                { "實測值", "Measured Value" },
                { "確認並進入下一段", "Confirm & Next" },
                { "確認完成校準", "Finish Calibration" },
                { "關閉", "Close" },
                { "請確認已接好高精度電壓表與電流表。校準會改寫從機校準資料；若為降壓型電源，請確認輸入電壓高於 28V。電流校準時，請將萬用表切到電流檔，表筆插入 10A/20A 孔位，依畫面提示輸入實測值。", "Confirm that high-precision voltage and current meters are connected. Calibration will rewrite device calibration data. For buck supplies, make sure input voltage is above 28V. For current calibration, set the multimeter to current mode, use the 10A/20A jack, and enter the measured value shown by the meter." },
                { "請確認校準工具與接線正確。自行校準錯誤造成的問題，請自行負責。", "Confirm calibration tools and wiring are correct. Incorrect manual calibration is your responsibility." },
                { "目前已送出準備值，請等待從機輸出穩定後輸入外部表具讀值。", "Preparation value sent. Wait for device output to stabilize, then enter the external meter reading." },
                { "確認校準寫入", "Confirm Calibration Write" },
                { "最後一步已送出，正在讀取校準結果...", "Final step sent. Reading calibration result..." },
                { "校準成功", "Calibration succeeded" },
                { "校準失敗", "Calibration failed" },
                { "校準寫入失敗，請檢查連線與從機狀態。", "Calibration write failed. Check connection and device status." },
                { "校準狀態：", "Calibration status: " },
                { "校準狀態讀取失敗，請觀察從機顯示。", "Failed to read calibration status. Check the device display." },
                { "校準結果", "Calibration Result" },
                { "步驟 1/2：5V 實測輸入", "Step 1/2: 5V measured input" },
                { "步驟 2/2：25V 實測輸入", "Step 2/2: 25V measured input" },
                { "步驟 1/2：1A 實測輸入", "Step 1/2: 1A measured input" },
                { "步驟 2/2：5A 實測輸入", "Step 2/2: 5A measured input" },
                { "請輸入目前實測輸出電壓。", "Enter the currently measured output voltage." },
                { "請輸入目前實測輸出電流。", "Enter the currently measured output current." },
                { "5V 電壓校準", "5V voltage calibration" },
                { "25V 電壓校準", "25V voltage calibration" },
                { "1A 電流校準", "1A current calibration" },
                { "5A 電流校準", "5A current calibration" },
                { "滿電截止電流說明", "Cutoff Current Help" },
                { "滿電截止電壓說明", "Cutoff Voltage Help" },
                { "充電接近滿電時，輸出電流會逐漸下降。\r\n當電流低於指定目標後，程式會自動關閉輸出。", "When charging is nearly full, output current gradually drops.\r\nWhen current falls below the target, the app automatically turns output off." },
                { "使用前需將探頭接在電池輸出端，確保能量測到電池正負極電壓。\r\n此功能會依設定週期偵測電壓，當電壓高於設定目標時自動關閉輸出。", "Before use, connect the probe to the battery output so the battery terminal voltage can be measured.\r\nThis feature checks voltage at the set interval and automatically turns output off when voltage is above the target." }
            };

            int from = english ? 0 : 1;
            int to = english ? 1 : 0;
            for (int i = 0; i < pairs.GetLength(0); i++)
            {
                if (text == pairs[i, from])
                {
                    translated = pairs[i, to];
                    return true;
                }
            }
            translated = text;
            return false;
        }

        public bool IsEnglishUi()
        {
            return languageBox.SelectedItem != null && languageBox.SelectedItem.ToString() == "English";
        }

        public string Ui(string zh)
        {
            return TranslateUiText(zh, IsEnglishUi());
        }

        private string StatusText(string zh)
        {
            return IsEnglishUi() ? "Status: " + TranslateUiText(zh, true) : "狀態：" + zh;
        }

        private void SetStatusText(string zh)
        {
            if (currentProtectCode != 0 && !zh.StartsWith("保護："))
            {
                zh = "保護：" + ProtectText(currentProtectCode);
            }
            stateLabel.Text = zh.StartsWith("保護：") ? TranslateUiText(zh, IsEnglishUi()) : StatusText(zh);
            bool alert = currentProtectCode != 0 || zh.StartsWith("保護：");
            stateLabel.BackColor = alert ? Color.FromArgb(185, 28, 28) : Color.FromArgb(35, 96, 73);
            stateLabel.ForeColor = Color.White;
        }

        private string ActivePresetText(string zh)
        {
            return IsEnglishUi() ? "Active Preset: " + TranslateUiText(zh, true) : "目前調用：" + zh;
        }

        private static void StyleIntervalButton(Button button, bool selected)
        {
            button.BackColor = selected ? Color.FromArgb(35, 96, 73) : SystemColors.Control;
            button.ForeColor = selected ? Color.White : Color.FromArgb(31, 38, 46);
        }

        private void UpdateCounters()
        {
            successLabel.Text = successCount.ToString(CultureInfo.InvariantCulture);
            failLabel.Text = failCount.ToString(CultureInfo.InvariantCulture);
            crcLabel.Text = crcErrorCount.ToString(CultureInfo.InvariantCulture);
            retryLabel.Text = retryCount.ToString(CultureInfo.InvariantCulture);
        }

        private void UpdateDeviceInfo(int? productModel, int? firmwareVersion)
        {
            productModelLabel.Text = productModel.HasValue ? productModel.Value.ToString(CultureInfo.InvariantCulture) : "--";
            firmwareVersionLabel.Text = firmwareVersion.HasValue ? firmwareVersion.Value.ToString(CultureInfo.InvariantCulture) : "--";
        }

        private void Log(string dir, string text)
        {
            if (InvokeRequired)
            {
                BeginInvoke((Action)(() => Log(dir, text)));
                return;
            }
            logBox.AppendText(DateTime.Now.ToString("HH:mm:ss.fff ") + dir.PadRight(3) + " " + text + Environment.NewLine);
            logLineCount++;
            TrimLogIfNeeded();
        }

        private void TrimLogIfNeeded()
        {
            if (logLineCount <= MaxLogLines + 200) return;
            int removeLines = logLineCount - MaxLogLines;
            int removeIndex = 0;
            for (int i = 0; i < removeLines; i++)
            {
                int next = logBox.Text.IndexOf('\n', removeIndex);
                if (next < 0)
                {
                    logBox.Clear();
                    logLineCount = 0;
                    return;
                }
                removeIndex = next + 1;
            }
            logBox.Select(0, removeIndex);
            logBox.SelectedText = "";
            logLineCount = MaxLogLines;
            logBox.SelectionStart = logBox.TextLength;
            logBox.ScrollToCaret();
        }

        private static string Hex(byte[] data)
        {
            return string.Join(" ", data.Select(b => b.ToString("X2")).ToArray());
        }

        private static Panel Box(string title)
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(0, 8, 8, 8) };
            panel.Paint += delegate(object sender, PaintEventArgs e)
            {
                var p = (Panel)sender;
                using (var pen = new Pen(Color.FromArgb(214, 220, 226)))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
                }
            };
            var label = new Label
            {
                Text = title,
                Font = new Font("Microsoft JhengHei UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(42, 48, 56),
                AutoSize = true,
                Location = new Point(12, 9)
            };
            panel.Controls.Add(label);
            return panel;
        }

        private static Label SectionText(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                Height = 22,
                ForeColor = Color.FromArgb(62, 70, 80),
                Font = new Font("Microsoft JhengHei UI", 9F, FontStyle.Bold)
            };
        }

        private static void AddRow(TableLayoutPanel flow, int row, string label, Control input)
        {
            var l = new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            input.Dock = DockStyle.Fill;
            flow.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            flow.Controls.Add(l, 0, row);
            flow.Controls.Add(input, 1, row);
        }

        private static void AddMetric(TableLayoutPanel flow, int row, string label, Label value)
        {
            var l = new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            value.Dock = DockStyle.Fill;
            value.TextAlign = ContentAlignment.MiddleRight;
            value.Font = new Font("Consolas", 11F, FontStyle.Bold);
            flow.Controls.Add(l, 0, row);
            flow.Controls.Add(value, 1, row);
        }
    }

    public sealed class CalibrationForm : Form
    {
        private enum CalibrationMode
        {
            None,
            Voltage,
            Current
        }

        private readonly MainForm owner;
        private readonly NumericUpDown voltage5Box = new NumericUpDown();
        private readonly NumericUpDown voltage25Box = new NumericUpDown();
        private readonly NumericUpDown current1Box = new NumericUpDown();
        private readonly NumericUpDown current5Box = new NumericUpDown();
        private readonly Panel stepPanel = new Panel();
        private readonly Label titleLabel = new Label();
        private readonly Label stepLabel = new Label();
        private readonly Label promptLabel = new Label();
        private readonly Label resultLabel = new Label();
        private readonly Button voltageModeButton = new Button();
        private readonly Button currentModeButton = new Button();
        private readonly Button confirmButton = new Button();
        private readonly Button closeButton = new Button();
        private CalibrationMode mode = CalibrationMode.None;
        private int step;

        public CalibrationForm(MainForm ownerForm)
        {
            owner = ownerForm;
            Text = T("SK150C 校準");
            Width = 590;
            Height = 560;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Microsoft JhengHei UI", 9F);
            BackColor = Color.White;
            BuildUi();
            RenderIdle();
        }

        private void BuildUi()
        {
            ConfigureNumber(voltage5Box, 0.50M, 40.00M, 5.00M, 2, "V");
            ConfigureNumber(voltage25Box, 0.50M, 40.00M, 25.00M, 2, "V");
            ConfigureNumber(current1Box, 0.001M, 8.000M, 1.000M, 3, "A");
            ConfigureNumber(current5Box, 0.001M, 8.000M, 5.000M, 3, "A");

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(14),
                ColumnCount = 1,
                RowCount = 5
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            Controls.Add(root);

            var warning = new Label
            {
                Dock = DockStyle.Fill,
                Text = T("請確認已接好高精度電壓表與電流表。校準會改寫從機校準資料；若為降壓型電源，請確認輸入電壓高於 28V。電流校準時，請將萬用表切到電流檔，表筆插入 10A/20A 孔位，依畫面提示輸入實測值。"),
                ForeColor = Color.FromArgb(185, 28, 28),
                Font = new Font("Microsoft JhengHei UI", 9F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            root.Controls.Add(warning, 0, 0);

            var modeRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(0, 8, 0, 8) };
            modeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            modeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            voltageModeButton.Text = T("校準電壓");
            currentModeButton.Text = T("校準電流");
            voltageModeButton.Dock = DockStyle.Fill;
            currentModeButton.Dock = DockStyle.Fill;
            voltageModeButton.Font = new Font("Microsoft JhengHei UI", 12F, FontStyle.Bold);
            currentModeButton.Font = new Font("Microsoft JhengHei UI", 12F, FontStyle.Bold);
            voltageModeButton.Click += delegate { StartVoltageCalibration(); };
            currentModeButton.Click += delegate { StartCurrentCalibration(); };
            modeRow.Controls.Add(voltageModeButton, 0, 0);
            modeRow.Controls.Add(currentModeButton, 1, 0);
            root.Controls.Add(modeRow, 0, 1);

            stepPanel.Dock = DockStyle.Fill;
            stepPanel.BackColor = Color.FromArgb(250, 251, 252);
            stepPanel.Padding = new Padding(16);
            stepPanel.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (var pen = new Pen(Color.FromArgb(214, 220, 226)))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, stepPanel.Width - 1, stepPanel.Height - 1);
                }
            };
            root.Controls.Add(stepPanel, 0, 2);

            var note = new Label
            {
                Dock = DockStyle.Fill,
                Text = T("請確認校準工具與接線正確。自行校準錯誤造成的問題，請自行負責。"),
                ForeColor = Color.FromArgb(92, 103, 115),
                TextAlign = ContentAlignment.MiddleLeft
            };
            root.Controls.Add(note, 0, 3);

            var bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 6, 0, 0) };
            closeButton.Text = T("關閉");
            closeButton.Width = 88;
            closeButton.Height = 30;
            closeButton.Click += delegate { Close(); };
            bottom.Controls.Add(closeButton);
            root.Controls.Add(bottom, 0, 4);
        }

        private void StartVoltageCalibration()
        {
            mode = CalibrationMode.Voltage;
            step = 0;
            owner.QueueCalibrationVoltageSetup(5.00M);
            RenderStep();
        }

        private void StartCurrentCalibration()
        {
            mode = CalibrationMode.Current;
            step = 0;
            owner.QueueCalibrationCurrentSetup(1.000M);
            RenderStep();
        }

        private void RenderIdle()
        {
            mode = CalibrationMode.None;
            step = 0;
            StyleModeButtons();
            stepPanel.Controls.Clear();

            var idle = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            idle.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            idle.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            idle.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            titleLabel.Text = T("請先選擇校準類別");
            titleLabel.Font = new Font("Microsoft JhengHei UI", 16F, FontStyle.Bold);
            titleLabel.ForeColor = Color.FromArgb(31, 38, 46);
            titleLabel.TextAlign = ContentAlignment.BottomCenter;
            titleLabel.Dock = DockStyle.Fill;
            var idleHint = new Label
            {
                Text = T("選擇後會顯示實測值輸入框，並立即送出第一段準備值。"),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(92, 103, 115)
            };
            idle.Controls.Add(titleLabel, 0, 0);
            idle.Controls.Add(idleHint, 0, 1);
            stepPanel.Controls.Add(idle);
        }

        private void RenderStep()
        {
            StyleModeButtons();
            stepPanel.Controls.Clear();

            NumericUpDown input = CurrentInput();
            string unit = Convert.ToString(input.Tag, CultureInfo.InvariantCulture);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

            titleLabel.Text = mode == CalibrationMode.Voltage ? T("校準電壓") : T("校準電流");
            titleLabel.Font = new Font("Microsoft JhengHei UI", 16F, FontStyle.Bold);
            titleLabel.ForeColor = mode == CalibrationMode.Voltage ? Color.FromArgb(36, 150, 96) : Color.FromArgb(184, 134, 11);
            titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            titleLabel.Dock = DockStyle.Fill;

            stepLabel.Text = StepTitle();
            stepLabel.Font = new Font("Microsoft JhengHei UI", 10F, FontStyle.Bold);
            stepLabel.ForeColor = Color.FromArgb(42, 48, 56);
            stepLabel.TextAlign = ContentAlignment.MiddleLeft;
            stepLabel.Dock = DockStyle.Fill;

            promptLabel.Text = StepPrompt();
            promptLabel.Font = new Font("Microsoft JhengHei UI", 10F);
            promptLabel.ForeColor = Color.FromArgb(62, 70, 80);
            promptLabel.TextAlign = ContentAlignment.MiddleLeft;
            promptLabel.Dock = DockStyle.Fill;

            var inputRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, Padding = new Padding(0, 10, 0, 10) };
            inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46));
            inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 172));
            var inputTitle = new Label
            {
                Text = T("實測值"),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Microsoft JhengHei UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(42, 48, 56)
            };
            input.Dock = DockStyle.Fill;
            input.MinimumSize = new Size(150, 42);
            input.BackColor = Color.White;
            input.ForeColor = mode == CalibrationMode.Voltage ? Color.FromArgb(36, 150, 96) : Color.FromArgb(184, 134, 11);
            var unitLabel = new Label { Text = unit, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Microsoft JhengHei UI", 13F, FontStyle.Bold) };
            confirmButton.Text = step == 0 ? T("確認並進入下一段") : T("確認完成校準");
            confirmButton.Dock = DockStyle.Fill;
            confirmButton.BackColor = Color.FromArgb(185, 28, 28);
            confirmButton.ForeColor = Color.White;
            confirmButton.Font = new Font("Microsoft JhengHei UI", 10F, FontStyle.Bold);
            confirmButton.Click -= ConfirmButtonClick;
            confirmButton.Click += ConfirmButtonClick;
            inputRow.Controls.Add(inputTitle, 0, 0);
            inputRow.Controls.Add(input, 1, 0);
            inputRow.Controls.Add(unitLabel, 2, 0);
            inputRow.Controls.Add(confirmButton, 3, 0);

            resultLabel.Dock = DockStyle.Fill;
            resultLabel.Text = T("目前已送出準備值，請等待從機輸出穩定後輸入外部表具讀值。");
            resultLabel.ForeColor = Color.FromArgb(92, 103, 115);
            resultLabel.TextAlign = ContentAlignment.MiddleLeft;

            layout.Controls.Add(titleLabel, 0, 0);
            layout.Controls.Add(stepLabel, 0, 1);
            layout.Controls.Add(promptLabel, 0, 2);
            layout.Controls.Add(inputRow, 0, 3);
            layout.Controls.Add(resultLabel, 0, 4);
            stepPanel.Controls.Add(layout);
            input.Focus();
            input.Select(0, input.Text.Length);
        }

        private void ConfirmButtonClick(object sender, EventArgs e)
        {
            ConfirmCurrentStep();
        }

        private void ConfirmCurrentStep()
        {
            if (mode == CalibrationMode.None) return;
            NumericUpDown input = CurrentInput();
            string label = StepLabelForWrite();
            int address = StepAddress();
            int scale = mode == CalibrationMode.Voltage ? 100 : 1000;
            string message = T(label) + " " + T("將寫入") + " 0x" + address.ToString("X4", CultureInfo.InvariantCulture) + T("。請確認外部表具量測值就是目前輸入值。");
            DialogResult result = MessageBox.Show(this, message, T("確認校準寫入"), MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (result != DialogResult.OK) return;

            if (mode == CalibrationMode.Voltage && step == 0)
            {
                owner.QueueCalibrationWriteThenVoltageSetup(address, input.Value, scale, label, 25.00M);
                step = 1;
                RenderStep();
                return;
            }

            if (mode == CalibrationMode.Current && step == 0)
            {
                owner.QueueCalibrationWriteThenCurrentSetup(address, input.Value, scale, label, 5.000M);
                step = 1;
                RenderStep();
                return;
            }

            resultLabel.ForeColor = Color.FromArgb(92, 103, 115);
            resultLabel.Text = T("最後一步已送出，正在讀取校準結果...");
            confirmButton.Enabled = false;
            owner.QueueCalibrationWriteAndReadStatus(address, input.Value, scale, label, delegate(int? status)
            {
                confirmButton.Enabled = true;
                ShowCalibrationResult(status);
            });
        }

        private void ShowCalibrationResult(int? status)
        {
            string text;
            MessageBoxIcon icon;
            if (status.HasValue && status.Value == 1)
            {
                text = T("校準成功");
                resultLabel.ForeColor = Color.FromArgb(36, 150, 96);
                icon = MessageBoxIcon.Information;
            }
            else if (status.HasValue && status.Value == 2)
            {
                text = T("校準失敗");
                resultLabel.ForeColor = Color.FromArgb(185, 28, 28);
                icon = MessageBoxIcon.Warning;
            }
            else if (status.HasValue && status.Value == -1)
            {
                text = T("校準寫入失敗，請檢查連線與從機狀態。");
                resultLabel.ForeColor = Color.FromArgb(185, 28, 28);
                icon = MessageBoxIcon.Warning;
            }
            else if (status.HasValue)
            {
                text = T("校準狀態：") + status.Value.ToString(CultureInfo.InvariantCulture);
                resultLabel.ForeColor = Color.FromArgb(184, 134, 11);
                icon = MessageBoxIcon.Information;
            }
            else
            {
                text = T("校準狀態讀取失敗，請觀察從機顯示。");
                resultLabel.ForeColor = Color.FromArgb(185, 28, 28);
                icon = MessageBoxIcon.Warning;
            }

            resultLabel.Text = text;
            MessageBox.Show(this, text, T("校準結果"), MessageBoxButtons.OK, icon);
        }

        private NumericUpDown CurrentInput()
        {
            if (mode == CalibrationMode.Voltage) return step == 0 ? voltage5Box : voltage25Box;
            return step == 0 ? current1Box : current5Box;
        }

        private string StepTitle()
        {
            if (mode == CalibrationMode.Voltage) return step == 0 ? T("步驟 1/2：5V 實測輸入") : T("步驟 2/2：25V 實測輸入");
            return step == 0 ? T("步驟 1/2：1A 實測輸入") : T("步驟 2/2：5A 實測輸入");
        }

        private string StepPrompt()
        {
            if (mode == CalibrationMode.Voltage) return T("請輸入目前實測輸出電壓。");
            return T("請輸入目前實測輸出電流。");
        }

        private string StepLabelForWrite()
        {
            if (mode == CalibrationMode.Voltage) return step == 0 ? "5V 電壓校準" : "25V 電壓校準";
            return step == 0 ? "1A 電流校準" : "5A 電流校準";
        }

        private string T(string zh)
        {
            return owner.Ui(zh);
        }

        private int StepAddress()
        {
            if (mode == CalibrationMode.Voltage) return step == 0 ? 0x0023 : 0x0024;
            return step == 0 ? 0x0026 : 0x0027;
        }

        private void StyleModeButtons()
        {
            StyleModeButton(voltageModeButton, mode == CalibrationMode.Voltage, Color.FromArgb(36, 150, 96));
            StyleModeButton(currentModeButton, mode == CalibrationMode.Current, Color.FromArgb(184, 134, 11));
        }

        private static void StyleModeButton(Button button, bool active, Color color)
        {
            button.BackColor = active ? color : SystemColors.Control;
            button.ForeColor = active ? Color.White : Color.FromArgb(31, 38, 46);
        }

        private static void ConfigureNumber(NumericUpDown box, decimal min, decimal max, decimal value, int decimals, string unit)
        {
            box.Minimum = min;
            box.Maximum = max;
            box.Value = value;
            box.DecimalPlaces = decimals;
            box.Increment = decimals == 3 ? 0.001M : 0.01M;
            box.TextAlign = HorizontalAlignment.Right;
            box.Font = new Font("Consolas", 20F, FontStyle.Bold);
            box.BorderStyle = BorderStyle.FixedSingle;
            box.Tag = unit;
        }
    }

    public sealed class ValueTile : Panel
    {
        private readonly Label titleLabel = new Label();
        private readonly Label value = new Label();
        private readonly string unit;
        private readonly float baseValueFontSize;

        public ValueTile(string title, string unitText)
            : this(title, unitText, 24F)
        {
        }

        public ValueTile(string title, string unitText, float valueFontSize)
        {
            unit = unitText;
            baseValueFontSize = valueFontSize;
            Dock = DockStyle.Fill;
            Margin = new Padding(0, 0, 8, 8);
            BackColor = Color.FromArgb(250, 251, 252);

            titleLabel.Text = title;
            titleLabel.ForeColor = Color.FromArgb(92, 103, 115);
            titleLabel.AutoSize = false;
            titleLabel.Location = new Point(10, 7);
            titleLabel.Height = 20;
            titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            value.Text = "--";
            value.ForeColor = Color.FromArgb(31, 38, 46);
            value.Font = new Font("Segoe UI", valueFontSize, FontStyle.Bold);
            value.AutoSize = false;
            value.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(titleLabel);
            Controls.Add(value);
            Resize += delegate { LayoutLabels(); };
            LayoutLabels();
        }

        public void SetValue(double number, string format)
        {
            value.Text = number.ToString(format, CultureInfo.InvariantCulture) + (unit.Length == 0 ? "" : " " + unit);
            FitValueFont();
        }

        public void SetText(string text)
        {
            value.Text = text;
            FitValueFont();
        }

        private void LayoutLabels()
        {
            titleLabel.SetBounds(10, 6, Math.Max(10, Width - 20), 20);
            value.SetBounds(10, 28, Math.Max(10, Width - 20), Math.Max(18, Height - 34));
            FitValueFont();
        }

        private void FitValueFont()
        {
            if (Width <= 0 || Height <= 0) return;
            float size = baseValueFontSize;
            using (Graphics g = CreateGraphics())
            {
                while (size > 13F)
                {
                    using (var testFont = new Font("Segoe UI", size, FontStyle.Bold))
                    {
                        SizeF measured = g.MeasureString(value.Text, testFont);
                        if (measured.Width <= value.Width + 2 && measured.Height <= value.Height + 4) break;
                    }
                    size -= 1F;
                }
            }
            value.Font = new Font("Segoe UI", size, FontStyle.Bold);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Color.FromArgb(222, 228, 234)))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }
    }

    public sealed class InlineValuePanel : Panel
    {
        private readonly Label titleLabel = new Label();
        private readonly Label valueLabel = new Label();
        private readonly string unit;
        private readonly Color accent;
        private readonly float valueFontSize;
        private readonly List<PowerTrendPoint> trend = new List<PowerTrendPoint>();
        private static readonly TimeSpan RawWindow = TimeSpan.FromHours(1);
        private Color baseBackColor = Color.FromArgb(250, 251, 252);
        private bool showTrend;
        private bool alert;

        public bool ShowTrend
        {
            get { return showTrend; }
            set
            {
                showTrend = value;
                LayoutLabels();
                Invalidate();
            }
        }

        public InlineValuePanel(string title, string unitText, Color color, float fontSize)
        {
            unit = unitText;
            accent = color;
            valueFontSize = fontSize;
            Dock = DockStyle.Fill;
            Margin = new Padding(0, 0, 8, 8);
            BackColor = baseBackColor;

            titleLabel.Text = title;
            titleLabel.ForeColor = Color.FromArgb(92, 103, 115);
            titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            titleLabel.AutoSize = false;
            titleLabel.Font = new Font("Microsoft JhengHei UI", 9F, FontStyle.Bold);

            valueLabel.Text = "--";
            valueLabel.ForeColor = accent;
            valueLabel.TextAlign = ContentAlignment.MiddleLeft;
            valueLabel.AutoSize = false;
            valueLabel.Font = new Font("Segoe UI", valueFontSize, FontStyle.Bold);

            Controls.Add(titleLabel);
            Controls.Add(valueLabel);
            Resize += delegate { LayoutLabels(); };
            LayoutLabels();
        }

        public void SetValue(double number, string format)
        {
            string next = number.ToString(format, CultureInfo.InvariantCulture) + (unit.Length == 0 ? "" : " " + unit);
            if (valueLabel.Text == next) return;
            valueLabel.Text = next;
            FitValueFont();
        }

        public void SetText(string text)
        {
            if (valueLabel.Text == text) return;
            valueLabel.Text = text;
            FitValueFont();
        }

        public void SetBaseBackColor(Color color)
        {
            baseBackColor = color;
            if (!alert) BackColor = baseBackColor;
            Invalidate();
        }

        public void SetAlert(bool value)
        {
            alert = value;
            BackColor = alert ? Color.FromArgb(185, 28, 28) : baseBackColor;
            titleLabel.ForeColor = alert ? Color.White : Color.FromArgb(92, 103, 115);
            valueLabel.ForeColor = alert ? Color.White : accent;
            Invalidate();
        }

        public void AddTrendPoint(double value)
        {
            if (!ShowTrend) return;
            DateTime now = DateTime.Now;
            trend.Add(new PowerTrendPoint { Time = now, Value = value });
            DateTime cutoff = now - RawWindow;
            while (trend.Count > 0 && trend[0].Time < cutoff) trend.RemoveAt(0);
            Invalidate();
        }

        private void LayoutLabels()
        {
            int titleWidth = Math.Min(82, Math.Max(58, Width / 2));
            if (ShowTrend)
            {
                titleLabel.SetBounds(10, 6, titleWidth, 28);
                valueLabel.SetBounds(titleWidth + 12, 4, Math.Max(20, Width - titleWidth - 20), Math.Max(32, Height / 2 - 6));
            }
            else
            {
                titleLabel.SetBounds(10, 5, titleWidth, Math.Max(22, Height - 10));
                valueLabel.SetBounds(titleWidth + 12, 4, Math.Max(20, Width - titleWidth - 20), Math.Max(22, Height - 8));
            }
            FitValueFont();
        }

        private void FitValueFont()
        {
            if (Width <= 0 || Height <= 0) return;
            float size = valueFontSize;
            using (Graphics g = CreateGraphics())
            {
                while (size > 11F)
                {
                    using (var testFont = new Font("Segoe UI", size, FontStyle.Bold))
                    {
                        SizeF measured = g.MeasureString(valueLabel.Text, testFont);
                        if (measured.Width <= valueLabel.Width + 2 && measured.Height <= valueLabel.Height + 4) break;
                    }
                    size -= 1F;
                }
            }
            valueLabel.Font = new Font("Segoe UI", size, FontStyle.Bold);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Color.FromArgb(222, 228, 234)))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
            using (var pen = new Pen(alert ? Color.White : accent, 3F))
            {
                e.Graphics.DrawLine(pen, 1, 3, 1, Height - 4);
            }
            if (ShowTrend) DrawMiniTrend(e.Graphics);
        }

        private void DrawMiniTrend(Graphics g)
        {
            if (trend.Count < 2) return;
            var rect = new Rectangle(12, Math.Max(42, Height / 2), Math.Max(10, Width - 24), Math.Max(24, Height - Math.Max(46, Height / 2) - 20));
            DateTime now = DateTime.Now;
            TimeSpan displayWindow = GetDisplayWindow(now);
            DateTime windowStart = now - displayWindow;
            double maxValue = 0;
            bool hasValue = false;
            for (int i = 0; i < trend.Count; i++)
            {
                if (trend[i].Time < windowStart || trend[i].Time > now) continue;
                hasValue = true;
                if (trend[i].Value > maxValue) maxValue = trend[i].Value;
            }
            if (!hasValue) return;

            double min = 0;
            double max = NicePowerMax(maxValue);
            double totalSeconds = Math.Max(1.0, (now - windowStart).TotalSeconds);
            int step = Math.Max(1, trend.Count / Math.Max(1, rect.Width * 2));
            var points = new List<PointF>();

            for (int i = 0; i < trend.Count; i += step)
            {
                PowerTrendPoint point = trend[i];
                if (point.Time < windowStart || point.Time > now) continue;
                double age = (now - point.Time).TotalSeconds;
                float x = rect.Left + (float)(age / totalSeconds * rect.Width);
                double normalized = (point.Value - min) / (max - min);
                normalized = Math.Max(0, Math.Min(1, normalized));
                float y = rect.Bottom - (float)(normalized * rect.Height);
                points.Add(new PointF(x, y));
            }
            if (points.Count < 2) return;

            using (var gridPen = new Pen(Color.FromArgb(235, 218, 218)))
            using (var trendPen = new Pen(Color.FromArgb(196, 68, 62), 2F))
            using (var brush = new SolidBrush(Color.FromArgb(145, 84, 84)))
            {
                g.DrawLine(gridPen, rect.Left, rect.Bottom, rect.Right, rect.Bottom);
                for (int i = 0; i <= 4; i++)
                {
                    int x = rect.Left + rect.Width * i / 4;
                    g.DrawLine(gridPen, x, rect.Top, x, rect.Bottom);
                    string label = FormatTimeLabel(i, displayWindow);
                    using (var font = new Font("Segoe UI", 7F, FontStyle.Regular))
                    {
                        SizeF size = g.MeasureString(label, font);
                        g.DrawString(label, font, brush, x - size.Width / 2, rect.Bottom + 3);
                    }
                }
                g.DrawLines(trendPen, points.ToArray());
                string range = "W 0~" + max.ToString("0", CultureInfo.InvariantCulture);
                using (var font = new Font("Segoe UI", 7.5F, FontStyle.Bold))
                {
                    g.DrawString(range, font, brush, rect.Left, rect.Top - 14);
                }
            }
        }

        private TimeSpan GetDisplayWindow(DateTime now)
        {
            if (trend.Count == 0) return TimeSpan.FromSeconds(60);
            TimeSpan elapsed = now - trend[0].Time;
            if (elapsed <= TimeSpan.Zero) return TimeSpan.FromSeconds(10);
            double targetSeconds = elapsed.TotalSeconds * 1.08;
            targetSeconds = Math.Max(10.0, Math.Min(RawWindow.TotalSeconds, targetSeconds));
            return TimeSpan.FromSeconds(targetSeconds);
        }

        private static double NicePowerMax(double maxValue)
        {
            double padded = Math.Max(0.5, maxValue * 1.12);
            double[] limits = new double[] { 5, 10, 20, 50, 100, 150 };
            for (int i = 0; i < limits.Length; i++)
            {
                if (padded <= limits[i]) return limits[i];
            }
            return 150;
        }

        private static string FormatTimeLabel(int index, TimeSpan displayWindow)
        {
            if (index <= 0) return "now";
            double seconds = displayWindow.TotalSeconds * index / 4.0;
            return "-" + FormatDuration(seconds);
        }

        private static string FormatDuration(double seconds)
        {
            if (seconds < 90)
            {
                return Math.Max(1, (int)Math.Round(seconds)).ToString(CultureInfo.InvariantCulture) + "s";
            }
            double minutes = seconds / 60.0;
            if (minutes < 10)
            {
                double rounded = Math.Round(minutes, 1);
                if (Math.Abs(rounded - Math.Round(rounded)) < 0.05)
                    return Math.Round(rounded).ToString("0", CultureInfo.InvariantCulture) + "m";
                return rounded.ToString("0.0", CultureInfo.InvariantCulture) + "m";
            }
            return Math.Round(minutes).ToString("0", CultureInfo.InvariantCulture) + "m";
        }

        private sealed class PowerTrendPoint
        {
            public DateTime Time;
            public double Value;
        }
    }

    public sealed class QuickGroupButton : Control
    {
        private static readonly Color VoltageColor = Color.FromArgb(36, 150, 96);
        private static readonly Color CurrentColor = Color.FromArgb(184, 134, 11);
        private int group;
        private string voltageText = "--V";
        private string currentText = "--A";
        private bool hovering;
        private bool pressed;
        private bool isActive;
        private bool hasSiniWarning;
        private bool hasCurrentCutoff;
        private bool hasVoltageCutoff;

        public QuickGroupButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            Cursor = Cursors.Hand;
            BackColor = Color.FromArgb(248, 250, 252);
            ForeColor = Color.FromArgb(31, 38, 46);
            Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        }

        public void SetGroup(int value)
        {
            group = value;
            Invalidate();
        }

        public void SetValues(string voltage, string current)
        {
            voltageText = voltage;
            currentText = current;
            Invalidate();
        }

        public bool IsActive
        {
            get { return isActive; }
            set
            {
                if (isActive == value) return;
                isActive = value;
                Invalidate();
            }
        }

        public bool HasSiniWarning
        {
            get { return hasSiniWarning; }
            set
            {
                if (hasSiniWarning == value) return;
                hasSiniWarning = value;
                Invalidate();
            }
        }

        public bool HasCurrentCutoff
        {
            get { return hasCurrentCutoff; }
            set
            {
                if (hasCurrentCutoff == value) return;
                hasCurrentCutoff = value;
                Invalidate();
            }
        }

        public bool HasVoltageCutoff
        {
            get { return hasVoltageCutoff; }
            set
            {
                if (hasVoltageCutoff == value) return;
                hasVoltageCutoff = value;
                Invalidate();
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hovering = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hovering = false;
            pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                pressed = true;
                Invalidate();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            pressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill;
            Color border;
            Color groupColor;
            Color voltageColor;
            Color currentColor;
            if (isActive)
            {
                fill = pressed ? Color.FromArgb(27, 95, 70) : hovering ? Color.FromArgb(33, 112, 82) : Color.FromArgb(35, 105, 77);
                border = Color.FromArgb(24, 83, 62);
                groupColor = Color.White;
                voltageColor = Color.FromArgb(204, 255, 226);
                currentColor = Color.FromArgb(255, 239, 170);
            }
            else
            {
                fill = pressed ? Color.FromArgb(226, 232, 240) : hovering ? Color.FromArgb(239, 246, 255) : BackColor;
                border = hovering ? Color.FromArgb(66, 153, 225) : Color.FromArgb(184, 194, 204);
                groupColor = Color.FromArgb(24, 30, 38);
                voltageColor = VoltageColor;
                currentColor = CurrentColor;
            }
            using (var brush = new SolidBrush(fill))
            using (var borderPen = new Pen(border))
            {
                e.Graphics.FillRectangle(brush, rect);
                e.Graphics.DrawRectangle(borderPen, rect);
            }

            Rectangle leftRect = new Rectangle(5, 0, Math.Max(28, Width / 2 - 6), Height);
            Rectangle rightTop = new Rectangle(Math.Max(38, Width / 2 - 4), 1, Math.Max(20, Width - Math.Max(38, Width / 2 - 4) - 5), Height / 2);
            Rectangle rightBottom = new Rectangle(rightTop.Left, Height / 2 - 1, rightTop.Width, Height / 2);
            DrawStatusDot(e.Graphics, hasSiniWarning, Color.FromArgb(220, 38, 38), Color.FromArgb(153, 27, 27), rightTop.Top + Math.Max(2, (rightTop.Height / 2) - 3));
            DrawStatusDot(e.Graphics, hasCurrentCutoff, Color.FromArgb(184, 134, 11), Color.FromArgb(146, 101, 8), leftRect.Top + Math.Max(2, (leftRect.Height / 2) - 3));
            DrawStatusDot(e.Graphics, hasVoltageCutoff, Color.FromArgb(36, 150, 96), Color.FromArgb(23, 78, 57), rightBottom.Top + Math.Max(2, (rightBottom.Height / 2) - 3));

            using (var groupFont = FitFont(e.Graphics, "M" + group.ToString(CultureInfo.InvariantCulture), 12F, FontStyle.Bold, leftRect.Size))
            using (var valueFont = FitFont(e.Graphics, voltageText.Length >= currentText.Length ? voltageText : currentText, 10F, FontStyle.Bold, rightTop.Size))
            using (var groupBrush = new SolidBrush(groupColor))
            using (var voltageBrush = new SolidBrush(voltageColor))
            using (var currentBrush = new SolidBrush(currentColor))
            {
                DrawText(e.Graphics, "M" + group.ToString(CultureInfo.InvariantCulture), groupFont, groupBrush, leftRect, ContentAlignment.MiddleCenter);
                DrawText(e.Graphics, voltageText, valueFont, voltageBrush, rightTop, ContentAlignment.MiddleRight);
                DrawText(e.Graphics, currentText, valueFont, currentBrush, rightBottom, ContentAlignment.MiddleRight);
            }
        }

        private static void DrawStatusDot(Graphics g, bool visible, Color fillColor, Color borderColor, int y)
        {
            if (!visible) return;
            Rectangle dot = new Rectangle(4, y, 5, 5);
            using (var brush = new SolidBrush(fillColor))
            using (var pen = new Pen(borderColor))
            {
                g.FillEllipse(brush, dot);
                g.DrawEllipse(pen, dot);
            }
        }

        private static Font FitFont(Graphics g, string text, float startSize, FontStyle style, Size bounds)
        {
            float size = startSize;
            while (size > 7F)
            {
                var font = new Font("Segoe UI", size, style);
                SizeF measured = g.MeasureString(text, font);
                if (measured.Width <= bounds.Width + 2 && measured.Height <= bounds.Height + 4) return font;
                font.Dispose();
                size -= 0.5F;
            }
            return new Font("Segoe UI", size, style);
        }

        private static void DrawText(Graphics g, string text, Font font, Brush brush, Rectangle rect, ContentAlignment alignment)
        {
            using (var format = new StringFormat())
            {
                format.LineAlignment = StringAlignment.Center;
                format.Alignment = alignment == ContentAlignment.MiddleRight ? StringAlignment.Far : StringAlignment.Center;
                g.DrawString(text, font, brush, rect, format);
            }
        }
    }

    public sealed class StepEditor : Panel
    {
        private readonly TextBox textBox = new TextBox();
        private readonly TableLayoutPanel layout;
        private readonly List<Button> stepButtons = new List<Button>();
        private readonly decimal minimum;
        private readonly decimal maximum;
        private readonly int decimalPlaces;
        private readonly string unit;
        private readonly Color accent;
        private decimal currentValue;
        private int state;
        private bool suppressTextChanged;
        private bool quickSelectOnFocus;

        public event EventHandler UserChanged;
        public event EventHandler EnterPressed;

        public StepEditor(decimal min, decimal max, decimal initial, int decimals, string unitText, decimal largeStep, decimal smallStep, Color color)
        {
            minimum = min;
            maximum = max;
            decimalPlaces = decimals;
            unit = unitText;
            accent = color;
            Dock = DockStyle.Fill;
            Height = 38;
            Margin = new Padding(0, 0, 0, 6);

            layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 1 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 22));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
            Controls.Add(layout);

            AddStepButton(layout, 0, "-" + FormatStep(largeStep), -largeStep);
            AddStepButton(layout, 1, "-" + FormatStep(smallStep), -smallStep);

            textBox.Dock = DockStyle.Fill;
            textBox.TextAlign = HorizontalAlignment.Right;
            textBox.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            textBox.ForeColor = accent;
            textBox.TextChanged += delegate
            {
                if (!suppressTextChanged && textBox.Focused && TextDiffersFromCurrentValue()) MarkDirty();
            };
            textBox.Enter += delegate
            {
                if (quickSelectOnFocus) textBox.BeginInvoke((Action)(() => textBox.SelectAll()));
            };
            textBox.MouseUp += delegate
            {
                if (quickSelectOnFocus) textBox.SelectAll();
            };
            textBox.Leave += delegate { CommitText(true); };
            textBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    CommitText(true);
                    if (EnterPressed != null) EnterPressed(this, EventArgs.Empty);
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.Up)
                {
                    Add(smallStep);
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.Down)
                {
                    Add(-smallStep);
                    e.SuppressKeyPress = true;
                }
            };
            layout.Controls.Add(textBox, 2, 0);

            var unitLabel = new Label { Text = unit, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = accent, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            layout.Controls.Add(unitLabel, 3, 0);

            AddStepButton(layout, 4, "+" + FormatStep(smallStep), smallStep);
            AddStepButton(layout, 5, "+" + FormatStep(largeStep), largeStep);
            SetValue((double)initial);
        }

        public void UseCompactInput()
        {
            if (layout.ColumnStyles.Count < 6) return;
            layout.ColumnStyles[0].Width = 0;
            layout.ColumnStyles[1].Width = 0;
            layout.ColumnStyles[4].Width = 0;
            layout.ColumnStyles[5].Width = 0;
            for (int i = 0; i < stepButtons.Count; i++) stepButtons[i].Visible = false;
            Height = 28;
            textBox.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        }

        public decimal Value
        {
            get
            {
                CommitText(false);
                return currentValue;
            }
        }

        public bool QuickSelectOnFocus
        {
            get { return quickSelectOnFocus; }
            set { quickSelectOnFocus = value; }
        }

        public bool IsDirty
        {
            get { return state != 0; }
        }

        public bool IsWriting
        {
            get { return state == 2; }
        }

        public void SetDirty(bool value)
        {
            int next = value ? 1 : 0;
            if (state == next)
            {
                ApplyDirtyStyle();
                return;
            }
            state = next;
            ApplyDirtyStyle();
            if (UserChanged != null) UserChanged(this, EventArgs.Empty);
        }

        public void SetWriting(bool value)
        {
            int next = value ? 2 : 0;
            if (state == next)
            {
                ApplyDirtyStyle();
                return;
            }
            state = next;
            ApplyDirtyStyle();
            if (UserChanged != null) UserChanged(this, EventArgs.Empty);
        }

        public void SetValue(double value)
        {
            SetValue((decimal)value, false);
        }

        private void SetValue(decimal value, bool userChange)
        {
            if (value < minimum) value = minimum;
            if (value > maximum) value = maximum;
            decimal rounded = decimal.Round(value, decimalPlaces);
            bool changed = rounded != currentValue;
            currentValue = rounded;
            suppressTextChanged = true;
            try
            {
                textBox.Text = currentValue.ToString(FormatValue(), CultureInfo.InvariantCulture);
            }
            finally
            {
                suppressTextChanged = false;
            }
            if (userChange && changed) MarkDirty();
            else SetDirty(false);
        }

        private void Add(decimal delta)
        {
            CommitText(false);
            SetValue(currentValue + delta, true);
        }

        private void CommitText(bool userChange)
        {
            decimal parsed;
            if (decimal.TryParse(textBox.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out parsed) ||
                decimal.TryParse(textBox.Text.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out parsed))
            {
                SetValue(parsed, userChange);
            }
            else
            {
                SetValue(currentValue, userChange);
            }
        }

        private bool TextDiffersFromCurrentValue()
        {
            decimal parsed;
            if (!decimal.TryParse(textBox.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out parsed) &&
                !decimal.TryParse(textBox.Text.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out parsed))
                return true;
            if (parsed < minimum) parsed = minimum;
            if (parsed > maximum) parsed = maximum;
            parsed = decimal.Round(parsed, decimalPlaces);
            return parsed != currentValue;
        }

        private void MarkDirty()
        {
            SetDirty(true);
        }

        private void ApplyDirtyStyle()
        {
            if (state == 2)
            {
                BackColor = Color.FromArgb(255, 205, 210);
                textBox.BackColor = Color.FromArgb(255, 235, 238);
            }
            else if (state == 1)
            {
                BackColor = Color.FromArgb(255, 242, 153);
                textBox.BackColor = Color.FromArgb(255, 250, 205);
            }
            else
            {
                BackColor = SystemColors.Control;
                textBox.BackColor = SystemColors.Window;
            }
        }

        private void AddStepButton(TableLayoutPanel layout, int column, string text, decimal delta)
        {
            var button = new Button
            {
                Text = text,
                Dock = DockStyle.Fill,
                Margin = new Padding(1),
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold)
            };
            button.Click += delegate { Add(delta); };
            stepButtons.Add(button);
            layout.Controls.Add(button, column, 0);
        }

        private string FormatValue()
        {
            return "0." + new string('0', decimalPlaces);
        }

        private string FormatStep(decimal value)
        {
            string text = value.ToString(FormatValue(), CultureInfo.InvariantCulture);
            if (text.IndexOf('.') >= 0) text = text.TrimEnd('0').TrimEnd('.');
            return text;
        }
    }

    public sealed class TrendChart : Control
    {
        private static readonly Color VoltageColor = Color.FromArgb(36, 150, 96);
        private static readonly Color CurrentColor = Color.FromArgb(184, 134, 11);
        private static readonly TimeSpan RawWindow = TimeSpan.FromHours(1);
        private static readonly TimeSpan CompressedWindow = TimeSpan.FromDays(3);
        private readonly List<RawPoint> rawPoints = new List<RawPoint>();
        private readonly List<MinutePoint> minuteHistory = new List<MinutePoint>();
        private MinuteAggregate currentMinute;
        private double targetVoltage;
        private double targetCurrent;

        public TrendChart()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(250, 251, 252);
            DoubleBuffered = true;
        }

        public void AddPoint(double v, double i, double p)
        {
            DateTime now = DateTime.Now;
            rawPoints.Add(new RawPoint { Time = now, Voltage = v, Current = i, Power = p });
            PruneRaw(now);
            PruneCompressed(now);
            Invalidate();
        }

        public void SetTargets(double voltage, double current)
        {
            targetVoltage = Math.Max(0, voltage);
            targetCurrent = Math.Max(0, current);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var rect = new Rectangle(64, 38, Width - 128, Height - 86);
            if (rect.Width < 20 || rect.Height < 20) return;

            DateTime now = DateTime.Now;
            TimeSpan displayWindow = GetDisplayWindow(now);
            DateTime windowStart = now - displayWindow;
            double vMin, vMax, iMin, iMax;
            GetRange(rawPoints, windowStart, true, targetVoltage, 1.0, out vMin, out vMax);
            GetRange(rawPoints, windowStart, false, targetCurrent, 0.5, out iMin, out iMax);

            using (var pen = new Pen(Color.FromArgb(218, 225, 232)))
            using (var textBrush = new SolidBrush(Color.FromArgb(92, 103, 115)))
            using (var titleFont = new Font("Microsoft JhengHei UI", 10F, FontStyle.Bold))
            {
                e.Graphics.DrawRectangle(pen, rect);
                for (int i = 0; i <= 5; i++)
                {
                    int y = rect.Top + rect.Height * i / 5;
                    e.Graphics.DrawLine(pen, rect.Left, y, rect.Right, y);
                    double leftValue = iMax - (iMax - iMin) * i / 5.0;
                    double rightValue = vMax - (vMax - vMin) * i / 5.0;
                    string left = leftValue.ToString("0.000", CultureInfo.InvariantCulture);
                    string right = rightValue.ToString("0.00", CultureInfo.InvariantCulture);
                    SizeF leftSize = e.Graphics.MeasureString(left, Font);
                    e.Graphics.DrawString(left, Font, textBrush, rect.Left - leftSize.Width - 8, y - leftSize.Height / 2);
                    e.Graphics.DrawString(right, Font, textBrush, rect.Right + 8, y - leftSize.Height / 2);
                }
                for (int i = 0; i <= 4; i++)
                {
                    int x = rect.Left + rect.Width * i / 4;
                    e.Graphics.DrawLine(pen, x, rect.Top, x, rect.Bottom);
                    e.Graphics.DrawLine(pen, x, rect.Bottom, x, rect.Bottom + 4);
                    string label = FormatTimeLabel(i, displayWindow);
                    SizeF size = e.Graphics.MeasureString(label, Font);
                    e.Graphics.DrawString(label, Font, textBrush, x - size.Width / 2, rect.Bottom + 8);
                }
                e.Graphics.DrawString("歷史曲線", titleFont, textBrush, 12, 8);
                e.Graphics.DrawString("左軸: A", Font, textBrush, rect.Left, 16);
                e.Graphics.DrawString("右軸: V", Font, textBrush, rect.Right - 48, 16);
                using (var vBrush = new SolidBrush(VoltageColor))
                using (var iBrush = new SolidBrush(CurrentColor))
                {
                    e.Graphics.DrawString("IOUT", Font, iBrush, rect.Left, Height - 30);
                    e.Graphics.DrawString("VOUT", Font, vBrush, rect.Left + 70, Height - 30);
                    e.Graphics.DrawString("A " + iMin.ToString("0.000", CultureInfo.InvariantCulture) + "~" + iMax.ToString("0.000", CultureInfo.InvariantCulture), Font, iBrush, rect.Left + 140, Height - 30);
                    e.Graphics.DrawString("V " + vMin.ToString("0.00", CultureInfo.InvariantCulture) + "~" + vMax.ToString("0.00", CultureInfo.InvariantCulture), Font, vBrush, rect.Left + 300, Height - 30);
                }
            }

            PointF? vHead = DrawSeries(e.Graphics, rect, rawPoints, windowStart, now, vMin, vMax, VoltageColor, true);
            PointF? iHead = DrawSeries(e.Graphics, rect, rawPoints, windowStart, now, iMin, iMax, CurrentColor, false);
            DrawHeadLabels(e.Graphics, rect, vHead, iHead);
        }

        private TimeSpan GetDisplayWindow(DateTime now)
        {
            if (rawPoints.Count == 0) return TimeSpan.FromSeconds(60);
            TimeSpan elapsed = now - rawPoints[0].Time;
            if (elapsed <= TimeSpan.Zero) return TimeSpan.FromSeconds(10);
            double targetSeconds = elapsed.TotalSeconds * 1.08;
            targetSeconds = Math.Max(10.0, Math.Min(RawWindow.TotalSeconds, targetSeconds));
            return TimeSpan.FromSeconds(targetSeconds);
        }

        private static string FormatTimeLabel(int index, TimeSpan displayWindow)
        {
            if (index <= 0) return "now";
            double seconds = displayWindow.TotalSeconds * index / 4.0;
            return "-" + FormatDuration(seconds);
        }

        private static string FormatDuration(double seconds)
        {
            if (seconds < 90)
            {
                return Math.Max(1, (int)Math.Round(seconds)).ToString(CultureInfo.InvariantCulture) + "s";
            }
            double minutes = seconds / 60.0;
            if (minutes < 10)
            {
                double rounded = Math.Round(minutes, 1);
                if (Math.Abs(rounded - Math.Round(rounded)) < 0.05)
                    return Math.Round(rounded).ToString("0", CultureInfo.InvariantCulture) + "m";
                return rounded.ToString("0.0", CultureInfo.InvariantCulture) + "m";
            }
            return Math.Round(minutes).ToString("0", CultureInfo.InvariantCulture) + "m";
        }

        private static void GetRange(List<RawPoint> data, DateTime windowStart, bool voltage, double target, double minimumSpan, out double min, out double max)
        {
            bool hasValue = false;
            min = 0;
            max = target > 0 ? target : minimumSpan;
            for (int i = 0; i < data.Count; i++)
            {
                if (data[i].Time < windowStart) continue;
                double value = voltage ? data[i].Voltage : data[i].Current;
                if (!hasValue && target <= 0)
                {
                    max = value;
                }
                hasValue = true;
                if (target <= 0)
                {
                    if (value > max) max = value;
                }
                else if (value > max)
                {
                    max = value * 1.05;
                }
            }
            if (!hasValue)
            {
                min = 0;
                max = target > 0 ? target : minimumSpan;
                return;
            }
            min = 0;
            double span = max - min;
            if (span < minimumSpan)
            {
                max = minimumSpan;
            }
        }

        private static PointF? DrawSeries(Graphics g, Rectangle rect, List<RawPoint> data, DateTime windowStart, DateTime windowEnd, double min, double max, Color color, bool voltage)
        {
            if (data.Count < 2) return null;
            double totalSeconds = Math.Max(1.0, (windowEnd - windowStart).TotalSeconds);
            int step = Math.Max(1, data.Count / Math.Max(1, rect.Width * 2));
            var points = new List<PointF>();
            PointF? newest = null;
            DateTime newestTime = DateTime.MinValue;
            for (int i = 0; i < data.Count; i += step)
            {
                RawPoint point = data[i];
                if (point.Time < windowStart || point.Time > windowEnd) continue;
                double age = (windowEnd - point.Time).TotalSeconds;
                float x = rect.Left + (float)(age / totalSeconds * rect.Width);
                double value = voltage ? point.Voltage : point.Current;
                double normalized = (value - min) / (max - min);
                normalized = Math.Max(0, Math.Min(1, normalized));
                float y = rect.Bottom - (float)(normalized * rect.Height);
                var plotted = new PointF(x, y);
                points.Add(plotted);
                if (point.Time > newestTime)
                {
                    newestTime = point.Time;
                    newest = plotted;
                }
            }
            if (points.Count < 2) return newest;
            using (var pen = new Pen(color, 2F))
            {
                g.DrawLines(pen, points.ToArray());
            }
            return newest;
        }

        private void DrawHeadLabels(Graphics g, Rectangle rect, PointF? voltageHead, PointF? currentHead)
        {
            if (rawPoints.Count == 0) return;
            RawPoint latest = rawPoints[rawPoints.Count - 1];
            using (var labelFont = new Font("Segoe UI", 20F, FontStyle.Bold))
            {
                RectangleF? vBox = null;
                RectangleF? iBox = null;
                if (voltageHead.HasValue)
                {
                    string text = "V " + latest.Voltage.ToString("0.00", CultureInfo.InvariantCulture) + "V";
                    vBox = MeasureLabelBox(g, labelFont, text, voltageHead.Value, rect, true);
                }
                if (currentHead.HasValue)
                {
                    string text = "A " + latest.Current.ToString("0.000", CultureInfo.InvariantCulture) + "A";
                    iBox = MeasureLabelBox(g, labelFont, text, currentHead.Value, rect, false);
                }

                if (vBox.HasValue && iBox.HasValue && vBox.Value.IntersectsWith(iBox.Value))
                {
                    RectangleF vb = vBox.Value;
                    RectangleF ib = iBox.Value;
                    vb.Y = Clamp(rect.Top + 4, vb.Y - vb.Height / 2 - 4, rect.Bottom - vb.Height - 4);
                    ib.Y = Clamp(rect.Top + 4, ib.Y + ib.Height / 2 + 4, rect.Bottom - ib.Height - 4);
                    if (vb.IntersectsWith(ib))
                    {
                        vb.Y = rect.Top + 6;
                        ib.Y = rect.Bottom - ib.Height - 6;
                    }
                    vBox = vb;
                    iBox = ib;
                }

                if (vBox.HasValue)
                {
                    string text = "V " + latest.Voltage.ToString("0.00", CultureInfo.InvariantCulture) + "V";
                    DrawLabelBox(g, vBox.Value, text, labelFont, VoltageColor);
                }
                if (iBox.HasValue)
                {
                    string text = "A " + latest.Current.ToString("0.000", CultureInfo.InvariantCulture) + "A";
                    DrawLabelBox(g, iBox.Value, text, labelFont, CurrentColor);
                }
            }
        }

        private static RectangleF MeasureLabelBox(Graphics g, Font font, string text, PointF head, Rectangle rect, bool voltage)
        {
            SizeF size = g.MeasureString(text, font);
            float width = size.Width + 16F;
            float height = size.Height + 8F;
            float x = head.X + 10F;
            if (x + width > rect.Right - 4) x = rect.Right - width - 4;
            if (x < rect.Left + 4) x = rect.Left + 4;

            float y = voltage ? head.Y + 6F : head.Y - height - 6F;
            y = Clamp(rect.Top + 4, y, rect.Bottom - height - 4);
            return new RectangleF(x, y, width, height);
        }

        private static void DrawLabelBox(Graphics g, RectangleF box, string text, Font font, Color color)
        {
            using (var back = new SolidBrush(Color.FromArgb(235, 255, 255, 255)))
            using (var border = new Pen(Color.FromArgb(180, color), 1.5F))
            using (var textBrush = new SolidBrush(color))
            {
                g.FillRectangle(back, box);
                g.DrawRectangle(border, box.X, box.Y, box.Width, box.Height);
                g.DrawString(text, font, textBrush, box.X + 8F, box.Y + 3F);
            }
        }

        private static float Clamp(float min, float value, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private void PruneRaw(DateTime now)
        {
            DateTime cutoff = now - RawWindow;
            while (rawPoints.Count > 0 && rawPoints[0].Time < cutoff)
            {
                RawPoint point = rawPoints[0];
                rawPoints.RemoveAt(0);
                AddCompressed(point.Time, point.Voltage, point.Current, point.Power);
            }
        }

        private void AddCompressed(DateTime now, double voltage, double current, double power)
        {
            DateTime minute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
            if (currentMinute == null)
            {
                currentMinute = new MinuteAggregate(minute);
            }
            if (currentMinute.Minute != minute)
            {
                minuteHistory.Add(currentMinute.ToPoint());
                currentMinute = new MinuteAggregate(minute);
            }
            currentMinute.Add(voltage, current, power);
        }

        private void PruneCompressed(DateTime now)
        {
            DateTime cutoff = now - CompressedWindow;
            while (minuteHistory.Count > 0 && minuteHistory[0].Minute < cutoff)
            {
                minuteHistory.RemoveAt(0);
            }
        }

        private sealed class RawPoint
        {
            public DateTime Time;
            public double Voltage;
            public double Current;
            public double Power;
        }

        private sealed class MinutePoint
        {
            public DateTime Minute;
            public int Count;
            public double VoltageAverage;
            public double VoltageMinimum;
            public double VoltageMaximum;
            public double CurrentAverage;
            public double CurrentMinimum;
            public double CurrentMaximum;
            public double PowerAverage;
            public double PowerMinimum;
            public double PowerMaximum;
        }

        private sealed class MinuteAggregate
        {
            public readonly DateTime Minute;
            private int count;
            private double voltageSum;
            private double voltageMinimum;
            private double voltageMaximum;
            private double currentSum;
            private double currentMinimum;
            private double currentMaximum;
            private double powerSum;
            private double powerMinimum;
            private double powerMaximum;

            public MinuteAggregate(DateTime minute)
            {
                Minute = minute;
            }

            public void Add(double voltage, double current, double power)
            {
                if (count == 0)
                {
                    voltageMinimum = voltageMaximum = voltage;
                    currentMinimum = currentMaximum = current;
                    powerMinimum = powerMaximum = power;
                }
                else
                {
                    if (voltage < voltageMinimum) voltageMinimum = voltage;
                    if (voltage > voltageMaximum) voltageMaximum = voltage;
                    if (current < currentMinimum) currentMinimum = current;
                    if (current > currentMaximum) currentMaximum = current;
                    if (power < powerMinimum) powerMinimum = power;
                    if (power > powerMaximum) powerMaximum = power;
                }
                voltageSum += voltage;
                currentSum += current;
                powerSum += power;
                count++;
            }

            public MinutePoint ToPoint()
            {
                int divisor = Math.Max(1, count);
                return new MinutePoint
                {
                    Minute = Minute,
                    Count = count,
                    VoltageAverage = voltageSum / divisor,
                    VoltageMinimum = voltageMinimum,
                    VoltageMaximum = voltageMaximum,
                    CurrentAverage = currentSum / divisor,
                    CurrentMinimum = currentMinimum,
                    CurrentMaximum = currentMaximum,
                    PowerAverage = powerSum / divisor,
                    PowerMinimum = powerMinimum,
                    PowerMaximum = powerMaximum
                };
            }
        }
    }

    internal sealed class CrcException : Exception
    {
    }
}
