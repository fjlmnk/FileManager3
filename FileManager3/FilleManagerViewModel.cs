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
    public class FileManagerViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<FileItem> leftPanelFiles;
        private ObservableCollection<FileItem> rightPanelFiles;
        private FileItem selectedLeftItem;
        private FileItem selectedRightItem;
        private string currentLeftPath;
        private string currentRightPath;
        private Stack<string> leftPathHistory;
        private Stack<string> rightPathHistory;
        private ObservableCollection<DriveInfo> availableDrives;
        private FileItem itemToPaste;
        private bool isCut;
        private bool isDualPanelMode;
        private string toggleViewModeText;

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

        public bool IsDualPanelMode
        {
            get => isDualPanelMode;
            set
            {
                isDualPanelMode = value;
                OnPropertyChanged();
                ToggleViewModeText = value ? "Одна панель" : "Дві панелі";
            }
        }

        public string ToggleViewModeText
        {
            get => toggleViewModeText;
            set
            {
                toggleViewModeText = value;
                OnPropertyChanged();
            }
        }

        public Visibility RightPanelVisibility => IsDualPanelMode ? Visibility.Visible : Visibility.Collapsed;

        public ICommand OpenFileCommand { get; }
        public ICommand CopyCommand { get; }
        public ICommand CutCommand { get; }
        public ICommand PasteCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand BackLeftCommand { get; }
        public ICommand BackRightCommand { get; }
        public ICommand SelectLeftDriveCommand { get; }
        public ICommand SelectRightDriveCommand { get; }
        public ICommand ToggleViewModeCommand { get; }
        public ICommand PreviewFileCommand { get; }

        public FileManagerViewModel()
        {
            LeftPanelFiles = new ObservableCollection<FileItem>();
            RightPanelFiles = new ObservableCollection<FileItem>();
            leftPathHistory = new Stack<string>();
            rightPathHistory = new Stack<string>();
            
            LoadAvailableDrives();
            
            currentLeftPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            currentRightPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            OpenFileCommand = new RelayCommand(OpenFile);
            CopyCommand = new RelayCommand(CopyToBuffer);
            CutCommand = new RelayCommand(CutToBuffer);
            PasteCommand = new RelayCommand(Paste, CanPaste);
            DeleteCommand = new RelayCommand(DeleteFile);
            BackLeftCommand = new RelayCommand(GoBackLeft);
            BackRightCommand = new RelayCommand(GoBackRight);
            SelectLeftDriveCommand = new RelayCommand<DriveInfo>(SelectLeftDrive);
            SelectRightDriveCommand = new RelayCommand<DriveInfo>(SelectRightDrive);
            ToggleViewModeCommand = new RelayCommand(ToggleViewMode);
            PreviewFileCommand = new RelayCommand(PreviewFile, CanPreviewFile);

            IsDualPanelMode = true;
            ToggleViewModeText = "Одна панель";

            LoadFiles();
        }

        private void ToggleViewMode()
        {
            IsDualPanelMode = !IsDualPanelMode;
            OnPropertyChanged(nameof(RightPanelVisibility));
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
                var dirInfo = new DirectoryInfo(dir);
                if (!dirInfo.Attributes.HasFlag(FileAttributes.Hidden))
                {
                    collection.Add(new FileItem
                    {
                        Name = Path.GetFileName(dir),
                        Path = dir,
                        IsDirectory = true,
                        Modified = Directory.GetLastWriteTime(dir)
                    });
                }
            }

            // Додаємо файли
            foreach (var file in Directory.GetFiles(path))
            {
                var fileInfo = new FileInfo(file);
                if (!fileInfo.Attributes.HasFlag(FileAttributes.Hidden))
                {
                    collection.Add(new FileItem
                    {
                        Name = Path.GetFileName(file),
                        Path = file,
                        Size = fileInfo.Length,
                        Modified = File.GetLastWriteTime(file)
                    });
                }
            }
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

        private void LoadFiles()
        {
            LoadDirectoryContents(currentLeftPath, LeftPanelFiles);
            LoadDirectoryContents(currentRightPath, RightPanelFiles);
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
                        if (selectedItem == SelectedLeftItem)
                            GoBackLeft();
                        else
                            GoBackRight();
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

        private void GoBackLeft()
        {
            if (leftPathHistory.Count > 0)
            {
                currentLeftPath = leftPathHistory.Pop();
                LoadDirectoryContents(currentLeftPath, LeftPanelFiles);
            }
        }

        private void GoBackRight()
        {
            if (rightPathHistory.Count > 0)
            {
                currentRightPath = rightPathHistory.Pop();
                LoadDirectoryContents(currentRightPath, RightPanelFiles);
            }
        }

        private void CopyToBuffer()
        {
            itemToPaste = SelectedLeftItem ?? SelectedRightItem;
            isCut = false;
            CommandManager.InvalidateRequerySuggested();
        }

        private void CutToBuffer()
        {
            itemToPaste = SelectedLeftItem ?? SelectedRightItem;
            isCut = true;
            CommandManager.InvalidateRequerySuggested();
        }

        private bool CanPaste()
        {
            return itemToPaste != null;
        }

        private void Paste()
        {
            if (itemToPaste == null) return;

            try
            {
                string targetPath;
                var selectedItem = SelectedLeftItem ?? SelectedRightItem;

                // Визначаємо цільовий шлях
                if (selectedItem != null && selectedItem.IsDirectory)
                {
                    // Якщо вибрана папка, вставляємо в неї
                    targetPath = selectedItem.Path;
                }
                else
                {
                    // Якщо папка не вибрана, вставляємо в поточну директорію
                    targetPath = SelectedLeftItem != null ? currentLeftPath : currentRightPath;
                }

                string targetFilePath = Path.Combine(targetPath, itemToPaste.Name);

                if (isCut)
                {
                    // Переміщення
                    if (itemToPaste.IsDirectory)
                    {
                        Directory.Move(itemToPaste.Path, targetFilePath);
                    }
                    else
                    {
                        File.Move(itemToPaste.Path, targetFilePath);
                    }
                    itemToPaste = null; // Очищаємо буфер після переміщення
                }
                else
                {
                    // Копіювання
                    if (itemToPaste.IsDirectory)
                    {
                        CopyDirectory(itemToPaste.Path, targetFilePath);
                    }
                    else
                    {
                        File.Copy(itemToPaste.Path, targetFilePath, true);
                    }
                }

                // Оновлюємо обидві панелі
                LoadDirectoryContents(currentLeftPath, LeftPanelFiles);
                LoadDirectoryContents(currentRightPath, RightPanelFiles);
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Помилка при вставці: {ex.Message}", "Помилка", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
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

        private bool CanPreviewFile()
        {
            var selectedItem = SelectedLeftItem ?? SelectedRightItem;
            return selectedItem != null && !selectedItem.IsDirectory;
        }

        private void PreviewFile()
        {
            var selectedItem = SelectedLeftItem ?? SelectedRightItem;
            if (selectedItem != null && !selectedItem.IsDirectory)
            {
                var previewWindow = new PreviewWindow(selectedItem.Path);
                previewWindow.Show();
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private class PreviewWindow
        {
            private string path;

            public PreviewWindow(string path)
            {
                this.path=path;
            }

            internal void Show()
            {
                throw new NotImplementedException();
            }
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
