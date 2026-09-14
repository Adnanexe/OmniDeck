using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using LibreHardwareMonitor.Hardware;

namespace OmniDeck
{
    public partial class MainWindow : Window
    {
        private PerformanceCounter? cpuCounter;
        private PerformanceCounter? ramCounter;
        private Computer? _computer;
        private string _actualIPAddress = "N/A";
        private bool _ipAddressVisible = false;

        public MainWindow()
        {
            InitializeComponent();

            // 1. Sigurna inicijalizacija LibreHardwareMonitora za GPU
            try
            {
                _computer = new Computer
                {
                    IsGpuEnabled = true
                };
                _computer.Open();
            }
            catch
            {
                // Ako nema Admin prava ili drajver ne može startati, ne rušimo aplikaciju
                _computer = null;
            }

            // 2. Inicijalizacija CPU brojača
            try
            {
                cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                cpuCounter.NextValue(); // Inicijalno očitanje
            }
            catch
            {
                cpuCounter = null;
            }

            // 3. Inicijalizacija RAM brojača
            try
            {
                ramCounter = new PerformanceCounter("Memory", "Available MBytes");
            }
            catch
            {
                ramCounter = null;
            }

            // 4. Pokretanje Timera za osvježavanje ekrana svakih 1 Sekundu
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += UpdateDashboard;
            timer.Start();

            UpdateDashboard(null, null);
            
            // Učitaj SystemInfo podatke nakon što se UI inicijalizira
            _ = LoadSystemInfoAsync();
        }

        private async Task LoadSystemInfoAsync()
        {
            // Pričekaj da se UI renderira
            await Task.Delay(500);
            LoadSystemInfo();
        }

        // Importacija Windows API funkcije za očitavanje ukupne sistemske memorije (RAM)
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool GetPhysicallyInstalledSystemMemory(out long totalMemoryInKilobytes);

        private void UpdateDashboard(object? sender, EventArgs? e)
        {
            // Vrijeme i Datum
            ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
            DateText.Text = DateTime.Now.ToString("dddd, d. MMMM yyyy.");
            UptimeText.Text = $"Uptime: {FormatUptime(TimeSpan.FromMilliseconds(Environment.TickCount64))}";

            try
            {
                DriveInfo systemDrive = new DriveInfo("C");
                long usedBytes = systemDrive.TotalSize - systemDrive.AvailableFreeSpace;
                int diskUsage = (int)Math.Clamp((double)usedBytes / systemDrive.TotalSize * 100, 0, 100);
                DiskText.Text = $"{diskUsage}%";
            }
            catch
            {
                DiskText.Text = "N/A";
            }

            // --- CPU LOGIKA ---
            if (cpuCounter != null)
            {
                try
                {
                    int cpuVal = (int)cpuCounter.NextValue();
                    CpuText.Text = $"{cpuVal}%";
                }
                catch
                {
                    CpuText.Text = "0%";
                }
            }
            else
            {
                CpuText.Text = "N/A";
            }

            // --- RAM LOGIKA ---
            if (ramCounter != null)
            {
                try
                {
                    float availableMB = ramCounter.NextValue(); // Slobodna memorija u MB
                    
                    if (GetPhysicallyInstalledSystemMemory(out long totalMemoryKB) && totalMemoryKB > 0)
                    {
                        float totalMB = totalMemoryKB / 1024f; // Ukupna memorija u MB
                        float usedMB = totalMB - availableMB;
                        int ramVal = (int)((usedMB / totalMB) * 100);

                        ramVal = Math.Clamp(ramVal, 0, 100);
                        RamText.Text = $"{ramVal}%";
                    }
                    else
                    {
                        RamText.Text = "N/A";
                    }
                }
                catch
                {
                    RamText.Text = "N/A";
                }
            }
            else
            {
                RamText.Text = "N/A";
            }

            // --- GPU LOGIKA ---
            float? gpuUsage = null;

            if (_computer != null)
            {
                try
                {
                    foreach (IHardware hardware in _computer.Hardware)
                    {
                        if (hardware.HardwareType == HardwareType.GpuNvidia || 
                            hardware.HardwareType == HardwareType.GpuAmd || 
                            hardware.HardwareType == HardwareType.GpuIntel)
                        {
                            hardware.Update();
                            foreach (ISensor sensor in hardware.Sensors)
                            {
                                if (sensor.SensorType == SensorType.Load && sensor.Name.ToLower().Contains("gpu"))
                                {
                                    gpuUsage = sensor.Value;
                                    break;
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            if (GpuText != null)
            {

                if (gpuUsage.HasValue)
                {
                    GpuText.Text = $"{Math.Round(gpuUsage.Value)}%";
                }
                else
                {
                    GpuText.Text = "N/A";
                }
            }
        }

        private static string FormatUptime(TimeSpan uptime)
        {
            if (uptime.TotalDays >= 1)
            {
                return $"{(int)uptime.TotalDays}d {uptime.Hours}h";
            }

            return $"{uptime.Hours}h {uptime.Minutes}m";
        }

        private void LoadSystemInfo()
        {
            try
            {
                string osVer = GetWindowsVersion() ?? "N/A";
                OSVersionText.Text = osVer;
            }
            catch (Exception ex) { OSVersionText.Text = $"Error: {ex.GetType().Name}"; }

            try
            {
                string proc = GetProcessorInfo() ?? "N/A";
                ProcessorText.Text = proc;
            }
            catch (Exception ex) { ProcessorText.Text = $"Error: {ex.GetType().Name}"; }

            try
            {
                string ram = GetTotalRAM() ?? "N/A";
                RAMTotalText.Text = ram;
            }
            catch (Exception ex) { RAMTotalText.Text = $"Error: {ex.GetType().Name}"; }

            try
            {
                string gpu = GetGPUInfo() ?? "N/A";
                GPUText.Text = gpu;
            }
            catch (Exception ex) { GPUText.Text = $"Error: {ex.GetType().Name}"; }

            try
            {
                string disk = GetDiskInfo() ?? "N/A";
                DiskTotalText.Text = disk;
            }
            catch (Exception ex) { DiskTotalText.Text = $"Error: {ex.GetType().Name}"; }

            try
            {
                string ip = GetIPAddress() ?? "N/A";
                _actualIPAddress = ip;
                _ipAddressVisible = false;
                IPAddressText.Text = "***.***.***.***.***";
            }
            catch (Exception ex) { IPAddressText.Text = $"Error: {ex.GetType().Name}"; }

            try
            {
                string user = Environment.UserName ?? "N/A";
                UsernameText.Text = user;
            }
            catch (Exception ex) { UsernameText.Text = $"Error: {ex.GetType().Name}"; }
        }

        private string GetWindowsVersion()
        {
            try
            {
                // Environment.OSVersion dobro radi
                var osVersion = Environment.OSVersion;
                return osVersion.VersionString;
            }
            catch { }
            return "N/A";
        }

        private string GetProcessorInfo()
        {
            try
            {
                return $"{Environment.ProcessorCount} Cores";
            }
            catch { }
            return "N/A";
        }

        private string GetTotalRAM()
        {
            try
            {
                // Koristi GetPhysicallyInstalledSystemMemory API
                if (GetPhysicallyInstalledSystemMemory(out long totalMemoryInKilobytes))
                {
                    long gb = totalMemoryInKilobytes / (1024 * 1024);
                    return $"{gb} GB";
                }
            }
            catch { }
            return "N/A";
        }

        private string GetGPUInfo()
        {
            try
            {
                Computer computer = new Computer();
                computer.IsCpuEnabled = false;
                computer.IsGpuEnabled = true;
                computer.Open();
                
                foreach (var hardware in computer.Hardware)
                {
                    if (hardware.HardwareType == HardwareType.GpuAmd || 
                        hardware.HardwareType == HardwareType.GpuIntel || 
                        hardware.HardwareType == HardwareType.GpuNvidia)
                    {
                        return hardware.Name;
                    }
                }
            }
            catch { }
            return "N/A";
        }

        private string GetDiskInfo()
        {
            try
            {
                DriveInfo drive = new DriveInfo("C");
                long totalGB = drive.TotalSize / (1024 * 1024 * 1024);
                return $"{totalGB} GB";
            }
            catch { }
            return "N/A";
        }

        private string GetIPAddress()
        {
            try
            {
                var hostEntry = Dns.GetHostEntry(Dns.GetHostName());
                var ipv4 = hostEntry.AddressList.FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                return ipv4?.ToString() ?? "N/A";
            }
            catch { }
            return "N/A";
        }

        private void Window_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                return;
            }

            ShowInTaskbar = true;
            Visibility = Visibility.Visible;
            Activate();
            Focus();
        }

        private void IPToggleButton_Click(object sender, RoutedEventArgs e)
        {
            _ipAddressVisible = !_ipAddressVisible;
            IPAddressText.Text = _ipAddressVisible ? _actualIPAddress : "***.***.***.***.***";
        }

        // --- STEAM LOGIC ---
        private void OpenSteamTab_Click(object sender, RoutedEventArgs e)
        {
            SteamTab.Visibility = Visibility.Visible;
            MainTabControl.SelectedItem = SteamTab;
            LoadSteamGames();
        }

        private void BackToGames_Click(object sender, RoutedEventArgs e)
        {
            SteamTab.Visibility = Visibility.Collapsed;
            MainTabControl.SelectedIndex = 1;
        }

        private void LoadSteamGames()
        {
            SteamGamesPanel.Children.Clear();

            List<string> steamFolders = GetSteamLibraryFolders();
            Style? gameBtnStyle = FindResource("GameButton") as Style;
            int totalGamesFound = 0;

            foreach (string folder in steamFolders)
            {
                if (!Directory.Exists(folder)) continue;

                string[] manifestFiles = Directory.GetFiles(folder, "appmanifest_*.acf");

                foreach (string file in manifestFiles)
                {
                    try
                    {
                        string content = File.ReadAllText(file);
                        
                        string nameMatch = Regex.Match(content, "\"name\"\\s+\"([^\"]+)\"").Groups[1].Value;
                        string appIdMatch = Regex.Match(content, "\"appid\"\\s+\"([^\"]+)\"").Groups[1].Value;

                        if (!string.IsNullOrEmpty(nameMatch) && !string.IsNullOrEmpty(appIdMatch))
                        {
                            Button gameButton = new Button
                            {
                                Content = nameMatch,
                                Tag = appIdMatch,
                                Style = gameBtnStyle
                            };

                            gameButton.Click += LaunchSteamGame_Click;
                            SteamGamesPanel.Children.Add(gameButton);
                            totalGamesFound++;
                        }
                    }
                    catch { }
                }
            }

            if (totalGamesFound == 0)
            {
                TextBlock noGamesText = new TextBlock
                {
                    Text = "Nijedna instalirana Steam igra nije pronađena.",
                    Foreground = System.Windows.Media.Brushes.Gray,
                    FontSize = 14
                };
                SteamGamesPanel.Children.Add(noGamesText);
            }
        }

        private List<string> GetSteamLibraryFolders()
        {
            List<string> folders = new List<string>();
            string defaultPath = @"C:\Program Files (x86)\Steam\steamapps";
            if (Directory.Exists(defaultPath))
            {
                folders.Add(defaultPath);

                string vdfPath = Path.Combine(defaultPath, "libraryfolders.vdf");
                if (File.Exists(vdfPath))
                {
                    try
                    {
                        string content = File.ReadAllText(vdfPath);
                        MatchCollection matches = Regex.Matches(content, "\"path\"\\s+\"([^\"]+)\"");

                        foreach (Match match in matches)
                        {
                            string extraPath = match.Groups[1].Value.Replace(@"\\", @"\");
                            string steamAppsExtra = Path.Combine(extraPath, "steamapps");

                            if (Directory.Exists(steamAppsExtra) && !folders.Contains(steamAppsExtra))
                            {
                                folders.Add(steamAppsExtra);
                            }
                        }
                    }
                    catch { }
                }
            }

            return folders;
        }

        private void LaunchSteamGame_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string appId)
            {
                Process.Start(new ProcessStartInfo($"steam://run/{appId}") { UseShellExecute = true });
            }
        }

        // --- DOWNLOAD APPS LOGIC (Winget & Web) ---
        private void InstallApp(string packageId)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", $"/c winget install --id {packageId} -e --accept-package-agreements --accept-source-agreements")
                {
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Greška pri pokretanju instalacije: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InstallSelectedApps_Click(object sender, RoutedEventArgs e)
        {
            CheckBox[] selections =
            {
                DiscordSelection,
                SteamSelection,
                ChromeSelection,
                BraveSelection,
                SpotifySelection,
                VSCodeSelection,
                ZenSelection,
                RockstarSelection,
                SevenZipSelection,
                VlcSelection,
                GitSelection,
                ObsSelection,
                PowerToysSelection,
                WhatsAppSelection,
                EpicSelection
            };

            List<string> packageIds = selections
                .Where(selection => selection.IsChecked == true && selection.Tag is string)
                .Select(selection => (string)selection.Tag)
                .ToList();

            if (packageIds.Count == 0)
            {
                MessageBox.Show("Odaberi barem jednu aplikaciju.", "Nema odabira", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string installArguments = string.Join(" && ", packageIds.Select(packageId =>
                $"winget install --id {packageId} -e --accept-package-agreements --accept-source-agreements"));

            try
            {
                Process.Start(new ProcessStartInfo("cmd.exe", $"/c {installArguments}")
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Greška pri pokretanju grupne instalacije: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearAppSelection_Click(object sender, RoutedEventArgs e)
        {
            DiscordSelection.IsChecked = false;
            SteamSelection.IsChecked = false;
            ChromeSelection.IsChecked = false;
            BraveSelection.IsChecked = false;
            SpotifySelection.IsChecked = false;
            VSCodeSelection.IsChecked = false;
            ZenSelection.IsChecked = false;
            RockstarSelection.IsChecked = false;
            SevenZipSelection.IsChecked = false;
            VlcSelection.IsChecked = false;
            GitSelection.IsChecked = false;
            ObsSelection.IsChecked = false;
            PowerToysSelection.IsChecked = false;
            WhatsAppSelection.IsChecked = false;
            EpicSelection.IsChecked = false;
            UpdateSelectedAppsText();
        }

        private void AppSelection_Changed(object sender, RoutedEventArgs e)
        {
            UpdateSelectedAppsText();
        }

        private void UpdateSelectedAppsText()
        {
            CheckBox[] selections =
            {
                DiscordSelection,
                SteamSelection,
                ChromeSelection,
                BraveSelection,
                SpotifySelection,
                VSCodeSelection,
                ZenSelection,
                RockstarSelection,
                SevenZipSelection,
                VlcSelection,
                GitSelection,
                ObsSelection,
                PowerToysSelection,
                WhatsAppSelection,
                EpicSelection
            };

            int selectedCount = selections.Count(selection => selection.IsChecked == true);
            SelectedAppsText.Text = $"{selectedCount} odabrano";
        }

        private void OpenUrl(string url)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        private void InstallSevenZip_Click(object sender, RoutedEventArgs e) => InstallApp("7zip.7zip");
        private void WebSevenZip_Click(object sender, RoutedEventArgs e) => OpenUrl("https://www.7-zip.org/");
        private void InstallVlc_Click(object sender, RoutedEventArgs e) => InstallApp("VideoLAN.VLC");
        private void WebVlc_Click(object sender, RoutedEventArgs e) => OpenUrl("https://www.videolan.org/vlc/");
        private void InstallGit_Click(object sender, RoutedEventArgs e) => InstallApp("Git.Git");
        private void WebGit_Click(object sender, RoutedEventArgs e) => OpenUrl("https://git-scm.com/downloads");
        private void InstallObs_Click(object sender, RoutedEventArgs e) => InstallApp("OBSProject.OBSStudio");
        private void WebObs_Click(object sender, RoutedEventArgs e) => OpenUrl("https://obsproject.com/download");
        private void InstallPowerToys_Click(object sender, RoutedEventArgs e) => InstallApp("Microsoft.PowerToys");
        private void WebPowerToys_Click(object sender, RoutedEventArgs e) => OpenUrl("https://learn.microsoft.com/windows/powertoys/");
        private void InstallWhatsApp_Click(object sender, RoutedEventArgs e) => InstallApp("9NKSQGP7F2NH");
        private void WebWhatsApp_Click(object sender, RoutedEventArgs e) => OpenUrl("https://www.whatsapp.com/download");
        private void InstallEpicLauncher_Click(object sender, RoutedEventArgs e) => InstallApp("EpicGames.EpicGamesLauncher");
        private void WebEpicLauncher_Click(object sender, RoutedEventArgs e) => OpenUrl("https://store.epicgames.com/download");

        // Discord
        private void InstallDiscord_Click(object sender, RoutedEventArgs e) => InstallApp("Discord.Discord");
        private void WebDiscord_Click(object sender, RoutedEventArgs e) => OpenUrl("https://discord.com/download");

        // Steam
        private void InstallSteam_Click(object sender, RoutedEventArgs e) => InstallApp("Valve.Steam");
        private void WebSteam_Click(object sender, RoutedEventArgs e) => OpenUrl("https://store.steampowered.com/about/");

        // Chrome
        private void InstallChrome_Click(object sender, RoutedEventArgs e) => InstallApp("Google.Chrome");
        private void WebChrome_Click(object sender, RoutedEventArgs e) => OpenUrl("https://www.google.com/chrome/");

        // Brave
        private void InstallBrave_Click(object sender, RoutedEventArgs e) => InstallApp("Brave.Brave");
        private void WebBrave_Click(object sender, RoutedEventArgs e) => OpenUrl("https://brave.com/");

        // Spotify
        private void InstallSpotify_Click(object sender, RoutedEventArgs e) => InstallApp("Spotify.Spotify");
        private void WebSpotify_Click(object sender, RoutedEventArgs e) => OpenUrl("https://www.spotify.com/download/");

        // VS Code
        private void InstallVSCode_Click(object sender, RoutedEventArgs e) => InstallApp("Microsoft.VisualStudioCode");
        private void WebVSCode_Click(object sender, RoutedEventArgs e) => OpenUrl("https://code.visualstudio.com/");

        // Zen Browser
        private void InstallZenBrowser_Click(object sender, RoutedEventArgs e) => InstallApp("Zen-Team.Zen-Browser");
        private void WebZenBrowser_Click(object sender, RoutedEventArgs e) => OpenUrl("https://zen-browser.app/");

        // Riot Client
        private void InstallRiotClient_Click(object sender, RoutedEventArgs e) => OpenUrl("https://www.riotgames.com");
        private void WebRiotClient_Click(object sender, RoutedEventArgs e) => OpenUrl("https://www.riotgames.com");

        // Rockstar Games Launcher
        private void InstallRockstarLauncher_Click(object sender, RoutedEventArgs e) => InstallApp("RockstarGames.Launcher");
        private void WebRockstarLauncher_Click(object sender, RoutedEventArgs e) => OpenUrl("https://socialclub.rockstargames.com/rockstar-games-launcher");

        // --- TOOLS ---
        private void OpenChrisutl_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string script = "irm christitus.com/win | iex";

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Process.Start(psi);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                MessageBox.Show("Moraš pokrenuti kao administrator", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch
            {
                MessageBox.Show("Greška prilikom pokretanja Chris' Ultimate Tools.", "Greška", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OpenWin11Debloat_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string script = "& ([scriptblock]::Create((irm \"https://debloat.raphi.re/\")))";
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Process.Start(psi);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                MessageBox.Show("Moraš pokrenuti kao administrator", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch
            {
                MessageBox.Show("Greška prilikom pokretanja Win11Debloat.", "Greška", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OpenCalculator_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo("calc.exe") { UseShellExecute = true });
        private void OpenNotepad_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo("notepad.exe") { UseShellExecute = true });
        private void OpenTaskManager_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true });
        private void OpenCMD_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo("cmd.exe") { UseShellExecute = true });

        private void RestartPC_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Jeste li sigurni da želite ponovno pokrenuti računalo?", "Potvrda", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                Process.Start("shutdown.exe", "/r /t 0");
            }
        }

        private void ShutdownPC_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Jeste li sigurni da želite ugasiti računalo?", "Potvrda", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                Process.Start("shutdown.exe", "/s /t 0");
            }
        }

        private void OpenEpic_Click(object sender, RoutedEventArgs e)
        {
            TryStartProcess(@"C:\Program Files (x86)\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe");
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("ms-settings:") { UseShellExecute = true });
        }

        private void TryStartProcess(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch
            {
                MessageBox.Show($"Aplikacija nije pronađena na lokaciji:\n{path}", "Greška", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // --- ZATVARANJE APLIKACIJE ---
        protected override void OnClosed(EventArgs e)
        {
            try
            {
                _computer?.Close();
            }
            catch { }

            base.OnClosed(e);
        }
    
            // --- LOGIKA ZA DRAJVERE ---

        // 1. NVIDIA App / Drivers (Otvara službenu stranicu za preuzimanje)
        private void DownloadNvidiaDrivers_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://www.nvidia.com/Download/index.aspx");
        }

        // 2. AMD Auto-Detect (Otvara službenu AMD stranicu s alatom za detekciju)
        private void DownloadAmdDrivers_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://www.amd.com/en/support/download/drivers.html");
        }

        // 3. Intel Driver & Support Assistant (Instalira alat putem winget-a)
        private void InstallIntelAssistant_Click(object sender, RoutedEventArgs e)
        {
            InstallApp("Intel.DriverAndSupportAssistant");
        }

        // 4. Otvara Windows Update postavke izravno na dijelu za ažuriranje drajvera
        private void OpenWindowsUpdateDrivers_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:windowsupdate-action") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Greška pri otvaranju Windows Updatea: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void AutoInstallDrivers_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(
                "Ovaj proces će automatski potražiti i instalirati sve nedostajuće drajvere putem Windows Update-a.\n\nTo može potrajati nekoliko minuta. Želite li nastaviti?",
                "Automatska instalacija drajvera",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information
            );

            if (result != MessageBoxResult.Yes) return;

            try
            {
                // PowerShell komanda koja instalira modul ako nedostaje i pokreće pretragu/instalaciju drajvera
                string psScript = @"
                    Set-ExecutionPolicy Bypass -Scope Process -Force;
                    if (-not (Get-Module -ListAvailable -Name PSWindowsUpdate)) {
                        Install-Module -Name PSWindowsUpdate -Force -SkipPublisherCheck -Scope CurrentUser;
                    }
                    Import-Module PSWindowsUpdate;
                    Get-WindowsUpdate -AcceptAll -Install -AutoReboot:$false -UpdateType Driver;
                ";

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psScript}\"",
                    UseShellExecute = true,
                    Verb = "runas" // Pokreće kao Administrator
                };

                Process.Start(psi);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                MessageBox.Show("Za instalaciju drajvera potrebna su administratorska prava.", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Greška pri pokretanju skripte: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}