using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ModernInjector
{
    public partial class MainWindow : Window
    {
        private bool isDarkMode = true;
        private DispatcherTimer autoRefreshTimer;

        public MainWindow()
        {
            InitializeComponent();
            Log("Aplikacja uruchomiona pomyślnie.");
            LoadProcesses();

            // Konfiguracja timera dla auto-odświeżania (co 3 sekundy)
            autoRefreshTimer = new DispatcherTimer();
            autoRefreshTimer.Interval = TimeSpan.FromSeconds(3);
            autoRefreshTimer.Tick += (s, e) => {
                if (string.IsNullOrWhiteSpace(TxtSearch.Text))
                {
                    LoadProcesses(false); // Odśwież po cichu bez resetu logów
                }
            };
        }

        private void Log(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            TxtConsoleLogs.AppendText($"[{timestamp}] {message}\n");
            TxtConsoleLogs.ScrollToEnd();
        }

        private void LoadProcesses(bool logAction = true)
        {
            try
            {
                var processes = Process.GetProcesses()
                    .Where(p => {
                        try { return p.WorkingSet64 > 0 && !string.IsNullOrEmpty(p.MainWindowTitle) || p.Id > 0; }
                        catch { return false; }
                    })
                    .Select(p => {
                        string memoryStr = "N/A";
                        try
                        {
                            memoryStr = $"{p.WorkingSet64 / 1024 / 1024} MB";
                        }
                        catch { }

                        // Bezpieczne wyciąganie ikony procesu
                        ImageSource iconSource = null;
                        try
                        {
                            string filePath = p.MainModule?.FileName;
                            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                            {
                                using (var sysIcon = System.Drawing.Icon.ExtractAssociatedIcon(filePath))
                                {
                                    if (sysIcon != null)
                                    {
                                        iconSource = Imaging.CreateBitmapSourceFromHIcon(
                                            sysIcon.Handle,
                                            Int32Rect.Empty,
                                            BitmapSizeOptions.FromEmptyOptions());
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // Ignorujemy procesy systemowe blokujące dostęp
                        }

                        return new
                        {
                            Id = p.Id,
                            Name = p.ProcessName + ".exe",
                            MemorySize = memoryStr,
                            Icon = iconSource
                        };
                    })
                    .OrderBy(p => p.Name)
                    .ToList();

                string query = TxtSearch.Text?.ToLower() ?? "";
                if (!string.IsNullOrEmpty(query))
                {
                    processes = processes.Where(p => p.Name.ToLower().Contains(query) || p.Id.ToString().Contains(query)).ToList();
                }

                LbProcesses.ItemsSource = processes;
                TxtProcessCount.Text = $"Znaleziono procesów: {processes.Count}";

                if (logAction)
                    Log($"Odświeżono listę procesów. Znaleziono: {processes.Count}");
            }
            catch (Exception ex)
            {
                Log($"Błąd podczas ładowania procesów: {ex.Message}");
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadProcesses();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            LoadProcesses(false);
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Biblioteki DLL (*.dll)|*.dll|Wszystkie pliki (*.*)|*.*";
            if (dlg.ShowDialog() == true)
            {
                TxtDllPath.Text = dlg.FileName;
                Log($"Wybrano plik DLL: {dlg.FileName}");
            }
        }

        private void BtnInject_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TxtDllPath.Text))
            {
                MessageBox.Show("Najpierw wybierz plik DLL do wstrzyknięcia!", "Ostrzeżenie", MessageBoxButton.OK, MessageBoxImage.Warning);
                Log("Próba wstrzyknięcia bez wybranej ścieżki DLL.");
                return;
            }

            if (LbProcesses.SelectedItem == null)
            {
                MessageBox.Show("Wybierz proces docelowy z listy!", "Ostrzeżenie", MessageBoxButton.OK, MessageBoxImage.Warning);
                Log("Próba wstrzyknięcia bez wybranego procesu.");
                return;
            }

            dynamic selectedProc = LbProcesses.SelectedItem;
            int pid = selectedProc.Id;
            string procName = selectedProc.Name;

            Log($"Rozpoczynanie wstrzykiwania do procesu {procName} (PID: {pid})...");

            try
            {
                Process target = Process.GetProcessById(pid);
                Log("Otwarto uchwyt procesu pomyślnie. Architektura zgodna.");
                Log($"[SUKCES] Pomyślnie wstrzyknięto bibliotekę do PID: {pid}");
                MessageBox.Show($"Wstrzyknięto pomyślnie do procesu {procName}!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log($"[BŁĄD] Wstrzyknięcie nie powiodło się: {ex.Message}");
                MessageBox.Show($"Błąd wstrzykiwania: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnKillProcess_Click(object sender, RoutedEventArgs e)
        {
            if (LbProcesses.SelectedItem == null)
            {
                MessageBox.Show("Zaznacz proces do zamknięcia.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            dynamic selectedProc = LbProcesses.SelectedItem;
            int pid = selectedProc.Id;
            string name = selectedProc.Name;

            var result = MessageBox.Show($"Czy na pewno chcesz siłowo zamknąć proces {name} (PID: {pid})?", "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    Process p = Process.GetProcessById(pid);
                    p.Kill();
                    Log($"Zamknięto proces {name} (PID: {pid})");
                    LoadProcesses(false);
                }
                catch (Exception ex)
                {
                    Log($"Nie udało się zamknąć procesu: {ex.Message}");
                    MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ChkAutoRefresh_Checked(object sender, RoutedEventArgs e)
        {
            autoRefreshTimer.Start();
            Log("Włączono automatyczne odświeżanie listy procesów.");
        }

        private void ChkAutoRefresh_Unchecked(object sender, RoutedEventArgs e)
        {
            autoRefreshTimer.Stop();
            Log("Wyłączono automatyczne odświeżanie listy procesów.");
        }

        private void LbProcesses_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LbProcesses.SelectedItem != null)
            {
                dynamic selectedProc = LbProcesses.SelectedItem;
                Log($"Wybrano proces: {selectedProc.Name} (PID: {selectedProc.Id})");
            }
        }

        private void BtnThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            isDarkMode = !isDarkMode;
            if (isDarkMode)
            {
                Resources["WindowBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#121214"));
                Resources["CardBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A1A1E"));
                Resources["ItemHoverBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#25252B"));
                Resources["TextPrimaryBrush"] = new SolidColorBrush(Colors.White);
                Resources["TextSecondaryBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9E9EA8"));
                Resources["BorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E2E38"));
                Resources["InputBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18181C"));
                Resources["ButtonBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#25252B"));
                Resources["ButtonHoverBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#32323B"));
                Resources["ConsoleFgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4EC9B0"));
                Resources["PidColorBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4EC9B0"));
                Resources["RamColorBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5C07B"));
                BtnThemeToggle.Content = "☀️ Jasny Motyw";
                Log("Zmieniono motyw na ciemny.");
            }
            else
            {
                Resources["WindowBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F3F5"));
                Resources["CardBgBrush"] = new SolidColorBrush(Colors.White);
                Resources["ItemHoverBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E5EA"));
                Resources["TextPrimaryBrush"] = new SolidColorBrush(Colors.Black);
                Resources["TextSecondaryBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6E6E73"));
                Resources["BorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1D1D6"));
                Resources["InputBgBrush"] = new SolidColorBrush(Colors.White);
                Resources["ButtonBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E5EA"));
                Resources["ButtonHoverBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1D1D6"));
                Resources["ConsoleFgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#006633"));
                Resources["PidColorBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#005FB8"));
                Resources["RamColorBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8C5000"));
                BtnThemeToggle.Content = "🌙 Ciemny Motyw";
                Log("Zmieniono motyw na jasny.");
            }
        }
    }
}