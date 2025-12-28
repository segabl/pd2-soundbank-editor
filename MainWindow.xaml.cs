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
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using MessageBox = AdonisUI.Controls.MessageBox;
using MessageBoxButton = AdonisUI.Controls.MessageBoxButton;
using MessageBoxImage = AdonisUI.Controls.MessageBoxImage;
using MessageBoxResult = AdonisUI.Controls.MessageBoxResult;

namespace PD2SoundBankEditor {
	public partial class MainWindow : AdonisWindow {
		static readonly string CONVERTER_NAME = "wwise_ima_adpcm.exe";
		static readonly string CONVERTER_PATH = Path.Join(AppDomain.CurrentDomain.BaseDirectory, CONVERTER_NAME);
		static readonly string SETTINGS_PATH = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
		static readonly string TEMPORARY_PATH = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "tmp");
		static readonly string LOG_PATH = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "log.txt");

		private ApplicationSettings appSettings = new();
		private MediaPlayer mediaPlayer = new();
		private SoundBank soundBank;
		private Button playingButton;
		private bool converterAvailable;
		private CollectionViewSource embeddedSoundsViewSource = new();
		private CollectionViewSource soundbankObjectsViewSource = new();
		private Timer autosaveNotesTimer;
		private Regex viewFilterRegex = null;

		public bool UpdateCheckEnabled {
			get => appSettings.checkForUpdates;
			set => appSettings.checkForUpdates = value;
		}

		public bool SuppressErrorsEnabled {
			get => appSettings.suppressErrors;
			set => appSettings.suppressErrors = value;
		}

		public bool HideUnreferencedEnabled {
			get => appSettings.hideUnreferenced;
			set => appSettings.hideUnreferenced = value;
		}

		public MainWindow() {
			if (File.Exists(SETTINGS_PATH)) {
				try {
					appSettings = JsonConvert.DeserializeObject<ApplicationSettings>(File.ReadAllText(SETTINGS_PATH));
				} catch (Exception ex) {
					Trace.WriteLine(ex.Message);
					File.AppendAllText(LOG_PATH, ex.Message);
				}
			}

			InitializeComponent();

			converterAvailable = File.Exists(CONVERTER_PATH);
			if (!converterAvailable) {
				MessageBox.Show($"The sound converter could not be found, you will not be able to play, convert or replace stream files! Please place {CONVERTER_NAME} in the directory of this application!", "Information", MessageBoxButton.OK, MessageBoxImage.Warning);
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
					File.AppendAllText(LOG_PATH, ex.Message);
				}
			}

			recentFilesList.ItemsSource = appSettings.recentlyOpenedFiles;
			recentFilesList.IsEnabled = appSettings.recentlyOpenedFiles.Count > 0;

			autosaveNotesTimer = new Timer(60000) {
				AutoReset = true,
				Enabled = true
			};
			autosaveNotesTimer.Elapsed += (object source, ElapsedEventArgs e) => { soundBank?.SaveNotes(); };

			mediaPlayer.MediaEnded += SetPlayButtonState;

			Width = appSettings.windowWidth;
			Height = appSettings.windowHeight;
			var screenW = SystemParameters.PrimaryScreenWidth;
			var screenH = SystemParameters.PrimaryScreenHeight;
			Left = appSettings.windowLeft >= 0 ? Math.Clamp(appSettings.windowLeft, -Width + 50, screenW - 50) : screenW / 2 - Width / 2;
			Top = appSettings.windowTop >= 0 ? Math.Clamp(appSettings.windowTop, -Height + 50, screenH - 50) : screenH / 2 - Height / 2;
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
				File.AppendAllText(LOG_PATH, ex.Message);
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
			e.CanExecute = soundBank != null && soundBank.IsDirty;
		}

		private void CommandSaveExecuted(object sender, System.Windows.Input.ExecutedRoutedEventArgs e) {
			soundBank.Save(soundBank.FilePath);
			UpdateWindowTitle();
		}

		private void CommandSaveAsCanExecute(object sender, System.Windows.Input.CanExecuteRoutedEventArgs e) {
			e.CanExecute = soundBank != null;
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
			UpdateWindowTitle();
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
			DoGenericProcessing(true, ExtractStreams, OnExtractStreamsFinished, ((Button)sender) == extractAllButton ? soundBank.StreamInfos : soundDataGrid.SelectedItems.Cast<StreamInfo>());
		}

		private void OnReplaceButtonClick(object sender, RoutedEventArgs e) {
			var diag = new OpenFileDialog {
				Filter = "Wave audio files (*.wav)|*.wav"
			};
			if (diag.ShowDialog() != true) {
				return;
			}

			if (!Directory.Exists(TEMPORARY_PATH)) {
				Directory.CreateDirectory(TEMPORARY_PATH);
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
			foreach (var info in soundDataGrid.SelectedItems.Cast<StreamInfo>()) {
				info.Data = data;
				info.ReplacementFile = fileNameNoExt + ".wav";
				var tmpFile = Path.Combine(TEMPORARY_PATH, info.Id + ".wav");
				if (File.Exists(tmpFile)) {
					File.Delete(tmpFile);
				}
			}

			soundBank.IsDirty = true;
			UpdateWindowTitle();
		}

		private void OnReplaceByNamesButtonClick(object sender, RoutedEventArgs e) {
			var diag = new OpenFileDialog {
				Filter = "Wave audio files (*.wav)|*.wav",
				Multiselect = true
			};
			if (diag.ShowDialog() != true) {
				return;
			}

			if (!Directory.Exists(TEMPORARY_PATH)) {
				Directory.CreateDirectory(TEMPORARY_PATH);
			}

			var notfound = new List<string>();
			var mappings = new Dictionary<string, StreamInfo>();
			foreach (var file in diag.FileNames) {
				var fileNameNoExt = Path.GetFileNameWithoutExtension(file);
				var fileName = Path.Combine(TEMPORARY_PATH, fileNameNoExt + ".stream");
				if (!uint.TryParse(fileNameNoExt, out var targetStreamId)) {
					notfound.Add(fileNameNoExt + ".wav");
					continue;
				}

				var targetStreamInfo = soundBank.StreamInfos.FirstOrDefault(info => info.Id == targetStreamId);
				if (targetStreamInfo == null) {
					notfound.Add(fileNameNoExt + ".wav");
					continue;
				}

				mappings[file] = targetStreamInfo;
			}

			if (notfound.Count > 0) {
				MessageBox.Show($"{notfound.Count} files could not be matched to any ID in this soundbank:\n{string.Join(", ", notfound)}", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
			}

			if (mappings.Count > 0) {
				DoGenericProcessing(true, ReplaceStreams, OnReplaceStreamsFinished, mappings);
			}
		}

		private void OnEditParametersButtonClick(object sender, RoutedEventArgs e) { }

		private void OnFilterChanged(object sender, RoutedEventArgs e) {
			var view = embeddedSoundsViewSource.View;
			if (view == null || filterTextBox == null) {
				return;
			}

			viewFilterRegex = null;
			if (filterTextBox.Text.Length > 0) {
				try {
					viewFilterRegex = new Regex(filterTextBox.Text, RegexOptions.Compiled);
				} catch (Exception) { }
			}

			view.Refresh();
		}

		private void OnTypeFilterChanged(object sender, RoutedEventArgs e) {
			var view = soundbankObjectsViewSource.View;
			if (view == null || typeFilterComboBox == null) {
				return;
			}

			view.Refresh();
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

			if (!Directory.Exists(TEMPORARY_PATH)) {
				Directory.CreateDirectory(TEMPORARY_PATH);
			}

			var fileName = Path.Combine(TEMPORARY_PATH, $"{info.Id}.stream");
			var convertedFileName = Path.ChangeExtension(fileName, "wav");
			var debugStr = "";
			if (!File.Exists(convertedFileName)) {
				try {
					debugStr = "Failed at saving stream";
					info.Save(fileName);
					debugStr = "Failed at converting stream";
					StartConverterProcess($"-d \"{fileName}\" \"{convertedFileName}\"");
					File.Delete(fileName);
				} catch (Exception ex) {
					if (!SuppressErrorsEnabled) {
						MessageBox.Show($"{debugStr}:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
					}

					return;
				}
			}

			if (File.Exists(convertedFileName)) {
				mediaPlayer.Open(new Uri(convertedFileName));
				mediaPlayer.Play();
				SetPlayButtonState(button, null);
			}
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

		private void OnConvertLooseFilesClick(object sender, RoutedEventArgs e) {
			var diag = new OpenFileDialog {
				Filter = "Stream audio files (*.stream)|*.stream|Wave audio files (*.wav)|*.wav",
				Multiselect = true,
			};
			if (diag.ShowDialog() != true) {
				return;
			}

			DoGenericProcessing(true, ConvertLooseFiles, OnConvertLooseFilesFinished, diag.FileNames);
		}

		private void OnSetAudioPropertiesClick(object sender, RoutedEventArgs e) {
			var paramsWindow = new ParamsWindow(soundBank) { Owner = this };
			paramsWindow.ShowDialog();
		}

		private void OnIncreaseSoundLimitClick(object sender, RoutedEventArgs e) {
			var changed = false;
			foreach (ActorMixer actorMixer in soundBank.GetSection<HircSection>().GetObjects<ActorMixer>()) {
				if (actorMixer.NodeBaseParams.MaxNumInstance > 0 && actorMixer.NodeBaseParams.MaxNumInstance < 20) {
					actorMixer.NodeBaseParams.MaxNumInstance = 20;
					changed = true;
				}
			}

			if (changed) {
				MessageBox.Show($"Sound limits increased.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
				soundBank.IsDirty = true;
				UpdateWindowTitle();
			} else {
				MessageBox.Show($"No sound limits needed to be changed.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
			}
		}

		private void OnSoundDataGridSelectionChanged(object sender, SelectionChangedEventArgs e) {
			replaceSelectedButton.IsEnabled = converterAvailable && soundDataGrid.SelectedItems.Count > 0;
			extractSelectedButton.IsEnabled = converterAvailable && soundDataGrid.SelectedItems.Count > 0;
			numSoundsSelectedLabel.Content = $"{soundDataGrid.SelectedItems.Count} of {soundDataGrid.Items.Count} selected";
		}

		private static object CreateProperty(string name, List<string> values) {
			return new { Name = name, Value = values.Count > 1 ? "<multiple>" : values[0] };
		}

		private void OnObjectDataGridSelectionChanged(object sender, SelectionChangedEventArgs e) {
			selectedObjectDataGrid.ItemsSource = null;

			numObjectsSelectedLabel.Content = $"{objectDataGrid.SelectedItems.Count} of {objectDataGrid.Items.Count} selected";

			if (objectDataGrid.SelectedItems.Count == 0) {
				return;
			}

			var selectedItems = objectDataGrid.SelectedItems.Cast<HircObject>().ToList();
			var ids = selectedItems.Select(x => x.Id.ToString()).Distinct().ToList();
			var types = selectedItems.Select(x => x.TypeName).Distinct().ToList();
			var properties = new List<object>() {
				CreateProperty("ID", ids),
				CreateProperty("Type", types)
			};

			if (types.Count == 1) {
				switch (selectedItems[0]) {
					case Sound: {
						var castItems = selectedItems.Cast<Sound>();
						properties.Add(CreateProperty("Sound Type", castItems.Select(x => x.StreamTypeName).Distinct().ToList()));
						properties.Add(CreateProperty("Sound ID", castItems.Select(x => x.SourceId.ToString()).Distinct().ToList()));
						break;
					}
					case Action: {
						var castItems = selectedItems.Cast<Action>();
						properties.Add(CreateProperty("Action Scope", castItems.Select(x => x.ActionScopeName).Distinct().ToList()));
						properties.Add(CreateProperty("Action Type", castItems.Select(x => x.ActionTypeName).Distinct().ToList()));
						properties.Add(CreateProperty("Ref. Object ID", castItems.Select(x => x.ObjectId.ToString()).Distinct().ToList()));
						for (byte i = 0; i < castItems.Select(x => x.Parameters.Count).First(); i++) {
							foreach (var key in castItems.Select(x => x.Parameters).First()) {
								properties.Add(CreateProperty(key.Key switch {
									0x0E => "Delay (ms)",
									0x0F => "Fade-in Time (ms)",
									0x10 => "Probability",
									_ => $"Unknown (0x{key.Key:x2})"
								}, key.Key switch {
									0x0E => new List<string> { BitConverter.ToUInt32(key.Value).ToString() },
									0x0F => new List<string> { BitConverter.ToUInt32(key.Value).ToString() },
									0x10 => new List<string> { BitConverter.ToSingle(key.Value).ToString() },
									_ => new List<string> { $"Unknown (0x{key.Key:x2})" }
								}));
							}
						}

						if (castItems.Select(x => x.ActionType).Distinct().First() == 0x12 || castItems.Select(x => x.ActionType).Distinct().First() == 0x19) {
							properties.Add(CreateProperty("Switch Group ID", castItems.Select(x => x.SwitchGroupId.ToString()).Distinct().ToList()));
							properties.Add(CreateProperty("Switch ID", castItems.Select(x => x.SwitchId.ToString()).Distinct().ToList()));
						}

						break;
					}
					case Event: {
						var castItems = selectedItems.Cast<Event>();
						if (castItems.Select(x => x.ActionNumber).First() > 0) {
							foreach (var actionId in castItems.SelectMany(x => x.ActionIDs).Distinct()) {
								properties.Add(CreateProperty("Action ID", new List<string> { actionId.ToString() }));
							}
						}

						break;
					}
				}
			}

			if (selectedItems.All(x => x.NodeBaseParams != null)) {
				var volumes = selectedItems.Select(x => { return x.NodeBaseParams.Properties1.TryGetValue(0, out var val) ? val.ToString() : ""; }).Distinct().ToList();
				var maxInstances = selectedItems.Select(x => x.NodeBaseParams.MaxNumInstance.ToString()).Distinct().ToList();
				properties.Add(CreateProperty("Volume", volumes));
				properties.Add(CreateProperty("Max Instances", maxInstances));
			}

			selectedObjectDataGrid.ItemsSource = properties;
		}

		private void OnWindowClosed(object sender, EventArgs e) {
			soundBank?.SaveNotes();
			if (Directory.Exists(TEMPORARY_PATH)) {
				Directory.Delete(TEMPORARY_PATH, true);
			}

			appSettings.windowWidth = Width;
			appSettings.windowHeight = Height;
			appSettings.windowLeft = Left;
			appSettings.windowTop = Top;
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
			BackgroundWorker worker = new() {
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

		private void OnFileDragOver(object sender, DragEventArgs e) {
			e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
			e.Handled = true;
		}

		private void OnFileDragDrop(object sender, DragEventArgs e) {
			if (!e.Data.GetDataPresent(DataFormats.FileDrop)) {
				return;
			}

			e.Handled = true;

			var files = (string[])e.Data.GetData(DataFormats.FileDrop);
			if (files.Length > 0) {
				soundBank?.SaveNotes();

				DoGenericProcessing(false, LoadSoundBank, OnSoundBankLoaded, files[0]);
			}
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

			embeddedSoundsViewSource.Source = soundBank.StreamInfos;
			embeddedSoundsViewSource.View.Filter = info => {
				if (HideUnreferencedEnabled && !(info as StreamInfo).HasReferences) {
					return false;
				} else if (viewFilterRegex == null) {
					return (info as StreamInfo).Note.Contains(filterTextBox.Text) || (info as StreamInfo).Id.ToString().Contains(filterTextBox.Text);
				} else {
					return viewFilterRegex.Match((info as StreamInfo).Note).Success || viewFilterRegex.Match((info as StreamInfo).Id.ToString()).Success;
				}
			};

			soundDataGrid.ItemsSource = embeddedSoundsViewSource.View;
			soundDataGrid.DataContext = embeddedSoundsViewSource.View;

			soundbankObjectsViewSource.Source = soundBank.GetSection<HircSection>()?.Objects ?? new List<HircObject>();
			soundbankObjectsViewSource.View.Filter = info => {
				var selectedValue = typeFilterComboBox.SelectedValue?.ToString() ?? "";
				return selectedValue == "" || (info as HircObject)?.TypeName == selectedValue;
			};

			objectDataGrid.ItemsSource = soundbankObjectsViewSource.View;

			var objectTypes = soundBank.GetSection<HircSection>()?.Objects.Select(x => x.TypeName).Distinct().ToList();
			objectTypes.Sort();
			objectTypes.Insert(0, "");
			typeFilterComboBox.ItemsSource = objectTypes;

			if (appSettings.recentlyOpenedFiles.Contains(soundBank.FilePath)) {
				appSettings.recentlyOpenedFiles.Remove(soundBank.FilePath);
			}

			appSettings.recentlyOpenedFiles.Insert(0, soundBank.FilePath);
			if (appSettings.recentlyOpenedFiles.Count > 10) {
				appSettings.recentlyOpenedFiles.RemoveRange(10, appSettings.recentlyOpenedFiles.Count - 10);
			}

			recentFilesList.ItemsSource = null; //too lazy for proper notify
			recentFilesList.ItemsSource = appSettings.recentlyOpenedFiles;

			UpdateWindowTitle();

			extractAllButton.IsEnabled = converterAvailable && soundBank.StreamInfos.Count > 0;
			replaceByNamesButton.IsEnabled = converterAvailable && soundBank.StreamInfos.Count > 0;
			setAudioPropertiesMenuItem.IsEnabled = soundBank.GetSection<HircSection>()?.GetObjects<Sound>().Any() ?? false;
			increaseSoundLimitMenuItem.IsEnabled = soundBank.GetSection<HircSection>()?.GetObjects<ActorMixer>().Any() ?? false;
		}

		public void UpdateWindowTitle() {
			Title = $"PD2 Soundbank Editor - {Path.GetFileName(soundBank.FilePath)}{(soundBank.IsDirty ? "*" : "")}";
		}

		private void ExtractStreams(object sender, DoWorkEventArgs e) {
			var streamDescriptions = (IEnumerable<StreamInfo>)e.Argument;
			var soundBankName = soundBank.FilePath;
			var savePath = Path.Join(Path.GetDirectoryName(soundBankName), Path.GetFileNameWithoutExtension(soundBankName));
			if (!Directory.Exists(savePath)) {
				Directory.CreateDirectory(savePath);
			}

			var n = 0;
			var errors = new List<string>();
			foreach (var info in streamDescriptions) {
				var file = Path.Join(savePath, $"{info.Id}.stream");
				var convertedFileName = Path.ChangeExtension(file, "wav");
				try {
					info.Save(file);
					StartConverterProcess($"-d \"{file}\" \"{convertedFileName}\"");
				} catch (Exception ex) {
					errors.Add(ex.Message);
				}

				(sender as BackgroundWorker).ReportProgress((int)(++n / (float)streamDescriptions.Count() * 100));
			}

			e.Result = errors;
		}

		void OnExtractStreamsFinished(object sender, RunWorkerCompletedEventArgs e) {
			var errors = (List<string>)e.Result;
			if (errors.Count > 0) {
				MessageBox.Show($"Extraction finished with {errors.Count} error(s)!", "Information", MessageBoxButton.OK, MessageBoxImage.Warning);
				File.AppendAllLines(LOG_PATH, errors);
			} else {
				MessageBox.Show("Extraction complete!", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
			}
		}

		private void ReplaceStreams(object sender, DoWorkEventArgs e) {
			var fileMappings = (Dictionary<string, StreamInfo>)e.Argument;
			var n = 0;
			var errors = new List<string>();
			if (!Directory.Exists(TEMPORARY_PATH)) {
				Directory.CreateDirectory(TEMPORARY_PATH);
			}

			foreach (var mapping in fileMappings) {
				var file = mapping.Key;
				var targetStreamInfo = mapping.Value;
				var fileNameNoExt = Path.GetFileNameWithoutExtension(file);
				var fileName = Path.Combine(TEMPORARY_PATH, fileNameNoExt + ".stream");
				try {
					StartConverterProcess($"-e \"{file}\" \"{fileName}\"");
				} catch (Exception ex) {
					errors.Add(ex.Message);
				}

				targetStreamInfo.Data = File.ReadAllBytes(fileName);
				targetStreamInfo.ReplacementFile = fileNameNoExt + ".wav";
				var tmpFile = Path.Combine(TEMPORARY_PATH, targetStreamInfo.Id + ".wav");
				if (File.Exists(tmpFile)) {
					File.Delete(tmpFile);
				}

				(sender as BackgroundWorker).ReportProgress((int)(++n / (float)fileMappings.Count * 100));
				soundBank.IsDirty = true;
			}

			e.Result = errors;
		}

		void OnReplaceStreamsFinished(object sender, RunWorkerCompletedEventArgs e) {
			var errors = (List<string>)e.Result;
			if (errors.Count > 0) {
				MessageBox.Show($"Sound replacement finished with {errors.Count} error(s)!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
				File.AppendAllLines(LOG_PATH, errors);
			} else {
				MessageBox.Show($"Sound replacement finished successfully!", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
			}

			UpdateWindowTitle();
		}

		private void ConvertLooseFiles(object sender, DoWorkEventArgs e) {
			var files = (string[])e.Argument;
			var n = 0;
			var errors = new List<string>();
			foreach (var file in files) {
				var fileExt = Path.GetExtension(file);
				var fileNameNoExt = Path.GetFileNameWithoutExtension(file);
				var fileDir = Path.GetDirectoryName(file);
				var fileName = Path.Combine(fileDir, fileNameNoExt + (fileExt == ".wav" ? ".stream" : ".wav"));
				try {
					StartConverterProcess($"-{(fileExt == ".wav" ? "e" : "d")} \"{file}\" \"{fileName}\"");
				} catch (Exception ex) {
					errors.Add(ex.Message);
				}

				(sender as BackgroundWorker).ReportProgress((int)(++n / (float)files.Length * 100));
			}

			e.Result = errors;
		}

		void OnConvertLooseFilesFinished(object sender, RunWorkerCompletedEventArgs e) {
			var errors = (List<string>)e.Result;
			if (errors.Count > 0) {
				MessageBox.Show($"Conversion finished with {errors.Count} error(s)!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
				File.AppendAllLines(LOG_PATH, errors);
			} else {
				MessageBox.Show($"Conversion finished successfully!", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
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
			try {
				var nums1 = v1.Split(".").Select(int.Parse).ToArray();
				var nums2 = v2.Split(".").Select(int.Parse).ToArray();
				for (var i = 0; i < nums1.Length && i < nums2.Length; i++) {
					if (nums1[i] != nums2[i]) {
						return Math.Sign(nums1[i] - nums2[i]);
					}
				}

				return Math.Sign(nums1.Length - nums2.Length);
			} catch (Exception ex) {
				File.AppendAllText(LOG_PATH, ex.Message);
				return 0;
			}
		}
	}
}