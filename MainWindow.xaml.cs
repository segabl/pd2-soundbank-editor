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
using System.Text.RegularExpressions;
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

			if (appSettings.checkForUpdates && (DateTime.Now - appSettings.lastUpdateCheck).TotalHours > 1) {
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

			recentFilesList.ItemsSource = appSettings.recentlyOpenedFiles;
			recentFilesList.IsEnabled = appSettings.recentlyOpenedFiles.Count > 0;

			mediaPlayer.MediaEnded += SetPlayButtonState;
		}

		private void OnReleaseDataFetched(object sender, DownloadStringCompletedEventArgs e) {
			if (e.Error != null || e.Cancelled) {
				return;
			}

			GitHubRelease latestRelease = null;
			try {
				var allReleases = JsonConvert.DeserializeObject<List<GitHubRelease>>(e.Result);
				latestRelease = allReleases.FirstOrDefault(r => !r.draft && !r.prerelease);
			} catch (Exception ex) {
				Trace.WriteLine(ex.Message);
			}
			if (latestRelease == null) {
				return;
			}

			var latestVersion = latestRelease.tag_name[1..];
			var productVersion = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion;
			if (CompareVersionStrings(latestVersion, productVersion) > 0) {
				var result = MessageBox.Show($"There's a newer release ({latestRelease.tag_name}) of this application available. Do you want to go to the release page to download it now?", "Information", MessageBoxButton.YesNo, MessageBoxImage.Information);
				if (result == MessageBoxResult.Yes) {
					Process.Start(new ProcessStartInfo(latestRelease.html_url) { UseShellExecute = true });
				}
			}
		}

		private void CommandSaveCanExecute(object sender, System.Windows.Input.CanExecuteRoutedEventArgs e) {
			e.CanExecute = soundBank != null && soundBank.IsDirty && soundBank.StreamInfos.Count > 0;
		}

		private void CommandSaveExecuted(object sender, System.Windows.Input.ExecutedRoutedEventArgs e) {
			soundBank.Save(soundBank.FilePath);
			Title = $"PD2 Soundbank Editor - {Path.GetFileName(soundBank.FilePath)}";
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
			soundBank?.SaveNotes();

			DoGenericProcessing(false, LoadSoundBank, OnSoundBankLoaded, diag.FileName);
		}

		private void OnExitButtonClick(object sender, RoutedEventArgs e) {
			Close();
		}

		private void OnAboutButtonClick(object sender, RoutedEventArgs e) {
			MessageBox.Show($"PD2 Soundbank Editor v{FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion}\nMade by Hoppip", "About", MessageBoxButton.OK, MessageBoxImage.Information);
		}

		private void OnExtractButtonClick(object sender, RoutedEventArgs e) {
			DoGenericProcessing(true, ExtractStreams, OnExtractStreamsFinished, ((Button)sender) == extractAllButton ? soundBank.StreamInfos : dataGrid.SelectedItems.Cast<StreamInfo>());
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
			foreach (var info in dataGrid.SelectedItems.Cast<StreamInfo>()) {
				info.Data = data;
				info.ReplacementFile = fileNameNoExt + ".wav";
				info.ConvertedFilePath = null;
			}
			soundBank.IsDirty = true;
			Title = $"PD2 Soundbank Editor - {Path.GetFileName(soundBank.FilePath)}*";
		}

		private void OnFilterTextBoxChanged(object sender, RoutedEventArgs e) {
			var text = (sender as TextBox).Text;
			if (text.Length > 0) {
				var rx = new Regex(text, RegexOptions.Compiled);
				var filtered = soundBank.StreamInfos.Where(info => rx.Match(info.Note).Success || rx.Match(info.Id.ToString()).Success);
				dataGrid.ItemsSource = filtered;
				dataGrid.DataContext = filtered;
			} else {
				dataGrid.ItemsSource = soundBank.StreamInfos;
				dataGrid.DataContext = soundBank.StreamInfos;
			}
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

		private void OnRecentFileClick(object sender, RoutedEventArgs e) {
			var menuItem = (MenuItem)sender;
			var file = (string)menuItem.DataContext;

			soundBank?.SaveNotes();

			DoGenericProcessing(false, LoadSoundBank, OnSoundBankLoaded, file);

			if (!File.Exists(file)) {
				appSettings.recentlyOpenedFiles.Remove(file);
				recentFilesList.ItemsSource = null; //too lazy for proper notify
				recentFilesList.ItemsSource = appSettings.recentlyOpenedFiles;
			}
		}

		private void OnDataGridSelectionChanged(object sender, SelectionChangedEventArgs e) {
			replaceSelectedButton.IsEnabled = converterAvailable && dataGrid.SelectedItems.Count > 0;
			extractSelectedButton.IsEnabled = converterAvailable && dataGrid.SelectedItems.Count > 0;
		}

		private void OnWindowClosed(object sender, EventArgs e) {
			soundBank?.SaveNotes();
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

		public void DoGenericProcessing(bool reportProgress, Action<object, DoWorkEventArgs> work, Action<object, RunWorkerCompletedEventArgs> workFinished = null, object argument = null) {
			mainGrid.IsEnabled = false;
			BackgroundWorker worker = new BackgroundWorker {
				WorkerReportsProgress = reportProgress
			};
			worker.DoWork += (sender, e) => work(sender, e);
			if (reportProgress) {
				worker.ProgressChanged += OnGenericProcessingProgress;
			} else {
				progressBar.IsIndeterminate = true;
			}
			worker.RunWorkerCompleted += OnGenericProcessingFinished;
			if (workFinished != null) {
				worker.RunWorkerCompleted += (sender, e) => workFinished(sender, e);
			}
			worker.RunWorkerAsync(argument);
		}

		private void OnGenericProcessingProgress(object sender, ProgressChangedEventArgs e) {
			progressBar.Value = e.ProgressPercentage;
		}

		private void OnGenericProcessingFinished(object sender, RunWorkerCompletedEventArgs e) {
			progressBar.IsIndeterminate = false;
			progressBar.Value = 0;
			mainGrid.IsEnabled = true;
		}

		public void LoadSoundBank(object sender, DoWorkEventArgs e) {
			try {
				soundBank = new SoundBank(e.Argument as string);
			} catch (Exception ex) {
				e.Result = ex.Message;
			}
			
		}

		public void OnSoundBankLoaded(object sender, RunWorkerCompletedEventArgs e) {
			if (e.Result != null) {
				MessageBox.Show($"Can't open soundbank:\n{e.Result}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			if (appSettings.recentlyOpenedFiles.Contains(soundBank.FilePath)) {
				appSettings.recentlyOpenedFiles.Remove(soundBank.FilePath);
			}
			appSettings.recentlyOpenedFiles.Insert(0, soundBank.FilePath);
			if (appSettings.recentlyOpenedFiles.Count > 10) {
				appSettings.recentlyOpenedFiles.RemoveRange(10, appSettings.recentlyOpenedFiles.Count - 10);
			}
			recentFilesList.ItemsSource = null; //too lazy for proper notify
			recentFilesList.ItemsSource = appSettings.recentlyOpenedFiles;

			Title = $"PD2 Soundbank Editor - {Path.GetFileName(soundBank.FilePath)}";

			var containsEmedded = soundBank.StreamInfos.Count > 0;
			OnFilterTextBoxChanged(filterTextBox, null);
			extractAllButton.IsEnabled = converterAvailable && containsEmedded;
			filterTextBox.IsEnabled = containsEmedded;
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
