using System.IO;
using System.Windows;

namespace PD2SoundBankEditor {
	public partial class App : Application {

		private void ApplicationStartup(object sender, StartupEventArgs e) {
			var wnd = new MainWindow();

			wnd.Show();

			if (e.Args.Length > 0 && File.Exists(e.Args[0])) {
				wnd.OpenSoundBank(e.Args[0]);
			}
		}
	}
}