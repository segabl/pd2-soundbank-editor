using System.ComponentModel;
using System.IO;

namespace PD2SoundBankEditor {
	// Helper Class containing information about embedded streams
	public class StreamInfo : INotifyPropertyChanged {
		private byte[] data;
		private string replacementFile;

		public event PropertyChangedEventHandler PropertyChanged;

		public SoundBank SoundBank { get; protected set; }
		public uint Id { get; private set; }
		public int Offset { get; set; }

		public byte[] Data {
			get => data;
			set {
				data = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Data"));
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Size"));
			}
		}

		public bool HasReferences { get; set; }

		public double Size {
			get => data.Length / 1024f;
		}

		public string ReplacementFile {
			get => replacementFile;
			set {
				replacementFile = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ReplacementFile"));
			}
		}

		public string Note {
			get => SoundBank.StreamNotes.TryGetValue(Id, out var n) ? n : "";
			set {
				if (value == "")
					SoundBank.StreamNotes.Remove(Id);
				else
					SoundBank.StreamNotes[Id] = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Note"));
			}
		}

		public StreamInfo(SoundBank soundBank, uint id, int offset, int length) {
			SoundBank = soundBank;
			Id = id;
			Offset = offset;
			Data = new byte[length];
		}

		public void Save(string file) {
			using var writer = new BinaryWriter(new FileStream(file, FileMode.Create));
			writer.Write(data);
		}
	}
}