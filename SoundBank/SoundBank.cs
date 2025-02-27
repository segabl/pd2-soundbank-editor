using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace PD2SoundBankEditor {
	public class SoundBank {
		public uint GeneratorVersion { get; set; }
		public uint Id { get; set; }
		public List<BankSection> Sections { get; private set; } = new List<BankSection>();
		public bool IsDirty { get; set; }
		public string FilePath { get; private set; }
		public List<StreamInfo> StreamInfos { get; private set; } = new List<StreamInfo>();
		public Dictionary<uint, string> StreamNotes { get; private set; } = new Dictionary<uint, string>();

		public SoundBank(string file) {
			FilePath = file;

			LoadNotes();

			// Read all sections
			using var reader = new BinaryReader(new FileStream(FilePath, FileMode.Open));
			while (reader.BaseStream.Position < reader.BaseStream.Length) {
				var section = BankSection.Read(this, reader);
				Sections.Add(section);
			}
		}

		public void Save(string file) {
			// Write all sections to file
			using var writer = new BinaryWriter(new FileStream(file, FileMode.Create));
			foreach (var section in Sections) {
				section.Write(writer);
			}

			FilePath = file;
			IsDirty = false;
		}

		public void LoadNotes() {
			var notesDir = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "notes");
			if (!Directory.Exists(notesDir))
				return;
			var notesFile = Path.Join(notesDir, Path.GetFileName(FilePath) + ".json");
			if (!File.Exists(notesFile))
				return;
			StreamNotes = JsonConvert.DeserializeObject<Dictionary<uint, string>>(File.ReadAllText(notesFile));
		}

		public void SaveNotes() {
			if (StreamNotes.Count == 0)
				return;
			var notesDir = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "notes");
			if (!Directory.Exists(notesDir))
				Directory.CreateDirectory(notesDir);
			var notesFile = Path.Join(notesDir, Path.GetFileName(FilePath) + ".json");
			File.WriteAllText(notesFile, JsonConvert.SerializeObject(StreamNotes));
		}
	}
}