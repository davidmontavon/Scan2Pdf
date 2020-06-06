using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Scan2Pdf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int IMAGE_WIDTH = 598;
        private const int IMAGE_HEIGHT = 842;
        private const int MAX_PROCESS_PAUSE_TIME_MILLISECONDS = 100000;

        private string[] SupportedScanExtensions = new string[] { ".jpg", ".jpeg", ".jpe", ".jif", ".jfif", ".jfi", ".png", ".bmp", ".gif", ".pdf" };
        private List<string> ScanFilePaths = new List<string>();
        private AboutWindow WdwAbout = new AboutWindow();

        private string R(string resourceKey)
        {
            return (string)Application.Current.FindResource(resourceKey);
        }

        public MainWindow()
        {
            InitializeComponent();
            SetLanguageDictionary();

            try
            {
                GatherScanFilePathsFromArguments();

                // Adapt the window title according to the number of selected scans
                // or throw an exception if no file was selected.
                if (ScanFilePaths.Count == 0)
                    throw new Exception(R("errorNoScanFileSelected"));
                else if (ScanFilePaths.Count == 1)
                    wdwMain.Title = R("conversionWindowsTitleOneFile");
                else
                    wdwMain.Title = R("conversionWindowsTitleMultipleFiles")
                                        .Replace("[SCANS_COUNT]", ScanFilePaths.Count.ToString());

                // Check that every file really exists.
                foreach (string scanFilePath in ScanFilePaths)
                    if (!File.Exists(scanFilePath))
                        throw new FileNotFoundException(R("errorScanFileNotFound").Replace("[SCAN_FILE_PATH]", scanFilePath));
            }
            catch (Win32Exception ex) when ((uint)ex.ErrorCode == 0x80004005)
            {
                // Intentionally empty - no security access to the process.
            }
            catch (InvalidOperationException)
            {
                // Intentionally empty - the process exited before getting details.
            }
            catch (Exception exc)
            {
                DisplayError(exc);
                Environment.Exit(0);
            }
        }

        private void WdwMain_Loaded(object sender, RoutedEventArgs e)
        {
            txtPdfOutputFileName.Focus();
            txtPdfOutputFileName.SelectAll();
        }

        private void BtnGeneratePdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(txtPdfOutputFileName.Text))
                    return;

                if (ScanFilePaths == null || ScanFilePaths.Count == 0)
                    throw new Exception(R("errorNoScanFileSelected"));

                string pdfOutputFilePath = $"{txtPdfOutputFileName.Text}.pdf";

                using (PdfDocument doc = new PdfDocument())
                {
                    PdfPage page;

                    foreach (string scanFilePath in ScanFilePaths)
                    {
                        if (!File.Exists(scanFilePath))
                            throw new FileNotFoundException(R("errorScanFileNotFound").Replace("[SCAN_FILE_PATH]", scanFilePath));

                        string scanFileExtension = Path.GetExtension(scanFilePath).ToLower();

                        // If the scan is a PDF document, add it to the output PDF document.
                        if (scanFileExtension == ".pdf")
                        {
                            try
                            {
                                using (PdfDocument pdfScan = PdfReader.Open(scanFilePath, PdfDocumentOpenMode.Import))
                                    for (int i = 0; i < pdfScan.PageCount; i++)
                                        doc.AddPage(pdfScan.Pages[i]);
                            }
                            catch (InvalidOperationException)
                            {
                                throw new Exception(R("errorInvalidPdfFile").Replace("[SCAN_FILE_PATH]", scanFilePath));
                            }
                        }
                        // Otherwise, the scan is an image --> add a new page and insert the image full size.
                        else
                        {
                            page = doc.AddPage();

                            try
                            {
                                using (XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append))
                                using (XImage image = XImage.FromFile(scanFilePath))
                                {
                                    // If the image width is larger than its height, use landscape orientation, otherwise use portrait.
                                    if (image.PixelWidth > image.PixelHeight)
                                    {
                                        page.Orientation = PageOrientation.Landscape;
                                        gfx.DrawImage(image, 0, 0, IMAGE_HEIGHT, IMAGE_WIDTH);
                                    }
                                    else
                                    {
                                        page.Orientation = PageOrientation.Portrait;
                                        gfx.DrawImage(image, 0, 0, IMAGE_WIDTH, IMAGE_HEIGHT);
                                    }
                                }
                            }
                            catch (OutOfMemoryException)
                            {
                                throw new Exception(R("errorInvalidImageFile").Replace("[SCAN_FILE_PATH]", scanFilePath));
                            }
                        }
                    }

                    // Generate the output PDF document.
                    doc.Save(pdfOutputFilePath);
                }

                // Open the output PDF document.
                Process.Start(pdfOutputFilePath);

                Environment.Exit(0);
            }
            catch (Exception exc)
            {
                DisplayError(exc);
                Environment.Exit(0);
            }
        }

        private void TxtPdfOutputFileName_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Filter out reserved characters.
            txtPdfOutputFileName.Text = Regex.Replace(txtPdfOutputFileName.Text, @"[<>:\""/\\|\?\*]", "");

            // Only enable the convert button if the output file name is specified and is correct.
            txtPdfOutputFileName.IsEnabled = !string.IsNullOrEmpty(txtPdfOutputFileName.Text);
        }

        private void GatherScanFilePathsFromArguments()
        {
            using (Process currentProcess = Process.GetCurrentProcess())
            {
                Process[] scan2PdfProcesses = Process.GetProcessesByName("Scan2Pdf");

                // Other Scan2Pdf processes - close them after a while if not already killed bz the main process.
                if (scan2PdfProcesses[0].Id != currentProcess.Id)
                {
                    Thread.Sleep(MAX_PROCESS_PAUSE_TIME_MILLISECONDS);
                    Environment.Exit(0);
                }
                // Main Scan2Pdf process.
                else
                {
                    string[] args = Environment.GetCommandLineArgs();

                    if (args.Length < 2)
                        throw new Exception(R("errorNoScanFileSelected"));

                    string scanFileExtension = Path.GetExtension(args[1]).ToLower();

                    if (SupportedScanExtensions.Contains(scanFileExtension))
                        ScanFilePaths.Add(args[1]);

                    string scanFilePath;
                    string commandLine;
                    string[] commandLineArgs;

                    foreach (Process process in scan2PdfProcesses)
                    {
                        if (process.Id != currentProcess.Id)
                        {
                            commandLine = process.GetCommandLine();
                            commandLineArgs = commandLine.Split(new string[] { "\" \"" }, StringSplitOptions.None);
                            scanFilePath = commandLineArgs[1].Replace("\"", "");

                            if (SupportedScanExtensions.Contains(System.IO.Path.GetExtension(scanFilePath)))
                                ScanFilePaths.Add(scanFilePath);

                            // Kill every other instance of the program.
                            try
                            {
                                process.Kill();
                            }
                            catch
                            {
                            }
                        }
                    }

                    // Sort the scan list alphabetically.
                    ScanFilePaths.Sort();
                }
            }
        }

        private void SetLanguageDictionary()
        {
            ResourceDictionary dict = new ResourceDictionary();
            switch (Thread.CurrentThread.CurrentCulture.ToString())
            {
                case "fr-CA":
                case "fr-CH":
                case "fr-BE":
                case "fr-FR":
                    dict.Source = new Uri("..\\Resources\\StringResources.fr.xaml", UriKind.Relative);
                    break;
                default:
                    dict.Source = new Uri("..\\Resources\\StringResources.xaml", UriKind.Relative);
                    break;
            }
            this.Resources.MergedDictionaries.Add(dict);
        }

        private void DisplayError(Exception exc)
        {
#if DEBUG
            MessageBox.Show(exc.ToString(), R("errorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
#else
            MessageBox.Show(exc.Message, R("errorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
#endif
        }

        private void MniAbout_Click(object sender, RoutedEventArgs e)
        {
            WdwAbout.Show();
        }
    }
}
