using AdonisUI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace PD2SoundBankEditor {
	public partial class ParamsWindow : AdonisWindow {
		private SoundBank soundBank;
		private IEnumerable<Sound> soundObjects;
		private bool usingSlider = false;
		private bool enteringText = false;

		public ParamsWindow(SoundBank soundBank) {
			InitializeComponent();

			this.soundBank = soundBank;
			soundObjects = soundBank.GetSection<HircSection>().GetObjects<Sound>();

			soundIdListBox.ItemsSource = soundObjects;
			soundIdListBox.SelectAll();
		}

		private void OnSoundIdSelectionChanged(object sender, RoutedEventArgs e) {
			numItemsSelectedLabel.Content = $"{soundIdListBox.SelectedItems.Count} of {soundIdListBox.Items.Count} selected";
			applyButton.IsEnabled = soundIdListBox.SelectedItem != null;
		}

		private void OnClipboardMatchClick(object sender, RoutedEventArgs e) {
			if (!Clipboard.ContainsText()) {
				return;
			}

			var re = new Regex("([0-9]{4,})", RegexOptions.Compiled);
			var idMatches = re.Matches(Clipboard.GetText()).Cast<Match>().Select(m => uint.Parse(m.Value)).ToHashSet();

			soundIdListBox.UnselectAll();

			foreach (Sound sound in soundIdListBox.Items) {
				if (idMatches.Contains(sound.SourceId)) {
					soundIdListBox.SelectedItems.Add(sound);
				}
			}

			if (soundIdListBox.SelectedItem != null) {
				soundIdListBox.ScrollIntoView(soundIdListBox.SelectedItem);
			}
		}

		private void OnAudioLevelSliderChange(object sender, RoutedEventArgs e) {
			if (enteringText) {
				return;
			}

			usingSlider = true;
			audioLevelTextBox.Text = audioLevelSlider.Value.ToString("F1", System.Globalization.NumberFormatInfo.InvariantInfo);
			usingSlider = false;
		}

		private void OnAudioLevelTextChange(object sender, RoutedEventArgs e) {
			if (usingSlider) {
				return;
			}

			var numberStyles = System.Globalization.NumberStyles.Any & ~System.Globalization.NumberStyles.AllowCurrencySymbol;
			if (!float.TryParse(audioLevelTextBox.Text, numberStyles, System.Globalization.NumberFormatInfo.InvariantInfo, out var num)) {
				num = 0;
			}

			enteringText = true;
			audioLevelSlider.Value = Math.Clamp(num, audioLevelSlider.Minimum, audioLevelSlider.Maximum);
			enteringText = false;
		}

		private void OnApplyClick(object sender, RoutedEventArgs e) {
			foreach (Sound sound in soundIdListBox.SelectedItems) {
				sound.NodeBaseParams.Properties1[0] = (float)audioLevelSlider.Value;
			}

			soundBank.IsDirty = true;
			(Owner as MainWindow).UpdateWindowTitle();

			Close();
		}

		private void OnCancelClick(object sender, RoutedEventArgs e) {
			Close();
		}

		private void CheckAudioLevelText(object sender, TextCompositionEventArgs e) {
			e.Handled = !Regex.IsMatch(e.Text, "[0-9.+-]");
		}
	}
}