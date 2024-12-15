using SqueezeIt.Extensions;
using SqueezeIt.JobProcessing;
using SqueezeIt.Localization;
using SqueezeIt.Models;
using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Reflection;
using SqueezeIt.Services;

namespace SqueezeIt
{
    public partial class MainWindow : MetroWindow, INotifyPropertyChanged
    {
        ObservableCollection<FileGridItem> gridItems = new ObservableCollection<FileGridItem>();

        public MainWindow()
        {
            InitializeComponent();

            switchLanguage(AppSettings.UILanguage);

            grdFiles.DataContext = gridItems;
            btnOptimize.IsEnabled = false;
            btnCancelCompression.Visibility = Visibility.Hidden;
            gridItems.CollectionChanged += new NotifyCollectionChangedEventHandler(gridFiles_CollectionChanged);
            this.Title = String.Format(AppResources.MainWindow_Title, Assembly.GetExecutingAssembly().GetName().Version.ToString());
        }

        private void btnAddFiles_Click(object sender, RoutedEventArgs e)
        {
            // Create OpenFileDialog 
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog()
            {
                Multiselect = true,
                Filter = "JPG Files (*.jpg)|*.jpg|JPEG Files (*.jpeg)|*.jpeg|PNG Files (*.png)|*.png|GIF Files (*.gif)|*.gif|ICO Files (*.ico)|*.ico",
                DefaultExt = "jpg"
            };

            // Display OpenFileDialog by calling ShowDialog method 
            var result = dlg.ShowDialog();

            // Get the selected file name and display in a TextBox 
            if (result == true)
            {
                insertFilesInGrid(dlg.FileNames);
            }
        }

        private void gridFiles_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            btnOptimize.IsEnabled = gridItems.Any();
            lblDropHere.Visibility = gridItems.Any() ? Visibility.Hidden : Visibility.Visible;
        }

        private void chkUseLossless_Click(object sender, RoutedEventArgs e)
        {
            var toggleSwitch = sender as ToggleSwitch;
            slidQuality.IsEnabled = toggleSwitch.IsOn != true;
            lblQuality.IsEnabled = toggleSwitch.IsOn != true;
            QualityText.IsEnabled = toggleSwitch.IsOn != true;

            cmbResize.IsEnabled = toggleSwitch.IsOn != true;
            lblResize.IsEnabled = toggleSwitch.IsOn != true;
            txtResizeExplain.IsEnabled = toggleSwitch.IsOn != true;
        }


        private bool? _isAllSelected;

        public bool? IsAllSelected
        {
            get => _isAllSelected;
            set
            {
                _isAllSelected = value;
                OnPropertyChanged();
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        private void insertFilesInGrid(IEnumerable<string> files)
        {
            var validExtensions = new string[] { ".jpg", ".jpeg", ".png", ".ico" };
            var images = files.Where(file => validExtensions.Any(ext => file.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));
            images = images.Except(gridItems.Select( item  => item.FilePath)); // remove files already selected in the grid

            foreach (var file in images)
            {
                var fileInfo = new FileInfo(file);
                System.Drawing.Image img = System.Drawing.Image.FromFile(file);

                var dimensions = img.GetCorrectedDimensions();

                var newItem = new FileGridItem
                {
                    FileName = System.IO.Path.GetFileName(file),
                    FilePath = System.IO.Path.GetFullPath(file),
                    FileSize = (float)(fileInfo.Length) / 1024.0F / 1024.0F,

                    ImageDimensions = $"{dimensions.width}x{dimensions.height}",
                    Width = dimensions.width,
                    Height = dimensions.height,
                    Rotated = dimensions.rotated,
                };
                gridItems.Add(newItem);
            }
            btnOptimize.IsEnabled = gridItems.Any();
            lblDropHere.Visibility = gridItems.Any() ? Visibility.Hidden : Visibility.Visible;
        }

        static JobQueueParallelProcessor<FileGridItem> _queueProcessor;
        CancellationTokenSource CTS { get; set; }
        private void btnCompressSelected_Click(object sender, RoutedEventArgs e)
        {
            if (toggleOverwriteOriginal.IsOn &&
                MessageBox.Show(AppResources.MessageBox_ConfirmOverwrite, 
                                AppResources.MessageBox_ConfirmHeader, 
                                MessageBoxButton.YesNo) == MessageBoxResult.No)
            { 
                return;
            }

            var maxProcessorsToUse = Environment.ProcessorCount == 1 ? 1 : Environment.ProcessorCount/2;
            if (_queueProcessor == null) {
                _queueProcessor = JobQueueParallelProcessor<FileGridItem>.Create("Compressor", maxProcessorsToUse);
            }
            else
            {
                if (_queueProcessor.IsRunningJobs)
                    return;
            }

            if (!gridItems.Any()) return;

            gridItems.ToList()
                 .ForEach(item => {
                     _queueProcessor.AddJobToQueue(item);
                     item.Result = AppResources.Result_Enqueued;
                     });

            CTS = new CancellationTokenSource();
            var cancellationToken = CTS.Token;

            btnCancelCompression.Visibility = Visibility.Visible;
            btnOptimize.IsEnabled = false;
            btnAddFiles.IsEnabled = false;

            var compressConfig = GetCompressConfigFromForm();
            var jobProcessor = new PingoCompressFileJob(compressConfig);

            _ = _queueProcessor.ProcessQueue(jobProcessor.CompressFile,
                                             cancellationToken,
                                             jobProcessor.CompressDone,
                                             jobProcessor.CompressError)
            .ContinueWith((t) =>
            {
                Debug.WriteLine($"Queue Continuewith Start");
                while (_queueProcessor.IsRunningJobs) // awaits the lastest jobs to finish
                    Thread.Sleep(200);

                btnCancelCompression.Visibility = Visibility.Hidden;
                btnOptimize.IsEnabled = true;
                btnAddFiles.IsEnabled = true;
                CTS.Dispose();
                Debug.WriteLine($"Queue Continuewith End");
            },
            TaskScheduler.FromCurrentSynchronizationContext() // this makes the ContinueWith continue in the UI Thread
                                                              // to be able to access UI Controls
            ); ;            
        }

        private UserConfigs GetCompressConfigFromForm()
        {
            var config = new UserConfigs();

            config.UseLossLess = chkUseLossless.IsOn == true;
            config.CompressionType = Convert.ToInt32(slidCompression.Value);
            config.CompressionQuality = Convert.ToInt32(slidQuality.Value);
            config.ResizeOption = cmbResize.SelectedIndex;
            config.UseLossLess = chkUseLossless.IsOn == true;
            config.KeepOriginalMetadata = true;
            config.KeepModificationDate = true;
            config.OverwriteOriginal = toggleOverwriteOriginal.IsOn == true;

            return config;
        }

        private void btnCancelCompression_Click(object sender, RoutedEventArgs e)
        {   
            CTS.Cancel();
        }

        private void cmbResize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(e.Source is ComboBox) || txtResizeExplain == null) return;
            
            var cmb = e.Source as ComboBox;

            if(cmb.SelectedIndex != 0)
                txtResizeExplain.Text = AppResources.ResizeWarning;
            else
                txtResizeExplain.Text = "";
        }

        private void grdFiles_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                insertFilesInGrid(files);
            }
        }

        private void slidCompress_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (CompressionText == null)
                return;

            adjustSlidCompressCaption(e.NewValue);
        }

        private void adjustSlidCompressCaption(double controlValue)
        {
            CompressionText.Content = AppResources.CompressRation_Low;
            if (controlValue >= 2)
                CompressionText.Content = AppResources.CompressRation_Medium;
            if (controlValue >= 3)
                CompressionText.Content = AppResources.CompressRation_High;
            if (controlValue >= 4)
                CompressionText.Content = AppResources.CompressRation_Best;
        }

        private void slidQuality_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (QualityText == null)
                return;
            QualityText.Content = e.NewValue.ToString() + "%";
        }

        private void btnGithub_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/fabiojbg/Squeeze-It",
                UseShellExecute = true
            });
        }

        private void btnDonate_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(new ProcessStartInfo
            {
                FileName = "https://www.paypal.com/donate/?business=G47L9N4UW8C2C&no_recurring=1&item_name=Thank+you+%21%21%21&currency_code=USD",
                UseShellExecute = true
            });
        }

        ResourceDictionary dictLanguage = new ResourceDictionary();
        private void switchLanguage(string newLanguage)
        {
            AppSettings.UILanguage = newLanguage;
            if (newLanguage == "PT")
            {
                Thread.CurrentThread.CurrentUICulture = new CultureInfo("pt-BR");
                mnuLang_English.IsChecked = false;
                mnuLang_Portuguese.IsChecked = true;
            }
            else
            {
                Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");
                mnuLang_English.IsChecked = true;
                mnuLang_Portuguese.IsChecked = false;
            }
            Thread.CurrentThread.CurrentCulture = Thread.CurrentThread.CurrentUICulture;

            switch (newLanguage)
            {
                case "PT":
                    dictLanguage.Source = new Uri("..\\Localization\\AppLanguage.pt-BR.xaml", UriKind.Relative);
                    break;
                case "EN":
                default:
                    dictLanguage.Source = new Uri("..\\Localization\\AppLanguage.xaml", UriKind.Relative);
                    break;
            }

            // Grid headers does not work with dynamic resource
            hFile.Header = AppResources.grdHeader_File;
            hFileSize.Header = AppResources.grdHeader_FileSize;
            hImgDimensions.Header = AppResources.grdHeader_ImgDimensions;
            hNewDimensions.Header = AppResources.grdHeader_NewDimensions;
            hNewFilesize.Header = AppResources.grdHeader_NewFileSize;
            hReduction.Header = AppResources.grdHeader_Reduction;
            hResult.Header = AppResources.grdHeader_Result;

            adjustSlidCompressCaption(slidCompression.Value);

            this.Resources.MergedDictionaries.Add(dictLanguage);
        }

        private void mnuAbout_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AboutDialog();
            dialog.Resources.MergedDictionaries.Add(dictLanguage);
            dialog.ShowDialog();

        }

        private void mnuLang_Change_Click(object sender, RoutedEventArgs e)
        {
            if (sender == mnuLang_English)
                switchLanguage("EN");
            if (sender == mnuLang_Portuguese)
                switchLanguage("PT");

        }
    }
}
