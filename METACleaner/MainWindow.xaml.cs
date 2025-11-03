using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Path = System.IO.Path;

namespace METACleaner
{
    public partial class MainWindow : Window
    {
        private List<string> currentFiles = new List<string>();
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Border_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void Border_PreviewDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                currentFiles.Clear();
                currentFiles.AddRange(files);
                DisplayFilesInfo(files);
                ClearMetadataButton.IsEnabled = true;
            }
        }

        private void DisplayFilesInfo(string[] files)
        {
            var fileDisplays = new List<UIElement>();

            foreach (string filePath in files)
            {
                try
                {
                    FileInfo fileInfo = new FileInfo(filePath);
                    var fileDisplay = CreateFileInfoDisplay(filePath, fileInfo);
                    fileDisplays.Add(fileDisplay);
                }
                catch (Exception ex)
                {
                    var errorPanel = CreateErrorDisplay(filePath, ex.Message);
                    fileDisplays.Add(errorPanel);
                }
            }

            FilesItemsControl.ItemsSource = fileDisplays;
        }

        private UIElement CreateFileInfoDisplay(string filePath, FileInfo fileInfo)
        {
            var mainStack = new StackPanel();

         
            var headerStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 20)
            };
            headerStack.Children.Add(new TextBlock
            {
                Text = GetFileIcon(fileInfo.Extension),
                FontSize = 24,
                Margin = new Thickness(0, 0, 10, 0),
                Foreground = Brushes.White
            });
            headerStack.Children.Add(new TextBlock
            {
                Text = fileInfo.Name,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            });
            mainStack.Children.Add(headerStack);

           
            mainStack.Children.Add(CreateInfoRow("Size: ", FormatFileSize(fileInfo.Length)));
            mainStack.Children.Add(CreateInfoRow("Created: ", fileInfo.CreationTime.ToString("dd.MM.yyyy HH:mm")));
            mainStack.Children.Add(CreateInfoRow("Changed: ", fileInfo.LastWriteTime.ToString("dd.MM.yyyy HH:mm")));
            mainStack.Children.Add(CreateInfoRow("Last Open: ", fileInfo.LastAccessTime.ToString("dd.MM.yyyy HH:mm")));
            mainStack.Children.Add(CreateInfoRow("Extension: ", fileInfo.Extension));
            mainStack.Children.Add(CreateInfoRow("Catalog: ", fileInfo.DirectoryName));
            mainStack.Children.Add(CreateInfoRow("Read only: ", fileInfo.IsReadOnly ? "Да" : "Нет"));
            mainStack.Children.Add(CreateInfoRow("Hidden: ", (fileInfo.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden ? "Да" : "Нет"));
            mainStack.Children.Add(CreateInfoRow("Archival: ", (fileInfo.Attributes & FileAttributes.Archive) == FileAttributes.Archive ? "Да" : "Нет"));



            
            if (IsImageFile(fileInfo.Extension))
            {
                var imageMetadata = GetImageMetadata(filePath);
                if (imageMetadata != null)
                {
                
                    mainStack.Children.Add(new TextBlock
                    {
                        Text = "🖼️ Image metadata:",
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 15, 0, 5),
                        Foreground = Brushes.White,
                        FontSize = 12
                    });

                    foreach (var metadata in imageMetadata)
                    {
                        mainStack.Children.Add(CreateInfoRow(metadata.Key + ":", metadata.Value));
                    }
                }
            }

            return mainStack;
        }

        private UIElement CreateInfoRow(string label, string value)
        {
            var textBlock = new TextBlock
            {
                FontSize = 12,
                Margin = new Thickness(0, 3, 0, 3),
                VerticalAlignment = VerticalAlignment.Center
            };

            textBlock.Inlines.Add(new Run(label)
            {
                Foreground = Brushes.LightGray,
                FontWeight = FontWeights.Medium
            });

            textBlock.Inlines.Add(new Run(value)
            {
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold
            });

            return textBlock;
        }

        private Dictionary<string, string> GetImageMetadata(string filePath)
        {
            try
            {
                using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    var bitmapFrame = System.Windows.Media.Imaging.BitmapFrame.Create(
                        stream, System.Windows.Media.Imaging.BitmapCreateOptions.DelayCreation,
                        System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);

                    var metadata = new Dictionary<string, string>
                    {
                        { "Permission ", $"{bitmapFrame.PixelWidth} × {bitmapFrame.PixelHeight}" },
                        { "DPI ", $"{bitmapFrame.DpiX} × {bitmapFrame.DpiY}" },
                        { "Format ", bitmapFrame.Format.ToString() }
                    };

                    if (bitmapFrame.Metadata is System.Windows.Media.Imaging.BitmapMetadata imgMetadata)
                    {
                        if (!string.IsNullOrEmpty(imgMetadata.Title))
                            metadata["Heading "] = imgMetadata.Title;
                        if (!string.IsNullOrEmpty(imgMetadata.Subject))
                            metadata["Subject "] = imgMetadata.Subject;
                        if (!string.IsNullOrEmpty(imgMetadata.Comment))
                            metadata["Comment "] = imgMetadata.Comment;
                    }

                    return metadata;
                }
            }
            catch
            {
                return null;
            }
        }

        private void ClearMetadataButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentFiles.Count == 0)
                return;

            var result = MessageBox.Show($"Are you sure you want to clear metadata for {currentFiles.Count} file(s)?",
                "Clearing metadata", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    int clearedCount = 0;
                    foreach (string filePath in currentFiles)
                    {
                        if (ClearFileMetadata(filePath))
                        {
                            clearedCount++;
                        }
                    }

                    MessageBox.Show($"Metadata successfully cleared for {clearedCount} file(s)!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                   
                    DisplayFilesInfo(currentFiles.ToArray());
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error clearing metadata: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private bool ClearFileMetadata(string filePath)
        {
            try
            {
                FileInfo fileInfo = new FileInfo(filePath);

               
                DateTime newDate = new DateTime(2000, 1, 1, 12, 0, 0);

                File.SetCreationTime(filePath, newDate);
                File.SetLastWriteTime(filePath, newDate);
                File.SetLastAccessTime(filePath, newDate);

                
                if (IsImageFile(fileInfo.Extension))
                {
                    ClearImageMetadata(filePath);
                }

                
                File.SetAttributes(filePath, FileAttributes.Normal);

                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to clear metadata for file {Path.GetFileName(filePath)}: {ex.Message}");
            }
        }

        private void ClearImageMetadata(string filePath)
        {
            try
            {

                using (var originalStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    var bitmapFrame = System.Windows.Media.Imaging.BitmapFrame.Create(originalStream);


                    var newBitmap = new System.Windows.Media.Imaging.WriteableBitmap(
                        bitmapFrame.PixelWidth,
                        bitmapFrame.PixelHeight,
                        bitmapFrame.DpiX,
                        bitmapFrame.DpiY,
                        bitmapFrame.Format,
                        null);

                  
                    int stride = bitmapFrame.PixelWidth * (bitmapFrame.Format.BitsPerPixel / 8);
                    byte[] pixels = new byte[bitmapFrame.PixelHeight * stride];
                    bitmapFrame.CopyPixels(pixels, stride, 0);
                    newBitmap.WritePixels(new System.Windows.Int32Rect(0, 0, bitmapFrame.PixelWidth, bitmapFrame.PixelHeight),
                                        pixels, stride, 0);

                   
                    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(newBitmap));

                    string tempFile = Path.GetTempFileName() + ".png";
                    using (var tempStream = new FileStream(tempFile, FileMode.Create))
                    {
                        encoder.Save(tempStream);
                    }

                    
                    File.Copy(tempFile, filePath, true);
                    File.Delete(tempFile);
                }
            }
            catch
            {
                
            }
        }

        private string GetFileIcon(string extension)
        {
            switch (extension.ToLower())
            {
                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".gif":
                case ".bmp":
                    return "🖼️";
                case ".pdf":
                    return "📕";
                case ".doc":
                case ".docx":
                    return "📄";
                case ".xls":
                case ".xlsx":
                    return "📊";
                case ".txt":
                    return "📝";
                case ".zip":
                case ".rar":
                case ".7z":
                    return "📦";
                case ".exe":
                    return "⚙️";
                case ".mp3":
                case ".wav":
                    return "🎵";
                case ".mp4":
                case ".avi":
                    return "🎬";
                default:
                    return "📄";
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private bool IsImageFile(string extension)
        {
            string[] imageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".webp" };
            return Array.Exists(imageExtensions, ext => ext.Equals(extension, StringComparison.OrdinalIgnoreCase));
        }

        private UIElement CreateErrorDisplay(string filePath, string error)
        {
            var stackPanel = new StackPanel();

            var headerStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            headerStack.Children.Add(new TextBlock
            {
                Text = "❌",
                FontSize = 20,
                Margin = new Thickness(0, 0, 10, 0),
                Foreground = Brushes.White
            });
            headerStack.Children.Add(new TextBlock
            {
                Text = Path.GetFileName(filePath),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Red
            });

            stackPanel.Children.Add(headerStack);
            stackPanel.Children.Add(new TextBlock
            {
                Text = $"Error: {error}",
                Foreground = Brushes.Red,
                TextWrapping = TextWrapping.Wrap
            });

            return stackPanel;
        }





        private void Button_Minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void Button_Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void MainBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }
    }
}
