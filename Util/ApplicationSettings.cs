using System;
using System.Collections.Generic;

namespace PD2SoundBankEditor {
	public class ApplicationSettings {
		public bool checkForUpdates = true;
		public bool suppressErrors = false;
		public bool hideUnreferenced = true;
		public DateTime lastUpdateCheck = new();
		public List<string> recentlyOpenedFiles = new();
	}
}