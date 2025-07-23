using System;
using System.Collections.Generic;

namespace PD2SoundBankEditor {
	public class ApplicationSettings {
		public bool checkForUpdates = true;
		public bool suppressErrors = false;
		public bool hideUnreferenced = true;
		public DateTime lastUpdateCheck = new();
		public List<string> recentlyOpenedFiles = new();
		public double windowWidth = 720;
		public double windowHeight = 480;
		public double windowLeft = -1;
		public double windowTop = -1;
	}
}