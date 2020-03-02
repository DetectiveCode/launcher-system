using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Windows;
using SevenZip;

namespace LauncherUpdater
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string ConfigLocation = "reslauncher/config.txt";

        private readonly Dictionary<string, string> _configValues = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _serverConfigValues = new Dictionary<string, string>();
        private readonly Dictionary<int, string> _launcherPatchLinks = new Dictionary<int, string>();
        private static readonly HttpClient Client = new HttpClient();

        private int _downloadVersion;
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
            //SevenZipBase.SetLibraryPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "7z.dll"));

            GetLauncherConfig();
        }

        private bool GetConfig()
        {
            _configValues.Clear();

            if (!File.Exists(ConfigLocation))
            {
                MessageBox.Show("Error no config file!");
                Environment.Exit(0);
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

                StatusLabel.Content = "Reading config file...";

                return true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Error reading config file!" + e.StackTrace);
            }

            return false;
        }

        private async void GetLauncherConfig()
        {
            if (!GetConfig()) return;
            if (!_configValues.ContainsKey(Properties.Resources.ConfigLauncherConfig))
            {
                MessageBox.Show("Could not find launcher config server!");
                return;
            }

            var configFile = _configValues[Properties.Resources.ConfigLauncherConfig];

            if (!String.IsNullOrWhiteSpace(configFile))
            {
                if (!configFile.StartsWith("http://") && !configFile.StartsWith("https://"))
                {
                    configFile = configFile.Insert(0, "http://");
                }
            }
            else
            {
                MessageBox.Show("Launcher config corrupted!");
                return;
            }

            try
            {
                StatusLabel.Content = "Trying to connect to launcher server...";
                var serverResponse = await Client.GetAsync(configFile);

                if (!serverResponse.IsSuccessStatusCode)
                {
                    StatusLabel.Content = "Failed to connect to launcher server!";
                    return;
                }

                StatusLabel.Content = "Successfully connected to launcher server!";
                var responseContent = await serverResponse.Content.ReadAsStringAsync();

                string[] lines = responseContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                foreach (var line in lines)
                {
                    string[] values = line.Split('=');

                    if (String.IsNullOrWhiteSpace(values[0]))
                    {
                        MessageBox.Show("Patch file is corrupt!");
                        break;
                    }

                    if (values.Length >= 2 && !_serverConfigValues.ContainsKey(values[0].Trim()))
                    {
                        _serverConfigValues.Add(values[0].Trim(), values[1].Trim());
                    }
                }

                CheckLauncherVersion();

            }
            catch (WebSocketException)
            {
                MessageBox.Show("Could not connect to server.");
            }
            catch (HttpRequestException)
            {
                MessageBox.Show("Failed connecting to launcher server, please check your internet connection.");
            }
            catch (Exception exception)
            {
                MessageBox.Show("Unknown error report to admin." + Environment.NewLine + exception.InnerException);
            }
        }

        private async void CheckLauncherVersion()
        {
            if (!_serverConfigValues.ContainsKey(Properties.Resources.ConfigLauncherPatchServer))
            {
                StatusLabel.Content = "Failed checking launcher version.";
                return;
            }
            var launcherPatchlink = _serverConfigValues[Properties.Resources.ConfigLauncherPatchServer];

            if (!String.IsNullOrWhiteSpace(launcherPatchlink))
            {
                if (!launcherPatchlink.StartsWith("http://") && !launcherPatchlink.StartsWith("https://"))
                {
                    launcherPatchlink = launcherPatchlink.Insert(0, "http://");
                }
            }
            else
            {
                MessageBox.Show("Corrupt launcher launcher patch link!");
                return;
            }

            try
            {
                StatusLabel.Content = "Waiting for reply from launcher patch server...";
                var serverResponse = await Client.GetAsync(launcherPatchlink);
                StatusLabel.Content = "Checking launcher version...";
                var responseContent = await serverResponse.Content.ReadAsStringAsync();

                string[] lines = responseContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                foreach (var line in lines)
                {
                    string[] values = line.Split('#');

                    if (String.IsNullOrWhiteSpace(values[0]))
                    {
                        MessageBox.Show("Corrupt launcher line!");
                        continue;
                    }

                    if (values.Length >= 2 && !_launcherPatchLinks.ContainsKey(Int32.Parse(values[0].Trim())))
                    {
                        _launcherPatchLinks.Add(Int32.Parse(values[0]), values[1].Trim());
                    }
                }

                UpdateClient();
            }
            catch (OverflowException)
            {
                MessageBox.Show("Too many patches!");
            }
            catch (FormatException)
            {
                MessageBox.Show("Incorrect format, expected number!");
            }
            catch (WebSocketException)
            {
                MessageBox.Show("Failed connecting to launcher patch link!");
            }
            catch (HttpRequestException)
            {
                MessageBox.Show("Failed connecting to launcher server, please check your internet connection.");
            }
            catch (Exception exception)
            {
                MessageBox.Show("Unknown error. Please report to admin!" + Environment.NewLine + exception.Message + exception.StackTrace);
            }
        }

        private void UpdateClient()
        {
            StatusLabel.Content = "Checking client version...";
            if (!_configValues.ContainsKey(Properties.Resources.ConfigLauncherVersion)) return;
            int clientVersion = Int32.Parse(_configValues[Properties.Resources.ConfigLauncherVersion]);

            if (clientVersion >= _launcherPatchLinks.Count)
            {
                StatusLabel.Content = "Client is up to date!";
                progressBar1.Value = progressBar1.Maximum;

                if (!_serverConfigValues.ContainsKey(Properties.Resources.ConfigLauncherName))
                {
                    MessageBox.Show("Can't find launcher exe!");
                    StatusLabel.Content = "Failed opening launcher.";
                    return;
                }

                if (!File.Exists(_serverConfigValues[Properties.Resources.ConfigLauncherName]))
                {
                    MessageBox.Show("Failed to find launcher file!");
                    return;
                }

                Process.Start(_serverConfigValues[Properties.Resources.ConfigLauncherName]);
                Environment.Exit(0);
            }

            _downloadVersion = clientVersion + 1;
            string downloadLink = _launcherPatchLinks[_downloadVersion].Trim();

            if (!String.IsNullOrWhiteSpace(downloadLink))
            {
                if (!downloadLink.StartsWith("http://") && !downloadLink.StartsWith("https://"))
                {
                    downloadLink = downloadLink.Insert(0, "http://");
                }
            }
            else
            {
                MessageBox.Show("Error reading launcher patch link!");
                return;
            }

            string patchDirectory = "reslauncher\\launcherpatches";
            string patchFile = patchDirectory + "\\patch" + _downloadVersion + ".7z";

            if (!Directory.Exists(patchDirectory)) Directory.CreateDirectory(patchDirectory);

            try
            {
                using (WebClient wc = new WebClient())
                {
                    StatusLabel.Content = "Downloading patch " + _downloadVersion;
                    progressBar1.Maximum = 100;
                    wc.DownloadProgressChanged += Wc_DownloadProgressChanged;
                    wc.DownloadFileCompleted += (sender, e) => Wc_DownloadFileCompleted(sender, e, patchFile);
                    wc.DownloadFileAsync(new System.Uri(downloadLink), patchFile);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("Failed connecting to patch server." + Environment.NewLine + e.Message + Environment.NewLine + e.InnerException + Environment.NewLine + e.StackTrace);
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
                StatusLabel.Content = "Extracting patch file " + _downloadVersion + "...";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error extracting patch file." + Environment.NewLine + ex.Message + Environment.NewLine + ex.InnerException + Environment.NewLine + ex.StackTrace);
            }
        }

        private void Extr_ExtractionFinished(object sender, EventArgs e)
        {
            progressBar1.Value = 100;
            StatusLabel.Content = "Finished extracting patch file " + _downloadVersion + "!";
            (sender as SevenZipExtractor)?.Dispose();

            if (!File.Exists(ConfigLocation))
            {
                MessageBox.Show("No config file!");
                return;
            }

            try
            {
                string oldLine = Properties.Resources.ConfigLauncherVersion + " = " + (_configValues[Properties.Resources.ConfigLauncherVersion]);
                string updateLine = Properties.Resources.ConfigLauncherVersion + " = " + (_downloadVersion);
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
                MessageBox.Show("Failed reading config file!" + Environment.NewLine + exception.Message + exception.StackTrace);
            }

            if (GetConfig()) UpdateClient();
        }

        private void Extr_Extracting(object sender, ProgressEventArgs e)
        {
            StatusLabel.Content = "Extracting patch file " + _downloadVersion + ": " + e.PercentDone + "%";
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

            StatusLabel.Content = "Downloading patch " + _downloadVersion + ": " + bytesIn + " / " + totalBytes + " (" + speed + ")";
            progressBar1.Value = e.ProgressPercentage;
        }
    }
}
