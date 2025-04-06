using System.IO;
using System.Windows;

namespace FileManager3
{
    public partial class PreviewWindow : Window
    {
        public PreviewWindow(string filePath)
        {
            InitializeComponent();
            FileNameText.Text = Path.GetFileName(filePath);
            LoadFileContent(filePath);
        }

        private void LoadFileContent(string filePath)
        {
            try
            {
                string extension = Path.GetExtension(filePath).ToLower();
                
                // Перевіряємо, чи це текстовий файл
                if (IsTextFile(extension))
                {
                    ContentTextBox.Text = File.ReadAllText(filePath);
                }
                else
                {
                    ContentTextBox.Text = "Цей тип файлу не підтримує перегляд.";
                }
            }
            catch (Exception ex)
            {
                ContentTextBox.Text = $"Помилка при читанні файлу: {ex.Message}";
            }
        }

        private bool IsTextFile(string extension)
        {
            string[] textExtensions = { ".txt", ".cs", ".xaml", ".xml", ".json", ".html", ".css", ".js", ".log", ".md", ".ini", ".config", ".bat", ".ps1", ".sh" };
            return textExtensions.Contains(extension);
        }
    }
} 