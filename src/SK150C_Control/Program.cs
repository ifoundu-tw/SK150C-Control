using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

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
        private const string VersionName = "SK150C_Control_v25";
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
        private readonly InlineValuePanel protectTile = new InlineValuePanel("保護狀態", "", Color.FromArgb(31, 38, 46), 15F);
        private readonly TrendChart trendChart = new TrendChart();

        private readonly StepEditor setVoltageBox = new StepEditor(0.50M, 40.00M, 12.00M, 2, "V", 1.00M, 0.10M, VoltageColor);
        private readonly StepEditor setCurrentBox = new StepEditor(0.001M, 8.000M, 1.000M, 3, "A", 1.000M, 0.100M, CurrentColor);
        private readonly StepEditor setLvpBox = new StepEditor(0.00M, 40.00M, 0.00M, 2, "V", 1.00M, 0.10M, VoltageColor);
        private readonly StepEditor setOvpBox = new StepEditor(0.50M, 42.00M, 42.00M, 2, "V", 1.00M, 0.10M, VoltageColor);
        private readonly StepEditor setOcpBox = new StepEditor(0.001M, 8.200M, 8.200M, 3, "A", 1.000M, 0.100M, CurrentColor);
        private readonly StepEditor setOppBox = new StepEditor(0.0M, 160.0M, 160.0M, 1, "W", 10.0M, 1.0M, PowerColor);
        private readonly ComboBox backlightBox = new ComboBox();
        private readonly NumericUpDown sleepBox = new NumericUpDown();
        private readonly CheckBox buzzerBox = new CheckBox();
        private readonly CheckBox keyLockBox = new CheckBox();
        private readonly CheckBox quickInputBox = new CheckBox();
        private readonly Button restartButton = new Button();
        private readonly Button factoryButton = new Button();
        private readonly Button zeroButton = new Button();
        private readonly Button writeVoltageButton = new Button();
        private readonly Button writeCurrentButton = new Button();
        private readonly Button writeLvpButton = new Button();
        private readonly Button writeOvpButton = new Button();
        private readonly Button writeOcpButton = new Button();
        private readonly Button writeOppButton = new Button();
        private readonly Button outputOnButton = new Button();
        private readonly Button outputOffButton = new Button();
        private readonly Button clearLogButton = new Button();
        private readonly Button refreshQuickGroupsButton = new Button();
        private readonly Label callGroupLabel = new Label();
        private readonly Label editGroupLabel = new Label();
        private readonly Dictionary<StepEditor, Panel> parameterBlocks = new Dictionary<StepEditor, Panel>();
        private readonly Dictionary<int, QuickGroupButton> quickGroupButtons = new Dictionary<int, QuickGroupButton>();
        private readonly double?[] quickGroupVoltages = new double?[11];
        private readonly double?[] quickGroupCurrents = new double?[11];

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
        private int pollCounter;
        private int editGroup = -1;
        private int pollIntervalMs = 1000;
        private bool syncingOptions;
        private bool startupAutoConnectAttempted;

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
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
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
            clearLogButton.Click += delegate { logBox.Clear(); };
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

            var flow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 27, Padding = new Padding(12, 32, 12, 4) };
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
            zeroButton.Text = "校準/清零";
            restartButton.Width = 66;
            factoryButton.Width = 82;
            zeroButton.Width = 82;
            restartButton.Height = factoryButton.Height = zeroButton.Height = 30;
            restartButton.Click += delegate { ConfirmAndWrite("重啟裝置", "確定要送出重啟命令？輸出可能會中斷。", 0x002F, "重啟"); };
            factoryButton.Click += delegate { ConfirmAndWrite("恢復出廠設定", "確定要恢復出廠設定？目前參數可能會被清除。", 0x0020, "恢復出廠設定"); };
            zeroButton.Click += delegate { ConfirmAndWrite("校準/清零", "確定要送出校準/清零命令？請確認設備處於適合校準的狀態。", 0x0021, "校準/清零"); };
            maintenance.Controls.Add(restartButton);
            maintenance.Controls.Add(factoryButton);
            maintenance.Controls.Add(zeroButton);
            flow.Controls.Add(maintenance, 0, 23);
            flow.SetColumnSpan(maintenance, 2);

            languageBox.DropDownStyle = ComboBoxStyle.DropDownList;
            languageBox.Items.AddRange(new object[] { "中文", "English" });
            languageBox.SelectedItem = "中文";
            languageBox.SelectedIndexChanged += delegate { ApplyLanguage(); };
            AddRow(flow, 24, "語言", languageBox);

            stateLabel.Text = StatusText("待命");
            stateLabel.Dock = DockStyle.Fill;
            stateLabel.ForeColor = Color.FromArgb(76, 86, 96);
            flow.Controls.Add(stateLabel, 0, 25);
            flow.SetColumnSpan(stateLabel, 2);
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
            for (int i = 0; i < 4; i++) tiles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            for (int i = 0; i < 5; i++) tiles.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
            tiles.Controls.Add(voutTile, 0, 0);
            tiles.Controls.Add(setVoltageTile, 1, 0);
            tiles.Controls.Add(powerTile, 2, 0);
            tiles.SetRowSpan(powerTile, 3);
            tiles.SetColumnSpan(powerTile, 2);
            tiles.Controls.Add(ioutTile, 0, 1);
            tiles.Controls.Add(setCurrentTile, 1, 1);
            tiles.Controls.Add(ovpTile, 0, 2);
            tiles.Controls.Add(ocpTile, 1, 2);
            tiles.Controls.Add(temperatureTile, 0, 3);
            tiles.Controls.Add(modeTile, 1, 3);
            tiles.Controls.Add(outputStateTile, 2, 3);
            tiles.Controls.Add(capacityTile, 0, 4);
            tiles.Controls.Add(energyTile, 1, 4);
            tiles.Controls.Add(protectTile, 2, 4);
            SetupCallGroupBadge();
            tiles.Controls.Add(callGroupLabel, 3, 3);
            tiles.SetRowSpan(callGroupLabel, 2);
            layout.Controls.Add(trendChart, 0, 0);
            layout.Controls.Add(tiles, 0, 1);
            return panel;
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
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(0, 0, 0, 0) };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
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

            var scrollHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(0) };
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(12, 32, 12, 12)
            };
            scrollHost.Controls.Add(flow);
            layout.Controls.Add(scrollHost, 0, 0);

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
            flow.Controls.Add(BuildParameterBlock("設定電壓", setVoltageBox, writeVoltageButton));

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
            flow.Controls.Add(BuildParameterBlock("設定電流", setCurrentBox, writeCurrentButton));

            writeLvpButton.Text = "寫入 LVP";
            writeLvpButton.Click += delegate
            {
                QueueVerifiedWrite(CurrentGroupBase() + 2, (int)Math.Round((double)setLvpBox.Value * 100.0), "設定 LVP M" + currentGroup, setLvpBox);
            };
            flow.Controls.Add(BuildParameterBlock("LVP 低壓保護", setLvpBox, writeLvpButton));

            writeOvpButton.Text = "寫入 OVP";
            writeOvpButton.Click += delegate
            {
                QueueVerifiedWrite(CurrentGroupBase() + 3, (int)Math.Round((double)setOvpBox.Value * 100.0), "設定 OVP M" + currentGroup, setOvpBox);
            };
            flow.Controls.Add(BuildParameterBlock("OVP 過壓保護", setOvpBox, writeOvpButton));

            writeOcpButton.Text = "寫入 OCP";
            writeOcpButton.Click += delegate
            {
                QueueVerifiedWrite(CurrentGroupBase() + 4, (int)Math.Round((double)setOcpBox.Value * 1000.0), "設定 OCP M" + currentGroup, setOcpBox);
            };
            flow.Controls.Add(BuildParameterBlock("OCP 過流保護", setOcpBox, writeOcpButton));

            writeOppButton.Text = "寫入 OPP";
            writeOppButton.Click += delegate
            {
                QueueVerifiedWrite(CurrentGroupBase() + 5, (int)Math.Round((double)setOppBox.Value * 10.0), "設定 OPP M" + currentGroup, setOppBox);
            };
            flow.Controls.Add(BuildParameterBlock("OPP 過功率保護", setOppBox, writeOppButton));

            flow.Controls.Add(SectionText("輸出開關"));
            var onOff = new FlowLayoutPanel { Width = 250, Height = 42, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 0, 0, 8) };
            outputOnButton.Text = "ON";
            outputOffButton.Text = "OFF";
            outputOnButton.Width = outputOffButton.Width = 96;
            outputOnButton.Height = outputOffButton.Height = 38;
            outputOnButton.BackColor = Color.FromArgb(65, 145, 108);
            outputOnButton.ForeColor = Color.White;
            outputOffButton.BackColor = Color.FromArgb(172, 79, 82);
            outputOffButton.ForeColor = Color.White;
            outputOnButton.Click += delegate { QueueWriteRegister(0x0012, 1, "輸出 ON"); };
            outputOffButton.Click += delegate { QueueWriteRegister(0x0012, 0, "輸出 OFF"); };
            onOff.Controls.Add(outputOnButton);
            onOff.Controls.Add(outputOffButton);
            flow.Controls.Add(onOff);

            var quick = new TableLayoutPanel { Width = 250, Height = 258, ColumnCount = 2, RowCount = 6, Margin = new Padding(0, 0, 0, 8) };
            quick.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            quick.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            for (int r = 0; r < 6; r++) quick.RowStyles.Add(new RowStyle(SizeType.Percent, 16.666F));
            flow.Controls.Add(SectionText("快捷組"));
            for (int i = 0; i <= 10; i++)
            {
                int column = i <= 5 ? 0 : 1;
                int row = i <= 5 ? i : i - 6;
                quick.Controls.Add(BuildQuickGroupPair(i), column, row);
            }
            flow.Controls.Add(quick);

            layout.Controls.Add(BuildGroupStatusPanel(), 0, 1);
            return panel;
        }

        private Control BuildGroupStatusPanel()
        {
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 1, Padding = new Padding(12, 4, 20, 8), BackColor = Color.White };
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            refreshQuickGroupsButton.Text = "刷新 M0-M10";
            refreshQuickGroupsButton.Dock = DockStyle.Fill;
            refreshQuickGroupsButton.Margin = new Padding(0);
            refreshQuickGroupsButton.Font = new Font("Microsoft JhengHei UI", 9.5F, FontStyle.Bold);
            refreshQuickGroupsButton.BackColor = Color.FromArgb(241, 245, 249);
            refreshQuickGroupsButton.ForeColor = Color.FromArgb(31, 38, 46);
            refreshQuickGroupsButton.Click += delegate { RefreshQuickGroupSummary(); };
            panel.Controls.Add(refreshQuickGroupsButton, 0, 0);
            return panel;
        }

        private void SetupCallGroupBadge()
        {
            SetupStatusBadge(callGroupLabel, "目前調用：--", Color.FromArgb(35, 96, 73), 16F);
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
            var block = new Panel { Width = 250, Height = 94, Margin = new Padding(0, 0, 0, 8), BackColor = Color.FromArgb(250, 251, 252) };
            parameterBlocks[editor] = block;
            editor.UserChanged += delegate { UpdateParameterBlockDirty(editor); };
            var titleLabel = SectionText(title);
            titleLabel.SetBounds(0, 0, 250, 22);
            titleLabel.Dock = DockStyle.None;
            editor.Dock = DockStyle.None;
            editor.SetBounds(0, 24, 250, 38);
            writeButton.SetBounds(0, 64, 250, 28);
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
                statusLabel.Text = Ui("已連線") + "  " + serial.PortName + "  " + serial.BaudRate + "  8N1";
                statusLabel.ForeColor = Color.FromArgb(134, 216, 160);
                stateLabel.Text = StatusText("已連線");
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
            stateLabel.Text = StatusText("待命");
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
                        int temperature = Word(response, 29);
                        int keyLock = Word(response, 33);
                        int protect = Word(response, 35);
                        int cvcc = Word(response, 37);
                        int onoff = Word(response, 39);
                        BeginInvoke((Action)(() =>
                        {
                            UpdateSetValues(vset / 100.0, iset / 1000.0);
                            UpdateValues(vout / 100.0, iout / 1000.0, power / 100.0);
                            UpdateStatusValues(temperature / 10.0, cvcc, onoff, protect, CombineWords(ahHigh, ahLow) / 1000.0, CombineWords(whHigh, whLow) / 1000.0, keyLock);
                        }));
                    }

                    SyncProtectionFromGroup();
                    pollCounter++;
                    if (pollCounter % 5 == 0) SyncDeviceOptions();
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
                callGroupLabel.Text = ActivePresetText("忙碌中");
                return;
            }
            callGroupLabel.Text = ActivePresetText("M" + group + " 讀取中");
            busy = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    var groupData = ExecuteWithRetry(BuildRead(GroupBase(group), 6), ExpectedReadLength(6));
                    if (groupData == null)
                    {
                        BeginInvoke((Action)(() => callGroupLabel.Text = ActivePresetText("M" + group + " 讀取失敗")));
                        return;
                    }

                    BeginInvoke((Action)(() =>
                    {
                        editGroup = -1;
                        UpdateGroupEditor(groupData, true);
                        UpdateQuickGroupButton(group, Word(groupData, 3) / 100.0, Word(groupData, 5) / 1000.0);
                        editGroupLabel.Text = "保護寫入：目前 M 組";
                        callGroupLabel.Text = ActivePresetText("M" + group + " 調用中");
                    }));

                    byte[] response = ExecuteWithRetry(BuildWriteSingle(0x001D, group), 8);
                    if (response != null)
                    {
                        currentGroup = group;
                        Thread.Sleep(300);
                        SyncSettingsFromDevice();
                        BeginInvoke((Action)(() =>
                        {
                            callGroupLabel.Text = ActivePresetText("M" + group);
                            stateLabel.Text = StatusText("已調用 M" + group + " 並同步");
                        }));
                    }
                    else
                    {
                        BeginInvoke((Action)(() => callGroupLabel.Text = ActivePresetText("M" + group + " 失敗")));
                    }
                }
                finally
                {
                    busy = false;
                }
            });
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
                        stateLabel.Text = StatusText("讀取快捷組摘要 M0-M10");
                        if (manualRefresh)
                        {
                            refreshQuickGroupsButton.Enabled = false;
                            refreshQuickGroupsButton.Text = Ui("刷新中...");
                        }
                    }));
                    for (int group = 0; group <= 10; group++)
                    {
                        var response = ExecuteWithRetry(BuildRead(GroupBase(group), 2), ExpectedReadLength(2));
                        int capturedGroup = group;
                        if (response != null)
                        {
                            int rawVoltage = Word(response, 3);
                            int rawCurrent = Word(response, 5);
                            BeginInvoke((Action)(() => UpdateQuickGroupButton(capturedGroup, rawVoltage / 100.0, rawCurrent / 1000.0)));
                        }
                        else
                        {
                            BeginInvoke((Action)(() => UpdateQuickGroupButton(capturedGroup, null, null)));
                        }
                        Thread.Sleep(35);
                    }
                    BeginInvoke((Action)(() =>
                    {
                        stateLabel.Text = StatusText("快捷組摘要已更新");
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
                stateLabel.Text = StatusText("忙碌，稍後再刷新 M0-M10");
                return;
            }
            bool restartPolling = pollTimer.Enabled && autoPollBox.Checked;
            pollTimer.Stop();
            QueueQuickGroupSummaryScan(restartPolling, true);
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
                    var groupData = ExecuteWithRetry(BuildRead(GroupBase(group), 6), ExpectedReadLength(6));
                    if (groupData != null)
                    {
                        editGroup = group;
                        BeginInvoke((Action)(() =>
                        {
                            UpdateGroupEditor(groupData, true);
                            editGroupLabel.Text = "正在編輯：M" + group;
                            stateLabel.Text = StatusText("正在編輯 M" + group);
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
                            stateLabel.Text = StatusText("忙碌，寫入未送出");
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
            stateLabel.Text = StatusText(label + " 寫入確認中");
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
                            stateLabel.Text = StatusText("忙碌，" + label + " 未送出");
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
                            stateLabel.Text = StatusText(label + " 寫入無回應");
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
                                stateLabel.Text = StatusText(label + " 已確認");
                            }
                            else
                            {
                                editor.SetDirty(true);
                                UpdateParameterBlockDirty(editor);
                                stateLabel.Text = StatusText(label + " 未確認，寫入 " + value.ToString(CultureInfo.InvariantCulture) + " 讀回 " + readBack.ToString(CultureInfo.InvariantCulture));
                            }
                        }));
                    }
                    else
                    {
                        BeginInvoke((Action)(() =>
                        {
                            editor.SetDirty(true);
                            UpdateParameterBlockDirty(editor);
                            stateLabel.Text = StatusText(label + " 讀回失敗");
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
                stateLabel.Text = StatusText("無回應");
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
            voutTile.SetValue(vout, "0.00");
            ioutTile.SetValue(iout, "0.000");
            powerTile.SetValue(power, "0.00");
            powerTile.AddTrendPoint(power);
            trendChart.AddPoint(vout, iout, power);
            stateLabel.Text = StatusText("正常");
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

        private void UpdateStatusValues(double temperature, int cvcc, int onoff, int protect, double ah, double wh, int keyLock)
        {
            temperatureTile.SetValue(temperature, "0.0");
            modeTile.SetText(cvcc == 1 ? "CC" : "CV");
            outputStateTile.SetText(onoff == 1 ? "ON" : "OFF");
            protectTile.SetText(ProtectText(protect));
            capacityTile.SetValue(ah, "0.000");
            energyTile.SetValue(wh, "0.000");

            syncingOptions = true;
            try
            {
                keyLockBox.Checked = keyLock != 0;
            }
            finally
            {
                syncingOptions = false;
            }
        }

        private static string ProtectText(int code)
        {
            switch (code)
            {
                case 0: return "正常";
                case 1: return "OVP";
                case 2: return "OCP";
                case 3: return "OPP";
                case 4: return "LVP";
                case 5: return "OAH";
                case 6: return "OHP";
                case 7: return "OTP";
                case 8: return "OEP";
                case 9: return "OWH";
                case 10: return "ICP";
                case 11: return "IVP";
                default: return "代碼 " + code.ToString(CultureInfo.InvariantCulture);
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
            QuickGroupButton button;
            if (!quickGroupButtons.TryGetValue(group, out button)) return;
            if (group >= 0 && group < quickGroupVoltages.Length)
            {
                quickGroupVoltages[group] = voltage;
                quickGroupCurrents[group] = current;
            }
            button.SetValues(voltage.HasValue ? FormatGroupVoltage(voltage.Value) : "--V",
                current.HasValue ? FormatGroupCurrent(current.Value) : "--A");
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
            return pollIntervalMs;
        }

        private void SetPollInterval(int interval)
        {
            pollIntervalMs = interval;
            pollTimer.Interval = interval;
            StyleIntervalButton(interval100Button, interval == 100);
            StyleIntervalButton(interval300Button, interval == 300);
            StyleIntervalButton(interval1000Button, interval == 1000);
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
        }

        private void ApplyLanguage()
        {
            bool english = languageBox.SelectedItem != null && languageBox.SelectedItem.ToString() == "English";
            Text = english ? "SK150C Modbus Control Console" : "SK150C Modbus 控制台";
            ApplyLanguageToControls(this, english);
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
                if (text.StartsWith("目前調用：")) return "Active Preset: " + TranslateUiText(text.Substring(5), true);
                if (text.StartsWith("正在編輯：")) return "Editing: " + TranslateUiText(text.Substring(5), true);
            }
            else
            {
                if (text.StartsWith("Status: ")) return "狀態：" + TranslateUiText(text.Substring(8), false);
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
                { "讀回", "readback" }
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
                { "校準/清零", "Calibrate/Zero" },
                { "語言", "Language" },
                { "待命", "Standby" },
                { "未連線", "Disconnected" },
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
                { "輸出開關", "Output Switch" },
                { "快捷組", "Presets" },
                { "刷新 M0-M10", "Refresh M0-M10" },
                { "刷新中...", "Refreshing..." },
                { "目前調用：--", "Active Preset: --" },
                { "忙碌中", "Busy" },
                { "讀取中", "Reading" },
                { "讀取失敗", "Read Failed" },
                { "調用中", "Calling" },
                { "校準/清零", "Calibrate/Zero" },
                { "TX / RX 通訊紀錄", "TX / RX Log" }
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

        private bool IsEnglishUi()
        {
            return languageBox.SelectedItem != null && languageBox.SelectedItem.ToString() == "English";
        }

        private string Ui(string zh)
        {
            return TranslateUiText(zh, IsEnglishUi());
        }

        private string StatusText(string zh)
        {
            return IsEnglishUi() ? "Status: " + TranslateUiText(zh, true) : "狀態：" + zh;
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
                Height = 26,
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
        private readonly List<double> trend = new List<double>();
        private const int MaxTrendPoints = 240;
        private bool showTrend;

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
            BackColor = Color.FromArgb(250, 251, 252);

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
            valueLabel.Text = number.ToString(format, CultureInfo.InvariantCulture) + (unit.Length == 0 ? "" : " " + unit);
            FitValueFont();
        }

        public void SetText(string text)
        {
            valueLabel.Text = text;
            FitValueFont();
        }

        public void AddTrendPoint(double value)
        {
            if (!ShowTrend) return;
            trend.Add(value);
            if (trend.Count > MaxTrendPoints) trend.RemoveAt(0);
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
            using (var pen = new Pen(accent, 3F))
            {
                e.Graphics.DrawLine(pen, 1, 3, 1, Height - 4);
            }
            if (ShowTrend) DrawMiniTrend(e.Graphics);
        }

        private void DrawMiniTrend(Graphics g)
        {
            if (trend.Count < 2) return;
            var rect = new Rectangle(12, Math.Max(34, Height / 2), Math.Max(10, Width - 24), Math.Max(18, Height - Math.Max(38, Height / 2) - 10));
            double min = trend[0];
            double max = trend[0];
            for (int i = 1; i < trend.Count; i++)
            {
                if (trend[i] < min) min = trend[i];
                if (trend[i] > max) max = trend[i];
            }
            if (max - min < 1.0)
            {
                double mid = (max + min) / 2.0;
                min = mid - 0.5;
                max = mid + 0.5;
            }

            PointF[] points = new PointF[trend.Count];
            for (int i = 0; i < trend.Count; i++)
            {
                float x = rect.Left + (float)i * rect.Width / Math.Max(1, trend.Count - 1);
                double normalized = (trend[i] - min) / (max - min);
                normalized = Math.Max(0, Math.Min(1, normalized));
                float y = rect.Bottom - (float)(normalized * rect.Height);
                points[i] = new PointF(x, y);
            }

            using (var gridPen = new Pen(Color.FromArgb(235, 218, 218)))
            using (var trendPen = new Pen(Color.FromArgb(196, 68, 62), 2F))
            using (var brush = new SolidBrush(Color.FromArgb(145, 84, 84)))
            {
                g.DrawLine(gridPen, rect.Left, rect.Bottom, rect.Right, rect.Bottom);
                g.DrawLines(trendPen, points);
                string range = min.ToString("0.0", CultureInfo.InvariantCulture) + "~" + max.ToString("0.0", CultureInfo.InvariantCulture) + "W";
                using (var font = new Font("Segoe UI", 7.5F, FontStyle.Bold))
                {
                    g.DrawString(range, font, brush, rect.Left, rect.Top - 14);
                }
            }
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
            Color fill = pressed ? Color.FromArgb(226, 232, 240) : hovering ? Color.FromArgb(239, 246, 255) : BackColor;
            using (var brush = new SolidBrush(fill))
            using (var borderPen = new Pen(hovering ? Color.FromArgb(66, 153, 225) : Color.FromArgb(184, 194, 204)))
            {
                e.Graphics.FillRectangle(brush, rect);
                e.Graphics.DrawRectangle(borderPen, rect);
            }

            Rectangle leftRect = new Rectangle(5, 0, Math.Max(28, Width / 2 - 6), Height);
            Rectangle rightTop = new Rectangle(Math.Max(38, Width / 2 - 4), 1, Math.Max(20, Width - Math.Max(38, Width / 2 - 4) - 5), Height / 2);
            Rectangle rightBottom = new Rectangle(rightTop.Left, Height / 2 - 1, rightTop.Width, Height / 2);

            using (var groupFont = FitFont(e.Graphics, "M" + group.ToString(CultureInfo.InvariantCulture), 12F, FontStyle.Bold, leftRect.Size))
            using (var valueFont = FitFont(e.Graphics, voltageText.Length >= currentText.Length ? voltageText : currentText, 10F, FontStyle.Bold, rightTop.Size))
            using (var groupBrush = new SolidBrush(Color.FromArgb(24, 30, 38)))
            using (var voltageBrush = new SolidBrush(VoltageColor))
            using (var currentBrush = new SolidBrush(CurrentColor))
            {
                DrawText(e.Graphics, "M" + group.ToString(CultureInfo.InvariantCulture), groupFont, groupBrush, leftRect, ContentAlignment.MiddleCenter);
                DrawText(e.Graphics, voltageText, valueFont, voltageBrush, rightTop, ContentAlignment.MiddleRight);
                DrawText(e.Graphics, currentText, valueFont, currentBrush, rightBottom, ContentAlignment.MiddleRight);
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

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 1 };
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
                if (!suppressTextChanged && textBox.Focused) MarkDirty();
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
            currentValue = decimal.Round(value, decimalPlaces);
            suppressTextChanged = true;
            try
            {
                textBox.Text = currentValue.ToString(FormatValue(), CultureInfo.InvariantCulture);
            }
            finally
            {
                suppressTextChanged = false;
            }
            if (userChange) MarkDirty();
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
                    double leftValue = vMax - (vMax - vMin) * i / 5.0;
                    double rightValue = iMax - (iMax - iMin) * i / 5.0;
                    string left = leftValue.ToString("0.00", CultureInfo.InvariantCulture);
                    string right = rightValue.ToString("0.000", CultureInfo.InvariantCulture);
                    SizeF leftSize = e.Graphics.MeasureString(left, Font);
                    e.Graphics.DrawString(left, Font, textBrush, rect.Left - leftSize.Width - 8, y - leftSize.Height / 2);
                    e.Graphics.DrawString(right, Font, textBrush, rect.Right + 8, y - leftSize.Height / 2);
                }
                for (int i = 0; i <= 4; i++)
                {
                    int x = rect.Left + rect.Width * i / 4;
                    e.Graphics.DrawLine(pen, x, rect.Bottom, x, rect.Bottom + 4);
                    string label = FormatTimeLabel(i, displayWindow);
                    SizeF size = e.Graphics.MeasureString(label, Font);
                    e.Graphics.DrawString(label, Font, textBrush, x - size.Width / 2, rect.Bottom + 8);
                }
                e.Graphics.DrawString("歷史曲線", titleFont, textBrush, 12, 8);
                e.Graphics.DrawString("左軸: V", Font, textBrush, rect.Left, 16);
                e.Graphics.DrawString("右軸: A", Font, textBrush, rect.Right - 48, 16);
                using (var vBrush = new SolidBrush(VoltageColor))
                using (var iBrush = new SolidBrush(CurrentColor))
                {
                    e.Graphics.DrawString("VOUT", Font, vBrush, rect.Left, Height - 30);
                    e.Graphics.DrawString("IOUT", Font, iBrush, rect.Left + 70, Height - 30);
                    e.Graphics.DrawString("V " + vMin.ToString("0.00", CultureInfo.InvariantCulture) + "~" + vMax.ToString("0.00", CultureInfo.InvariantCulture), Font, vBrush, rect.Left + 140, Height - 30);
                    e.Graphics.DrawString("A " + iMin.ToString("0.000", CultureInfo.InvariantCulture) + "~" + iMax.ToString("0.000", CultureInfo.InvariantCulture), Font, iBrush, rect.Left + 300, Height - 30);
                }
            }

            DrawSeries(e.Graphics, rect, rawPoints, windowStart, now, vMin, vMax, VoltageColor, true);
            DrawSeries(e.Graphics, rect, rawPoints, windowStart, now, iMin, iMax, CurrentColor, false);
        }

        private TimeSpan GetDisplayWindow(DateTime now)
        {
            if (rawPoints.Count == 0) return TimeSpan.FromSeconds(60);
            TimeSpan elapsed = now - rawPoints[0].Time;
            if (elapsed <= TimeSpan.Zero) return TimeSpan.FromSeconds(10);
            if (elapsed > RawWindow) return RawWindow;
            double targetSeconds = elapsed.TotalSeconds * 1.08;
            if (targetSeconds <= 10) return TimeSpan.FromSeconds(10);
            if (targetSeconds <= 30) return TimeSpan.FromSeconds(30);
            if (targetSeconds <= 60) return TimeSpan.FromSeconds(60);
            if (targetSeconds <= 120) return TimeSpan.FromMinutes(2);
            if (targetSeconds <= 300) return TimeSpan.FromMinutes(5);
            if (targetSeconds <= 600) return TimeSpan.FromMinutes(10);
            if (targetSeconds <= 900) return TimeSpan.FromMinutes(15);
            if (targetSeconds <= 1800) return TimeSpan.FromMinutes(30);
            return RawWindow;
        }

        private static string FormatTimeLabel(int index, TimeSpan displayWindow)
        {
            if (index >= 4) return "now";
            double seconds = displayWindow.TotalSeconds * (4 - index) / 4.0;
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

        private static void DrawSeries(Graphics g, Rectangle rect, List<RawPoint> data, DateTime windowStart, DateTime windowEnd, double min, double max, Color color, bool voltage)
        {
            if (data.Count < 2) return;
            double totalSeconds = Math.Max(1.0, (windowEnd - windowStart).TotalSeconds);
            int step = Math.Max(1, data.Count / Math.Max(1, rect.Width * 2));
            var points = new List<PointF>();
            for (int i = 0; i < data.Count; i += step)
            {
                RawPoint point = data[i];
                if (point.Time < windowStart || point.Time > windowEnd) continue;
                double elapsed = (point.Time - windowStart).TotalSeconds;
                float x = rect.Left + (float)(elapsed / totalSeconds * rect.Width);
                double value = voltage ? point.Voltage : point.Current;
                double normalized = (value - min) / (max - min);
                normalized = Math.Max(0, Math.Min(1, normalized));
                float y = rect.Bottom - (float)(normalized * rect.Height);
                points.Add(new PointF(x, y));
            }
            if (points.Count < 2) return;
            using (var pen = new Pen(color, 2F))
            {
                g.DrawLines(pen, points.ToArray());
            }
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
