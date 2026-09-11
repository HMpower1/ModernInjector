using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ModernInjector
{
    public partial class MainWindow : Window
    {
        private readonly List<ProcessViewModel> _allProcesses = new();

        // --- Win32 API Imports ---
        [Flags]
        public enum ProcessAccessFlags : uint
        {
            All = 0x001F0FFF,
            CreateThread = 0x0002,
            QueryInformation = 0x0400,
            VirtualMemoryOperation = 0x0008,
            VirtualMemoryRead = 0x0010,
            VirtualMemoryWrite = 0x0020
        }

        [Flags]
        public enum AllocationType
        {
            Commit = 0x1000,
            Reserve = 0x2000
        }

        [Flags]
        public enum MemoryProtection
        {
            ExecuteReadWrite = 0x40
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(ProcessAccessFlags processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, AllocationType flAllocationType, MemoryProtection flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        public MainWindow()
        {
            InitializeComponent();
            Log("Aplikacja została uruchomiona pomyślnie.");
            LoadProcesses();
        }

        // Metoda pomocnicza do logowania w konsoli interfejsu
        private void Log(string message)
        {
            TxtConsoleLogs.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
            TxtConsoleLogs.ScrollToEnd();
        }

        // Bezpieczne pobieranie ikony procesu
        private ImageSource? GetProcessIcon(Process p)
        {
            try
            {
                string? path = p.MainModule?.FileName;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                    if (icon != null)
                    {
                        return Imaging.CreateBitmapSourceFromHIcon(
                            icon.Handle,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                    }
                }
            }
            catch
            {
                // Ignorujemy procesy systemowe do których nie mamy uprawnień odczytu ścieżki
            }
            return null;
        }

        // Ładowanie i cache'owanie procesów
        private void LoadProcesses()
        {
            _allProcesses.Clear();
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    var vm = new ProcessViewModel
                    {
                        Name = p.ProcessName,
                        Id = p.Id,
                        MemorySize = $"{p.WorkingSet64 / 1024 / 1024} MB",
                        Architecture = "x64 / x86",
                        MainWindowTitle = p.MainWindowTitle ?? string.Empty,
                        Icon = GetProcessIcon(p)
                    };
                    _allProcesses.Add(vm);
                }
                catch { }
            }
            FilterProcesses(TxtSearch?.Text ?? string.Empty);
        }

        // Filtrowanie listy procesów na podstawie wpisanego tekstu
        private void FilterProcesses(string query)
        {
            LvProcesses.Items.Clear();
            string q = query.ToLower().Trim();

            foreach (var p in _allProcesses)
            {
                if (string.IsNullOrEmpty(q) ||
                    (p.Name != null && p.Name.ToLower().Contains(q)) ||
                    p.Id.ToString().Contains(q))
                {
                    LvProcesses.Items.Add(p);
                }
            }
            TxtProcessCount.Text = $"Znaleziono procesów: {LvProcesses.Items.Count}";
        }

        // Główna logika wstrzykiwania
        private void BtnInject_Click(object sender, RoutedEventArgs e)
        {
            if (LvProcesses.SelectedItem is not ProcessViewModel selectedProc)
            {
                Log("Błąd: Nie wybrano żadnego procesu z listy!");
                return;
            }

            if (LbDlls.Items.Count == 0)
            {
                Log("Błąd: Nie dodano żadnej biblioteki DLL do listy!");
                return;
            }

            string dllPath = LbDlls.Items[0].ToString() ?? string.Empty;
            if (!File.Exists(dllPath))
            {
                Log($"Błąd: Plik DLL nie istnieje pod ścieżką: {dllPath}");
                return;
            }

            Log($"Próba wstrzykiwania do procesu: {selectedProc.Name} (PID: {selectedProc.Id})");

            try
            {
                IntPtr hProcess = OpenProcess(ProcessAccessFlags.All, false, selectedProc.Id);
                if (hProcess == IntPtr.Zero)
                {
                    Log($"Błąd: Nie udało się otworzyć procesu. Kod błędu: {Marshal.GetLastWin32Error()}");
                    return;
                }

                byte[] dllPathBytes = Encoding.ASCII.GetBytes(dllPath + "\0");
                IntPtr pDllPath = VirtualAllocEx(hProcess, IntPtr.Zero, (uint)dllPathBytes.Length, AllocationType.Commit | AllocationType.Reserve, MemoryProtection.ExecuteReadWrite);

                if (pDllPath == IntPtr.Zero)
                {
                    Log("Błąd: Alokacja pamięci w obcym procesie nie powiodła się.");
                    CloseHandle(hProcess);
                    return;
                }

                bool written = WriteProcessMemory(hProcess, pDllPath, dllPathBytes, (uint)dllPathBytes.Length, out var bytesWritten);
                if (!written || bytesWritten == IntPtr.Zero)
                {
                    Log("Błąd: Nie udało się zapisać pamięci w procesie docelowym.");
                    CloseHandle(hProcess);
                    return;
                }

                IntPtr hKernel32 = GetModuleHandle("kernel32.dll");
                IntPtr pLoadLibrary = GetProcAddress(hKernel32, "LoadLibraryA");

                if (pLoadLibrary == IntPtr.Zero)
                {
                    Log("Błąd: Nie znaleziono adresu funkcji LoadLibraryA.");
                    CloseHandle(hProcess);
                    return;
                }

                IntPtr hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0, pLoadLibrary, pDllPath, 0, IntPtr.Zero);
                if (hThread == IntPtr.Zero)
                {
                    Log($"Błąd: Nie udało się utworzyć zdalnego wątku. Kod: {Marshal.GetLastWin32Error()}");
                }
                else
                {
                    Log($"Sukces! DLL została pomyślnie wstrzykiwana do PID: {selectedProc.Id}");
                    CloseHandle(hThread);
                }

                CloseHandle(hProcess);
            }
            catch (Exception ex)
            {
                Log($"Wystąpił wyjątek krytyczny: {ex.Message}");
            }
        }

        // Obsługa zdarzeń interfejsu
        private void BtnThemeToggle_Click(object sender, RoutedEventArgs e) { Log("Przełączono motyw."); }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtSearch != null)
            {
                FilterProcesses(TxtSearch.Text);
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadProcesses();
            Log("Odświeżono listę procesów.");
        }

        private void SliderDelay_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtDelayVal != null) TxtDelayVal.Text = $"{e.NewValue}s";
        }
        private void LvProcesses_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
        private void LvProcesses_MouseRightButtonUp(object sender, MouseButtonEventArgs e) { }
        private void ChkAutoRefresh_Checked(object sender, RoutedEventArgs e) { }
        private void ChkAutoRefresh_Unchecked(object sender, RoutedEventArgs e) { }

        private void BtnKillProcess_Click(object sender, RoutedEventArgs e)
        {
            if (LvProcesses.SelectedItem is ProcessViewModel p)
            {
                try
                {
                    Process.GetProcessById(p.Id).Kill();
                    Log($"Zabito proces: {p.Name}");
                    LoadProcesses();
                }
                catch (Exception ex) { Log($"Nie udało się zabić procesu: {ex.Message}"); }
            }
        }

        private void BtnExportLogs_Click(object sender, RoutedEventArgs e)
        {
            File.WriteAllText("injector_logs.txt", TxtConsoleLogs.Text);
            Log("Eksportowano logi do pliku injector_logs.txt");
        }

        private void BtnAddDll_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new() { Filter = "Biblioteki DLL (*.dll)|*.dll" };
            if (dlg.ShowDialog() == true)
            {
                LbDlls.Items.Add(dlg.FileName);
                Log($"Dodano DLL: {dlg.FileName}");
            }
        }

        private void BtnRemoveDll_Click(object sender, RoutedEventArgs e)
        {
            if (LbDlls.SelectedItem != null)
            {
                string removed = LbDlls.SelectedItem.ToString() ?? string.Empty;
                LbDlls.Items.Remove(LbDlls.SelectedItem);
                Log($"Usunięto DLL: {removed}");
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (var file in files)
                {
                    if (file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        LbDlls.Items.Add(file);
                        Log($"Dodano DLL przez przeciągnięcie: {file}");
                    }
                }
            }
        }
    }

    public class ProcessViewModel
    {
        public string? Name { get; set; }
        public int Id { get; set; }
        public string? MemorySize { get; set; }
        public string? Architecture { get; set; }
        public string? MainWindowTitle { get; set; }
        public ImageSource? Icon { get; set; }
    }
}