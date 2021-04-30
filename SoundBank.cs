using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;

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
		public double Size { get => data.Length / 1024f; }
		public string ConvertedFilePath { get; set; }
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

	public class SoundBank {
		// A soundbank contains multiple data sections, see http://wiki.xentax.com/index.php/Wwise_SoundBank_(*.bnk)

		// Base Section
		public class SectionBase {
			public static SectionBase Read(SoundBank soundBank, BinaryReader reader) {
				var name = Encoding.ASCII.GetString(reader.ReadBytes(4));
				return name switch {
					"DIDX" => new SectionDIDX(soundBank, name, reader),
					"DATA" => new SectionDATA(soundBank, name, reader),
					"HIRC" => new SectionHIRC(soundBank, name, reader),
					_ => new SectionBase(soundBank, name, reader)
				};
			}

			public SoundBank SoundBank { get; protected set; }
			public string Name { get; protected set; }
			public byte[] Data { get; set; }
			public long DataOffset { get; protected set; }

			public SectionBase(SoundBank soundBank, string name, BinaryReader reader) {
				SoundBank = soundBank;
				Name = name;
				var length = reader.ReadInt32();
				DataOffset = reader.BaseStream.Position;
				Read(reader, length);
			}

			protected virtual void Read(BinaryReader reader, int amount) {
				Data = reader.ReadBytes(amount);
				if (reader.BaseStream.Position != DataOffset + amount) {
					throw new FileFormatException("Soundbank data is malformed.");
				}
			}

			public virtual void Write(BinaryWriter writer) {
				writer.Write(Encoding.ASCII.GetBytes(Name));
				writer.Write(Data.Length);
				DataOffset = writer.BaseStream.Position;
				writer.Write(Data);
			}
		}

		// DIDX Section
		public class SectionDIDX : SectionBase {
			public SectionDIDX(SoundBank soundBank, string name, BinaryReader reader) : base(soundBank, name, reader) { }

			protected override void Read(BinaryReader reader, int amount) {
				for (var i = 0; i < amount; i += 12) {
					var id = reader.ReadUInt32();
					var offset = reader.ReadInt32();
					var length = reader.ReadInt32();
					SoundBank.StreamInfos.Add(new StreamInfo(SoundBank, id, offset, length));
				}
				if (reader.BaseStream.Position != DataOffset + amount) {
					throw new FileFormatException("Soundbank data is malformed.");
				}
			}

			public override void Write(BinaryWriter writer) {
				using var dataWriter = new BinaryWriter(new MemoryStream());
				var totalDataSize = 0;
				foreach (var info in SoundBank.StreamInfos) {
					var align = 16 - (totalDataSize % 16); // pad to nearest 16
					if (align < 16) {
						totalDataSize += align;
					}
					info.Offset = totalDataSize;

					dataWriter.Write(info.Id);
					dataWriter.Write(info.Offset);
					dataWriter.Write(info.Data.Length);

					totalDataSize += info.Data.Length;
				}
				Data = (dataWriter.BaseStream as MemoryStream).ToArray();

				base.Write(writer);
			}
		}

		// DATA Section
		public class SectionDATA : SectionBase {
			public SectionDATA(SoundBank soundBank, string name, BinaryReader reader) : base(soundBank, name, reader) { }

			protected override void Read(BinaryReader reader, int amount) {
				foreach (var info in SoundBank.StreamInfos) {
					reader.BaseStream.Seek(DataOffset + info.Offset, SeekOrigin.Begin);
					var data = reader.ReadBytes(info.Data.Length);
					Array.Copy(data, info.Data, data.Length);
				}
				if (reader.BaseStream.Position != DataOffset + amount) {
					throw new FileFormatException("Soundbank data is malformed.");
				}
			}

			public override void Write(BinaryWriter writer) {
				using var dataWriter = new BinaryWriter(new MemoryStream());
				foreach (var info in SoundBank.StreamInfos) {
					var padding = info.Offset - dataWriter.BaseStream.Position;
					if (padding > 0) {
						dataWriter.Write(new byte[padding]);
					}
					dataWriter.Write(info.Data);
				}
				Data = (dataWriter.BaseStream as MemoryStream).ToArray();

				base.Write(writer);
			}
		}

		// HIRC Section
		public class SectionHIRC : SectionBase {

			// Base object
			public class ObjectBase {
				public static ObjectBase Read(SectionHIRC section, BinaryReader reader) {
					var type = reader.ReadByte();
					return type switch {
						2 => new ObjectSound(section, type, reader),
						_ => new ObjectBase(section, type, reader)
					};
				}

				public SectionHIRC Section { get; set; }
				public byte Type { get; set; }
				public byte[] Data { get; set; }

				public ObjectBase(SectionHIRC section, byte type, BinaryReader reader) {
					Section = section;
					Type = type;
					var length = reader.ReadInt32();
					Read(reader, length);
				}

				protected virtual void Read(BinaryReader reader, int amount) {
					Data = reader.ReadBytes(amount);
				}

				public virtual void Write(BinaryWriter writer) {
					writer.Write(Type);
					writer.Write(Data.Length);
					writer.Write(Data);
				}
			}

			// Sound object
			public class ObjectSound : ObjectBase {
				public StreamInfo StreamInfo { get; protected set; }
				public bool IsEmbedded { get; protected set; }
				public uint AudioId { get; protected set; }

				public ObjectSound(SectionHIRC section, byte type, BinaryReader reader) : base(section, type, reader) {
					IsEmbedded = BitConverter.ToUInt32(Data, 8) == 0;
					AudioId = BitConverter.ToUInt32(Data, 12);
					StreamInfo = IsEmbedded ? Section.SoundBank.StreamInfos.Find(x => x.Id == AudioId) : null;
				}

				public override void Write(BinaryWriter writer) {
					if (StreamInfo != null) {
						var offset = BitConverter.GetBytes(Section.SoundBank.Sections.Find(x => x.Name == "DATA").DataOffset + StreamInfo.Offset);
						var length = BitConverter.GetBytes(StreamInfo.Data.Length);
						Array.Copy(offset, 0, Data, 20, 4);
						Array.Copy(length, 0, Data, 24, 4);
					}

					base.Write(writer);
				}
			}

			//* HIRC SECTION MEMBERS *//
			public List<ObjectBase> Objects { get; protected set; } = new List<ObjectBase>();

			public SectionHIRC(SoundBank soundBank, string name, BinaryReader reader) : base(soundBank, name, reader) { }

			protected override void Read(BinaryReader reader, int amount) {
				var numObjects = reader.ReadUInt32();
				for (var i = 0; i < numObjects; i++) {
					var obj = ObjectBase.Read(this, reader);
					Objects.Add(obj);
				}
				if (reader.BaseStream.Position != DataOffset + amount) {
					throw new FileFormatException("Soundbank data is malformed.");
				}
			}

			public override void Write(BinaryWriter writer) {
				using var dataWriter = new BinaryWriter(new MemoryStream());
				dataWriter.Write(Objects.Count);
				foreach (var obj in Objects) {
					obj.Write(dataWriter);
				}
				Data = (dataWriter.BaseStream as MemoryStream).ToArray();

				base.Write(writer);
			}
		}

		//* SOUNDBANK MEMBERS *//
		public List<SectionBase> Sections { get; private set; } = new List<SectionBase>();
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
				var section = SectionBase.Read(this, reader);
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