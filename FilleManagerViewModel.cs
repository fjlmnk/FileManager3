using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FileManager3
{
    public class FileTypeGroup
    {
        public string FileType { get; set; }
        public int FileCount { get; set; }
        public string TotalSize { get; set; }
        public ObservableCollection<FileItem> Files { get; set; }
    }

    public class FileManagerViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<FileItem> leftPanelFiles;
        private ObservableCollection<FileItem> rightPanelFiles;
        private ObservableCollection<FileTypeGroup> sortedFiles;
        private FileItem selectedLeftItem;
        private FileItem selectedRightItem;
        private string currentLeftPath;
        private string currentRightPath;
        private Stack<string> leftPathHistory;
        private Stack<string> rightPathHistory;
        private ObservableCollection<DriveInfo> availableDrives;
        private string currentSortingPath;

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<FileItem> LeftPanelFiles
        {
            get => leftPanelFiles;
            set
            {
                leftPanelFiles = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<FileItem> RightPanelFiles
        {
            get => rightPanelFiles;
            set
            {
                rightPanelFiles = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<FileTypeGroup> SortedFiles
        {
            get => sortedFiles;
            set
            {
                sortedFiles = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<DriveInfo> AvailableDrives
        {
            get => availableDrives;
            set
            {
                availableDrives = value;
                OnPropertyChanged();
            }
        }

        public FileItem SelectedLeftItem
        {
            get => selectedLeftItem;
            set
            {
                selectedLeftItem = value;
                OnPropertyChanged();
            }
        }

        public FileItem SelectedRightItem
        {
            get => selectedRightItem;
            set
            {
                selectedRightItem = value;
                OnPropertyChanged();
            }
        }

        public ICommand OpenFileCommand { get; }
        public ICommand CopyCommand { get; }
        public ICommand MoveCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand SelectLeftDriveCommand { get; }
        public ICommand SelectRightDriveCommand { get; }
        public ICommand SelectDriveForSortingCommand { get; }

        public FileManagerViewModel()
        {
            LeftPanelFiles = new ObservableCollection<FileItem>();
            RightPanelFiles = new ObservableCollection<FileItem>();
            SortedFiles = new ObservableCollection<FileTypeGroup>();
            leftPathHistory = new Stack<string>();
            rightPathHistory = new Stack<string>();
            
            LoadAvailableDrives();
            
            // Встановлюємо початкові шляхи
            currentLeftPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            currentRightPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            OpenFileCommand = new RelayCommand(OpenFile);
            CopyCommand = new RelayCommand(CopyFile);
            MoveCommand = new RelayCommand(MoveFile);
            DeleteCommand = new RelayCommand(DeleteFile);
            BackCommand = new RelayCommand(GoBack);
            SelectLeftDriveCommand = new RelayCommand<DriveInfo>(SelectLeftDrive);
            SelectRightDriveCommand = new RelayCommand<DriveInfo>(SelectRightDrive);
            SelectDriveForSortingCommand = new RelayCommand<DriveInfo>(SelectDriveForSorting);

            LoadFiles();
        }

        private void LoadAvailableDrives()
        {
            AvailableDrives = new ObservableCollection<DriveInfo>(
                DriveInfo.GetDrives()
                    .Where(d => d.IsReady)
                    .OrderBy(d => d.Name)
            );
        }

        private void SelectLeftDrive(DriveInfo drive)
        {
            if (drive != null)
            {
                leftPathHistory.Push(currentLeftPath);
                currentLeftPath = drive.RootDirectory.FullName;
                LoadDirectoryContents(currentLeftPath, LeftPanelFiles);
            }
        }

        private void SelectRightDrive(DriveInfo drive)
        {
            if (drive != null)
            {
                rightPathHistory.Push(currentRightPath);
                currentRightPath = drive.RootDirectory.FullName;
                LoadDirectoryContents(currentRightPath, RightPanelFiles);
            }
        }

        private void SelectDriveForSorting(DriveInfo drive)
        {
            if (drive != null)
            {
                currentSortingPath = drive.RootDirectory.FullName;
                LoadSortedFiles(currentSortingPath);
            }
        }

        private void LoadSortedFiles(string path)
        {
            var files = new List<FileItem>();
            LoadAllFiles(path, files);

            var groupedFiles = files
                .GroupBy(f => f.Extension)
                .Select(g => new FileTypeGroup
                {
                    FileType = string.IsNullOrEmpty(g.Key) ? "Без розширення" : g.Key,
                    FileCount = g.Count(),
                    TotalSize = FormatFileSize(g.Sum(f => f.Size)),
                    Files = new ObservableCollection<FileItem>(g.OrderBy(f => f.Name))
                })
                .OrderByDescending(g => g.FileCount)
                .ToList();

            SortedFiles = new ObservableCollection<FileTypeGroup>(groupedFiles);
        }

        private void LoadAllFiles(string path, List<FileItem> files)
        {
            try
            {
                // Додаємо файли
                foreach (var file in Directory.GetFiles(path))
                {
                    files.Add(new FileItem
                    {
                        Name = Path.GetFileName(file),
                        Path = file,
                        Size = new FileInfo(file).Length,
                        Modified = File.GetLastWriteTime(file)
                    });
                }

                // Рекурсивно додаємо файли з підпапок
                foreach (var dir in Directory.GetDirectories(path))
                {
                    try
                    {
                        LoadAllFiles(dir, files);
                    }
                    catch (Exception)
                    {
                        // Пропускаємо папки, до яких немає доступу
                        continue;
                    }
                }
            }
            catch (Exception)
            {
                // Пропускаємо папки, до яких немає доступу
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {sizes[order]}";
        }

        private void LoadFiles()
        {
            LoadDirectoryContents(currentLeftPath, LeftPanelFiles);
            LoadDirectoryContents(currentRightPath, RightPanelFiles);
        }

        private void LoadDirectoryContents(string path, ObservableCollection<FileItem> collection)
        {
            collection.Clear();

            // Додаємо ".." для навігації вгору
            if (path != Path.GetPathRoot(path))
            {
                collection.Add(new FileItem { Name = "..", IsDirectory = true });
            }

            // Додаємо доступні диски, якщо ми в корені
            if (path == Path.GetPathRoot(path))
            {
                foreach (var drive in AvailableDrives)
                {
                    collection.Add(new FileItem
                    {
                        Name = $"{drive.Name} ({drive.VolumeLabel})",
                        Path = drive.RootDirectory.FullName,
                        IsDirectory = true,
                        Modified = drive.RootDirectory.LastWriteTime
                    });
                }
            }

            // Додаємо директорії
            foreach (var dir in Directory.GetDirectories(path))
            {
                collection.Add(new FileItem
                {
                    Name = Path.GetFileName(dir),
                    Path = dir,
                    IsDirectory = true,
                    Modified = Directory.GetLastWriteTime(dir)
                });
            }

            // Додаємо файли
            foreach (var file in Directory.GetFiles(path))
            {
                collection.Add(new FileItem
                {
                    Name = Path.GetFileName(file),
                    Path = file,
                    Size = new FileInfo(file).Length,
                    Modified = File.GetLastWriteTime(file)
                });
            }
        }

        public void OpenFile()
        {
            var selectedItem = SelectedLeftItem ?? SelectedRightItem;
            if (selectedItem != null)
            {
                if (selectedItem.IsDirectory)
                {
                    if (selectedItem.Name == "..")
                    {
                        GoBack();
                    }
                    else
                    {
                        if (selectedItem == SelectedLeftItem)
                        {
                            leftPathHistory.Push(currentLeftPath);
                            currentLeftPath = selectedItem.Path;
                            LoadDirectoryContents(currentLeftPath, LeftPanelFiles);
                        }
                        else
                        {
                            rightPathHistory.Push(currentRightPath);
                            currentRightPath = selectedItem.Path;
                            LoadDirectoryContents(currentRightPath, RightPanelFiles);
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = selectedItem.Path,
                        UseShellExecute = true
                    });
                }
            }
        }

        private void GoBack()
        {
            if (leftPathHistory.Count > 0)
            {
                currentLeftPath = leftPathHistory.Pop();
                LoadDirectoryContents(currentLeftPath, LeftPanelFiles);
            }
            if (rightPathHistory.Count > 0)
            {
                currentRightPath = rightPathHistory.Pop();
                LoadDirectoryContents(currentRightPath, RightPanelFiles);
            }
        }

        private void CopyFile()
        {
            var sourceItem = SelectedLeftItem ?? SelectedRightItem;
            var targetPath = sourceItem == SelectedLeftItem ? currentRightPath : currentLeftPath;

            if (sourceItem != null)
            {
                try
                {
                    if (sourceItem.IsDirectory)
                    {
                        CopyDirectory(sourceItem.Path, Path.Combine(targetPath, sourceItem.Name));
                    }
                    else
                    {
                        File.Copy(sourceItem.Path, Path.Combine(targetPath, sourceItem.Name), true);
                    }
                    LoadFiles();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Помилка при копіюванні: {ex.Message}", "Помилка", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private void MoveFile()
        {
            var sourceItem = SelectedLeftItem ?? SelectedRightItem;
            var targetPath = sourceItem == SelectedLeftItem ? currentRightPath : currentLeftPath;

            if (sourceItem != null)
            {
                try
                {
                    if (sourceItem.IsDirectory)
                    {
                        Directory.Move(sourceItem.Path, Path.Combine(targetPath, sourceItem.Name));
                    }
                    else
                    {
                        File.Move(sourceItem.Path, Path.Combine(targetPath, sourceItem.Name));
                    }
                    LoadFiles();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Помилка при переміщенні: {ex.Message}", "Помилка", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private void DeleteFile()
        {
            var selectedItem = SelectedLeftItem ?? SelectedRightItem;
            if (selectedItem != null)
            {
                try
                {
                    if (selectedItem.IsDirectory)
                    {
                        Directory.Delete(selectedItem.Path, true);
                    }
                    else
                    {
                        File.Delete(selectedItem.Path);
                    }
                    LoadFiles();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Помилка при видаленні: {ex.Message}", "Помилка", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), true);
            }

            foreach (var directory in Directory.GetDirectories(sourceDir))
            {
                CopyDirectory(directory, Path.Combine(targetDir, Path.GetFileName(directory)));
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute();
        }

        public void Execute(object parameter)
        {
            _execute();
        }
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool> _canExecute;

        public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute((T)parameter);
        }

        public void Execute(object parameter)
        {
            _execute((T)parameter);
        }
    }
}
