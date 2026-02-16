using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using StalkingStairsModManager.Models;
using StalkingStairsModManager.Services;

namespace StalkingStairsModManager
{
    public partial class MainWindow : Window
    {
        private string? gamePath;
        private readonly GitHubModService modService = new();
        private readonly ModInstallerService installer = new();
        private List<ModInfo>? currentMods;

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
                currentMods = await modService.GetModsAsync();

                // Group by 'group' property
                var view = CollectionViewSource.GetDefaultView(currentMods);
                view.GroupDescriptions.Clear();
                view.GroupDescriptions.Add(new PropertyGroupDescription("group"));

                ModsGrid.ItemsSource = view;

                // subscribe to changes so toggling Enabled triggers install/uninstall
                foreach (var mod in currentMods)
                {
                    if (mod is INotifyPropertyChanged pc)
                        pc.PropertyChanged += Mod_PropertyChanged;

                    // force BepInEx enabled (checkbox disabled in UI), but DO NOT auto-install on load
                    if (string.Equals(mod.name, "BepInEx", StringComparison.OrdinalIgnoreCase))
                    {
                        mod.enabled = true;
                    }
                    else
                    {
                        // ensure other mods are OFF by default (user must enable)
                        mod.enabled = false;
                    }

                    // IMPORTANT: do not auto-install here; wait for user action (checkbox or Install/Update)
                }

                SetStatus($"Loaded {currentMods?.Count ?? 0} mods", System.Windows.Media.Colors.LightGray);
            }
            catch (Exception ex)
            {
                string logPath = LogException(ex, "Failed to load mods");
                string userMessage = "Failed to load mods. A detailed error log has been saved.";
                System.Windows.MessageBox.Show($"{userMessage}\n\nLog file: {logPath}", "Error loading mods", MessageBoxButton.OK, MessageBoxImage.Error);
                SetStatus("Failed to load mods (see logs)", System.Windows.Media.Colors.Red);
            }
        }

        private void Mod_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ModInfo.enabled) && sender is ModInfo mod)
            {
                // Prevent disabling forced BepInEx
                if (string.Equals(mod.name, "BepInEx", StringComparison.OrdinalIgnoreCase) && !mod.enabled)
                {
                    // revert change and inform user
                    mod.enabled = true;
                    System.Windows.MessageBox.Show("BepInEx is required and cannot be disabled.", "Action not allowed", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // When a user toggles enabled, perform install/uninstall immediately
                _ = HandleEnableChangedAsync(mod, mod.enabled);
            }
        }

        private async Task HandleEnableChangedAsync(ModInfo mod, bool enabled)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(gamePath))
                {
                    mod.enabled = false;
                    System.Windows.MessageBox.Show("Game path not set. Select or auto-detect a game path before installing mods.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (string.Equals(mod.name, "BepInEx", StringComparison.OrdinalIgnoreCase))
                {
                    // BepInEx is forced; if enabled is true we ensure it's installed when the user requests (Install/Update)
                    if (enabled)
                    {
                        SetStatus("Ensuring BepInEx is installed (user-triggered)...", System.Windows.Media.Colors.Orange);
                        await BepInExInstaller.InstallAsync(gamePath);
                        SetStatus("BepInEx installed", System.Windows.Media.Colors.LightGreen);
                    }
                    return;
                }

                if (enabled)
                {
                    SetStatus($"Installing {mod.name}...", System.Windows.Media.Colors.Orange);
                    await installer.InstallAsync(mod, gamePath);
                    SetStatus($"Installed {mod.DisplayName}", System.Windows.Media.Colors.LightGreen);
                }
                else
                {
                    SetStatus($"Uninstalling {mod.name}...", System.Windows.Media.Colors.Orange);
                    await installer.UninstallAsync(mod, gamePath);
                    SetStatus($"Uninstalled {mod.name}", System.Windows.Media.Colors.LightGray);
                }
            }
            catch (Exception ex)
            {
                string logPath = LogException(ex, $"Error installing/uninstalling {mod.name}");
                System.Windows.MessageBox.Show($"Failed to {(enabled ? "install" : "uninstall")} {mod.name}. See log: {logPath}", "Install error", MessageBoxButton.OK, MessageBoxImage.Error);
                mod.enabled = !enabled;
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

        private async void InstallUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(gamePath))
            {
                System.Windows.MessageBox.Show("Game path not set.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                SetStatus("Installing/updating BepInEx...", System.Windows.Media.Colors.Orange);
                await BepInExInstaller.InstallAsync(gamePath);
                SetStatus("BepInEx installed", System.Windows.Media.Colors.LightGreen);

                // install all enabled mods except BepInEx
                if (currentMods != null)
                {
                    var toInstall = currentMods.Where(m => m.enabled && !string.Equals(m.name, "BepInEx", StringComparison.OrdinalIgnoreCase)).ToList();
                    foreach (var mod in toInstall)
                    {
                        SetStatus($"Installing {mod.DisplayName}...", System.Windows.Media.Colors.Orange);
                        await installer.InstallAsync(mod, gamePath);
                    }

                    SetStatus($"Installed {toInstall.Count} mods", System.Windows.Media.Colors.LightGreen);
                    System.Windows.MessageBox.Show($"Installed/updated BepInEx and {toInstall.Count} enabled mods.", "Install complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                string logPath = LogException(ex, "Install/Update failed");
                System.Windows.MessageBox.Show($"Install/update failed. See log: {logPath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                SetStatus("Install failed", System.Windows.Media.Colors.Red);
            }
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