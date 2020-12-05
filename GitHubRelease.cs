using System;
using System.Collections.Generic;
using System.Text;

namespace PD2SoundBankEditor {
	public class GitHubRelease {
		public class Asset {
			public string name;
			public string browser_download_url;
		}

		public string name;
		public string tag_name;
		public bool draft;
		public bool prerelease;
		public List<Asset> assets;
	}
}
