using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using StalkingStairsModManager.Models;
using StalkingStairsModManager.Services;

namespace StalkingStairsModManager
{
    public partial class MainWindow : Window
    {
        private string gamePath;
        private readonly GitHubModService modService = new();

        public MainWindow()
        {
            InitializeComponent();
            DetectGamePath();
            _ = LoadMods();
        }

        private void SetStatus(string text, System.Windows.Media.Color? color = null)
        {
            StatusText.Text = text;
            if (color.HasValue)
            {
                StatusText.Foreground = new SolidColorBrush(color.Value);
            }
            else
            {
                StatusText.Foreground = new SolidColorBrush(System.Windows.Media.Colors.LightGray);
            }
        }

        private void DetectGamePath()
        {
            PathStatus.Text = "Detecting common Steam install locations...";
            SetStatus("Detecting path...", System.Windows.Media.Colors.Orange);

            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string candidate = Path.Combine(programFilesX86, "Steam", "steamapps", "common", "Jaden Williams' The Stalking Stairs");

            // try alternate ProgramFiles if not found
            if (!Directory.Exists(candidate))
            {
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string alt = Path.Combine(programFiles, "Steam", "steamapps", "common", "Jaden Williams' The Stalking Stairs");
                if (Directory.Exists(alt))
                {
                    candidate = alt;
                }
            }

            if (Directory.Exists(candidate))
            {
                gamePath = candidate;
                PathStatus.Text = $"Auto-detected: {gamePath}";
                PathStatus.Foreground = new SolidColorBrush(System.Windows.Media.Colors.LightGreen);
                SetStatus("Game path auto-detected", System.Windows.Media.Colors.LightGreen);
                return;
            }

            // not auto-detected -> prompt user
            var locate = new LocateGameWindow();
            if (locate.ShowDialog() == true)
            {
                gamePath = locate.SelectedPath;
                PathStatus.Text = $"Selected: {gamePath}";
                PathStatus.Foreground = new SolidColorBrush(System.Windows.Media.Colors.LightGreen);
                SetStatus("Game path selected", System.Windows.Media.Colors.LightGreen);
            }
            else
            {
                System.Windows.MessageBox.Show("Game path is required.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                SetStatus("No game path provided", System.Windows.Media.Colors.Red);
                Close();
            }
        }

        private async Task LoadMods()
        {
            try
            {
                SetStatus("Loading mods...", System.Windows.Media.Colors.Orange);
                List<ModInfo> mods = await modService.GetModsAsync();
                ModsGrid.ItemsSource = mods;
                SetStatus($"Loaded {mods?.Count ?? 0} mods", System.Windows.Media.Colors.LightGray);
            }
            catch (Exception ex)
            {
                string logPath = LogException(ex, "Failed to load mods");
                string userMessage = "Failed to load mods. A detailed error log has been saved.";
                System.Windows.MessageBox.Show($"{userMessage}\n\nLog file: {logPath}", "Error loading mods", MessageBoxButton.OK, MessageBoxImage.Error);
                SetStatus("Failed to load mods (see logs)", System.Windows.Media.Colors.Red);
            }
        }

        private string LogException(Exception ex, string context)
        {
            try
            {
                string baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StalkingStairsModManager", "logs");
                Directory.CreateDirectory(baseDir);
                string logPath = Path.Combine(baseDir, "errors.log");
                using var sw = new StreamWriter(logPath, append: true);
                sw.WriteLine("------------------------------------------------------------");
                sw.WriteLine(DateTime.UtcNow.ToString("u"));
                sw.WriteLine(context);
                sw.WriteLine();
                sw.WriteLine(ex.ToString());
                sw.WriteLine();
                return logPath;
            }
            catch
            {
                // If logging fails, return fallback path in working directory
                try
                {
                    string fallback = Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? ".", "errors.log");
                    File.AppendAllText(fallback, $"{DateTime.UtcNow:u} {context} - {ex}\n");
                    return fallback;
                }
                catch
                {
                    return "Unable to write log file";
                }
            }
        }

        private async void RefreshMods_Click(object sender, RoutedEventArgs e)
        {
            await LoadMods();
        }

        private async void InstallBepInEx_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(gamePath))
            {
                System.Windows.MessageBox.Show("Game path not set.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            SetStatus("Installing BepInEx...", System.Windows.Media.Colors.Orange);
            await BepInExInstaller.InstallAsync(gamePath);
            System.Windows.MessageBox.Show("BepInEx installed!", "Install complete", MessageBoxButton.OK, MessageBoxImage.Information);
            SetStatus("BepInEx installed", System.Windows.Media.Colors.LightGreen);
        }

        private void OpenMods_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(gamePath))
            {
                System.Windows.MessageBox.Show("Game path not set.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string modsPath = Path.Combine(gamePath, "BepInEx", "plugins");
            Directory.CreateDirectory(modsPath);
            Process.Start(new ProcessStartInfo("explorer.exe", modsPath) { UseShellExecute = true });
            SetStatus("Opened mods folder", System.Windows.Media.Colors.LightGray);
        }

        private void UninstallMods_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(gamePath))
            {
                System.Windows.MessageBox.Show("Game path not set.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string modsPath = Path.Combine(gamePath, "BepInEx");

            if (Directory.Exists(modsPath))
            {
                Directory.Delete(modsPath, true);
                System.Windows.MessageBox.Show("Mods removed.", "Uninstall", MessageBoxButton.OK, MessageBoxImage.Information);
                SetStatus("Mods removed", System.Windows.Media.Colors.LightGray);
            }
            else
            {
                System.Windows.MessageBox.Show("No BepInEx installation found.", "Uninstall", MessageBoxButton.OK, MessageBoxImage.Warning);
                SetStatus("No BepInEx installation", System.Windows.Media.Colors.Orange);
            }
        }
    }
}