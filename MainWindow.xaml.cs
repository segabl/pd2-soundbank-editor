using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PD2SoundBankEditor {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : AdonisUI.Controls.AdonisWindow {
		static readonly string CONVERTER_NAME = "wwise_ima_adpcm.exe";
		static readonly string CONVERTER_PATH = Path.Join(AppDomain.CurrentDomain.BaseDirectory, CONVERTER_NAME);
		static readonly string TEMP_DIR = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "tmp");

		private SoundBank soundBank;
		private int extractErrors;
		private Button playingButton;
		private MediaPlayer mediaPlayer = new MediaPlayer();

		public MainWindow() {
			InitializeComponent();
			if (!File.Exists(CONVERTER_PATH)) {
				AdonisUI.Controls.MessageBox.Show($"The sound converter could not be found! Please place {CONVERTER_NAME} in the directory of this application!", "Information", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Warning);
			}
			mediaPlayer.MediaEnded += ChangePlayButton;
			if (!Directory.Exists(TEMP_DIR)) {
				Directory.CreateDirectory(TEMP_DIR);
			}
		}

		private void OnImportButtonClick(object sender, RoutedEventArgs e) {
			var diag = new OpenFileDialog {
				Filter = "Soundbanks (*.bnk)|*.bnk"
			};
			if (diag.ShowDialog() != true) {
				return;
			}
			try {
				soundBank = new SoundBank(diag.FileName);
			} catch (Exception ex) {
				AdonisUI.Controls.MessageBox.Show($"There was an error trying to read the soundbank:\n{ex.Message}", "Error", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Error);
				return;
			}
			importTextBox.Text = diag.FileName;
			listView.ItemsSource = soundBank.GetWemFiles();
			listView.DataContext = listView.ItemsSource;
			extractAllButton.IsEnabled = true;
			saveButton.IsEnabled = true;
		}

		private void OnListViewSelectionChanged(object sender, SelectionChangedEventArgs e) {
			replaceSelectedButton.IsEnabled = listView.SelectedItems.Count > 0;
			extractSelectedButton.IsEnabled = listView.SelectedItems.Count > 0;
		}

		private string StartConverterProcess(string args) {
			if (!File.Exists(CONVERTER_PATH)) {
				return $"The sound converter could not be found! Please place {CONVERTER_NAME} in the directory of this application!";
			}
			Trace.WriteLine($"Running converter with arguments {args}");
			var convertProcess = new Process();
			convertProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
			convertProcess.StartInfo.UseShellExecute = false;
			convertProcess.StartInfo.RedirectStandardOutput = true;
			convertProcess.StartInfo.FileName = CONVERTER_PATH;
			convertProcess.StartInfo.Arguments = args;
			convertProcess.Start();
			var output = convertProcess.StandardOutput.ReadToEnd();
			convertProcess.WaitForExit();
			return output;
		}

		private void ExtractWemFiles(IEnumerable<WemFile> wemFiles) {
			mainGrid.IsEnabled = false;
			BackgroundWorker worker = new BackgroundWorker {
				WorkerReportsProgress = true
			};
			worker.DoWork += ExtractWemFilesAsync;
			worker.ProgressChanged += OnExtractWemFilesProgress;
			worker.RunWorkerCompleted += OnExtractWemFilesCompleted;
			worker.RunWorkerAsync(wemFiles);
		}

		private void ExtractWemFilesAsync(object sender, DoWorkEventArgs e) {
			var wemFiles = (IEnumerable<WemFile>)e.Argument;
			var soundBankName = soundBank.GetFilePath();
			var savePath = Path.Join(Path.GetDirectoryName(soundBankName), Path.GetFileNameWithoutExtension(soundBankName));
			if (!Directory.Exists(savePath)) {
				Directory.CreateDirectory(savePath);
			}
			var converterAvailable = File.Exists(CONVERTER_PATH);
			var n = 0;
			extractErrors = 0;
			foreach (var wem in wemFiles) {
				var errorStr = "";
				var fileName = Path.Join(savePath, wem.id.ToString() + ".stream");
				var convertedFileName = Path.ChangeExtension(fileName, "wav");
				if (!wem.Save(fileName)) {
					errorStr = wem.errorString;
				} else if (converterAvailable) {
					errorStr = StartConverterProcess($"-d \"{fileName}\" \"{convertedFileName}\"");
				}
				if (errorStr != "") {
					errorStr = $"There was an error processing the stream {wem.id}:\n{errorStr}";
					extractErrors++;
				} else {
					wem.convertedFilePath = convertedFileName;
				}
				(sender as BackgroundWorker).ReportProgress((int)(++n / (float)wemFiles.Count() * 100), errorStr);
			}
		}

		void OnExtractWemFilesProgress(object sender, ProgressChangedEventArgs e) {
			progressBar.Value = e.ProgressPercentage;
			var errorString = (string)e.UserState;
			if (errorString != null && errorString != "") {
				AdonisUI.Controls.MessageBox.Show(errorString, "Error", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Error);
			}
		}

		void OnExtractWemFilesCompleted(object sender, RunWorkerCompletedEventArgs e) {
			if (extractErrors > 0) {
				AdonisUI.Controls.MessageBox.Show($"Extraction completed with {extractErrors} error(s)!", "Information", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Warning);
			} else {
				AdonisUI.Controls.MessageBox.Show("Extraction complete!", "Information", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Information);
			}
			progressBar.Value = 0;
			mainGrid.IsEnabled = true;
		}

		private void OnExtractButtonClick(object sender, RoutedEventArgs e) {
			ExtractWemFiles(((Button)sender) == extractAllButton ? soundBank.GetWemFiles() : listView.SelectedItems.Cast<WemFile>());
		}

		private void OnSaveButtonClick(object sender, RoutedEventArgs e) {
			var diag = new SaveFileDialog {
				Filter = "Soundbanks (*.bnk)|*.bnk",
				AddExtension = true
			};
			if (diag.ShowDialog() != true) {
				return;
			}
			soundBank.Save(diag.FileName);
			AdonisUI.Controls.MessageBox.Show("Soundbank saved!", "Information", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Information);
		}

		private void OnReplaceButtonClick(object sender, RoutedEventArgs e) {
			var diag = new OpenFileDialog {
				Filter = "Wave audio files (*.wav)|*.wav"
			};
			if (diag.ShowDialog() != true) {
				return;
			}
			var fileNameNoExt = Path.GetFileNameWithoutExtension(diag.FileName);
			var fileName = Path.Combine(TEMP_DIR, fileNameNoExt + ".stream");
			var errorString = StartConverterProcess($"-e \"{diag.FileName}\" \"{fileName}\"");
			if (errorString != "") {
				AdonisUI.Controls.MessageBox.Show($"An error occured while trying to convert {diag.FileName}:\n{errorString}", "Error", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Error);
				return;
			}
			var data = File.ReadAllBytes(fileName);
			foreach (var wem in listView.SelectedItems.Cast<WemFile>()) {
				wem.data = data;
				wem.replacementFile = fileNameNoExt + ".wav";
			}
			listView.Items.Refresh();
			AdonisUI.Controls.MessageBox.Show($"Files replaced!", "Information", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Information);
		}

		private void OnPlayButtonClick(object sender, RoutedEventArgs e) {
			var button = (Button)sender;
			var wem = (WemFile)button.DataContext;

			Trace.WriteLine($"{wem.id} clicked");

			var sameButton = playingButton == button;

			mediaPlayer.Stop();
			ChangePlayButton(sender, e);

			if (sameButton) {
				return;
			}

			if (wem.convertedFilePath == null) {
				var fileName = Path.Combine(TEMP_DIR, $"{wem.id}.stream");
				var convertedFileName = Path.ChangeExtension(fileName, "wav");
				string errorString;
				if (!wem.Save(fileName)) {
					errorString = wem.errorString;
				} else {
					errorString = StartConverterProcess($"-d \"{fileName}\" \"{convertedFileName}\"");
				}
				File.Delete(fileName);
				if (errorString != "") {
					AdonisUI.Controls.MessageBox.Show($"Can't play file:\n{errorString}", "Error", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Error);
					return;
				}
				wem.convertedFilePath = convertedFileName;
			}
			mediaPlayer.Open(new Uri(wem.convertedFilePath));
			mediaPlayer.Play();
			button.Content = "■";
			playingButton = button;
		}

		private void ChangePlayButton(object sender, EventArgs e) {
			if (playingButton != null) {
				playingButton.Content = "▶";
			}
			playingButton = null;
		}

		private void OnWindowClosed(object sender, EventArgs e) {
			if (Directory.Exists(TEMP_DIR)) {
				Directory.Delete(TEMP_DIR, true);
			}
		}
	}
}
