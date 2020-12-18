using AdonisUI.Controls;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MessageBox = AdonisUI.Controls.MessageBox;
using MessageBoxButton = AdonisUI.Controls.MessageBoxButton;
using MessageBoxImage = AdonisUI.Controls.MessageBoxImage;
using MessageBoxResult = AdonisUI.Controls.MessageBoxResult;

namespace PD2SoundBankEditor {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : AdonisWindow {
		static readonly string CONVERTER_NAME = "wwise_ima_adpcm.exe";
		static readonly string CONVERTER_PATH = Path.Join(AppDomain.CurrentDomain.BaseDirectory, CONVERTER_NAME);
		static readonly string SETTINGS_PATH = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
		static readonly string TEMPORARY_PATH = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "tmp");

		private ApplicationSettings appSettings = new ApplicationSettings();
		private MediaPlayer mediaPlayer = new MediaPlayer();
		private SoundBank soundBank;
		private Button playingButton;
		private bool converterAvailable;

		public bool UpdateCheckEnabled { get => appSettings.checkForUpdates; set => appSettings.checkForUpdates = value; }

		public MainWindow() {
			InitializeComponent();
			DataContext = this;

			if (File.Exists(SETTINGS_PATH)) {
				try {
					appSettings = JsonConvert.DeserializeObject<ApplicationSettings>(File.ReadAllText(SETTINGS_PATH));
				} catch (Exception ex) {
					Trace.WriteLine(ex.Message);
				}
			}

			converterAvailable = File.Exists(CONVERTER_PATH);
			if (!converterAvailable) {
				MessageBox.Show($"The sound converter could not be found, you will not be able to play, convert or replace stream files! Please place {CONVERTER_NAME} in the directory of this application!", "Information", MessageBoxButton.OK, MessageBoxImage.Warning);
			}

			if (!Directory.Exists(TEMPORARY_PATH)) {
				Directory.CreateDirectory(TEMPORARY_PATH);
			}

			if (appSettings.checkForUpdates && (DateTime.Now - appSettings.lastUpdateCheck).TotalHours > 6) {
				try {
					var client = new WebClient();
					client.Headers.Add("User-Agent:PD2SoundbankEditor");
					client.DownloadStringAsync(new Uri("https://api.github.com/repos/segabl/pd2-soundbank-editor/releases"));
					client.DownloadStringCompleted += OnReleaseDataFetched;
					appSettings.lastUpdateCheck = DateTime.Now;
				} catch (Exception ex) {
					Trace.WriteLine(ex.Message);
				}
			}

			mediaPlayer.MediaEnded += SetPlayButtonState;
		}

		private void OnReleaseDataFetched(object sender, DownloadStringCompletedEventArgs e) {
			if (e.Error != null || e.Cancelled) {
				return;
			}

			GitHubRelease latestRelease = null;
			try {
				var allReleases = JsonConvert.DeserializeObject<List<GitHubRelease>>(e.Result);
				latestRelease = allReleases.FirstOrDefault(r => !r.draft && !r.prerelease && r.assets.Count > 0);
			} catch (Exception ex) {
				Trace.WriteLine(ex.Message);
			}
			if (latestRelease == null) {
				return;
			}

			var latestVersion = latestRelease.tag_name[1..];
			var productVersion = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion;
			if (CompareVersionStrings(latestVersion, productVersion) > 0) {
				var result = MessageBox.Show($"There's a newer release ({latestRelease.tag_name}) of this application available. Do you want to download it now?", "Information", MessageBoxButton.YesNo, MessageBoxImage.Information);
				if (result == MessageBoxResult.Yes) {
					Process.Start(new ProcessStartInfo(latestRelease.assets[0].browser_download_url) { UseShellExecute = true });
				}
			}
		}

		private void CommandSaveCanExecute(object sender, System.Windows.Input.CanExecuteRoutedEventArgs e) {
			e.CanExecute = soundBank != null && soundBank.IsDirty && soundBank.StreamInfos.Count > 0;
		}

		private void CommandSaveExecuted(object sender, System.Windows.Input.ExecutedRoutedEventArgs e) {
			soundBank.Save(soundBank.FilePath);
		}
		private void CommandSaveAsCanExecute(object sender, System.Windows.Input.CanExecuteRoutedEventArgs e) {
			e.CanExecute = soundBank != null && soundBank.StreamInfos.Count > 0;
		}

		private void CommandSaveAsExecuted(object sender, System.Windows.Input.ExecutedRoutedEventArgs e) {
			var diag = new SaveFileDialog {
				Filter = "Soundbanks (*.bnk)|*.bnk",
				FileName = Path.GetFileName(soundBank.FilePath),
				AddExtension = true
			};
			if (diag.ShowDialog() != true) {
				return;
			}
			soundBank.Save(diag.FileName);
			Title = $"PD2 Soundbank Editor - {Path.GetFileName(soundBank.FilePath)}";
		}

		private void OnOpenButtonClick(object sender, RoutedEventArgs e) {
			var diag = new OpenFileDialog {
				Filter = "Soundbanks (*.bnk)|*.bnk"
			};
			if (diag.ShowDialog() != true) {
				return;
			}
			soundBank = new SoundBank(diag.FileName);
			Title = $"PD2 Soundbank Editor - {Path.GetFileName(soundBank.FilePath)}";
			DoGenericProcessing(ProcessSoundbankStreamData, OnProcessSoundbankStreamDataFinished);
		}

		private void OnExitButtonClick(object sender, RoutedEventArgs e) {
			Close();
		}

		private void OnAboutButtonClick(object sender, RoutedEventArgs e) {
			MessageBox.Show($"PD2 Soundbank Editor v{FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion}\nMade by Hoppip", "About", MessageBoxButton.OK, MessageBoxImage.Information);
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
			var fileName = Path.Combine(TEMPORARY_PATH, fileNameNoExt + ".stream");
			try {
				StartConverterProcess($"-e \"{diag.FileName}\" \"{fileName}\"");
			} catch (Exception ex) {
				MessageBox.Show($"An error occured while trying to convert {diag.FileName}:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}
			var data = File.ReadAllBytes(fileName);
			foreach (var info in listView.SelectedItems.Cast<StreamInfo>()) {
				info.Data = data;
				info.ReplacementFile = fileNameNoExt + ".wav";
				info.ConvertedFilePath = null;
			}
			listView.Items.Refresh();
		}

		private void OnPlayButtonClick(object sender, RoutedEventArgs e) {
			if (!converterAvailable) {
				return;
			}

			var button = (Button)sender;
			var info = (StreamInfo)button.DataContext;

			var sameButton = playingButton == button;

			SetPlayButtonState(null, null);

			if (sameButton) {
				return;
			}

			if (info.ConvertedFilePath == null) {
				var fileName = Path.Combine(TEMPORARY_PATH, $"{info.Id}.stream");
				var convertedFileName = Path.ChangeExtension(fileName, "wav");
				try {
					info.Save(fileName);
					StartConverterProcess($"-d \"{fileName}\" \"{convertedFileName}\"");
					info.ConvertedFilePath = convertedFileName;
				} catch (Exception ex) {
					MessageBox.Show($"Can't play file:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
					return;
				} finally {
					File.Delete(fileName);
				}
			}
			mediaPlayer.Open(new Uri(info.ConvertedFilePath));
			mediaPlayer.Play();
			SetPlayButtonState(button, null);
		}

		private void OnListViewSelectionChanged(object sender, SelectionChangedEventArgs e) {
			replaceSelectedButton.IsEnabled = converterAvailable && listView.SelectedItems.Count > 0;
			extractSelectedButton.IsEnabled = converterAvailable && listView.SelectedItems.Count > 0;
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

		private void OnWindowClosed(object sender, EventArgs e) {
			if (Directory.Exists(TEMPORARY_PATH)) {
				Directory.Delete(TEMPORARY_PATH, true);
			}
			File.WriteAllText(SETTINGS_PATH, JsonConvert.SerializeObject(appSettings));
		}

		private void StartConverterProcess(string args) {
			var convertProcess = new Process();
			convertProcess.StartInfo.UseShellExecute = false;
			convertProcess.StartInfo.CreateNoWindow = true;
			convertProcess.StartInfo.RedirectStandardOutput = true;
			convertProcess.StartInfo.FileName = CONVERTER_PATH;
			convertProcess.StartInfo.Arguments = args;
			convertProcess.Start();
			var output = convertProcess.StandardOutput.ReadToEnd();
			convertProcess.WaitForExit();
			if (output != "") {
				throw new FileFormatException(output);
			}
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
			soundBank.ProcessData(sender);
		}

		private void OnProcessSoundbankStreamDataFinished(object sender, RunWorkerCompletedEventArgs e) {
			var containsEmedded = soundBank.StreamInfos.Count > 0;
			listView.ItemsSource = soundBank.StreamInfos;
			listView.DataContext = soundBank.StreamInfos;
			extractAllButton.IsEnabled = converterAvailable && containsEmedded;
			if (!containsEmedded) {
				MessageBox.Show($"This soundbank does not contain any embedded streams.", "Information", MessageBoxButton.OK, MessageBoxImage.Warning);
			}
		}

		private void ExtractStreams(object sender, DoWorkEventArgs e) {
			var streamDescriptions = (IEnumerable<StreamInfo>)e.Argument;
			var soundBankName = soundBank.FilePath;
			var savePath = Path.Join(Path.GetDirectoryName(soundBankName), Path.GetFileNameWithoutExtension(soundBankName));
			if (!Directory.Exists(savePath)) {
				Directory.CreateDirectory(savePath);
			}
			var n = 0;
			var errors = 0;
			foreach (var info in streamDescriptions) {
				var fileName = Path.Join(savePath, $"{info.Id}.stream");
				var convertedFileName = Path.ChangeExtension(fileName, "wav");
				try {
					info.Save(fileName);
					StartConverterProcess($"-d \"{fileName}\" \"{convertedFileName}\"");
					info.ConvertedFilePath = convertedFileName;
				} catch (Exception) {
					errors++;
				}
				(sender as BackgroundWorker).ReportProgress((int)(++n / (float)streamDescriptions.Count() * 100));
			}
			e.Result = errors;
		}

		void OnExtractStreamsFinished(object sender, RunWorkerCompletedEventArgs e) {
			var errors = (int)e.Result;
			if (errors > 0) {
				MessageBox.Show($"Extraction finished with {errors} converter {(errors == 1 ? "error" : "errors")}!", "Information", MessageBoxButton.OK, MessageBoxImage.Warning);
			} else {
				MessageBox.Show("Extraction complete!", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
			}
		}

		private void SetPlayButtonState(object sender, EventArgs e) {
			if (sender == mediaPlayer || sender == null) {
				mediaPlayer.Stop();
				mediaPlayer.Close();
				if (playingButton != null) {
					playingButton.Content = "▶";
				}
				playingButton = null;
			} else {
				playingButton = (Button)sender;
				playingButton.Content = "■";
			}
		}
		private int CompareVersionStrings(string v1, string v2) {
			var nums1 = v1.Split(".").Select(int.Parse).ToArray();
			var nums2 = v2.Split(".").Select(int.Parse).ToArray();
			for (var i = 0; i < nums1.Length && i < nums2.Length; i++) {
				if (nums1[i] == nums2[i]) {
					continue;
				} else {
					return Math.Sign(nums1[i] - nums2[i]);
				}
			}
			return Math.Sign(nums1.Length - nums2.Length);
		}
	}
}
