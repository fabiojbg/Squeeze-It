using SqueezeIt.Extensions;
using SqueezeIt.Localization;
using SqueezeIt.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.UI.WebControls.WebParts;

namespace SqueezeIt
{
    public class PingoCompressFileJob
    {
        UserConfigs _compressConfig;

        public PingoCompressFileJob(UserConfigs compressConfig)
        {
            _compressConfig = compressConfig;
        }

        public void CompressFile(FileGridItem item, CancellationToken? cancellation)
        {
            Debug.WriteLine($"CompressFile Start: {item}");
            item.Result = AppResources.Result_Processing;

            string currentPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            currentPath = System.IO.Path.GetDirectoryName(currentPath);
            
            string fileName = $"{currentPath}\\Tools\\pingo.exe"; // Replace with your application path
            string args = getPingoArgumentsUserConfigs(item, _compressConfig);

            var workPath = System.IO.Path.GetDirectoryName(item.FilePath);
            var fileOutput = item.FilePath;
            if (!_compressConfig.OverwriteOriginal)
            {
                fileOutput = getNewFileOutputName(item.FilePath, false);
                System.IO.File.Copy(item.FilePath, fileOutput, true);
            }

            // Configure the process start info
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = $"\"{fileOutput}\" {args}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                // Start the process
                using (Process process = new Process())
                {
                    process.StartInfo = startInfo;
                    process.Start();

                    // Read the output streams
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();

                    // Wait for the process to exit
                    process.WaitForExit();

                    // Display or handle the output
                    if (output.Trim().StartsWith("Error") || !String.IsNullOrEmpty(error))
                    {
                        item.Result = AppResources.Result_Error;
                    }

                    var fileInfo = new FileInfo(fileOutput);
                    using (System.Drawing.Image img = System.Drawing.Image.FromFile(fileOutput))
                    {
                        var dimensions = img.GetCorrectedDimensions();
                        item.NewDimensions = $"{dimensions.width}x{dimensions.height}";
                        item.NewFileSize = (float)fileInfo.Length / 1024F / 1024F;
                        item.Reduction = (1 - item.NewFileSize.Value / item.FileSize) * 100;
                    }

                    if (_compressConfig.OverwriteOriginal)
                        item.Result = AppResources.Result_Ok;
                    else
                        item.Result = String.Format(AppResources.Result_OkOverwrite, System.IO.Path.GetFileName(fileOutput));
                }
            }
            catch (Exception ex)
            {
                if (!_compressConfig.OverwriteOriginal)
                {
                    try { System.IO.File.Delete(fileOutput); } catch { }
                }
                item.Result = String.Format(AppResources.Result_ErrorWithException, ex.Message);
            }
        }

        public void CompressDone(FileGridItem item)
        {
            Debug.WriteLine($"CompressDone: {item}");
            item.Processed = true;
        }

        public void CompressError(FileGridItem item, Exception ex)
        {
            item.Processed = true;
            if (ex is TaskCanceledException || ex is OperationCanceledException)
                item.Result = AppResources.Result_Cancelled;
            else
                item.Result = String.Format(AppResources.Result_ErrorWithException, ex.Message);
        }

        private string getNewFileOutputName(string originalFilePath, bool suffixWhenColide)
        {
            // Get the directory, file name without extension, and extension
            string directory = System.IO.Path.GetDirectoryName(originalFilePath);
            string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(originalFilePath);
            string extension = System.IO.Path.GetExtension(originalFilePath);

            // Start with a suffix of 1
            int suffix = 1;
            string newFileName;
            string newFilePath;
            do
            {
                var pars = getCompressParamsAsString();
                // Generate a new file name with the current suffix
                if(suffixWhenColide)
                    newFileName = $"{fileNameWithoutExtension}_{pars}_{suffix:D3}{extension}";
                else
                    newFileName = $"{fileNameWithoutExtension}_{pars}{extension}";

                newFilePath = System.IO.Path.Combine(directory, newFileName);

                // Increment the suffix for the next iteration if needed
                suffix++;
            }
            while (suffixWhenColide && File.Exists(newFilePath)); // Check if the file already exists

            return newFilePath;
        }

        private string getCompressParamsAsString()
        {
            string[] compressionType = { AppResources.CompressRation_Low, 
                                         AppResources.CompressRation_Medium, 
                                         AppResources.CompressRation_High,
                                         AppResources.CompressRation_Best};
            string[] resizeOption = { "R-100", "R-80", "R-70", "R-50", "R-35", "R-25", "R-720p", "R-1080p", "R-1440p", "R-2160" };

            string pars = "";
            if (_compressConfig.UseLossLess)
                pars += "_Lossless";

            pars += "_" + compressionType[_compressConfig.CompressionType-1];

            if (!_compressConfig.UseLossLess)
            {
                pars += $"_Q-{_compressConfig.CompressionQuality}";
                pars += "_" + resizeOption[_compressConfig.ResizeOption];
            }
            return pars;
        }

        private string getPingoArgumentsUserConfigs(FileGridItem item, UserConfigs config)
        {
            string args = "";

            if (config.KeepOriginalMetadata)
                args += " -nostrip";
            if (config.KeepModificationDate)
                args += " -notime";
            if (config.UseLossLess)
                args += " -lossless";

            args += " -s" + config.CompressionType;

            if (!config.UseLossLess) // althought not documented by pingo, the quality and resize parameter are not compatible with LossLess
            {
                args += " -quality=" + config.CompressionQuality;

                var resize = !item.Rotated ? item.Width : item.Height;
                switch (config.ResizeOption)
                {
                    case 1: args += $" -resize={resize * 80 / 100}"; break;
                    case 2: args += $" -resize={resize * 70 / 100}"; break;
                    case 3: args += $" -resize={resize * 50 / 100}"; break;
                    case 4: args += $" -resize={resize * 35 / 100}"; break;
                    case 5: args += $" -resize={resize * 25 / 100}"; break;
                    case 6: args += $" -resize=720"; break;
                    case 7: args += $" -resize=1080"; break;
                    case 8: args += $" -resize=1440"; break;
                    case 9: args += $" -resize=2160"; break;
                    default:
                        args += ""; break;
                }
            }

            return args;
        }

    }
}
