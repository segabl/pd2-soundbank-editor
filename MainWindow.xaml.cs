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
			DoGenericProcessing(ProcessSoundbankStreamData, OnProcessSoundbankStreamDataFinished);
		}

		private void OnExtractButtonClick(object sender, RoutedEventArgs e) {
			DoGenericProcessing(ExtractStreams, OnExtractStreamsFinished, ((Button)sender) == extractAllButton ? soundBank.StreamInfos : listView.SelectedItems.Cast<StreamInfo>());
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
			foreach (var info in listView.SelectedItems.Cast<StreamInfo>()) {
				info.data = data;
				info.replacementFile = fileNameNoExt + ".wav";
				info.convertedFilePath = null;
			}
			listView.Items.Refresh();
			AdonisUI.Controls.MessageBox.Show($"Files replaced!", "Information", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Information);
		}

		private void OnSaveButtonClick(object sender, RoutedEventArgs e) {
			var diag = new SaveFileDialog {
				Filter = "Soundbanks (*.bnk)|*.bnk",
				FileName = Path.GetFileName(soundBank.FilePath),
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
			var info = (StreamInfo)button.DataContext;

			var sameButton = playingButton == button;

			SetPlayButtonState(null, null);

			if (sameButton) {
				return;
			}

			if (info.convertedFilePath == null) {
				var fileName = Path.Combine(TEMP_DIR, $"{info.id}.stream");
				var convertedFileName = Path.ChangeExtension(fileName, "wav");
				string errorString;
				if (!info.Save(fileName)) {
					errorString = info.errorString;
				} else {
					errorString = StartConverterProcess($"-d \"{fileName}\" \"{convertedFileName}\"");
				}
				File.Delete(fileName);
				if (errorString != "") {
					AdonisUI.Controls.MessageBox.Show($"Can't play file:\n{errorString}", "Error", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Error);
					return;
				}
				info.convertedFilePath = convertedFileName;
			}
			mediaPlayer.Open(new Uri(info.convertedFilePath));
			mediaPlayer.Play();
			SetPlayButtonState(button, null);
		}

		private void OnListViewSelectionChanged(object sender, SelectionChangedEventArgs e) {
			replaceSelectedButton.IsEnabled = listView.SelectedItems.Count > 0;
			extractSelectedButton.IsEnabled = listView.SelectedItems.Count > 0;
			if (listView.SelectedItems.Count > 0) {
				var info = (StreamInfo)listView.SelectedItem;
				Trace.WriteLine($"{info.id} clicked");
			}
		}

		private void OnListViewSizeChanged(object sender, SizeChangedEventArgs e) {
			ListView listView = sender as ListView;
			GridView gridView = listView.View as GridView;

			gridView.Columns[0].Width = 40;
			var workingWidth = Math.Max(0, listView.ActualWidth - SystemParameters.VerticalScrollBarWidth * 2 - gridView.Columns[0].Width - 16);
			for (var i = 1; i < gridView.Columns.Count; i++) {
				gridView.Columns[i].Width = workingWidth / (gridView.Columns.Count - 1);
			}
		}

		private string StartConverterProcess(string args) {
			if (!File.Exists(CONVERTER_PATH)) {
				return $"The sound converter could not be found! Please place {CONVERTER_NAME} in the directory of this application!";
			}
			Trace.WriteLine($"Running converter with arguments {args}");
			var convertProcess = new Process();
			convertProcess.StartInfo.UseShellExecute = false;
			convertProcess.StartInfo.CreateNoWindow = true;
			convertProcess.StartInfo.RedirectStandardOutput = true;
			convertProcess.StartInfo.FileName = CONVERTER_PATH;
			convertProcess.StartInfo.Arguments = args;
			convertProcess.Start();
			var output = convertProcess.StandardOutput.ReadToEnd();
			convertProcess.WaitForExit();
			return output;
		}

		private void DoGenericProcessing(Action<object, DoWorkEventArgs> work, Action<object, RunWorkerCompletedEventArgs> workFinished = null, object argument = null) {
			mainGrid.IsEnabled = false;
			BackgroundWorker worker = new BackgroundWorker {
				WorkerReportsProgress = true
			};
			worker.DoWork += (sender, e) => work(sender, e);
			worker.ProgressChanged += OnGenericProcessingProgress;
			if (workFinished != null) {
				worker.RunWorkerCompleted += (sender, e) => workFinished(sender, e);
			}
			worker.RunWorkerCompleted += OnGenericProcessingFinished;
			worker.RunWorkerAsync(argument);
		}

		void OnGenericProcessingProgress(object sender, ProgressChangedEventArgs e) {
			progressBar.Value = e.ProgressPercentage;
		}

		void OnGenericProcessingFinished(object sender, RunWorkerCompletedEventArgs e) {
			progressBar.Value = 0;
			mainGrid.IsEnabled = true;
		}

		private void ProcessSoundbankStreamData(object sender, DoWorkEventArgs e) {
			soundBank.ProcessStreamData(sender);
		}

		private void OnProcessSoundbankStreamDataFinished(object sender, RunWorkerCompletedEventArgs e) {
			var containsEmedded = soundBank.StreamInfos.Count > 0;
			listView.ItemsSource = soundBank.StreamInfos;
			listView.DataContext = soundBank.StreamInfos;
			extractAllButton.IsEnabled = containsEmedded;
			saveButton.IsEnabled = containsEmedded;
			if (!containsEmedded) {
				AdonisUI.Controls.MessageBox.Show($"This soundbank does not contain any embedded streams.", "Information", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Warning);
			}
		}

		private void ExtractStreams(object sender, DoWorkEventArgs e) {
			var streamDescriptions = (IEnumerable<StreamInfo>)e.Argument;
			var soundBankName = soundBank.FilePath;
			var savePath = Path.Join(Path.GetDirectoryName(soundBankName), Path.GetFileNameWithoutExtension(soundBankName));
			if (!Directory.Exists(savePath)) {
				Directory.CreateDirectory(savePath);
			}
			var converterAvailable = File.Exists(CONVERTER_PATH);
			var n = 0;
			extractErrors = 0;
			foreach (var info in streamDescriptions) {
				var errorStr = "";
				var fileName = Path.Join(savePath, info.id.ToString() + ".stream");
				var convertedFileName = Path.ChangeExtension(fileName, "wav");
				if (!info.Save(fileName)) {
					errorStr = info.errorString;
				} else if (converterAvailable) {
					errorStr = StartConverterProcess($"-d \"{fileName}\" \"{convertedFileName}\"");
				}
				if (errorStr != "") {
					errorStr = $"There was an error processing the stream {info.id}:\n{errorStr}";
					extractErrors++;
				} else {
					info.convertedFilePath = convertedFileName;
				}
				(sender as BackgroundWorker).ReportProgress((int)(++n / (float)streamDescriptions.Count() * 100));
			}
		}
		void OnExtractStreamsFinished(object sender, RunWorkerCompletedEventArgs e) {
			if (extractErrors > 0) {
				AdonisUI.Controls.MessageBox.Show($"Extraction finished with {extractErrors} coverter error(s)!", "Information", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Warning);
			} else {
				AdonisUI.Controls.MessageBox.Show("Extraction complete!", "Information", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Information);
			}
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
