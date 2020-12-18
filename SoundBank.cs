using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

// http://wiki.xentax.com/index.php/Wwise_SoundBank_(*.bnk)

namespace PD2SoundBankEditor {

	// Helper Class containing information about embedded streams
	public class StreamInfo {
		
		private byte[] data;

		public uint Id { get; private set; }
		public byte[] Data { 
			get => data;
			set {
				data = value;
				IsDirty = true;
			}
		}
		public double Size { get => data.Length / 1024f; }
		public string ConvertedFilePath { get; set; }
		public string ReplacementFile { get; set; }
		public string ErrorString { get; private set; }
		public bool IsDirty { get; set; }
		public int HIRCOffset { get; set; }

		public StreamInfo(uint id, byte[] data) {
			Id = id;
			this.data = data;
		}

		public void Save(string file) {
			using var writer = new BinaryWriter(new FileStream(file, FileMode.Create));
			writer.Write(data);
		}
	}

	public class SoundBank {

		// Represents a generic section of the soundbank
		public class Section {
			public string Name { get; set; }
			public byte[] Data { get; set; }

			public Section(string name, byte[] data) {
				Name = name;
				Data = data;
			}

			public void Write(BinaryWriter writer) {
				writer.Write(Encoding.ASCII.GetBytes(Name));
				writer.Write(Data.Length);
				writer.Write(Data);
			}
		}

		// Represents a generic object in the HIRC section of the soundbank
		public class HIRCObject {
			public byte Type { get; set; }
			public byte[] Data { get; set; }

			public HIRCObject(byte type, byte[] data) {
				Type = type;
				Data = data;
			}

			public virtual void Write(BinaryWriter writer) {
				writer.Write(Type);
				writer.Write(Data.Length);
				writer.Write(Data);
			}
		}

		// Represents a sound object in the HIRC section of the soundbank
		public class SoundObject : HIRCObject {
			public StreamInfo StreamInfo { get; set; }

			public SoundObject(byte type, byte[] data, List<StreamInfo> streamInfos) : base(type, data) {
				var embed = BitConverter.ToUInt32(data, 8);
				var audioId = BitConverter.ToUInt32(data, 12);
				StreamInfo = embed == 0 ? streamInfos.Find(x => x.Id == audioId) : null;
			}

			public override void Write(BinaryWriter writer) {
				if (StreamInfo != null) {
					var offset = BitConverter.GetBytes(StreamInfo.HIRCOffset);
					var length = BitConverter.GetBytes(StreamInfo.Data.Length);
					Array.Copy(offset, 0, Data, 20, 4);
					Array.Copy(length, 0, Data, 24, 4);
				}
				base.Write(writer);
			}
		}

		private List<Section> sections = new List<Section>();

		public bool IsDirty { get => StreamInfos.Any(info => info.IsDirty); }
		public string FilePath { get; private set; }
		public List<StreamInfo> StreamInfos { get; private set; } = new List<StreamInfo>();
		public List<HIRCObject> HIRCObjects { get; private set; } = new List<HIRCObject>();

		public SoundBank(string file) {
			FilePath = file;

			// Read all headers and their data
			using var reader = new BinaryReader(new FileStream(file, FileMode.Open));
			while (true) {
				var sectionString = Encoding.ASCII.GetString(reader.ReadBytes(4));
				if (sectionString == "") {
					break;
				}
				var sectionLength = reader.ReadUInt32();
				Trace.WriteLine($"{sectionString}: {sectionLength} bytes");
				sections.Add(new Section(sectionString, reader.ReadBytes((int)sectionLength)));
			}
		}

		public void ProcessData(object sender = null) {
			var didxSection = sections.Find(x => x.Name == "DIDX");
			var dataSection = sections.Find(x => x.Name == "DATA");
			var hircSection = sections.Find(x => x.Name == "HIRC");
			if (didxSection == null || dataSection == null || hircSection == null) {
				return;
			}

			// Get the embedded stream data
			using var didxReader = new BinaryReader(new MemoryStream(didxSection.Data));
			for (var i = 0; i < didxSection.Data.Length; i += 12) {
				var id = didxReader.ReadUInt32();
				var offset = didxReader.ReadInt32();
				var length = didxReader.ReadInt32();
				StreamInfos.Add(new StreamInfo(id, dataSection.Data.Skip(offset).Take(length).ToArray()));
				if (sender != null) {
					(sender as BackgroundWorker).ReportProgress((int)((float)i / didxSection.Data.Length * 100));
				}
			}

			// Get the HIRC data
			using var hircReader = new BinaryReader(new MemoryStream(hircSection.Data));
			var numObjects = hircReader.ReadUInt32();
			for (var i = 0; i < numObjects; i++) {
				var type = hircReader.ReadByte();
				var len = hircReader.ReadInt32();
				if (type == 2) {
					HIRCObjects.Add(new SoundObject(type, hircReader.ReadBytes(len), StreamInfos));
				} else {
					HIRCObjects.Add(new HIRCObject(type, hircReader.ReadBytes(len)));
				}
				if (sender != null) {
					(sender as BackgroundWorker).ReportProgress((int)((float)i / numObjects * 100));
				}
			}
		}

		public void Save(string file) {

			// Get sound data offset for HIRC section
			var hircOffset = 8;
			foreach (var section in sections) {
				if (section.Name == "DATA") {
					break;
				}
				hircOffset += 8 + section.Data.Length;
			}

			// Write new DIDX and DATA
			using var didxWriter = new BinaryWriter(new MemoryStream());
			using var dataWriter = new BinaryWriter(new MemoryStream());
			var totalDataSize = 0;
			foreach (var info in StreamInfos) {
				var align = 16 - (totalDataSize % 16); // pad to nearest 16
				if (align < 16) {
					dataWriter.Write(new byte[align]);
					totalDataSize += align;
				}

				didxWriter.Write(info.Id);
				didxWriter.Write(totalDataSize);
				didxWriter.Write(info.Data.Length);

				dataWriter.Write(info.Data);

				info.IsDirty = false;
				info.HIRCOffset = hircOffset + totalDataSize;

				totalDataSize += info.Data.Length;
			}
			sections.Find(x => x.Name == "DIDX").Data = ((MemoryStream)didxWriter.BaseStream).ToArray();
			sections.Find(x => x.Name == "DATA").Data = ((MemoryStream)dataWriter.BaseStream).ToArray();

			// Write new HIRC
			using var hircWriter = new BinaryWriter(new MemoryStream());
			hircWriter.Write(HIRCObjects.Count);
			foreach (var obj in HIRCObjects) {
				obj.Write(hircWriter);
			}
			sections.Find(x => x.Name == "HIRC").Data = ((MemoryStream)hircWriter.BaseStream).ToArray();

			// Write all data to file
			using var writer = new BinaryWriter(new FileStream(file, FileMode.Create));
			foreach (var section in sections) {
				section.Write(writer);
			}

			FilePath = file;
		}
	}
}