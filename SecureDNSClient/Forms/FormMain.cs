using System.Net;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using MsmhTools;
using MsmhTools.HTTPProxyServer;
using MsmhTools.Themes;
using CustomControls;
using System.Net.NetworkInformation;
using System.Text;
using SecureDNSClient.DNSCrypt;
using SecureDNSClient.DPIBasic;
using System.Media;
using MsmhTools.DnsTool;
// https://github.com/msasanmh/SecureDNSClient

namespace SecureDNSClient
{
    public partial class FormMain : Form
    {
        private static readonly CustomLabel LabelMoving = new();
        public List<Tuple<long, string>> WorkingDnsList = new();
        public List<string> SavedDnsList = new();
        public List<string> SavedEncodedDnsList = new();
        private List<string> WorkingDnsListToFile = new();
        private bool Once = true;
        private bool IsCheckingStarted = false;
        private bool StopChecking = false;
        private bool IsCheckDone = false;
        private bool IsConnecting = false;
        private bool IsDisconnecting = false;
        private bool IsConnected = false;
        private bool IsDNSConnected = false;
        private bool IsDoHConnected = false;
        private bool IsDPIActive = false;
        private bool IsProxyDPIActive = false;
        private bool IsGoodbyeDPIActive = false;
        private bool IsDNSSet = false;
        private bool IsSharing = false;
        private bool IsProxySet = false;
        private int LocalDnsLatency = -1;
        private int LocalDohLatency = -1;
        private int LastProxyPort = 0;
        private bool ConnectAllClicked = false;
        private int NumberOfWorkingServers = 0;
        private IPAddress? LocalIP = IPAddress.Loopback; // as default
        public Settings AppSettings;
        private ToolStripMenuItem ToolStripMenuItemIcon = new();
        private HTTPProxyServer FakeProxy = new();
        private HTTPProxyServer HTTPProxy = new();
        public static HTTPProxyServer.Program.DPIBypass StaticDPIBypassProgram { get; private set; } = new();
        private bool AudioAlertOnline = true;
        private bool AudioAlertOffline = false;
        private bool AudioAlertRequestsExceeded = false;
        private readonly Stopwatch StopWatchCheckDPIWorks = new();
        private readonly Stopwatch StopWatchShowRequests = new();
        private readonly Stopwatch StopWatchShowChunkDetails = new();
        private readonly Stopwatch StopWatchAudioAlertDelay = new();
        private string TheDll = string.Empty;
        private readonly string NL = Environment.NewLine;
        private readonly int LogHeight;

        // PIDs
        private int PIDDNSProxy { get; set; } = -1;
        private int PIDDNSCrypt { get; set; } = -1;
        private int PIDGoodbyeDPI { get; set; } = -1;

        // Camouflage
        private HTTPProxyServer CamouflageProxyServer = new();
        private CamouflageDNSServer? CamouflageDNSServer;
        private bool IsBypassProxyActive { get; set; } = false;
        private bool IsBypassDNSActive { get; set; } = false;
        private int PIDDNSCryptBypass = -1;
        private int PIDDNSProxyBypass = -1;
        private int PIDGoodbyeDPIBypass = -1;

        public FormMain()
        {
            InitializeComponent();
            //CustomStatusStrip1.SizingGrip = false;

            // Set Min Size for Toggle Log View
            MinimumSize = new Size(Width, Height - CustomGroupBoxLog.Height);
            LogHeight = CustomGroupBoxLog.Height;

            // Fix Screen DPI
            ScreenDPI.ScaleForm(this, true, false);
            FixScreenDPI(this);

            // Rightclick on NotifyIcon
            ToolStripMenuItemIcon.Text = "Exit";
            ToolStripMenuItemIcon.Click += ToolStripMenuItemIcon_Click;
            CustomContextMenuStripIcon.Items.Add(ToolStripMenuItemIcon);

            // Startup Defaults
            Text = Info.InfoExecutingAssembly.ProductName + " v" + Info.InfoExecutingAssembly.ProductVersion;
            CustomButtonSetDNS.Enabled = false;
            CustomButtonSetProxy.Enabled = false;
            CustomTextBoxHTTPProxy.Enabled = false;
            DefaultSettings();

            // Set NotifyIcon Text
            NotifyIconMain.Text = Text;

            // Add Tooltips
            string msgViewCustomServers = "View working custom servers";
            CustomButtonViewWorkingServers.SetToolTip("Info", msgViewCustomServers);

            string msgFragmentChunks = "More chunks means more CPU usage.";
            CustomNumericUpDownPDpiFragmentChunks.SetToolTip("Warning", msgFragmentChunks);

            // Add Tooltips to advanced DPI
            string msgP = "Block passive DPI.";
            CustomCheckBoxDPIAdvP.SetToolTip("Info", msgP);
            string msgR = "Replace Host with hoSt.";
            CustomCheckBoxDPIAdvR.SetToolTip("Info", msgR);
            string msgS = "Remove space between host header and its value.";
            CustomCheckBoxDPIAdvS.SetToolTip("Info", msgS);
            string msgM = "Mix Host header case (test.com -> tEsT.cOm).";
            CustomCheckBoxDPIAdvM.SetToolTip("Info", msgM);
            string msgF = "Set HTTP fragmentation to value";
            CustomCheckBoxDPIAdvF.SetToolTip("Info", msgF);
            string msgK = "Enable HTTP persistent (keep-alive) fragmentation and set it to value.";
            CustomCheckBoxDPIAdvK.SetToolTip("Info", msgK);
            string msgN = "Do not wait for first segment ACK when -k is enabled.";
            CustomCheckBoxDPIAdvN.SetToolTip("Info", msgN);
            string msgE = "Set HTTPS fragmentation to value.";
            CustomCheckBoxDPIAdvE.SetToolTip("Info", msgE);
            string msgA = "Additional space between Method and Request-URI (enables -s, may break sites).";
            CustomCheckBoxDPIAdvA.SetToolTip("Info", msgA);
            string msgW = "Try to find and parse HTTP traffic on all processed ports (not only on port 80).";
            CustomCheckBoxDPIAdvW.SetToolTip("Info", msgW);
            string msgPort = "Additional TCP port to perform fragmentation on (and HTTP tricks with -w).";
            CustomCheckBoxDPIAdvPort.SetToolTip("Info", msgPort);
            string msgIpId = "Handle additional IP ID (decimal, drop redirects and TCP RSTs with this ID).";
            CustomCheckBoxDPIAdvIpId.SetToolTip("Info", msgIpId);
            string msgAllowNoSni = "Perform circumvention if TLS SNI can't be detected with --blacklist enabled.";
            CustomCheckBoxDPIAdvAllowNoSNI.SetToolTip("Info", msgAllowNoSni);
            string msgSetTtl = "Activate Fake Request Mode and send it with supplied TTL value.\nDANGEROUS! May break websites in unexpected ways. Use with care(or--blacklist).";
            CustomCheckBoxDPIAdvSetTTL.SetToolTip("Info", msgSetTtl);
            string msgAutoTtl = "Activate Fake Request Mode, automatically detect TTL and decrease\nit based on a distance. If the distance is shorter than a2, TTL is decreased\nby a2. If it's longer, (a1; a2) scale is used with the distance as a weight.\nIf the resulting TTL is more than m(ax), set it to m.\nDefault (if set): --auto-ttl 1-4-10. Also sets --min-ttl 3.\nDANGEROUS! May break websites in unexpected ways. Use with care (or --blacklist).";
            CustomCheckBoxDPIAdvAutoTTL.SetToolTip("[a1-a2-m]", msgAutoTtl);
            string msgMinTtl = "Minimum TTL distance (128/64 - TTL) for which to send Fake Request\nin --set - ttl and--auto - ttl modes.";
            CustomCheckBoxDPIAdvMinTTL.SetToolTip("Info", msgMinTtl);
            string msgWrongChksum = "Activate Fake Request Mode and send it with incorrect TCP checksum.\nMay not work in a VM or with some routers, but is safer than set - ttl.";
            CustomCheckBoxDPIAdvWrongChksum.SetToolTip("Info", msgWrongChksum);
            string msgWrongSeq = "Activate Fake Request Mode and send it with TCP SEQ/ACK in the past.";
            CustomCheckBoxDPIAdvWrongSeq.SetToolTip("Info", msgWrongSeq);
            string msgNativeFrag = "Fragment (split) the packets by sending them in smaller packets, without\nshrinking the Window Size. Works faster(does not slow down the connection)\nand better.";
            CustomCheckBoxDPIAdvNativeFrag.SetToolTip("Info", msgNativeFrag);
            string msgReverseFrag = "Fragment (split) the packets just as --native-frag, but send them in the\nreversed order. Works with the websites which could not handle segmented\nHTTPS TLS ClientHello(because they receive the TCP flow \"combined\").";
            CustomCheckBoxDPIAdvReverseFrag.SetToolTip("Info", msgReverseFrag);
            string msgMaxPayload = "Packets with TCP payload data more than [value] won't be processed.\nUse this option to reduce CPU usage by skipping huge amount of data\n(like file transfers) in already established sessions.\nMay skip some huge HTTP requests from being processed.\nDefault(if set): --max-payload 1200.";
            CustomCheckBoxDPIAdvMaxPayload.SetToolTip("Info", msgMaxPayload);
            string msgBlacklist = "Perform circumvention tricks only to host names and subdomains from\nsupplied text file(HTTP Host / TLS SNI).";
            CustomCheckBoxDPIAdvBlacklist.SetToolTip("Info", msgBlacklist);

            // Load Theme
            Theme.LoadTheme(this, Theme.Themes.Dark);

            // Add colors and texts to About page
            CustomLabelAboutThis.ForeColor = Color.DodgerBlue;
            CustomLabelAboutVersion.Text = " v" + Info.InfoExecutingAssembly.ProductVersion;
            CustomLabelAboutThis2.ForeColor = Color.IndianRed;

            // In case application closed unexpectedly Kill processes and set DNS to dynamic
            KillAll();

            // Initialize and load Settings
            if (File.Exists(SecureDNS.SettingsXmlPath) && Xml.IsValidXMLFile(SecureDNS.SettingsXmlPath))
                AppSettings = new(this, SecureDNS.SettingsXmlPath);
            else
                AppSettings = new(this);

            // Update NICs
            SecureDNS.UpdateNICs(CustomComboBoxNICs);

            LogAutoClear();
            UpdateBools();
            UpdateBoolDnsDohAuto();
            UpdateStatusShortAuto();
            UpdateStatusLongAuto();

            // Auto Save Settings (Timer)
            AutoSaveSettings();

            Controls.Add(LabelMoving);
            LabelMoving.Text = "Now Moving...";
            LabelMoving.Size = new(300, 150);
            LabelMoving.Location = new((ClientSize.Width / 2) - (LabelMoving.Width / 2), (ClientSize.Height / 2) - (LabelMoving.Height / 2));
            LabelMoving.TextAlign = ContentAlignment.MiddleCenter;
            LabelMoving.Font = new(FontFamily.GenericSansSerif, 12);
            Theme.SetColors(LabelMoving);
            LabelMoving.Visible = false;
            LabelMoving.SendToBack();

            Shown += FormMain_Shown;
            Move += FormMain_Move;
            ResizeEnd += FormMain_ResizeEnd;
            Resize += FormMain_Resize;
        }

        private async void FormMain_Shown(object? sender, EventArgs e)
        {
            if (Once)
            {
                // Write binaries if not exist or needs update
                await WriteNecessaryFilesToDisk();

                // Load Saved Servers
                SavedDnsLoad();

                Once = false;
            }
        }

        private void FormMain_Move(object? sender, EventArgs e)
        {
            SplitContainerMain.Visible = false;
            LabelMoving.Location = new((ClientSize.Width / 2) - (LabelMoving.Width / 2), (ClientSize.Height / 2) - (LabelMoving.Height / 2));
            LabelMoving.Visible = true;
            LabelMoving.BringToFront();
        }

        private void FormMain_ResizeEnd(object? sender, EventArgs e)
        {
            SplitContainerMain.Visible = true;
            LabelMoving.Visible = false;
            LabelMoving.SendToBack();
        }

        private void FormMain_Resize(object? sender, EventArgs e)
        {
            if (WindowState != FormWindowState.Minimized)
            {
                SplitContainerMain.Visible = true;
                LabelMoving.Visible = false;
                LabelMoving.SendToBack();
            }
        }

        //============================== Constant

        private void DefaultSettings()
        {
            // Check
            CustomRadioButtonBuiltIn.Checked = true;
            CustomRadioButtonCustom.Checked = false;
            CustomCheckBoxInsecure.Checked = false;

            // Connect
            CustomRadioButtonConnectCheckedServers.Checked = true;
            CustomRadioButtonConnectFakeProxyDohViaProxyDPI.Checked = false;
            CustomRadioButtonConnectFakeProxyDohViaGoodbyeDPI.Checked = false;
            CustomRadioButtonConnectDNSCrypt.Checked = false;
            CustomTextBoxHTTPProxy.Text = string.Empty;

            // Share
            CustomCheckBoxHTTPProxyEventShowRequest.Checked = false;
            CustomCheckBoxHTTPProxyEventShowChunkDetails.Checked = false;
            CustomCheckBoxPDpiEnableDpiBypass.Checked = true;
            CustomNumericUpDownPDpiDataLength.Value = (decimal)100;
            CustomNumericUpDownPDpiFragmentSize.Value = (decimal)2;
            CustomNumericUpDownPDpiFragmentChunks.Value = (decimal)100;
            CustomCheckBoxPDpiFragModeRandom.Checked = false;
            CustomNumericUpDownPDpiAntiPatternOffset.Value = (decimal)5;
            CustomCheckBoxPDpiDontChunkBigData.Checked = false;
            CustomNumericUpDownPDpiFragDelay.Value = (decimal)0;

            // DPI Basic
            CustomRadioButtonDPIMode1.Checked = false;
            CustomRadioButtonDPIMode2.Checked = false;
            CustomRadioButtonDPIMode3.Checked = false;
            CustomRadioButtonDPIMode4.Checked = false;
            CustomRadioButtonDPIMode5.Checked = false;
            CustomRadioButtonDPIMode6.Checked = false;
            CustomRadioButtonDPIModeLight.Checked = true;
            CustomRadioButtonDPIModeMedium.Checked = false;
            CustomRadioButtonDPIModeHigh.Checked = false;
            CustomRadioButtonDPIModeExtreme.Checked = false;
            CustomNumericUpDownSSLFragmentSize.Value = (decimal)40;

            // DPI Advanced
            CustomCheckBoxDPIAdvP.Checked = true;
            CustomCheckBoxDPIAdvR.Checked = true;
            CustomCheckBoxDPIAdvS.Checked = true;
            CustomCheckBoxDPIAdvM.Checked = true;
            CustomCheckBoxDPIAdvF.Checked = false;
            CustomNumericUpDownDPIAdvF.Value = (decimal)2;
            CustomCheckBoxDPIAdvK.Checked = false;
            CustomNumericUpDownDPIAdvK.Value = (decimal)2;
            CustomCheckBoxDPIAdvN.Checked = false;
            CustomCheckBoxDPIAdvE.Checked = true;
            CustomNumericUpDownDPIAdvE.Value = (decimal)40;
            CustomCheckBoxDPIAdvA.Checked = false;
            CustomCheckBoxDPIAdvW.Checked = true;
            CustomCheckBoxDPIAdvPort.Checked = false;
            CustomNumericUpDownDPIAdvPort.Value = (decimal)80;
            CustomCheckBoxDPIAdvIpId.Checked = false;
            CustomTextBoxDPIAdvIpId.Text = string.Empty;
            CustomCheckBoxDPIAdvAllowNoSNI.Checked = false;
            CustomCheckBoxDPIAdvSetTTL.Checked = false;
            CustomNumericUpDownDPIAdvSetTTL.Value = (decimal)1;
            CustomCheckBoxDPIAdvAutoTTL.Checked = false;
            CustomTextBoxDPIAdvAutoTTL.Text = "1-4-10";
            CustomCheckBoxDPIAdvMinTTL.Checked = false;
            CustomNumericUpDownDPIAdvMinTTL.Value = (decimal)3;
            CustomCheckBoxDPIAdvWrongChksum.Checked = false;
            CustomCheckBoxDPIAdvWrongSeq.Checked = false;
            CustomCheckBoxDPIAdvNativeFrag.Checked = true;
            CustomCheckBoxDPIAdvReverseFrag.Checked = false;
            CustomCheckBoxDPIAdvMaxPayload.Checked = true;
            CustomNumericUpDownDPIAdvMaxPayload.Value = (decimal)1200;

            // Settings Working Mode
            CustomRadioButtonSettingWorkingModeDNS.Checked = true;
            CustomRadioButtonSettingWorkingModeDNSandDoH.Checked = false;

            // Settings Check
            CustomNumericUpDownSettingCheckTimeout.Value = (decimal)5;
            CustomTextBoxSettingCheckDPIHost.Text = "www.youtube.com";
            CustomCheckBoxSettingProtocolDoH.Checked = true;
            CustomCheckBoxSettingProtocolTLS.Checked = true;
            CustomCheckBoxSettingProtocolDNSCrypt.Checked = true;
            CustomCheckBoxSettingProtocolDNSCryptRelay.Checked = true;
            CustomCheckBoxSettingProtocolDoQ.Checked = true;
            CustomCheckBoxSettingProtocolPlainDNS.Checked = false;
            CustomCheckBoxSettingSdnsDNSSec.Checked = false;
            CustomCheckBoxSettingSdnsNoLog.Checked = false;
            CustomCheckBoxSettingSdnsNoFilter.Checked = true;

            // Settings Connect
            CustomCheckBoxSettingEnableCache.Checked = true;
            CustomNumericUpDownSettingMaxServers.Value = (decimal)5;
            CustomNumericUpDownSettingCamouflageDnsPort.Value = (decimal)5380;

            // Settings Set/Unset DNS
            CustomRadioButtonSettingUnsetDnsToDhcp.Checked = false;
            CustomRadioButtonSettingUnsetDnsToStatic.Checked = true;
            CustomTextBoxSettingUnsetDns1.Text = "8.8.8.8";
            CustomTextBoxSettingUnsetDns2.Text = "8.8.4.4";

            // Settings Share Basic
            CustomNumericUpDownSettingHTTPProxyPort.Value = (decimal)8080;
            CustomNumericUpDownSettingHTTPProxyHandleRequests.Value = (decimal)2000;
            CustomCheckBoxSettingProxyBlockPort80.Checked = true;
            CustomCheckBoxSettingHTTPProxyUpstream.Checked = false;
            CustomComboBoxSettingHttpProxyUpstreamMode.SelectedIndex = 1;
            CustomTextBoxSettingHTTPProxyUpstreamHost.Text = IPAddress.Loopback.ToString();
            CustomNumericUpDownSettingHTTPProxyUpstreamPort.Value = (decimal)1090;

            // Settings Share Advanced
            CustomCheckBoxSettingHTTPProxyEnableFakeProxy.Checked = false;
            CustomCheckBoxSettingHTTPProxyCfCleanIP.Checked = false;
            CustomTextBoxSettingHTTPProxyCfCleanIP.Text = string.Empty;
            CustomCheckBoxSettingHTTPProxyEnableFakeDNS.Checked = false;
            CustomCheckBoxSettingHTTPProxyEnableBlackWhiteList.Checked = false;

            // Settings Fake Proxy
            CustomNumericUpDownSettingFakeProxyPort.Value = (decimal)8070;
            CustomTextBoxSettingFakeProxyDohAddress.Text = "https://dns.cloudflare.com/dns-query";
            CustomTextBoxSettingFakeProxyDohCleanIP.Text = "104.16.132.229";

            // Settings CPU
            CustomRadioButtonSettingCPUHigh.Checked = false;
            CustomRadioButtonSettingCPUAboveNormal.Checked = false;
            CustomRadioButtonSettingCPUNormal.Checked = true;
            CustomRadioButtonSettingCPUBelowNormal.Checked = false;
            CustomRadioButtonSettingCPULow.Checked = false;

            // Settings Others
            CustomTextBoxSettingBootstrapDnsIP.Text = "8.8.8.8";
            CustomNumericUpDownSettingBootstrapDnsPort.Value = (decimal)53;
            CustomTextBoxSettingFallbackDnsIP.Text = "8.8.8.8";
            CustomNumericUpDownSettingFallbackDnsPort.Value = (decimal)53;
            CustomCheckBoxSettingDontAskCertificate.Checked = false;
            CustomCheckBoxSettingDisableAudioAlert.Checked = false;
        }

        //============================== Methods

        public enum ConnectMode
        {
            ConnectToWorkingServers,
            ConnectToFakeProxyDohViaProxyDPI,
            ConnectToFakeProxyDohViaGoodbyeDPI,
            ConnectToPopularServersWithProxy
        }

        public ConnectMode GetConnectMode()
        {
            // Get Connect modes
            bool a = CustomRadioButtonConnectCheckedServers.Checked;
            bool b = CustomRadioButtonConnectFakeProxyDohViaProxyDPI.Checked;
            bool c = CustomRadioButtonConnectFakeProxyDohViaGoodbyeDPI.Checked;
            bool d = CustomRadioButtonConnectDNSCrypt.Checked;

            ConnectMode connectMode = ConnectMode.ConnectToWorkingServers;
            if (a) connectMode = ConnectMode.ConnectToWorkingServers;
            else if (b) connectMode = ConnectMode.ConnectToFakeProxyDohViaProxyDPI;
            else if (c) connectMode = ConnectMode.ConnectToFakeProxyDohViaGoodbyeDPI;
            else if (d) connectMode = ConnectMode.ConnectToPopularServersWithProxy;
            return connectMode;
        }
        
        private void AutoSaveSettings()
        {
            // Using System.Timers.Timer needs Invoke.
            System.Windows.Forms.Timer autoSaveTimer = new();
            autoSaveTimer.Interval = int.Parse(TimeSpan.FromMinutes(1).TotalMilliseconds.ToString());
            autoSaveTimer.Tick += async (s, e) =>
            {
                // Select Control type and properties to save
                AppSettings.AddSelectedControlAndProperty(typeof(CustomCheckBox), "Checked");
                AppSettings.AddSelectedControlAndProperty(typeof(CustomNumericUpDown), "Value");
                AppSettings.AddSelectedControlAndProperty(typeof(CustomRadioButton), "Checked");
                AppSettings.AddSelectedControlAndProperty(typeof(CustomTextBox), "Text");
                AppSettings.AddSelectedControlAndProperty(typeof(CustomTextBox), "Texts");
                AppSettings.AddSelectedControlAndProperty(typeof(CustomComboBox), "SelectedIndex");

                // Add Settings to save
                AppSettings.AddSelectedSettings(this);

                // Save Application Settings
                await AppSettings.SaveAsync(SecureDNS.SettingsXmlPath);
            };
            autoSaveTimer.Start();
        }

        private void FixScreenDPI(Form form)
        {
            using Graphics g = form.CreateGraphics();

            int x1 = 120; int y1 = 21;
            int splitMainD = SplitContainerMain.SplitterDistance;
            int splitTopD = SplitContainerTop.SplitterDistance;

            if (form.AutoScaleDimensions == form.CurrentAutoScaleDimensions)
            {
                // 96 = 100%
                // 120 = 125%
                // 144 = 150%
                if (g.DpiX == 120) // 125%
                {
                    setSize(x1 + 35, y1 + 10, splitMainD, splitTopD + 100);
                }
                else if (g.DpiX == 144) // 150%
                {
                    setSize(x1 + 80, y1 + 20, splitMainD, splitTopD + 450);
                }

                void setSize(int x1, int y1, int splitMainD, int splitTopD)
                {
                    CustomTabControlMain.SizeMode = TabSizeMode.Fixed;
                    CustomTabControlMain.ItemSize = new Size(x1, y1);
                    CustomTabControlSecureDNS.SizeMode = TabSizeMode.Fixed;
                    CustomTabControlSecureDNS.ItemSize = new Size(x1, y1);
                    CustomTabControlDPIBasicAdvanced.SizeMode = TabSizeMode.Fixed;
                    CustomTabControlDPIBasicAdvanced.ItemSize = new Size(x1, y1);
                    CustomTabControlSettings.SizeMode = TabSizeMode.Fixed;
                    CustomTabControlSettings.ItemSize = new Size(y1 + 9, x1);

                    SplitContainerMain.SplitterDistance = splitMainD;
                    SplitContainerTop.SplitterDistance = splitTopD;
                }
            }
        }

        private void LogAutoClear()
        {
            System.Timers.Timer logAutoClearTimer = new();
            logAutoClearTimer.Interval = 5000;
            logAutoClearTimer.Elapsed += (s, e) =>
            {
                int length = 0;
                this.InvokeIt(() => length = CustomRichTextBoxLog.Text.Length);
                if (length > 90000)
                {
                    this.InvokeIt(() => CustomRichTextBoxLog.ResetText());
                    this.InvokeIt(() => CustomRichTextBoxLog.AppendText($"Log Auto Clear.{NL}", Color.MediumSeaGreen));
                }
            };
            logAutoClearTimer.Start();
        }

        private bool IsInternetAlive()
        {
            if (!Network.IsInternetAlive())
            {
                string msgNet = "There is no Internet connectivity." + NL;
                CustomRichTextBoxLog.AppendText(msgNet, Color.IndianRed);
                return false;
            }
            else
                return true;
        }

        private void UpdateBools()
        {
            System.Timers.Timer updateBoolsTimer = new();
            updateBoolsTimer.Interval = 4000;
            updateBoolsTimer.Elapsed += (s, e) =>
            {
                // Update bool IsConnected
                IsConnected = ProcessManager.FindProcessByID(PIDDNSProxy) ||
                              ProcessManager.FindProcessByID(PIDDNSProxyBypass) ||
                              ProcessManager.FindProcessByID(PIDDNSCrypt) ||
                              ProcessManager.FindProcessByID(PIDDNSCryptBypass);

                // In case dnsproxy or dnscrypt processes terminated
                if (!IsConnected)
                {
                    IsDNSConnected = IsDoHConnected = IsConnected;
                    LocalDnsLatency = LocalDohLatency = -1;
                    if (CamouflageDNSServer != null && CamouflageDNSServer.IsRunning)
                        CamouflageDNSServer.Stop();
                    if (IsDNSSet) UnsetSavedDNS();
                }

                // Update bool IsDnsSet
                //IsDNSSet = UpdateBoolIsDnsSet(out bool _); // I need to test this on Win7 myself!

                // Update bool IsHTTPProxyRunning
                if (HTTPProxy != null)
                    IsSharing = HTTPProxy.IsRunning;
                else
                    IsSharing = false;

                // Update bool IsProxySet
                IsProxySet = UpdateBoolIsProxySet();

                // Update bool IsProxyDPIActive
                IsProxyDPIActive = (HTTPProxy != null && HTTPProxy.IsRunning && HTTPProxy.IsDpiActive);

                // Update bool IsGoodbyeDPIActive
                IsGoodbyeDPIActive = ProcessManager.FindProcessByID(PIDGoodbyeDPI);

                // Update bool IsDPIActive
                IsDPIActive = (IsProxyDPIActive || IsGoodbyeDPIActive);

            };
            updateBoolsTimer.Start();
        }

        private void UpdateBoolDnsDohAuto()
        {
            int timeout = 10000;
            System.Timers.Timer dnsLatencyTimer = new();
            dnsLatencyTimer.Interval = timeout + 500;
            dnsLatencyTimer.Elapsed += (s, e) =>
            {
                Parallel.Invoke(
                    () => UpdateBoolDnsOnce(timeout),
                    () => UpdateBoolDohOnce(timeout)
                );
            };
            dnsLatencyTimer.Start();
        }

        private void UpdateBoolDnsOnce(int timeout)
        {
            if (IsConnected)
            {
                // DNS
                LocalDnsLatency = SecureDNS.CheckDns("google.com", IPAddress.Loopback.ToString(), timeout, GetCPUPriority());
                IsDNSConnected = LocalDnsLatency != -1;

                try
                {
                    if (!string.IsNullOrEmpty(TheDll))
                        if (File.Exists(TheDll) && IsDNSConnected) File.Delete(TheDll);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }
            else
            {
                LocalDnsLatency = -1;
                IsDNSConnected = LocalDnsLatency != -1;
            }
        }

        private void UpdateBoolDohOnce(int timeout)
        {
            if (IsConnected && CustomRadioButtonSettingWorkingModeDNSandDoH.Checked)
            {
                // DoH
                string dohServer = "https://" + IPAddress.Loopback.ToString() + "/dns-query";
                LocalDohLatency = SecureDNS.CheckDns("google.com", dohServer, timeout, GetCPUPriority());
                IsDoHConnected = LocalDohLatency != -1;

                try
                {
                    if (!string.IsNullOrEmpty(TheDll))
                        if (File.Exists(TheDll) && IsDoHConnected) File.Delete(TheDll);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }
            else
            {
                LocalDohLatency = -1;
                IsDoHConnected = LocalDohLatency != -1;
            }
        }

        private bool UpdateBoolIsDnsSet(out bool isAnotherDnsSet)
        {
            isAnotherDnsSet = false;
            if (File.Exists(SecureDNS.NicNamePath))
            {
                string nicName = File.ReadAllText(SecureDNS.NicNamePath).Replace(NL, string.Empty);
                if (nicName.Length > 0)
                {
                    NetworkInterface? nic = Network.GetNICByName(nicName);
                    if (nic != null)
                    {
                        bool isDnsSet = Network.IsDnsSet(nic, out string dnsServer1, out string _);
                        if (!isDnsSet) return false; // DNS is set to DHCP
                        else
                        {
                            if (dnsServer1 == IPAddress.Loopback.ToString())
                                return true;
                            else
                            {
                                isAnotherDnsSet = true;
                                return false;
                            }
                        }
                    }
                }
            }
            return false;
        }

        private bool UpdateBoolIsProxySet()
        {
            if (IsSharing)
            {
                bool isAnyProxySet = Network.IsProxySet(out string httpProxy, out string _, out string _, out string _);
                if (isAnyProxySet)
                    if (!string.IsNullOrEmpty(httpProxy))
                        if (httpProxy.Contains(':'))
                        {
                            string[] split = httpProxy.Split(':');
                            string ip = split[0];
                            string portS = split[1];
                            bool isPortInt = int.TryParse(portS, out int port);
                            if (isPortInt)
                                if (ip == IPAddress.Loopback.ToString() && port == LastProxyPort)
                                    return true;
                        }
            }
            return false;
        }

        private void UpdateStatusShortAuto()
        {
            System.Windows.Forms.Timer timer = new();
            timer.Interval = 300;
            timer.Tick += (s, e) =>
            {
                UpdateStatusShort();
            };
            timer.Start();
        }

        private void UpdateStatusShort()
        {
            // Update Status Working Servers
            NumberOfWorkingServers = WorkingDnsList.Count;
            CustomRichTextBoxStatusWorkingServers.ResetText();
            CustomRichTextBoxStatusWorkingServers.AppendText("Working Servers: ", ForeColor);
            CustomRichTextBoxStatusWorkingServers.AppendText(NumberOfWorkingServers.ToString(), Color.DodgerBlue);

            // Check Button
            CustomButtonCheck.Enabled = !IsConnecting;

            // Insecure and parallel
            if (CustomCheckBoxInsecure.Checked)
                CustomCheckBoxCheckInParallel.Checked = false;

            // Connect Button
            if (!CustomRadioButtonConnectCheckedServers.Checked)
            {
                CustomButtonConnect.Enabled = true;
            }
            else
            {
                if (WorkingDnsList.Any() && !IsCheckingStarted && !IsConnecting)
                    CustomButtonConnect.Enabled = true;
                else
                    CustomButtonConnect.Enabled = IsConnected;
            }

            // Connect to popular servers using proxy Textbox
            CustomTextBoxHTTPProxy.Enabled = CustomRadioButtonConnectDNSCrypt.Checked;

            // SetDNS Button
            if (IsConnected && (IsDNSConnected || IsDoHConnected))
                CustomButtonSetDNS.Enabled = true;

            // SetProxy Button
            if (IsSharing)
                CustomButtonSetProxy.Enabled = true;

            // Settings -> Share -> Advanced
            CustomTextBoxSettingHTTPProxyCfCleanIP.Enabled = CustomCheckBoxSettingHTTPProxyCfCleanIP.Checked;

        }

        private void UpdateStatusLongAuto()
        {
            System.Windows.Forms.Timer timer = new();
            timer.Interval = 2000;
            timer.Tick += (s, e) =>
            {
                UpdateStatusLong();
            };
            timer.Start();
        }

        private void UpdateStatusLong()
        {
            // Update Status IsConnected
            string textConnect = IsConnected ? "Yes" : "No";
            Color colorConnect = IsConnected ? Color.MediumSeaGreen : Color.IndianRed;
            this.InvokeIt(() => CustomRichTextBoxStatusIsConnected.ResetText());
            this.InvokeIt(() => CustomRichTextBoxStatusIsConnected.AppendText("Is Connected: ", ForeColor));
            this.InvokeIt(() => CustomRichTextBoxStatusIsConnected.AppendText(textConnect, colorConnect));

            // Update Status IsDNSConnected
            string statusLocalDNS = IsDNSConnected ? "Online" : "Offline";
            Color colorStatusLocalDNS = IsDNSConnected ? Color.MediumSeaGreen : Color.IndianRed;
            this.InvokeIt(() => CustomRichTextBoxStatusLocalDNS.ResetText());
            this.InvokeIt(() => CustomRichTextBoxStatusLocalDNS.AppendText("Local DNS: ", ForeColor));
            this.InvokeIt(() => CustomRichTextBoxStatusLocalDNS.AppendText(statusLocalDNS, colorStatusLocalDNS));

            // Update Status LocalDnsLatency
            string statusLocalDnsLatency = LocalDnsLatency != -1 ? $"{LocalDnsLatency}" : "-1";
            Color colorStatusLocalDnsLatency = LocalDnsLatency != -1 ? Color.MediumSeaGreen : Color.IndianRed;
            this.InvokeIt(() => CustomRichTextBoxStatusLocalDnsLatency.ResetText());
            this.InvokeIt(() => CustomRichTextBoxStatusLocalDnsLatency.AppendText("Local DNS Latency: ", ForeColor));
            this.InvokeIt(() => CustomRichTextBoxStatusLocalDnsLatency.AppendText(statusLocalDnsLatency, colorStatusLocalDnsLatency));

            // Update Status IsDoHConnected
            string statusLocalDoH = IsDoHConnected ? "Online" : "Offline";
            Color colorStatusLocalDoH = IsDoHConnected ? Color.MediumSeaGreen : Color.IndianRed;
            this.InvokeIt(() => CustomRichTextBoxStatusLocalDoH.ResetText());
            this.InvokeIt(() => CustomRichTextBoxStatusLocalDoH.AppendText("Local DoH: ", ForeColor));
            this.InvokeIt(() => CustomRichTextBoxStatusLocalDoH.AppendText(statusLocalDoH, colorStatusLocalDoH));

            // Update Status LocalDohLatency
            string statusLocalDoHLatency = LocalDohLatency != -1 ? $"{LocalDohLatency}" : "-1";
            Color colorStatusLocalDoHLatency = LocalDohLatency != -1 ? Color.MediumSeaGreen : Color.IndianRed;
            this.InvokeIt(() => CustomRichTextBoxStatusLocalDoHLatency.ResetText());
            this.InvokeIt(() => CustomRichTextBoxStatusLocalDoHLatency.AppendText("Local DoH Latency: ", ForeColor));
            this.InvokeIt(() => CustomRichTextBoxStatusLocalDoHLatency.AppendText(statusLocalDoHLatency, colorStatusLocalDoHLatency));

            // Update Status IsDnsSet
            string textDNS = IsDNSSet ? "Yes" : "No";
            Color colorDNS = IsDNSSet ? Color.MediumSeaGreen : Color.IndianRed;
            this.InvokeIt(() => CustomRichTextBoxStatusIsDNSSet.ResetText());
            this.InvokeIt(() => CustomRichTextBoxStatusIsDNSSet.AppendText("Is DNS Set: ", ForeColor));
            this.InvokeIt(() => CustomRichTextBoxStatusIsDNSSet.AppendText(textDNS, colorDNS));

            // Update Status IsSharing
            string textSharing = IsSharing ? "Yes" : "No";
            Color colorSharing = IsSharing ? Color.MediumSeaGreen : Color.IndianRed;
            this.InvokeIt(() => CustomRichTextBoxStatusIsSharing.ResetText());
            this.InvokeIt(() => CustomRichTextBoxStatusIsSharing.AppendText("Is Sharing: ", ForeColor));
            this.InvokeIt(() => CustomRichTextBoxStatusIsSharing.AppendText(textSharing, colorSharing));

            // Update Status ProxyRequests
            string textProxyRequests = "0 of 0";
            Color colorProxyRequests = Color.MediumSeaGreen;
            if (HTTPProxy != null)
            {
                textProxyRequests = $"{HTTPProxy.ActiveRequests} of {HTTPProxy.MaxRequests}";
                colorProxyRequests = HTTPProxy.ActiveRequests < HTTPProxy.MaxRequests ? Color.MediumSeaGreen : Color.IndianRed;
            }
            this.InvokeIt(() => CustomRichTextBoxStatusProxyRequests.ResetText());
            this.InvokeIt(() => CustomRichTextBoxStatusProxyRequests.AppendText("Proxy Requests ", ForeColor));
            this.InvokeIt(() => CustomRichTextBoxStatusProxyRequests.AppendText(textProxyRequests, colorProxyRequests));

            // Update Status IsProxySet
            string textProxySet = IsProxySet ? "Yes" : "No";
            Color colorProxySet = IsProxySet ? Color.MediumSeaGreen : Color.IndianRed;
            this.InvokeIt(() => CustomRichTextBoxStatusIsProxySet.ResetText());
            this.InvokeIt(() => CustomRichTextBoxStatusIsProxySet.AppendText("Is Proxy Set: ", ForeColor));
            this.InvokeIt(() => CustomRichTextBoxStatusIsProxySet.AppendText(textProxySet, colorProxySet));

            // Update Status IsProxyDPIActive
            string textProxyDPI = IsProxyDPIActive ? "Active" : "Inactive";
            Color colorProxyDPI = IsProxyDPIActive ? Color.MediumSeaGreen : Color.IndianRed;
            this.InvokeIt(() => CustomRichTextBoxStatusProxyDpiBypass.ResetText());
            this.InvokeIt(() => CustomRichTextBoxStatusProxyDpiBypass.AppendText("Proxy DPI Bypass: ", ForeColor));
            this.InvokeIt(() => CustomRichTextBoxStatusProxyDpiBypass.AppendText(textProxyDPI, colorProxyDPI));

            // Update Status IsGoodbyeDPIActive
            string textGoodbyeDPI = IsGoodbyeDPIActive ? "Active" : "Inactive";
            Color colorGoodbyeDPI = IsGoodbyeDPIActive ? Color.MediumSeaGreen : Color.IndianRed;
            this.InvokeIt(() => CustomRichTextBoxStatusGoodbyeDPI.ResetText());
            this.InvokeIt(() => CustomRichTextBoxStatusGoodbyeDPI.AppendText("GoodbyeDPI: ", ForeColor));
            this.InvokeIt(() => CustomRichTextBoxStatusGoodbyeDPI.AppendText(textGoodbyeDPI, colorGoodbyeDPI));

            // Play Audio Alert
            if (!CustomCheckBoxSettingDisableAudioAlert.Checked && !IsCheckingStarted)
            {
                if (!StopWatchAudioAlertDelay.IsRunning) StopWatchAudioAlertDelay.Start();
                if (StopWatchAudioAlertDelay.ElapsedMilliseconds > 5000)
                    PlayAudioAlert();
            }
        }

        private void PlayAudioAlert()
        {
            if ((IsDNSConnected || IsDoHConnected) && AudioAlertOnline)
            {
                AudioAlertOnline = false;
                AudioAlertOffline = true;

                Task.Run(() =>
                {
                    SoundPlayer soundPlayer = new(Audio.Resource1.DNS_Online);
                    soundPlayer.PlaySync();
                    soundPlayer.Stop();
                    soundPlayer.Dispose();
                });
            }

            if (!IsDNSConnected && !IsDoHConnected && AudioAlertOffline)
            {
                AudioAlertOffline = false;
                AudioAlertOnline = true;

                int softEtherPID = -1;
                if (ProcessManager.FindProcessByName("vpnclient_x64"))
                    softEtherPID = ProcessManager.GetFirstPIDByName("vpnclient_x64");

                if (softEtherPID != -1)
                    ProcessManager.SuspendProcess(softEtherPID); // On net disconnect SoftEther cause noise to audio.

                Task.Run(() =>
                {
                    Task.Delay(1000).Wait();
                    SoundPlayer soundPlayer = new(Audio.Resource1.DNS_Offline);
                    soundPlayer.PlaySync();
                    soundPlayer.Stop();
                    soundPlayer.Dispose();
                    Task.Delay(5000).Wait();
                });
                
                if (softEtherPID != -1)
                    ProcessManager.ResumeProcess(softEtherPID);
            }
            
            if (HTTPProxy != null)
            {
                if (HTTPProxy.IsRunning && (HTTPProxy.ActiveRequests >= HTTPProxy.MaxRequests) && !AudioAlertRequestsExceeded)
                {
                    AudioAlertRequestsExceeded = true;
                    Task.Run(() =>
                    {
                        SoundPlayer soundPlayer = new(Audio.Resource1.Warning_Handle_Requests_Exceeded);
                        soundPlayer.PlaySync();
                        soundPlayer.Stop();
                        soundPlayer.Dispose();
                    });
                }

                if (HTTPProxy.ActiveRequests < HTTPProxy.MaxRequests - 5)
                    AudioAlertRequestsExceeded = false;
            }

            StopWatchAudioAlertDelay.Stop();
            StopWatchAudioAlertDelay.Reset();
        }

        private void FlushDNS(bool writeToLog = true)
        {
            string? flushDNS = ProcessManager.Execute("ipconfig", "/flushdns");
            if (!string.IsNullOrWhiteSpace(flushDNS) && writeToLog)
            {
                // Write flush DNS message to log
                this.InvokeIt(() => CustomRichTextBoxLog.AppendText(flushDNS + NL, Color.LightGray));
            }
        }

        private static void FlushDnsOnExit()
        {
            ProcessManager.Execute("ipconfig", "/flushdns", true, true);
            ProcessManager.Execute("ipconfig", "/registerdns", true, true);
            ProcessManager.Execute("ipconfig", "/release", true, true);
            ProcessManager.ExecuteOnly("ipconfig", "/renew", true, true);
            //ProcessManager.Execute("netsh", "winsock reset"); // Needs PC Restart
        }

        private void KillAll()
        {
            if (ProcessManager.FindProcessByName("dnslookup"))
                ProcessManager.KillProcessByName("dnslookup");
            if (ProcessManager.FindProcessByName("dnsproxy"))
                ProcessManager.KillProcessByName("dnsproxy");
            if (ProcessManager.FindProcessByName("dnscrypt-proxy"))
                ProcessManager.KillProcessByName("dnscrypt-proxy");
            if (ProcessManager.FindProcessByName("goodbyedpi"))
                ProcessManager.KillProcessByName("goodbyedpi");
            // Unset DNS
            UnsetSavedDNS();
        }

        private void UnsetSavedDNS()
        {
            bool unsetToDHCP = CustomRadioButtonSettingUnsetDnsToDhcp.Checked;
            if (unsetToDHCP)
            {
                // Unset to DHCP
                UnsetSavedDnsDHCP();
            }
            else
            {
                // Unset to Static
                string dns1 = CustomTextBoxSettingUnsetDns1.Text;
                string dns2 = CustomTextBoxSettingUnsetDns2.Text;
                UnsetSavedDnsStatic(dns1, dns2);
            }
        }

        // Unset to DHCP
        private void UnsetSavedDnsDHCP()
        {
            if (File.Exists(SecureDNS.NicNamePath))
            {
                string nicName = File.ReadAllText(SecureDNS.NicNamePath).Replace(NL, string.Empty);
                if (nicName.Length > 0)
                {
                    NetworkInterface? nic = Network.GetNICByName(nicName);
                    if (nic != null)
                    {
                        Network.UnsetDNS(nic);
                        IsDNSSet = false;
                    }
                }
            }
        }

        // Unset to Static
        private void UnsetSavedDnsStatic(string dns1, string dns2)
        {
            if (File.Exists(SecureDNS.NicNamePath))
            {
                string nicName = File.ReadAllText(SecureDNS.NicNamePath).Replace(NL, string.Empty);
                if (nicName.Length > 0)
                {
                    NetworkInterface? nic = Network.GetNICByName(nicName);
                    if (nic != null)
                    {
                        Network.UnsetDNS(nic, dns1, dns2);
                        IsDNSSet = false;
                    }
                }
            }
        }

        private ProcessPriorityClass GetCPUPriority()
        {
            if (CustomRadioButtonSettingCPUHigh.Checked)
                return ProcessPriorityClass.High;
            else if (CustomRadioButtonSettingCPUAboveNormal.Checked)
                return ProcessPriorityClass.AboveNormal;
            else if (CustomRadioButtonSettingCPUNormal.Checked)
                return ProcessPriorityClass.Normal;
            else if (CustomRadioButtonSettingCPUBelowNormal.Checked)
                return ProcessPriorityClass.BelowNormal;
            else if (CustomRadioButtonSettingCPULow.Checked)
                return ProcessPriorityClass.Idle;
            else
                return ProcessPriorityClass.Normal;
        }

        public bool CheckNecessaryFiles(bool showMessage = true)
        {
            if (!File.Exists(SecureDNS.DnsLookup) || !File.Exists(SecureDNS.DnsProxy) || !File.Exists(SecureDNS.DNSCrypt) ||
                !File.Exists(SecureDNS.DNSCryptConfigPath) || !File.Exists(SecureDNS.DNSCryptConfigCloudflarePath) ||
                !File.Exists(SecureDNS.GoodbyeDpi) || !File.Exists(SecureDNS.WinDivert) ||
                !File.Exists(SecureDNS.WinDivert32) || !File.Exists(SecureDNS.WinDivert64))
            {
                if (showMessage)
                {
                    string msg = "ERROR: Some of binary files are missing!" + NL;
                    CustomRichTextBoxLog.AppendText(msg, Color.IndianRed);
                }
                return false;
            }
            else
                return true;
        }

        private async Task WriteNecessaryFilesToDisk()
        {
            // Get New Versions
            string dnslookupNewVer = SecureDNS.GetBinariesVersionFromResource("dnslookup");
            string dnsproxyNewVer = SecureDNS.GetBinariesVersionFromResource("dnsproxy");
            string dnscryptNewVer = SecureDNS.GetBinariesVersionFromResource("dnscrypt-proxy");
            string goodbyedpiNewVer = SecureDNS.GetBinariesVersionFromResource("goodbyedpi");

            // Get Old Versions
            string dnslookupOldVer = SecureDNS.GetBinariesVersion("dnslookup");
            string dnsproxyOldVer = SecureDNS.GetBinariesVersion("dnsproxy");
            string dnscryptOldVer = SecureDNS.GetBinariesVersion("dnscrypt-proxy");
            string goodbyedpiOldVer = SecureDNS.GetBinariesVersion("goodbyedpi");

            // Get Version Result
            int dnslookupResult = Info.VersionCompare(dnslookupNewVer, dnslookupOldVer);
            int dnsproxyResult = Info.VersionCompare(dnsproxyNewVer, dnsproxyOldVer);
            int dnscryptResult = Info.VersionCompare(dnscryptNewVer, dnscryptOldVer);
            int goodbyedpiResult = Info.VersionCompare(goodbyedpiNewVer, goodbyedpiOldVer);

            // Check Missing/Update Binaries
            if (!CheckNecessaryFiles(false) || dnslookupResult == 1 || dnsproxyResult == 1 || dnscryptResult == 1 || goodbyedpiResult == 1)
            {
                string msg1 = "Creating/Updating binaries. Please Wait..." + NL;
                CustomRichTextBoxLog.AppendText(msg1, Color.LightGray);

                await writeBinariesAsync();
            }

            async Task writeBinariesAsync()
            {
                if (!Directory.Exists(SecureDNS.BinaryDirPath))
                    Directory.CreateDirectory(SecureDNS.BinaryDirPath);

                if (!File.Exists(SecureDNS.DnsLookup) || dnslookupResult == 1)
                    await Resource.WriteResourceToFileAsync(NecessaryFiles.Resource1.dnslookup, SecureDNS.DnsLookup);

                if (!File.Exists(SecureDNS.DnsProxy) || dnsproxyResult == 1)
                    await Resource.WriteResourceToFileAsync(NecessaryFiles.Resource1.dnsproxy, SecureDNS.DnsProxy);

                if (!File.Exists(SecureDNS.DNSCrypt) || dnscryptResult == 1)
                    await Resource.WriteResourceToFileAsync(NecessaryFiles.Resource1.dnscrypt_proxyEXE, SecureDNS.DNSCrypt);

                if (!File.Exists(SecureDNS.DNSCryptConfigPath))
                    await Resource.WriteResourceToFileAsync(NecessaryFiles.Resource1.dnscrypt_proxyTOML, SecureDNS.DNSCryptConfigPath);

                if (!File.Exists(SecureDNS.DNSCryptConfigCloudflarePath))
                    await Resource.WriteResourceToFileAsync(NecessaryFiles.Resource1.dnscrypt_proxy_cloudflareTOML, SecureDNS.DNSCryptConfigCloudflarePath);

                if (!File.Exists(SecureDNS.GoodbyeDpi) || goodbyedpiResult == 1)
                    await Resource.WriteResourceToFileAsync(NecessaryFiles.Resource1.goodbyedpi, SecureDNS.GoodbyeDpi);

                if (!File.Exists(SecureDNS.WinDivert))
                    await Resource.WriteResourceToFileAsync(NecessaryFiles.Resource1.WinDivert, SecureDNS.WinDivert);

                if (!File.Exists(SecureDNS.WinDivert32))
                    await Resource.WriteResourceToFileAsync(NecessaryFiles.Resource1.WinDivert32, SecureDNS.WinDivert32);

                if (!File.Exists(SecureDNS.WinDivert64))
                    await Resource.WriteResourceToFileAsync(NecessaryFiles.Resource1.WinDivert64, SecureDNS.WinDivert64);

                // Update old version numbers
                await File.WriteAllTextAsync(SecureDNS.BinariesVersionPath, NecessaryFiles.Resource1.versions);

                string msg2 = $"{Info.InfoExecutingAssembly.ProductName} is ready.{NL}";
                CustomRichTextBoxLog.AppendText(msg2, Color.LightGray);
            }
        }

        private void GenerateCertificate()
        {
            // Create certificate directory
            FileDirectory.CreateEmptyDirectory(SecureDNS.CertificateDirPath);
            string issuerSubjectName = SecureDNS.CertIssuerSubjectName;
            string subjectName = SecureDNS.CertSubjectName;

            // Generate certificate
            if (!File.Exists(SecureDNS.IssuerCertPath) || !File.Exists(SecureDNS.CertPath) || !File.Exists(SecureDNS.KeyPath))
            {
                IPAddress? gateway = Network.GetDefaultGateway();
                if (gateway != null)
                {
                    CertificateTool.GenerateCertificate(SecureDNS.CertificateDirPath, gateway, issuerSubjectName, subjectName);
                    CertificateTool.CreateP12(SecureDNS.IssuerCertPath, SecureDNS.IssuerKeyPath);
                    CertificateTool.CreateP12(SecureDNS.CertPath, SecureDNS.KeyPath);
                }
            }

            // Install certificate
            if (File.Exists(SecureDNS.IssuerCertPath) && !CustomCheckBoxSettingDontAskCertificate.Checked)
            {
                bool certInstalled = CertificateTool.InstallCertificate(SecureDNS.IssuerCertPath, StoreName.Root, StoreLocation.CurrentUser);
                if (!certInstalled)
                {
                    string msg = "Local DoH Server doesn't work without certificate.\nYou can remove certificate anytime from Windows.\nTry again?";
                    using (new CenterWinDialog(this))
                    {
                        DialogResult dr = CustomMessageBox.Show(msg, "Certificate", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                        if (dr == DialogResult.Yes)
                            CertificateTool.InstallCertificate(SecureDNS.IssuerCertPath, StoreName.Root, StoreLocation.CurrentUser);
                    }
                }
            }
        }

        private void UninstallCertificate()
        {
            string issuerSubjectName = SecureDNS.CertIssuerSubjectName.Replace("CN=", string.Empty);
            bool isCertInstalled = CertificateTool.IsCertificateInstalled(issuerSubjectName, StoreName.Root, StoreLocation.CurrentUser);
            if (isCertInstalled)
            {
                if (IsDoHConnected)
                {
                    string msg = "Disconnect local DoH first.";
                    CustomMessageBox.Show(msg, "Certificate", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Uninstall Certs
                CertificateTool.UninstallCertificate(issuerSubjectName, StoreName.Root, StoreLocation.CurrentUser);

                // Delete Cert Files
                try
                {
                    Directory.Delete(SecureDNS.CertificateDirPath, true);
                }
                catch (Exception)
                {
                    // do nothing
                }
            }
            else
            {
                string msg = "Certificate is already uninstalled.";
                CustomMessageBox.Show(msg, "Certificate", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private static bool IsDnsProtocolSupported(string dns)
        {
            dns = dns.Trim();
            StringComparison sc = StringComparison.OrdinalIgnoreCase;
            if (dns.StartsWith("tcp://", sc) || dns.StartsWith("udp://", sc) || dns.StartsWith("http://", sc) || dns.StartsWith("https://", sc) ||
                dns.StartsWith("tls://", sc) || dns.StartsWith("quic://", sc) || dns.StartsWith("h3://", sc) || dns.StartsWith("sdns://", sc))
                return true;
            else
                return isPlainDnsWithUnusualPort(dns);

            static bool isPlainDnsWithUnusualPort(string dns) // Support for plain DNS with unusual port
            {
                if (dns.Contains(':'))
                {
                    string[] split = dns.Split(':');
                    string ip = split[0];
                    string port = split[1];
                    if (Network.IsIPv4Valid(ip, out IPAddress _))
                    {
                        bool isPortValid = int.TryParse(port, out int outPort);
                        if (isPortValid && outPort >= 1 && outPort <= 65535)
                            return true;
                    }
                }
                return false;
            }
        }

        //============================== Check
        private void StartCheck()
        {
            // Return if binary files are missing
            if (!CheckNecessaryFiles()) return;

            if (!IsCheckingStarted)
            {
                // Start Checking
                // Check Internet Connectivity
                if (!IsInternetAlive()) return;

                // Unset DNS if it's not connected before checking.
                if (!IsConnected)
                {
                    if (IsDNSSet)
                        SetDNS(); // Unset DNS
                    else
                        UnsetSavedDNS();
                }

                try
                {
                    Task task = Task.Run(async () =>
                    {
                        IsCheckingStarted = true;
                        IsCheckDone = false;
                        await CheckServers();
                    });

                    task.ContinueWith(_ =>
                    {
                        // Save working servers to file
                        if (!CustomRadioButtonBuiltIn.Checked && WorkingDnsListToFile.Any())
                        {
                            WorkingDnsListToFile = WorkingDnsListToFile.RemoveDuplicates();
                            WorkingDnsListToFile.SaveToFile(SecureDNS.WorkingServersPath);
                        }

                        IsCheckingStarted = false;
                        IsCheckDone = true;

                        string msg = NL + "Check operation finished." + NL;
                        CustomRichTextBoxLog.AppendText(msg, Color.DodgerBlue);
                        CustomButtonCheck.Enabled = true;

                        // Go to Connect Tab if it's not already connected
                        if (ConnectAllClicked && !IsConnected && NumberOfWorkingServers > 0)
                        {
                            this.InvokeIt(() => CustomTabControlMain.SelectedIndex = 0);
                            this.InvokeIt(() => CustomTabControlSecureDNS.SelectedIndex = 1);
                        }
                        Debug.WriteLine("Checking Task: " + task.Status);
                        StopChecking = false;
                    }, TaskScheduler.FromCurrentSynchronizationContext());
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show(ex.Message, "Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                // Stop Checking
                StopChecking = true;
                this.InvokeIt(() => CustomButtonCheck.Enabled = false);
            }
        }

        //============================== Saved Servers

        private async Task WriteSavedServersDelayToLog()
        {
            List<string> savedDnsList = SavedDnsList.ToList();
            if (savedDnsList.Any() && !IsCheckingStarted && !IsConnecting && !IsDisconnecting)
            {
                if (savedDnsList.Count > 1)
                {
                    // Get blocked domain
                    string blockedDomain = GetBlockedDomainSetting(out string blockedDomainNoWww);
                    if (string.IsNullOrEmpty(blockedDomain)) return;

                    // Get Check timeout value
                    decimal timeoutSec = 1;
                    this.InvokeIt(() => timeoutSec = CustomNumericUpDownSettingCheckTimeout.Value);
                    int timeoutMS = decimal.ToInt32(timeoutSec * 1000);

                    // Get Bootstrap IP and Port
                    string bootstrap = GetBootstrapSetting(out int bootstrapPort).ToString();

                    // Add start msg
                    string msgStart = $"{NL}Contains {savedDnsList.Count} servers:{NL}";
                    this.InvokeIt(() => CustomRichTextBoxLog.AppendText(msgStart, Color.LightGray));

                    // Get insecure state
                    bool insecure = CustomCheckBoxInsecure.Checked;
                    int localPortInsecure = 5390;
                    if (insecure)
                    {
                        // Check open ports
                        bool isPortOk = GetListeningPort(localPortInsecure, "You need to resolve the conflict.", Color.IndianRed);
                        if (!isPortOk) return;

                        int nt = 1;

                        for (int n = 0; n < savedDnsList.Count; n++)
                        {
                            if (IsCheckingStarted || IsConnecting || IsDisconnecting) break;
                            await getDelay(n, nt++);
                        }
                    }
                    else
                    {
                        await Task.Run(async () =>
                        {
                            var lists = savedDnsList.SplitToLists(3);
                            int nt = 1;

                            for (int n = 0; n < lists.Count; n++)
                            {
                                if (IsCheckingStarted || IsConnecting || IsDisconnecting) break;

                                List<string> list = lists[n];
                                var parallelLoopResult = Parallel.For(0, list.Count, async i =>
                                {
                                    await getDelay(i, nt++);
                                });

                                await Task.Run(async () =>
                                {
                                    while (!parallelLoopResult.IsCompleted)
                                    {
                                        if (parallelLoopResult.IsCompleted)
                                            return Task.CompletedTask;
                                        await Task.Delay(500);
                                    }
                                    return Task.CompletedTask;
                                });
                            }
                        });
                    }

                    async Task getDelay(int n, int nt)
                    {
                        string dns = savedDnsList[n];

                        // Get Status and Latency
                        int latency = -1;
                        if (insecure)
                            latency = SecureDNS.CheckDns(true, blockedDomainNoWww, dns, timeoutMS, localPortInsecure, bootstrap, bootstrapPort, GetCPUPriority());
                        else
                            latency = SecureDNS.CheckDns(blockedDomainNoWww, dns, timeoutMS, GetCPUPriority());

                        string msg = $"DNS {nt}: {latency} ms.{NL}";
                        Color color = (latency == -1) ? Color.IndianRed : Color.MediumSeaGreen;
                        this.InvokeIt(() => CustomRichTextBoxLog.AppendText(msg, color));
                        await Task.Delay(10);
                    }
                }
                else
                {
                    string msg = $"There is only one saved server.{NL}";
                    this.InvokeIt(() => CustomRichTextBoxLog.AppendText(msg, Color.MediumSeaGreen));
                }
            }
            else
            {
                string msg = $"There is no saved server.{NL}";
                this.InvokeIt(() => CustomRichTextBoxLog.AppendText(msg, Color.MediumSeaGreen));
            }
        }

        private async void SavedDnsLoad()
        {
            await Task.Run(async () =>
            {
                FileDirectory.CreateEmptyFile(SecureDNS.SavedEncodedDnsPath);

                List<string> savedEncodedDnsList = new();
                savedEncodedDnsList.LoadFromFile(SecureDNS.SavedEncodedDnsPath, true, true);
                savedEncodedDnsList.RemoveDuplicates();

                if (savedEncodedDnsList.Any())
                {
                    // Built-in or Custom
                    bool builtInMode = CustomRadioButtonBuiltIn.Checked;

                    string? fileContent = string.Empty;
                    if (builtInMode)
                        fileContent = await Resource.GetResourceTextFileAsync("SecureDNSClient.DNS-Servers.txt");
                    else
                    {
                        FileDirectory.CreateEmptyFile(SecureDNS.CustomServersPath);
                        fileContent = await File.ReadAllTextAsync(SecureDNS.CustomServersPath);
                    }

                    if (!string.IsNullOrEmpty(fileContent) && !string.IsNullOrWhiteSpace(fileContent))
                    {
                        List<string> dnsList = fileContent.SplitToLines();

                        for (int n = 0; n < savedEncodedDnsList.Count; n++)
                        {
                            string encodedDns = savedEncodedDnsList[n];
                            for (int i = 0; i < dnsList.Count; i++)
                            {
                                string dns = dnsList[i];
                                if (EncodingTool.GetSHA512(dns).Equals(encodedDns))
                                {
                                    SavedDnsList.Add(dns);
                                    WorkingDnsList.Add(new Tuple<long, string>(100, dns));
                                    break;
                                }
                            }
                        }

                        WorkingDnsList.RemoveDuplicates(); // not important
                        SavedDnsList.RemoveDuplicates();
                    }
                }

                SavedDnsUpdateAuto();
            });
        }

        private async void SavedDnsUpdateAuto()
        {
            await SavedDnsUpdate();
            System.Timers.Timer savedDnsUpdateTimer = new();
            savedDnsUpdateTimer.Interval = TimeSpan.FromMinutes(60).TotalMilliseconds;
            savedDnsUpdateTimer.Elapsed += async (s, e) =>
            {
                await SavedDnsUpdate();
            };
            savedDnsUpdateTimer.Start();
        }

        private async Task SavedDnsUpdate()
        {
            // Update Working Servers?
            bool updateWorkingServers = true;

            // Get blocked domain
            string blockedDomain = GetBlockedDomainSetting(out string blockedDomainNoWww);
            if (string.IsNullOrEmpty(blockedDomain)) return;

            // Get Check timeout value
            decimal timeoutSec = 1;
            this.InvokeIt(() => timeoutSec = CustomNumericUpDownSettingCheckTimeout.Value);
            int timeoutMS = decimal.ToInt32(timeoutSec * 1000);

            // Get Bootstrap IP and Port
            string bootstrap = GetBootstrapSetting(out int bootstrapPort).ToString();

            // Get insecure state
            bool insecure = CustomCheckBoxInsecure.Checked;
            int localPortInsecure = 5390;

            // Get number of max servers
            int maxServers = decimal.ToInt32(CustomNumericUpDownSettingMaxServers.Value);

            List<string> newSavedDnsList = new();
            List<string> newSavedEncodedDnsList = new();
            List<Tuple<long, string>> newWorkingDnsList = new();

            // Check saved dns servers can work
            if (SavedDnsList.Any())
            {
                List<string> savedDnsList = SavedDnsList.ToList();

                int latency = -1;

                if (insecure)
                {
                    int origPort = localPortInsecure;
                    bool isPortOpen1 = Network.IsPortOpen(IPAddress.Loopback.ToString(), localPortInsecure, 3);
                    if (isPortOpen1)
                    {
                        localPortInsecure = Network.GetNextPort(localPortInsecure);
                        bool isPortOpen2 = Network.IsPortOpen(IPAddress.Loopback.ToString(), localPortInsecure, 3);
                        if (isPortOpen2)
                        {
                            localPortInsecure = Network.GetNextPort(localPortInsecure);
                            bool isPortOpen3 = Network.IsPortOpen(IPAddress.Loopback.ToString(), localPortInsecure, 3);
                            if (isPortOpen3)
                            {
                                localPortInsecure = Network.GetNextPort(localPortInsecure);
                                bool isPortOpen4 = Network.IsPortOpen(IPAddress.Loopback.ToString(), localPortInsecure, 3);
                                if (isPortOpen4)
                                {
                                    string existingProcessName = ProcessManager.GetProcessNameByListeningPort(origPort);
                                    existingProcessName = existingProcessName == string.Empty ? "Unknown" : existingProcessName;
                                    string msg = $"Cannot auto update Saved DNS List, port {origPort} is occupied by \"{existingProcessName}\".{NL}";
                                    this.InvokeIt(() => CustomRichTextBoxLog.AppendText(msg, Color.IndianRed));
                                    return;
                                }
                            }
                        }
                    }

                    for (int n = 0; n < savedDnsList.Count; n++)
                    {
                        if (IsCheckingStarted) break;

                        string dns = savedDnsList[n];
                        latency = SecureDNS.CheckDns(true, blockedDomainNoWww, dns, timeoutMS, localPortInsecure, bootstrap, bootstrapPort, GetCPUPriority());
                        if (latency != -1)
                        {
                            newSavedDnsList.Add(dns);
                            newSavedEncodedDnsList.Add(EncodingTool.GetSHA512(dns));

                            if (updateWorkingServers)
                                newWorkingDnsList.Add(new Tuple<long, string>(latency, dns));
                        }
                    }
                }
                else
                {
                    // If Secure
                    await Task.Run(async () =>
                    {
                        var lists = savedDnsList.SplitToLists(3);

                        for (int n = 0; n < lists.Count; n++)
                        {
                            if (IsCheckingStarted) break;

                            List<string> list = lists[n];
                            var parallelLoopResult = Parallel.For(0, list.Count, i =>
                            {
                                string dns = list[i];
                                latency = SecureDNS.CheckDns(blockedDomainNoWww, dns, timeoutMS, GetCPUPriority());
                                if (latency != -1)
                                {
                                    newSavedDnsList.Add(dns);
                                    newSavedEncodedDnsList.Add(EncodingTool.GetSHA512(dns));

                                    if (updateWorkingServers)
                                        newWorkingDnsList.Add(new Tuple<long, string>(latency, dns));
                                }
                            });

                            await Task.Run(async () =>
                            {
                                while (!parallelLoopResult.IsCompleted)
                                {
                                    if (parallelLoopResult.IsCompleted)
                                        return Task.CompletedTask;
                                    await Task.Delay(500);
                                }
                                return Task.CompletedTask;
                            });
                        }
                    });
                }
            }

            if (newSavedDnsList.Any())
            {
                SavedDnsList.Clear();
                SavedDnsList = newSavedDnsList.ToList();

                SavedEncodedDnsList.Clear();
                SavedEncodedDnsList = newSavedEncodedDnsList.ToList();
                SavedEncodedDnsList.SaveToFile(SecureDNS.SavedEncodedDnsPath);

                if (updateWorkingServers && !IsCheckingStarted)
                {
                    // Add
                    WorkingDnsList = WorkingDnsList.Concat(newWorkingDnsList).ToList();

                    // Remove Duplicates
                    WorkingDnsList = WorkingDnsList.DistinctBy(x => x.Item2).ToList();
                }

                if (newSavedDnsList.Count >= maxServers)
                    return;
            }

            // There is not enough working server lets find some
            // Built-in or Custom
            bool builtInMode = CustomRadioButtonBuiltIn.Checked;

            string? fileContent = string.Empty;
            if (builtInMode)
                fileContent = await Resource.GetResourceTextFileAsync("SecureDNSClient.DNS-Servers.txt");
            else
            {
                FileDirectory.CreateEmptyFile(SecureDNS.CustomServersPath);
                fileContent = await File.ReadAllTextAsync(SecureDNS.CustomServersPath);
            }

            if (string.IsNullOrEmpty(fileContent) || string.IsNullOrWhiteSpace(fileContent)) return;

            List<string> dnsList = fileContent.SplitToLines();

            int currentServers = newSavedDnsList.Count;
            for (int n = 0; n < dnsList.Count; n++)
            {
                if (IsCheckingStarted) break;

                string dns = dnsList[n].Trim();
                if (!string.IsNullOrEmpty(dns) && !string.IsNullOrWhiteSpace(dns))
                {
                    if (IsDnsProtocolSupported(dns))
                    {
                        // Get DNS Details
                        DnsReader dnsReader = new(dns, null);

                        // Apply Protocol Selection
                        bool matchRules = CheckDnsMatchRules(dnsReader);
                        if (!matchRules) continue;

                        // Get Status and Latency
                        bool dnsOK = false;
                        int latency = -1;

                        if (insecure)
                            latency = SecureDNS.CheckDns(true, blockedDomainNoWww, dns, timeoutMS, localPortInsecure, bootstrap, bootstrapPort, GetCPUPriority());
                        else
                            latency = SecureDNS.CheckDns(blockedDomainNoWww, dns, timeoutMS, GetCPUPriority());

                        dnsOK = latency != -1;

                        if (dnsOK)
                        {
                            if (!newSavedDnsList.Contains(dns))
                            {
                                newSavedDnsList.Add(dns);
                                newSavedEncodedDnsList.Add(EncodingTool.GetSHA512(dns));

                                if (updateWorkingServers)
                                    newWorkingDnsList.Add(new Tuple<long, string>(latency, dns));

                                currentServers++;
                                if (currentServers >= maxServers) break;
                            }
                        }
                    }
                }
            }

            if (newSavedDnsList.Any())
            {
                SavedDnsList.Clear();
                SavedDnsList = newSavedDnsList.ToList();

                SavedEncodedDnsList.Clear();
                SavedEncodedDnsList = newSavedEncodedDnsList.ToList();
                SavedEncodedDnsList.SaveToFile(SecureDNS.SavedEncodedDnsPath);

                if (updateWorkingServers && !IsCheckingStarted)
                {
                    // Add
                    WorkingDnsList = WorkingDnsList.Concat(newWorkingDnsList).ToList();

                    // Remove Duplicates
                    WorkingDnsList = WorkingDnsList.DistinctBy(x => x.Item2).ToList();
                }
                
                return;
            }
        }

        //============================== Connect

        private async void StartConnect()
        {
            // Return if binary files are missing
            if (!CheckNecessaryFiles()) return;

            if (!ProcessManager.FindProcessByName("dnsproxy") && !ProcessManager.FindProcessByName("dnscrypt-proxy") && !IsConnecting)
            {
                try
                {
                    // Connect
                    // Check Internet Connectivity
                    if (!IsInternetAlive()) return;

                    // Update NICs
                    SecureDNS.UpdateNICs(CustomComboBoxNICs);

                    Task taskConnect = Task.Run(async () =>
                    {
                        // Stop Check
                        if (IsCheckingStarted)
                        {
                            CustomButtonCheck_Click(null, null);

                            // Wait until check is done
                            while (!IsCheckDone)
                                Task.Delay(100).Wait();
                        }

                        IsConnecting = true;
                        await Connect();
                    });

                    await taskConnect.ContinueWith(_ =>
                    {
                        IsConnecting = false;
                        Debug.WriteLine("Connect Task: " + taskConnect.Status);
                    }, TaskScheduler.FromCurrentSynchronizationContext());
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show(ex.Message, "Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                try
                {
                    // Disconnect
                    IsDisconnecting = true;

                    // Write Disconnecting message to log
                    string msgDisconnecting = NL + "Disconnecting..." + NL;
                    this.InvokeIt(() => CustomRichTextBoxLog.AppendText(msgDisconnecting, Color.MediumSeaGreen));

                    Task taskWait1 = await Task.Run(async () =>
                    {
                        while (IsConnecting || IsConnected)
                        {
                            if (!IsConnecting && !IsConnected)
                                return Task.CompletedTask;
                            disconnect();
                            await Task.Delay(1000);
                        }
                        return Task.CompletedTask;
                    });

                    void disconnect()
                    {
                        // Unset DNS
                        if (IsDNSSet)
                            UnsetSavedDNS();

                        // Deactivate DPI
                        DPIDeactive();

                        // Kill processes (DNSProxy, DNSCrypt)
                        if (ProcessManager.FindProcessByName("dnsproxy"))
                            ProcessManager.KillProcessByName("dnsproxy");
                        if (ProcessManager.FindProcessByName("dnscrypt-proxy"))
                            ProcessManager.KillProcessByName("dnscrypt-proxy");

                        // Stop Cloudflare Bypass
                        BypassFakeProxyDohStop(true, true, true, false);

                        //// Stop Fake Proxy
                        //if (FakeProxy != null && FakeProxy.IsRunning)
                        //    FakeProxy.Stop();

                        //// Stop HTTP Proxy (Sharing)
                        //if (HTTPProxy != null && HTTPProxy.IsRunning)
                        //    HTTPProxy.Stop();

                        // Unset Proxy
                        if (IsProxySet && !HTTPProxy.IsRunning)
                            Network.UnsetProxy(false, false);

                        // Update Groupbox Status
                        UpdateStatusLong();

                        // To see offline status immediately
                        Parallel.Invoke(
                            () => UpdateBoolDnsOnce(1000),
                            () => UpdateBoolDohOnce(1000)
                        );
                    }

                    // Write Disconnected message to log
                    string msgDisconnected = "Disconnected." + NL;
                    this.InvokeIt(() => CustomRichTextBoxLog.AppendText(msgDisconnected, Color.MediumSeaGreen));

                    IsDisconnecting = false;
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show(ex.Message, "Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private bool CheckBypassWorks(int timeoutMS, int attempts)
        {
            if (!IsConnected || IsDisconnecting) return false;

            // Get loopback
            string loopback = IPAddress.Loopback.ToString();

            // Get blocked domain
            string blockedDomain = GetBlockedDomainSetting(out string blockedDomainNoWww);
            if (string.IsNullOrEmpty(blockedDomain)) return false;

            // Message
            string msg1 = "Bypassing";
            string msg2 = "...";
            this.InvokeIt(() => CustomRichTextBoxLog.AppendText(msg1, Color.MediumSeaGreen));

            for (int n = 0; n < attempts; n++)
            {
                if (!IsConnected || IsDisconnecting) return false;

                // Message before
                this.InvokeIt(() => CustomRichTextBoxLog.AppendText(msg2, Color.MediumSeaGreen));

                // Delay
                int latency = SecureDNS.CheckDns(blockedDomainNoWww, loopback, timeoutMS, GetCPUPriority());
                bool result = latency != -1;
                Task.Delay(500).Wait(); // Wait a moment
                if (result)
                {
                    // Update bool
                    IsConnected = true;
                    IsDNSConnected = true;

                    // Message add NL on success
                    this.InvokeIt(() => CustomRichTextBoxLog.AppendText(msg2 + NL, Color.MediumSeaGreen));

                    // Write delay to log
                    string msgDelay1 = "Server delay: ";
                    string msgDelay2 = $" ms.{NL}";
                    this.InvokeIt(() => CustomRichTextBoxLog.AppendText(msgDelay1, Color.Orange));
                    this.InvokeIt(() => CustomRichTextBoxLog.AppendText(latency.ToString(), Color.DodgerBlue));
                    this.InvokeIt(() => CustomRichTextBoxLog.AppendText(msgDelay2, Color.Orange));
                    return true;
                }

                Task.Delay(500).Wait();
            }

            // Message add NL on failure
            this.InvokeIt(() => CustomRichTextBoxLog.AppendText(msg2 + NL, Color.MediumSeaGreen));

            return false;
        }

        //============================== Set DNS

        private async void SetDNS()
        {
            // Get NIC Name
            string? nicName = CustomComboBoxNICs.SelectedItem as string;

            // Check if NIC Name is empty
            if (string.IsNullOrEmpty(nicName))
            {
                string msg = "Select a Network Interface first." + NL;
                CustomRichTextBoxLog.AppendText(msg, Color.IndianRed);
                return;
            }

            // Check if NIC is null
            NetworkInterface? nic = Network.GetNICByName(nicName);
            if (nic == null) return;

            string loopbackIP = IPAddress.Loopback.ToString();
            string dnss = loopbackIP;
            if (LocalIP != null)
                dnss += "," + LocalIP;

            if (!IsDNSSet)
            {
                // Set DNS
                // Write Connect first to log
                string msgConnect = string.Empty;
                if (!IsConnected)
                {
                    msgConnect = "Connect first." + NL;
                    this.InvokeIt(() => CustomRichTextBoxLog.AppendText(msgConnect, Color.IndianRed));
                    return;
                }
                else if (!IsDNSConnected)
                {
                    msgConnect = "Wait until DNS gets online." + NL;
                    this.InvokeIt(() => CustomRichTextBoxLog.AppendText(msgConnect, Color.IndianRed));
                    return;
                }

                // Check Internet Connectivity
                if (!IsInternetAlive()) return;

                // Get blocked domain
                string blockedDomain = GetBlockedDomainSetting(out string _);
                if (string.IsNullOrEmpty(blockedDomain)) return;

                // Show warning while connected using dnscrypt + proxy
                if (ProcessManager.FindProcessByID(PIDDNSCrypt) && CustomRadioButtonConnectDNSCrypt.Checked)
                {
                    string msg = "Set DNS while connected via proxy is not a good idea.\nYou may break the connection.\nContinue?";
                    DialogResult dr = CustomMessageBox.Show(msg, "Info", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                    if (dr == DialogResult.No) return;
                }

                // Set DNS
                Network.SetDNS(nic, dnss); // Set DNS
                IsDNSSet = true;

                // Save NIC name to file
                FileDirectory.CreateEmptyFile(SecureDNS.NicNamePath);
                File.WriteAllText(SecureDNS.NicNamePath, nicName);

                // Update Groupbox Status
                UpdateStatusLong();

                // Write Set DNS message to log
                string msg1 = "Local DNS ";
                string msg2 = loopbackIP;
                string msg3 = " set to ";
                string msg4 = nicName + " (" + nic.Description + ")";
                CustomRichTextBoxLog.AppendText(msg1, Color.LightGray);
                CustomRichTextBoxLog.AppendText(msg2, Color.DodgerBlue);
                CustomRichTextBoxLog.AppendText(msg3, Color.LightGray);
                CustomRichTextBoxLog.AppendText(msg4 + NL, Color.DodgerBlue);

                // Go to Check Tab
                if (ConnectAllClicked && IsConnected)
                {
                    this.InvokeIt(() => CustomTabControlMain.SelectedIndex = 0);
                    this.InvokeIt(() => CustomTabControlSecureDNS.SelectedIndex = 0);
                    ConnectAllClicked = false;
                }

                // Check DPI works if DPI is Active
                if (IsDPIActive)
                    await CheckDPIWorks(blockedDomain);
            }
            else
            {
                // Unset DNS
                bool unsetToDHCP = CustomRadioButtonSettingUnsetDnsToDhcp.Checked;
                if (unsetToDHCP)
                {
                    // Unset to DHCP
                    Network.UnsetDNS(nic);
                    Task.Delay(200).Wait();
                    UnsetSavedDnsDHCP();
                }
                else
                {
                    // Unset to Static
                    string dns1 = CustomTextBoxSettingUnsetDns1.Text;
                    string dns2 = CustomTextBoxSettingUnsetDns2.Text;
                    Network.UnsetDNS(nic, dns1, dns2);
                    Task.Delay(200).Wait();
                    UnsetSavedDnsStatic(dns1, dns2);
                }
                
                IsDNSSet = false;

                // Flush DNS
                FlushDNS();

                // Update Groupbox Status
                UpdateStatusLong();

                // Write Unset DNS message to log
                string msg1 = "Local DNS ";
                string msg2 = loopbackIP;
                string msg3 = " removed from ";
                string msg4 = nicName + " (" + nic.Description + ")";
                CustomRichTextBoxLog.AppendText(msg1, Color.LightGray);
                CustomRichTextBoxLog.AppendText(msg2, Color.DodgerBlue);
                CustomRichTextBoxLog.AppendText(msg3, Color.LightGray);
                CustomRichTextBoxLog.AppendText(msg4 + NL, Color.DodgerBlue);
            }
        }

        //============================== Set Proxy

        private async void SetProxy()
        {
            if (!IsProxySet)
            {
                // Set Proxy
                // Write Enable Proxy first to log
                if (!IsSharing)
                {
                    string msg = "Enable Proxy first." + NL;
                    this.InvokeIt(() => CustomRichTextBoxLog.AppendText(msg, Color.IndianRed));
                    return;
                }

                if (HTTPProxy != null)
                {
                    // Get IP:Port
                    string ip = IPAddress.Loopback.ToString();
                    int port = HTTPProxy.ListeningPort;

                    // Start Set Proxy
                    Network.SetHttpProxy(ip, port);

                    Task.Delay(300).Wait(); // Wait a moment

                    bool isProxySet = Network.IsProxySet(out string _, out string _, out string _, out string _);
                    if (isProxySet)
                    {
                        // Update bool
                        IsProxySet = true;

                        // Write Set Proxy message to log
                        string msg1 = "HTTP Proxy ";
                        string msg2 = $"{ip}:{port}";
                        string msg3 = " set to system.";
                        CustomRichTextBoxLog.AppendText(msg1, Color.LightGray);
                        CustomRichTextBoxLog.AppendText(msg2, Color.DodgerBlue);
                        CustomRichTextBoxLog.AppendText(msg3 + NL, Color.LightGray);

                        // Check DPI Works
                        if (CustomCheckBoxPDpiEnableDpiBypass.Checked)
                        {
                            // Get blocked domain
                            string blockedDomain = GetBlockedDomainSetting(out string _);
                            if (!string.IsNullOrEmpty(blockedDomain))
                                await CheckDPIWorks(blockedDomain);
                        }
                    }
                    else
                    {
                        // Write Set Proxy error to log
                        string msg = "Couldn't set HTTP Proxy to system.";
                        CustomRichTextBoxLog.AppendText(msg + NL, Color.IndianRed);
                    }
                }
            }
            else
            {
                // Unset Proxy
                Network.UnsetProxy(false, true);

                Task.Delay(300).Wait(); // Wait a moment

                bool isProxySet = Network.IsProxySet(out string _, out string _, out string _, out string _);
                if (!isProxySet)
                {
                    // Update bool
                    IsProxySet = false;

                    // Write Unset Proxy message to log
                    string msg1 = "HTTP Proxy removed from system.";
                    CustomRichTextBoxLog.AppendText(msg1 + NL, Color.LightGray);
                }
                else
                {
                    // Write Unset Proxy error to log
                    string msg = "Couldn't unset HTTP Proxy from system.";
                    CustomRichTextBoxLog.AppendText(msg + NL, Color.IndianRed);
                }
            }
        }

        //============================== DPI

        private async void ApplyPDpiChanges(HTTPProxyServer httpProxy)
        {
            if (httpProxy != null)
            {
                // Get fragment settings
                bool enableDpiBypass = CustomCheckBoxPDpiEnableDpiBypass.Checked;
                int dataLength = Convert.ToInt32(CustomNumericUpDownPDpiDataLength.Value);
                int fragmentSize = Convert.ToInt32(CustomNumericUpDownPDpiFragmentSize.Value);
                int fragmentChunks = Convert.ToInt32(CustomNumericUpDownPDpiFragmentChunks.Value);
                bool randomMode = CustomCheckBoxPDpiFragModeRandom.Checked;
                int antiPatternOffset = Convert.ToInt32(CustomNumericUpDownPDpiAntiPatternOffset.Value);
                bool dontChunkBigdata = CustomCheckBoxPDpiDontChunkBigData.Checked;
                int fragmentDelay = Convert.ToInt32(CustomNumericUpDownPDpiFragDelay.Value);

                HTTPProxyServer.Program.DPIBypass.Mode bypassMode = enableDpiBypass ? HTTPProxyServer.Program.DPIBypass.Mode.Program : HTTPProxyServer.Program.DPIBypass.Mode.Disable;

                StaticDPIBypassProgram.Set(bypassMode, dataLength, fragmentSize, fragmentChunks, fragmentDelay);
                StaticDPIBypassProgram.AntiPatternOffset = antiPatternOffset;
                StaticDPIBypassProgram.DontChunkTheBiggestRequest = dontChunkBigdata;
                StaticDPIBypassProgram.SendInRandom = randomMode;
                httpProxy.EnableStaticDPIBypass(StaticDPIBypassProgram);

                // Check DPI Works
                if (httpProxy != HTTPProxy) return;
                if (CustomCheckBoxPDpiEnableDpiBypass.Checked && httpProxy.IsRunning)
                {
                    IsProxyDPIActive = StaticDPIBypassProgram.DPIBypassMode == HTTPProxyServer.Program.DPIBypass.Mode.Program;
                    IsProxyDPIActive = true;
                    IsDPIActive = true;
                    Task.Delay(100).Wait();

                    // Get blocked domain
                    string blockedDomain = GetBlockedDomainSetting(out string _);
                    if (!string.IsNullOrEmpty(blockedDomain))
                        await CheckDPIWorks(blockedDomain);
                }
            }
        }

        private async Task CheckDPIWorks(string host, int timeoutSec = 30) //Default timeout: 100 sec
        {
            if (string.IsNullOrWhiteSpace(host)) return;

            // If user changing DPI mode fast, return.
            if (StopWatchCheckDPIWorks.IsRunning)
                return;

            Task.Delay(1000).Wait();

            // Start StopWatch
            StopWatchCheckDPIWorks.Start();

            // Write start DPI checking to log
            string msgDPI = $"Checking DPI Bypass ({host})...{NL}";
            this.InvokeIt(() => CustomRichTextBoxLog.AppendText(msgDPI, Color.LightGray));

            try
            {
                if (!IsDPIActive)
                {
                    // Write activate DPI first to log
                    string msgDPI1 = $"DPI Check: ";
                    string msgDPI2 = $"Activate DPI Bypass to check.{NL}";
                    this.InvokeIt(() => CustomRichTextBoxLog.AppendText(msgDPI1, Color.LightGray));
                    this.InvokeIt(() => CustomRichTextBoxLog.AppendText(msgDPI2, Color.IndianRed));
                    StopWatchCheckDPIWorks.Stop();
                    StopWatchCheckDPIWorks.Reset();

                    return;
                }

                // Is HTTP Proxy Direct DNS Set?!
                bool isProxyDnsSet = false;
                if (FakeProxy.IsRunning &&
                    HTTPProxy.IsRunning &&
                    HTTPProxy.DNSProgram.DNSMode != HTTPProxyServer.Program.Dns.Mode.Disable &&
                    HTTPProxyServer.StaticDPIBypassProgram.DPIBypassMode != HTTPProxyServer.Program.DPIBypass.Mode.Disable)
                    isProxyDnsSet = true;
                
                if (!IsDNSSet && !isProxyDnsSet)
                {
                    // Write set DNS first to log
                    string msgDPI1 = $"DPI Check: ";
                    string msgDPI2 = $"Set DNS to check.{NL}";
                    this.InvokeIt(() => CustomRichTextBoxLog.AppendText(msgDPI1, Color.LightGray));
                    this.InvokeIt(() => CustomRichTextBoxLog.AppendText(msgDPI2, Color.IndianRed));
                    StopWatchCheckDPIWorks.Stop();
                    StopWatchCheckDPIWorks.Reset();

                    return;
                }

                string url = $"https://{host}/";
                Uri uri = new(url, UriKind.Absolute);

                bool isProxyPortOpen = Network.IsPortOpen(IPAddress.Loopback.ToString(), LastProxyPort, 5);
                Debug.WriteLine($"Is Proxy Port Open: {isProxyPortOpen}");

                if (IsProxyDPIActive && isProxyPortOpen)
                {
                    Debug.WriteLine("Proxy");

                    // Kill all requests before check
                    HTTPProxy.KillAll();
                    Task.Delay(500).Wait();

                    string proxyScheme = $"http://{IPAddress.Loopback}:{LastProxyPort}";

                    using SocketsHttpHandler socketsHttpHandler = new();
                    socketsHttpHandler.Proxy = new WebProxy(proxyScheme, true);

                    using HttpClient httpClientWithProxy = new(socketsHttpHandler);
                    httpClientWithProxy.Timeout = TimeSpan.FromSeconds(timeoutSec);

                    HttpResponseMessage r = await httpClientWithProxy.GetAsync(uri);
                    Task.Delay(500).Wait();

                    if (r.IsSuccessStatusCode || r.StatusCode.ToString() == "NotFound")
                    {
                        msgSuccess();
                        r.Dispose();
                    }
                    else
                        msgFailed(r);
                }
                else
                {
                    Debug.WriteLine("No Proxy");
                    using HttpClient httpClient = new();
                    httpClient.Timeout = new TimeSpan(0, 0, timeoutSec);

                    HttpResponseMessage r = await httpClient.GetAsync(uri);
                    Task.Delay(500).Wait();

                    if (r.IsSuccessStatusCode || r.StatusCode.ToString() == "NotFound")
                    {
                        msgSuccess();
                        r.Dispose();
                    }
                    else
                        msgFailed(r);
                }

                void msgSuccess()
                {
                    // Write Success to log
                    var elapsedTime = Math.Round((double)StopWatchCheckDPIWorks.ElapsedMilliseconds / 1000);
                    string msgDPI1 = $"DPI Check: ";
                    string msgDPI2 = $"Successfully opened {host} in {elapsedTime} seconds.{NL}";
                    this.InvokeIt(() => CustomRichTextBoxLog.AppendText(msgDPI1, Color.LightGray));
                    this.InvokeIt(() => CustomRichTextBoxLog.AppendText(msgDPI2, Color.MediumSeaGreen));
                }

                void msgFailed(HttpResponseMessage r)
                {
                    // Write Status to log
                    string msgDPI1 = $"DPI Check: ";
                    string msgDPI2 = $"Status {r.StatusCode}: {r.ReasonPhrase}.{NL}";
                    this.InvokeIt(() => CustomRichTextBoxLog.AppendText(msgDPI1, Color.LightGray));
                    this.InvokeIt(() => CustomRichTextBoxLog.AppendText(msgDPI2, Color.DodgerBlue));

                    r.Dispose();
                }

                StopWatchCheckDPIWorks.Stop();
                StopWatchCheckDPIWorks.Reset();
            }
            catch (Exception ex)
            {
                // Write Failed to log
                string msgDPI1 = $"DPI Check: ";
                string msgDPI2 = $"{ex.Message}{NL}";
                this.InvokeIt(() => CustomRichTextBoxLog.AppendText(msgDPI1, Color.LightGray));
                this.InvokeIt(() => CustomRichTextBoxLog.AppendText(msgDPI2, Color.IndianRed));
                StopWatchCheckDPIWorks.Stop();
                StopWatchCheckDPIWorks.Reset();
            }
        }

        //============================== Buttons

        private void CustomButtonToggleLogView_Click(object sender, EventArgs e)
        {
            int logHeight = LogHeight;

            if (CustomGroupBoxLog.Visible)
            {
                SuspendLayout();
                CustomGroupBoxLog.Visible = false;
                SplitContainerMain.Panel2Collapsed = true;
                SplitContainerMain.Panel2.Hide();
                Size = new(Width, Height - logHeight);
                ResumeLayout();
                Invalidate();
            }
            else
            {
                SuspendLayout();
                Size = new(Width, Height + logHeight);
                CustomGroupBoxLog.Visible = true;
                SplitContainerMain.Panel2Collapsed = false;
                SplitContainerMain.Panel2.Show();
                ResumeLayout();
                Invalidate();
            }
        }

        private void CustomButtonEditCustomServers_Click(object sender, EventArgs e)
        {
            FileDirectory.CreateEmptyFile(SecureDNS.CustomServersPath);
            int notepad = ProcessManager.ExecuteOnly("notepad", SecureDNS.CustomServersPath, false, false, Info.CurrentPath);
            if (notepad == -1)
            {
                string msg = "Notepad is not installed on your system.";
                CustomRichTextBoxLog.AppendText(msg + NL, Color.IndianRed);
            }
        }

        private void CustomButtonViewWorkingServers_Click(object sender, EventArgs e)
        {
            FileDirectory.CreateEmptyFile(SecureDNS.WorkingServersPath);
            int notepad = ProcessManager.ExecuteOnly("notepad", SecureDNS.WorkingServersPath, false, false, Info.CurrentPath);
            if (notepad == -1)
            {
                string msg = "Notepad is not installed on your system.";
                CustomRichTextBoxLog.AppendText(msg + NL, Color.IndianRed);
            }
        }

        private void CustomButtonCheck_Click(object? sender, EventArgs? e)
        {
            StartCheck();
        }

        private async void CustomButtonConnectAll_Click(object sender, EventArgs e)
        {
            if (!IsCheckingStarted && !IsConnected && !ProcessManager.FindProcessByName("goodbyedpi") && !IsDNSSet)
            {
                if (HTTPProxy == null)
                {
                    await connectAll();
                }
                else
                {
                    if (!HTTPProxy.IsRunning)
                        await connectAll();
                    else
                        disconnectAll();
                }
            }
            else
                disconnectAll();

            async Task connectAll()
            {
                ConnectAllClicked = true;
                CustomButtonCheck_Click(null, null);
                Task taskWait1 = await Task.Run(async () =>
                {
                    while (IsCheckingStarted)
                    {
                        if (!IsCheckingStarted)
                            return Task.CompletedTask;
                        await Task.Delay(1000);
                    }
                    return Task.CompletedTask;
                });
                await Task.Delay(1000);
                if (!IsCheckingStarted)
                {
                    CustomButtonConnect_Click(null, null);
                    Task taskWait2 = await Task.Run(async () =>
                    {
                        while (!IsDNSConnected && !IsDoHConnected)
                        {
                            if (NumberOfWorkingServers < 1)
                                return Task.CompletedTask;
                            await Task.Delay(1000);
                        }
                        return Task.CompletedTask;
                    });
                    await Task.Delay(1000);
                    if (IsDNSConnected || IsDoHConnected)
                    {
                        UpdateStatusLong();
                        if (!ProcessManager.FindProcessByID(PIDGoodbyeDPI))
                            DPIBasic();
                        UpdateStatusLong();
                        await Task.Delay(1000);
                        if (!IsDNSSet)
                            SetDNS();
                        UpdateStatusLong();
                    }
                    ConnectAllClicked = false;
                }
            }

            void disconnectAll()
            {
                if (IsConnected)
                {
                    CustomButtonConnect_Click(null, null);
                    UpdateStatusLong();
                }
                if (ProcessManager.FindProcessByName("goodbyedpi"))
                {
                    DPIBasic();
                    UpdateStatusLong();
                }
                if (IsDNSSet)
                {
                    SetDNS();
                    UpdateStatusLong();
                }
                if (IsCheckingStarted)
                    CustomButtonCheck_Click(null, null);
            }
        }

        private async void CustomButtonWriteSavedServersDelay_Click(object sender, EventArgs e)
        {
            await WriteSavedServersDelayToLog();
        }

        private void CustomButtonConnect_Click(object? sender, EventArgs? e)
        {
            StartConnect();
        }

        private void CustomButtonDPIBasic_Click(object sender, EventArgs e)
        {
            DPIBasic();
        }

        private void CustomButtonDPIBasicDeactivate_Click(object sender, EventArgs e)
        {
            DPIDeactive();
        }

        private void CustomButtonDPIAdvBlacklist_Click(object sender, EventArgs e)
        {
            FileDirectory.CreateEmptyFile(SecureDNS.DPIBlacklistPath);
            int notepad = ProcessManager.ExecuteOnly("notepad", SecureDNS.DPIBlacklistPath, false, false, Info.CurrentPath);
            if (notepad == -1)
            {
                string msg = "Notepad is not installed on your system.";
                CustomRichTextBoxLog.AppendText(msg + NL, Color.IndianRed);
            }
        }

        private void CustomButtonDPIAdvActivate_Click(object sender, EventArgs e)
        {
            DPIAdvanced();
        }

        private void CustomButtonDPIAdvDeactivate_Click(object sender, EventArgs e)
        {
            DPIDeactive();
        }

        private void CustomButtonSetDNS_Click(object sender, EventArgs e)
        {
            SetDNS();
        }

        private void CustomButtonShare_Click(object sender, EventArgs e)
        {
            Share();
        }

        private void CustomButtonSetProxy_Click(object sender, EventArgs e)
        {
            SetProxy();
        }
        private void CustomButtonPDpiApplyChanges_Click(object sender, EventArgs e)
        {
            if (CustomCheckBoxSettingHTTPProxyEnableFakeProxy.Checked)
                ApplyPDpiChanges(FakeProxy);
            ApplyPDpiChanges(HTTPProxy);
        }

        private void CustomButtonToolsIpScanner_Click(object sender, EventArgs e)
        {
            FormIpScanner formIpScanner = new();
            formIpScanner.Show();
        }

        private void CustomButtonSettingUninstallCertificate_Click(object sender, EventArgs e)
        {
            UninstallCertificate();
        }

        private void CustomButtonSettingHTTPProxyFakeDNS_Click(object sender, EventArgs e)
        {
            FileDirectory.CreateEmptyFile(SecureDNS.FakeDnsRulesPath);
            int notepad = ProcessManager.ExecuteOnly("notepad", SecureDNS.FakeDnsRulesPath, false, false, Info.CurrentPath);
            if (notepad == -1)
            {
                string msg = "Notepad is not installed on your system.";
                CustomRichTextBoxLog.AppendText(msg + NL, Color.IndianRed);
            }
        }

        private void CustomButtonSettingHTTPProxyBlackWhiteList_Click(object sender, EventArgs e)
        {
            FileDirectory.CreateEmptyFile(SecureDNS.BlackWhiteListPath);
            int notepad = ProcessManager.ExecuteOnly("notepad", SecureDNS.BlackWhiteListPath, false, false, Info.CurrentPath);
            if (notepad == -1)
            {
                string msg = "Notepad is not installed on your system.";
                CustomRichTextBoxLog.AppendText(msg + NL, Color.IndianRed);
            }
        }

        private void CustomButtonSettingRestoreDefault_Click(object sender, EventArgs e)
        {
            if (IsCheckingStarted)
            {
                string msgChecking = "Stop check operation first." + NL;
                CustomRichTextBoxLog.AppendText(msgChecking, Color.IndianRed);
                return;
            }
            
            if (IsConnected)
            {
                string msgConnected = "Disconnect first." + NL;
                CustomRichTextBoxLog.AppendText(msgConnected, Color.IndianRed);
                return;
            }
            
            if (IsDNSSet)
            {
                string msgDNSIsSet = "Unset DNS first." + NL;
                CustomRichTextBoxLog.AppendText(msgDNSIsSet, Color.IndianRed);
                return;
            }

            DefaultSettings();

            string msgDefault = "Settings restored to default." + NL;
            CustomRichTextBoxLog.AppendText(msgDefault, Color.MediumSeaGreen);
        }

        //============================== Events
        private void SecureDNSClient_CheckedChanged(object sender, EventArgs e)
        {
            if (AppSettings == null) return;

            if (sender is CustomRadioButton crbBuiltIn && crbBuiltIn.Name == CustomRadioButtonBuiltIn.Name)
            {
                AppSettings.AddSetting(CustomRadioButtonBuiltIn, nameof(CustomRadioButtonBuiltIn.Checked), CustomRadioButtonBuiltIn.Checked);
            }

            if (sender is CustomRadioButton crbCustom && crbCustom.Name == CustomRadioButtonCustom.Name)
            {
                AppSettings.AddSetting(CustomRadioButtonCustom, nameof(CustomRadioButtonCustom.Checked), CustomRadioButtonCustom.Checked);
            }

            if (sender is CustomCheckBox cchInsecure && cchInsecure.Name == CustomCheckBoxInsecure.Name)
            {
                AppSettings.AddSetting(CustomCheckBoxInsecure, nameof(CustomCheckBoxInsecure.Checked), CustomCheckBoxInsecure.Checked);
            }
        }

        //============================== Closing
        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing || e.CloseReason == CloseReason.WindowsShutDown)
            {
                e.Cancel = true;
                Hide();
                //ShowInTaskbar = false; // Makes Titlebar white (I use Show and Hide instead)
                NotifyIconMain.BalloonTipText = "Minimized to tray.";
                NotifyIconMain.BalloonTipIcon = ToolTipIcon.Info;
                NotifyIconMain.ShowBalloonTip(500);
            }
        }

        private void NotifyIconMain_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                this.SetDarkTitleBar(true); // Just in case
                if (!Visible) Show();
                BringToFront();
            }
            else if (e.Button == MouseButtons.Right)
            {
                CustomContextMenuStripIcon.Show();
            }
        }

        private async void ToolStripMenuItemIcon_Click(object? sender, EventArgs e)
        {
            // Write Closing message to log
            string msg = "Exiting..." + NL;
            this.InvokeIt(() => CustomRichTextBoxLog.AppendText(msg, Color.LightGray));

            // Hide NotifyIcon
            NotifyIconMain.Visible = false;

            // Disconnect
            if (IsConnected)
                CustomButtonConnect_Click(null, null);

            // Stop Cloudflare Bypass
            BypassFakeProxyDohStop(true, true, true, false);

            // Unset Saved DNS
            UnsetSavedDNS();

            // Stop Fake Proxy
            if (FakeProxy != null && FakeProxy.IsRunning)
                FakeProxy.Stop();

            // Stop HTTP Proxy (Sharing)
            if (HTTPProxy != null && HTTPProxy.IsRunning)
                HTTPProxy.Stop();

            // Unset Proxy
            if (IsProxySet)
                Network.UnsetProxy(false, false);

            // Kill processes and set DNS to dynamic
            KillAll();

            // Flush DNS On Exit
            FlushDnsOnExit();

            // Select Control type and properties to save
            AppSettings.AddSelectedControlAndProperty(typeof(CustomCheckBox), "Checked");
            AppSettings.AddSelectedControlAndProperty(typeof(CustomNumericUpDown), "Value");
            AppSettings.AddSelectedControlAndProperty(typeof(CustomRadioButton), "Checked");
            AppSettings.AddSelectedControlAndProperty(typeof(CustomTextBox), "Text");
            AppSettings.AddSelectedControlAndProperty(typeof(CustomTextBox), "Texts");
            AppSettings.AddSelectedControlAndProperty(typeof(CustomComboBox), "SelectedIndex");

            // Add Settings to save
            AppSettings.AddSelectedSettings(this);

            // Save Application Settings
            await AppSettings.SaveAsync(SecureDNS.SettingsXmlPath);

            // Exit
            Environment.Exit(0);
            Application.Exit();
        }

        //============================== About
        private void CustomLabelAboutThis_Click(object sender, EventArgs e)
        {
            OpenLinks.OpenUrl("https://github.com/msasanmh/SecureDNSClient/");
        }

        private void LinkLabelDNSLookup_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            OpenLinks.OpenUrl("https://github.com/ameshkov/dnslookup");
        }

        private void LinkLabelDNSProxy_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            OpenLinks.OpenUrl("https://github.com/AdguardTeam/dnsproxy");
        }

        private void LinkLabelDNSCrypt_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            OpenLinks.OpenUrl("https://github.com/DNSCrypt/dnscrypt-proxy");
        }

        private void LinkLabelGoodbyeDPI_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            OpenLinks.OpenUrl("https://github.com/ValdikSS/GoodbyeDPI");
        }

        private void LinkLabelStAlidxdydz_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            OpenLinks.OpenUrl("https://github.com/alidxdydz");
        }

    }
}