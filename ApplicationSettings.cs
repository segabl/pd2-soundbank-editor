using System;
using System.Collections.Generic;
using System.Text;

namespace PD2SoundBankEditor {
	public class ApplicationSettings {
		public bool checkForUpdates = true;
		public bool suppressErrors = false;
		public DateTime lastUpdateCheck = new DateTime();
		public List<string> recentlyOpenedFiles = new List<string>();
	}
}
