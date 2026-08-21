using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace OmniDeck
{
    public partial class MainWindow : Window
    {
        private PerformanceCounter? cpuCounter;
        private PerformanceCounter? gpuCounter;

        public MainWindow()
        {
            InitializeComponent();

            try
            {
                cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            }
            catch
            {
                cpuCounter = null;
            }

            try
            {
                PerformanceCounterCategory category = new PerformanceCounterCategory("GPU Engine");
                string[] instanceNames = category.GetInstanceNames();
                
                foreach (string name in instanceNames)
                {
                    if (name.EndsWith("engtype_3D"))
                    {
                        gpuCounter = new PerformanceCounter("GPU Engine", "Utilization Percentage", name);
                        break;
                    }
                }
            }
            catch
            {
                gpuCounter = null;
            }

            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += UpdateDashboard;
            timer.Start();

            UpdateDashboard(null, null);
        }

        private void UpdateDashboard(object? sender, EventArgs? e)
        {
            ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
            DateText.Text = DateTime.Now.ToString("dddd, d. MMMM yyyy.");

            if (cpuCounter != null)
            {
                int cpuVal = (int)cpuCounter.NextValue();
                CpuText.Text = $"{cpuVal}%";
            }
            else
            {
                CpuText.Text = "N/A";
            }

            var totalAvailableBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            var allocatedBytes = GC.GetTotalMemory(false);
            if (totalAvailableBytes > 0)
            {
                int ramVal = (int)((allocatedBytes * 100) / totalAvailableBytes);
                RamText.Text = $"{ramVal}%";
            }
            else
            {
                RamText.Text = "N/A";
            }

            if (gpuCounter != null)
            {
                try
                {
                    int gpuVal = (int)gpuCounter.NextValue();
                    GpuText.Text = $"{gpuVal}%";
                }
                catch
                {
                    GpuText.Text = "0%";
                }
            }
            else
            {
                GpuText.Text = "N/A";
            }
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

        private void OpenUrl(string url)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

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
                MessageBox.Show("Moras pokrenuiti kao administrator", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch
            {
                MessageBox.Show("Greška prilikom pokretanja Chris' Ultimate Tools.", "Greška", MessageBoxButton.OK, MessageBoxImage.Warning);
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
    }
}