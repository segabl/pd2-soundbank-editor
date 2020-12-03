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
		private bool suppressErrors = false;

		public MainWindow() {
			InitializeComponent();
			if (!File.Exists(CONVERTER_PATH)) {
				AdonisUI.Controls.MessageBox.Show($"The sound converter could not be found! Please place {CONVERTER_NAME} in the directory of this application!", "Information", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Warning);
			}
			mediaPlayer.MediaEnded += SetPlayButtonState;
			if (!Directory.Exists(TEMP_DIR)) {
				Directory.CreateDirectory(TEMP_DIR);
			}
		}

		private void OnWindowClosed(object sender, EventArgs e) {
			if (Directory.Exists(TEMP_DIR)) {
				Directory.Delete(TEMP_DIR, true);
			}
		}

		private void OnOpenButtonClick(object sender, RoutedEventArgs e) {
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

		private void OnExtractButtonClick(object sender, RoutedEventArgs e) {
			var extractAll = ((Button)sender) == extractAllButton;
			suppressErrors = extractAll;
			mainGrid.IsEnabled = false;
			BackgroundWorker worker = new BackgroundWorker {
				WorkerReportsProgress = true
			};
			worker.DoWork += ExtractStreams;
			worker.ProgressChanged += OnExtractStreamsProgress;
			worker.RunWorkerCompleted += OnExtractStreamsFinished;
			worker.RunWorkerAsync(extractAll ? soundBank.GetWemFiles() : listView.SelectedItems.Cast<StreamDescription>());
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
			foreach (var desc in listView.SelectedItems.Cast<StreamDescription>()) {
				desc.data = data;
				desc.replacementFile = fileNameNoExt + ".wav";
			}
			listView.Items.Refresh();
			AdonisUI.Controls.MessageBox.Show($"Files replaced!", "Information", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Information);
		}

		private void OnSaveButtonClick(object sender, RoutedEventArgs e) {
			var diag = new SaveFileDialog {
				Filter = "Soundbanks (*.bnk)|*.bnk",
				FileName = Path.GetFileName(soundBank.GetFilePath()),
				AddExtension = true
			};
			if (diag.ShowDialog() != true) {
				return;
			}
			soundBank.Save(diag.FileName);
			AdonisUI.Controls.MessageBox.Show("Soundbank saved!", "Information", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Information);
		}

		private void OnPlayButtonClick(object sender, RoutedEventArgs e) {
			var button = (Button)sender;
			var desc = (StreamDescription)button.DataContext;

			var sameButton = playingButton == button;

			SetPlayButtonState(null, null);

			if (sameButton) {
				return;
			}

			if (desc.convertedFilePath == null) {
				var fileName = Path.Combine(TEMP_DIR, $"{desc.id}.stream");
				var convertedFileName = Path.ChangeExtension(fileName, "wav");
				string errorString;
				if (!desc.Save(fileName)) {
					errorString = desc.errorString;
				} else {
					errorString = StartConverterProcess($"-d \"{fileName}\" \"{convertedFileName}\"");
				}
				File.Delete(fileName);
				if (errorString != "") {
					AdonisUI.Controls.MessageBox.Show($"Can't play file:\n{errorString}", "Error", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Error);
					return;
				}
				desc.convertedFilePath = convertedFileName;
			}
			mediaPlayer.Open(new Uri(desc.convertedFilePath));
			mediaPlayer.Play();
			SetPlayButtonState(button, null);
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

		private void ExtractStreams(object sender, DoWorkEventArgs e) {
			var streamDescriptions = (IEnumerable<StreamDescription>)e.Argument;
			var soundBankName = soundBank.GetFilePath();
			var savePath = Path.Join(Path.GetDirectoryName(soundBankName), Path.GetFileNameWithoutExtension(soundBankName));
			if (!Directory.Exists(savePath)) {
				Directory.CreateDirectory(savePath);
			}
			var converterAvailable = File.Exists(CONVERTER_PATH);
			var n = 0;
			extractErrors = 0;
			foreach (var desc in streamDescriptions) {
				var errorStr = "";
				var fileName = Path.Join(savePath, desc.id.ToString() + ".stream");
				var convertedFileName = Path.ChangeExtension(fileName, "wav");
				if (!desc.Save(fileName)) {
					errorStr = desc.errorString;
				} else if (converterAvailable) {
					errorStr = StartConverterProcess($"-d \"{fileName}\" \"{convertedFileName}\"");
				}
				if (errorStr != "") {
					errorStr = $"There was an error processing the stream {desc.id}:\n{errorStr}";
					extractErrors++;
				} else {
					desc.convertedFilePath = convertedFileName;
				}
				(sender as BackgroundWorker).ReportProgress((int)(++n / (float)streamDescriptions.Count() * 100), errorStr);
			}
		}

		void OnExtractStreamsProgress(object sender, ProgressChangedEventArgs e) {
			progressBar.Value = e.ProgressPercentage;
			if (suppressErrors) {
				return;
			}
			var errorString = (string)e.UserState;
			if (errorString != null && errorString != "") {
				AdonisUI.Controls.MessageBox.Show(errorString, "Error", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Error);
			}
		}

		void OnExtractStreamsFinished(object sender, RunWorkerCompletedEventArgs e) {
			if (extractErrors > 0) {
				AdonisUI.Controls.MessageBox.Show($"Extraction finished with {extractErrors} error(s)!", "Information", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Warning);
			} else {
				AdonisUI.Controls.MessageBox.Show("Extraction complete!", "Information", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Information);
			}
			progressBar.Value = 0;
			mainGrid.IsEnabled = true;
		}

		private void SetPlayButtonState(object sender, EventArgs e) {
			if (sender == mediaPlayer || sender == null) {
				mediaPlayer.Stop();
				if (playingButton != null) {
					playingButton.Content = "▶";
				}
				playingButton = null;
			} else {
				playingButton = (Button)sender;
				playingButton.Content = "■";
			}
		}
	}
}
