using SevenZip;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Input;

namespace Launcher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string ConfigLocation = "reslauncher//config.txt";

        private readonly Dictionary<string, string> _configValues = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _serverconfigValues = new Dictionary<string, string>();
        private readonly Dictionary<int, string> _patchLinks = new Dictionary<int, string>();
        private readonly Dictionary<int, string> _launcherPatchLinks = new Dictionary<int, string>();
        private readonly Dictionary<string, string> _fileChecks = new Dictionary<string, string>();

        private static readonly HttpClient Client = new HttpClient();
        private int _downloadVersion;
        private bool _securityPassed;

        private double _bytesPerSecond;
        private long _lastBytesRecevied;
        private DateTime _lastReceivedMeasurement;

        public MainWindow()
        {
            InitializeComponent();

            if (!File.Exists("7za.dll"))
            {
                MessageBox.Show("Missing 7za.dll");
                Environment.Exit(0);
            }

            SevenZipBase.SetLibraryPath("7za.dll");
            //SevenZipBase.SetLibraryPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "7za.dll"));

            GetLauncherConfig();
        }

        private bool GetConfiguration()
        {
            _configValues.Clear();

            if (!File.Exists(ConfigLocation))
            {
                MessageBox.Show(Properties.Resources.Error1);
                File.Create(ConfigLocation).Dispose();
                StreamWriter file = new StreamWriter(ConfigLocation);
                file.WriteLine(Properties.Resources.ConfigLauncherConfig + " = http://www.google.com/");
                file.WriteLine(Properties.Resources.ConfigClientVersion + " = 0");
                file.WriteLine(Properties.Resources.ConfigLauncherVersion + " = 0");
                file.Dispose();

                File.SetAttributes(ConfigLocation, File.GetAttributes(ConfigLocation) | FileAttributes.Hidden);
                File.SetAttributes(ConfigLocation, File.GetAttributes(ConfigLocation) | FileAttributes.System);

                Environment.Exit(0);
            }
            else
            {
#if DEBUG
                File.SetAttributes(ConfigLocation, FileAttributes.Normal);
#else
                File.SetAttributes(ConfigLocation, File.GetAttributes(ConfigLocation) | FileAttributes.Hidden);
                File.SetAttributes(ConfigLocation, File.GetAttributes(ConfigLocation) | FileAttributes.System);
#endif
            }

            try
            {
                string line;
                StreamReader file = new StreamReader(ConfigLocation);

                while ((line = file.ReadLine()) != null)
                {
                    string[] lineSplit = line.Split('=');
                    _configValues.Add(lineSplit[0].Trim(), lineSplit[1].Trim());
                }

                file.Dispose();

                VersionLabel.Content = "V ";
                VersionLabel.Content += _configValues[Properties.Resources.ConfigClientVersion] ?? "0";

                return true;
            }
            catch (Exception e)
            {
                MessageBox.Show(Properties.Resources.Error2 + e.StackTrace);
            }

            return false;
        }

        private async void GetLauncherConfig()
        {
            if (!GetConfiguration()) return;
            if (!_configValues.ContainsKey(Properties.Resources.ConfigLauncherConfig)) return;
            var configFile = _configValues[Properties.Resources.ConfigLauncherConfig].Trim();

            if (!String.IsNullOrWhiteSpace(configFile))
            {
                if (!configFile.StartsWith("http://") && !configFile.StartsWith("https://"))
                {
                    configFile = configFile.Insert(0, "http://");
                }
            }
            else
            {
                MessageBox.Show(Properties.Resources.Error10);
                return;
            }

            try
            {
                InfoLabel.Content = "Trying to connect to launcher server...";
                var serverResponse = await Client.GetAsync(configFile);

                if (!serverResponse.IsSuccessStatusCode)
                {
                    InfoLabel.Content = Properties.Resources.Error14;
                    return;
                }

                InfoLabel.Content = "Successfully connected to launcher server!";
                var responseContent = await serverResponse.Content.ReadAsStringAsync();

                string[] lines = responseContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                foreach (var line in lines)
                {
                    string[] values = line.Split('=');

                    if (String.IsNullOrWhiteSpace(values[0]))
                    {
                        continue;
                    }

                    if (values.Length >= 2 && !_serverconfigValues.ContainsKey(values[0].Trim()))
                    {
                        _serverconfigValues.Add(values[0].Trim(), values[1].Trim());
                    }
                }

                string button = Properties.Resources.ConfigButton1Name;
                if (_serverconfigValues.ContainsKey(button)) Button1.Content = _serverconfigValues[button];

                button = Properties.Resources.ConfigButton2Name;
                if (_serverconfigValues.ContainsKey(button)) Button2.Content = _serverconfigValues[button];

                button = Properties.Resources.ConfigButton3Name;
                if (_serverconfigValues.ContainsKey(button)) Button3.Content = _serverconfigValues[button];

                button = Properties.Resources.ConfigButton4Name;
                if (_serverconfigValues.ContainsKey(button)) Button4.Content = _serverconfigValues[button];

                button = Properties.Resources.ConfigButton5Name;
                if (_serverconfigValues.ContainsKey(button)) Button5.Content = _serverconfigValues[button];

                GetPatchNotes();

            }
            catch (WebSocketException)
            {
                MessageBox.Show(Properties.Resources.Error14);
            }
            catch (HttpRequestException)
            {
                MessageBox.Show(Properties.Resources.Error19);
            }
            catch (Exception exception)
            {
                MessageBox.Show(Properties.Resources.Error15 + Environment.NewLine + exception.Message + exception.StackTrace);
            }
        }

        private async void GetPatchNotes()
        {
            if (!_serverconfigValues.ContainsKey(Properties.Resources.ConfigPatchNotes))
            {
                InfoLabel.Content = "Failed getting patch notes.";
                CheckLauncherVersion();
                return;
            }

            var patchNotes = _serverconfigValues[Properties.Resources.ConfigPatchNotes];

            if (!String.IsNullOrWhiteSpace(patchNotes))
            {
                if (!patchNotes.StartsWith("http://") && !patchNotes.StartsWith("https://"))
                {
                    patchNotes = patchNotes.Insert(0, "http://");
                }
            }
            else
            {
                MessageBox.Show(Properties.Resources.Error10);
                return;
            }

            try
            {
                InfoLabel.Content = "Trying to retrieve patch notes for reply from server...";
                var serverResponse = await Client.GetAsync(patchNotes);
                InfoLabel.Content = "Successfully retrieved patch notes!";
                var responseContent = await serverResponse.Content.ReadAsStringAsync();
                TextBlock.Text = responseContent;

                CheckLauncherVersion();
            }
            catch (WebSocketException)
            {
                MessageBox.Show(Properties.Resources.Error5);
            }
            catch (HttpRequestException)
            {
                MessageBox.Show(Properties.Resources.Error19);
            }
            catch (Exception exception)
            {
                MessageBox.Show(Properties.Resources.Error6 + Environment.NewLine + exception.Message + exception.StackTrace);
            }
        }

        private async void CheckLauncherVersion()
        {
            if (!_serverconfigValues.ContainsKey(Properties.Resources.ConfigLauncherPatchServer))
            {
                InfoLabel.Content = "Failed checking launcher version.";
                return;
            }
            var launcherPatchlink = _serverconfigValues[Properties.Resources.ConfigLauncherPatchServer].Trim();

            if (!String.IsNullOrWhiteSpace(launcherPatchlink))
            {
                if (!launcherPatchlink.StartsWith("http://") && !launcherPatchlink.StartsWith("https://"))
                {
                    launcherPatchlink = launcherPatchlink.Insert(0, "http://");
                }
            }
            else
            {
                MessageBox.Show(Properties.Resources.Error11);
                return;
            }

            try
            {
                InfoLabel.Content = "Waiting for reply from launcher patch server...";
                var serverResponse = await Client.GetAsync(launcherPatchlink);
                InfoLabel.Content = "Checking launcher version...";
                var responseContent = await serverResponse.Content.ReadAsStringAsync();

                string[] lines = responseContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                foreach (var line in lines)
                {
                    string[] values = line.Split('#');

                    if (String.IsNullOrWhiteSpace(values[0].Trim()))
                    {
                        //MessageBox.Show(Properties.Resources.Error7);
                        continue;
                    }

                    if (values.Length >= 2 && !_launcherPatchLinks.ContainsKey(Int32.Parse(values[0].Trim())))
                    {
                        _launcherPatchLinks.Add(Int32.Parse(values[0]), values[1].Trim());
                    }
                }

                if (_launcherPatchLinks.ContainsKey(
                    Int32.Parse(_configValues[Properties.Resources.ConfigLauncherVersion]) + 1))
                {
                    if (!_serverconfigValues.ContainsKey(Properties.Resources.ConfigLauncherUpdaterName))
                    {
                        MessageBox.Show(Properties.Resources.Error17);
                        InfoLabel.Content = "Failed opening launcher updater.";
                        return;
                    }

                    if (!File.Exists(_serverconfigValues[Properties.Resources.ConfigLauncherUpdaterName]))
                    {
                        MessageBox.Show(Properties.Resources.Error17);
                        return;
                    }

                    Process.Start(_serverconfigValues[Properties.Resources.ConfigLauncherUpdaterName]);
                    Environment.Exit(0);
                }
                else
                {
                    GetPatchLinks();
                }
            }
            catch (OverflowException)
            {
                MessageBox.Show(Properties.Resources.Error9);
            }
            catch (FormatException)
            {
                MessageBox.Show(Properties.Resources.Error8);
            }
            catch (WebSocketException)
            {
                MessageBox.Show(Properties.Resources.Error5);
            }
            catch (HttpRequestException)
            {
                MessageBox.Show(Properties.Resources.Error19);
            }
            catch (Exception exception)
            {
                MessageBox.Show(Properties.Resources.Error6 + Environment.NewLine + exception.Message + exception.StackTrace);
            }
        }

        private async void GetPatchLinks()
        {
            if (!_serverconfigValues.ContainsKey(Properties.Resources.ConfigPatchServer)) return;
            var patchlink = _serverconfigValues[Properties.Resources.ConfigPatchServer].Trim();

            if (!String.IsNullOrWhiteSpace(patchlink))
            {
                if (!patchlink.StartsWith("http://") && !patchlink.StartsWith("https://"))
                {
                    patchlink = patchlink.Insert(0, "http://");
                }
            }
            else
            {
                MessageBox.Show(Properties.Resources.Error4);
                return;
            }

            try
            {
                InfoLabel.Content = "Waiting for reply from patch server...";
                var serverResponse = await Client.GetAsync(patchlink);
                InfoLabel.Content = "Successfully retrieved patch file!";
                var responseContent = await serverResponse.Content.ReadAsStringAsync();
                InfoLabel.Content = "Attempting to parse patch file details...";
                string[] lines = responseContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                foreach (var line in lines)
                {
                    string[] values = line.Split('#');

                    if (String.IsNullOrWhiteSpace(values[0].Trim()))
                    {
                        //MessageBox.Show(Properties.Resources.Error7);
                        continue;
                    }

                    if (values.Length >= 2 && !_patchLinks.ContainsKey(Int32.Parse(values[0])))
                    {
                        _patchLinks.Add(Int32.Parse(values[0].Trim()), values[1].Trim());
                    }
                }

                VersionLabelServer.Content = "V ";
                VersionLabelServer.Content += _patchLinks.Count.ToString();

                UpdateClient();
            }
            catch (OverflowException)
            {
                MessageBox.Show(Properties.Resources.Error9);
            }
            catch (FormatException)
            {
                MessageBox.Show(Properties.Resources.Error8);
            }
            catch (WebSocketException)
            {
                MessageBox.Show(Properties.Resources.Error5);
            }
            catch (HttpRequestException)
            {
                MessageBox.Show(Properties.Resources.Error19);
            }
            catch (Exception exception)
            {
                MessageBox.Show(Properties.Resources.Error6 + Environment.NewLine + exception.Message + exception.StackTrace);
            }
        }

        private void UpdateClient()
        {
            InfoLabel.Content = "Checking client version...";
            if (!_configValues.ContainsKey(Properties.Resources.ConfigClientVersion)) return;
            int clientVersion = Int32.Parse(_configValues[Properties.Resources.ConfigClientVersion]);

            if (clientVersion >= _patchLinks.Count)
            {
                InfoLabel.Content = "Client is up to date!";
                LaunchButton.IsEnabled = true;
                progressBar1.Value = progressBar1.Maximum;
                return;
            }

            _downloadVersion = clientVersion + 1;
            string downloadLink = _patchLinks[_downloadVersion].Trim();

            if (!String.IsNullOrWhiteSpace(downloadLink))
            {
                if (!downloadLink.StartsWith("http://") && !downloadLink.StartsWith("https://"))
                {
                    downloadLink = downloadLink.Insert(0, "http://");
                }
            }
            else
            {
                MessageBox.Show(Properties.Resources.Error16);
                return;
            }

            string patchDirectory = "reslauncher\\patches";
            string patchFile = patchDirectory + "\\patch" + _downloadVersion + ".7z";

            if (!Directory.Exists(patchDirectory)) Directory.CreateDirectory(patchDirectory);

            try
            {
                using (WebClient wc = new WebClient())
                {
                    InfoLabel.Content = "Downloading patch " + _downloadVersion;
                    progressBar1.Maximum = 100;
                    wc.DownloadProgressChanged += Wc_DownloadProgressChanged;
                    wc.DownloadFileCompleted += (sender, e) => Wc_DownloadFileCompleted(sender, e, patchFile);
                    wc.DownloadFileAsync(new System.Uri(downloadLink), patchFile);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(Properties.Resources.Error6 + Environment.NewLine + e.Message + Environment.NewLine +
                                e.InnerException + Environment.NewLine + e.StackTrace);
            }

        }

        private void Wc_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e, string patchFile)
        {
            try
            {
                string fileName = patchFile;
                string directory = AppDomain.CurrentDomain.BaseDirectory;
                var extractor = new SevenZipExtractor(fileName);
                extractor.Extracting += new EventHandler<ProgressEventArgs>(Extr_Extracting);
                extractor.ExtractionFinished += new EventHandler<EventArgs>(Extr_ExtractionFinished);
                extractor.BeginExtractArchive(directory);
                InfoLabel.Content = "Extracting patch file " + _downloadVersion + "...";
            }
            catch (Exception ex)
            {
                MessageBox.Show(Properties.Resources.Error18 + Environment.NewLine + ex.Message + Environment.NewLine +
                                ex.InnerException + Environment.NewLine + ex.StackTrace);
            }
        }

        private void Extr_ExtractionFinished(object sender, EventArgs e)
        {
            progressBar1.Value = 100;
            InfoLabel.Content = "Finished extracting patch file " + _downloadVersion + "!";
            (sender as SevenZipExtractor)?.Dispose();

            if (!File.Exists(ConfigLocation))
            {
                MessageBox.Show(Properties.Resources.Error1);
                return;
            }

            try
            {
                string oldLine = Properties.Resources.ConfigClientVersion + " = " + (_configValues[Properties.Resources.ConfigClientVersion]);
                string updateLine = Properties.Resources.ConfigClientVersion + " = " + (_downloadVersion);
                string fileText;

                using (StreamReader sr = new StreamReader(ConfigLocation))
                {
                    fileText = sr.ReadToEnd().Replace(oldLine, updateLine);
                }

                using (FileStream fs = File.Open(ConfigLocation, FileMode.Truncate, FileAccess.Write))
                {
                    using (TextWriter tw = new StreamWriter(fs))
                    {
                        tw.Write(fileText);
                    }
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(Properties.Resources.Error2 + Environment.NewLine + exception.Message + exception.StackTrace);
            }

            if (GetConfiguration()) UpdateClient();
        }

        private void Extr_Extracting(object sender, ProgressEventArgs e)
        {
            InfoLabel.Content = "Extracting patch file " + _downloadVersion + ": " + e.PercentDone + "%";
            progressBar1.Value = e.PercentDone;
        }

        private void Wc_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            string bytesIn;
            string totalBytes;
            string speed;

            double bytesRec = e.BytesReceived;
            double bytesToRec = e.TotalBytesToReceive;

            if (e.BytesReceived - _lastBytesRecevied != 0)
            {
                _bytesPerSecond = (e.BytesReceived - _lastBytesRecevied) / (DateTime.UtcNow - _lastReceivedMeasurement).TotalSeconds;
            }

            _lastReceivedMeasurement = DateTime.UtcNow;
            _lastBytesRecevied = e.BytesReceived;

            if (e.TotalBytesToReceive < 1024)
            {
                bytesIn = bytesRec.ToString("F") + "b";
                totalBytes = bytesToRec.ToString("F") + "b";
            }
            else if (e.TotalBytesToReceive < 1048576)
            {
                bytesIn = (bytesRec / 1024).ToString("F") + "kb";
                totalBytes = (bytesToRec / 1024).ToString("F") + "kb";
            }
            else
            {
                bytesIn = (bytesRec / 1024 / 1024).ToString("F") + "mb";
                totalBytes = (bytesToRec / 1024 / 1024).ToString("F") + "mb";
            }

            if (_bytesPerSecond < 1024)
            {
                speed = ((double)_bytesPerSecond).ToString("F") + "b/s";
            }
            else if (_bytesPerSecond < 1048576)
            {
                speed = ((double)_bytesPerSecond / 1024).ToString("F") + "kb/s";
            }
            else
            {
                speed = ((double)_bytesPerSecond / 1024 / 1024).ToString("F") + "mb/s";
            }

            InfoLabel.Content = "Downloading patch " + _downloadVersion + ": " + bytesIn + " / " + totalBytes + " (" + speed + ")";
            progressBar1.Value = e.ProgressPercentage;
        }

        private async void GetFileSecurityInfo()
        {
            if (!_serverconfigValues.ContainsKey(Properties.Resources.ConfigFileServer)) return;
            var fileServer = _serverconfigValues[Properties.Resources.ConfigFileServer].Trim();

            if (!String.IsNullOrWhiteSpace(fileServer))
            {
                if (!fileServer.StartsWith("http://") && !fileServer.StartsWith("https://"))
                {
                    fileServer = fileServer.Insert(0, "http://");
                }
            }
            else
            {
                MessageBox.Show(Properties.Resources.Error11);
                return;
            }

            try
            {
                InfoLabel.Content = "Waiting for reply from launcher file server...";
                var serverResponse = Client.GetAsync(fileServer);
                InfoLabel.Content = "Successfully retrieved file list!";
                var responseContent = await serverResponse.Result.Content.ReadAsStringAsync();

                string[] lines = responseContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                foreach (var line in lines)
                {
                    string[] values = line.Split('=');

                    if (String.IsNullOrWhiteSpace(values[0]))
                    {
                        //MessageBox.Show(Properties.Resources.Error7);
                        continue;
                    }

                    if (values.Length >= 2 && !_serverconfigValues.ContainsKey(values[0].Trim()))
                    {
                        _fileChecks.Add(values[0].Trim(), values[1].Trim());
                    }
                }

                InfoLabel.Content = "Checking client file integrity... 0 / " + _fileChecks.Count;
                int currentFileNumber = 0;

                foreach (var file in _fileChecks)
                {
                    if (!File.Exists(file.Key))
                    {
                        MessageBox.Show(Properties.Resources.Error12 + " " + file.Key);
                        break;
                    }

                    using (var md5 = MD5.Create())
                    {
                        using (var stream = File.OpenRead(file.Key))
                        {
                            var hash = md5.ComputeHash(stream);
                            var hashString = BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();

                            if (file.Value != hashString)
                            {
                                MessageBox.Show(Properties.Resources.Error12 + " " + file.Key);
                                break;
                            }
                        }
                    }

                    currentFileNumber++;
                    InfoLabel.Content = "Checking client file integrity... " + currentFileNumber + " / " + _fileChecks.Count;

                    if (_fileChecks.Count == currentFileNumber) _securityPassed = true;
                }

                if (_fileChecks.Count == 0) _securityPassed = true;

                if (_securityPassed)
                {
                    InfoLabel.Content = "Completed checking client file integrity! Starting game...";
                }
                else
                {
                    InfoLabel.Content = "Client file integrity check failed!";
                }

                _fileChecks.Clear();
            }
            catch (OverflowException)
            {
                MessageBox.Show(Properties.Resources.Error9);
            }
            catch (FormatException)
            {
                MessageBox.Show(Properties.Resources.Error8);
            }
            catch (WebSocketException)
            {
                MessageBox.Show(Properties.Resources.Error5);
            }
            catch (HttpRequestException)
            {
                MessageBox.Show(Properties.Resources.Error19);
            }
            catch (Exception exception)
            {
                MessageBox.Show(Properties.Resources.Error6 + Environment.NewLine + exception.Message + exception.StackTrace);
            }
        }

        private bool OpenLink(string key, bool open = true)
        {
            if (!_serverconfigValues.ContainsKey(key)) return false;
            if (String.IsNullOrWhiteSpace(_serverconfigValues[key])) return false;
            if (!open) return true;

            Process.Start(_serverconfigValues[key]);
            return true;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            OpenLink(Properties.Resources.ConfigButton1Link);
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            OpenLink(Properties.Resources.ConfigButton2Link);
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            OpenLink(Properties.Resources.ConfigButton3Link);
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            OpenLink(Properties.Resources.ConfigButton4Link);
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            OpenLink(Properties.Resources.ConfigButton5Link);
        }

        private void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            string keyName = Properties.Resources.ConfigClientName;

            if (OpenLink(keyName, false))
            {
                if (File.Exists(_serverconfigValues[keyName]))
                {
                    string clientip = Properties.Resources.ConfigClientIp;
                    if (!OpenLink(clientip, false)) return;
                    GetFileSecurityInfo();
                    if (!_securityPassed) return;

                    _securityPassed = false;
                    Process.Start(_serverconfigValues[keyName], "-i " + _serverconfigValues[clientip].Trim());
                    Environment.Exit(0);
                }
                else
                {
                    MessageBox.Show(Properties.Resources.Error3);
                }
            }
        }

        private void LaunchButton_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && e.ChangedButton == MouseButton.Right)
            {
                if (MessageBox.Show("Would you like to repatch?", "Repatch", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                {
                    return;
                }

                if (!File.Exists(ConfigLocation))
                {
                    MessageBox.Show(Properties.Resources.Error1);
                    return;
                }

                try
                {
                    string oldLine = Properties.Resources.ConfigClientVersion + " = " + (_configValues[Properties.Resources.ConfigClientVersion]);
                    string updateLine = Properties.Resources.ConfigClientVersion + " = 0";
                    string fileText;

                    using (StreamReader sr = new StreamReader(ConfigLocation))
                    {
                        fileText = sr.ReadToEnd().Replace(oldLine, updateLine);
                    }

                    using (FileStream fs = File.Open(ConfigLocation, FileMode.Truncate, FileAccess.Write))
                    {
                        using (TextWriter tw = new StreamWriter(fs))
                        {
                            tw.Write(fileText);
                        }
                    }

                    if (GetConfiguration()) GetPatchLinks();
                }
                catch (Exception exception)
                {
                    MessageBox.Show(Properties.Resources.Error2 + Environment.NewLine + exception.Message + exception.StackTrace);
                }
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Debug.WriteLine(e.GetPosition(this).Y);
            if (e.ChangedButton == MouseButton.Left && e.GetPosition(this).Y <= 20)
                DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MinimiseButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }
    }
}
