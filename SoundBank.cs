using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

// http://wiki.xentax.com/index.php/Wwise_SoundBank_(*.bnk)

namespace PD2SoundBankEditor {
	public class StreamDescription {
		public uint id;
		public uint dataOffset;
		public uint dataLength;
		public byte[] data;

		public string errorString;
		public string replacementFile;
		public string convertedFilePath;

		public string Name { get => id.ToString(); }
		public string ReplacementFile { get => replacementFile; }

		public override string ToString() {
			return id.ToString();
		}

		public bool Save(string file) {
			Trace.WriteLine($"Saving file {file}...");

			try {
				var writer = new BinaryWriter(new FileStream(file, FileMode.Create));
				writer.Write(data);
				writer.Close();
			} catch (Exception e) {
				errorString = e.Message;
				return false;
			}

			return true;
		}
	}

	public class SoundBank {

		private string filePath;

		private List<KeyValuePair<string, byte[]>> sectionData = new List<KeyValuePair<string, byte[]>>();

		private List<StreamDescription> streamDescriptions;

		public SoundBank(string file) {
			filePath = file;

			using var reader = new BinaryReader(new FileStream(file, FileMode.Open));

			// Read all headers and their data
			while (true) {
				var sectionString = Encoding.UTF8.GetString(reader.ReadBytes(4));
				if (sectionString == "") {
					break;
				}
				var sectionLength = reader.ReadUInt32();
				Trace.WriteLine($"{sectionString} {sectionLength}");
				sectionData.Add(new KeyValuePair<string, byte[]>(sectionString, reader.ReadBytes((int)sectionLength)));
			}

			var didxIndex = sectionData.FindIndex(x => x.Key == "DIDX");
			var dataIndex = sectionData.FindIndex(x => x.Key == "DATA");

			if (didxIndex < 0 || dataIndex < 0) {
				throw new FileFormatException($"Unsupported soundbank type, missing DIDX/DATA headers.");
			}

			// Get the DIDX data
			var didxData = sectionData[didxIndex].Value;
			using var didxReader = new BinaryReader(new MemoryStream(didxData));
			streamDescriptions = new List<StreamDescription>();
			for (var i = 0; i < didxData.Length; i += 12) {
				var desc = new StreamDescription {
					id = didxReader.ReadUInt32(),
					dataOffset = didxReader.ReadUInt32(),
					dataLength = didxReader.ReadUInt32()
				};
				streamDescriptions.Add(desc);
			}

			// Get the DATA data
			var dataData = sectionData[dataIndex].Value;
			using var dataReader = new BinaryReader(new MemoryStream(dataData));
			for (var i = 0; i < streamDescriptions.Count; i++) {
				var desc = streamDescriptions[i];
				Trace.WriteLine($"Reading data for {desc.id}...");
				desc.data = dataReader.ReadBytes((int)desc.dataLength);
				var nextOffset = i < streamDescriptions.Count - 1 ? streamDescriptions[i + 1].dataOffset : (uint)dataData.Length;
				var paddingAmount = nextOffset - desc.dataOffset - desc.dataLength;
				dataReader.ReadBytes((int)paddingAmount);
			}
		}

		public void Save(string file) {

			// Write new DIDX and DATA
			using var didxWriter = new BinaryWriter(new MemoryStream());
			using var dataWriter = new BinaryWriter(new MemoryStream());
			var totalDataSize = 0;
			foreach (var desc in streamDescriptions) {
				var align = 16 - (totalDataSize % 16); // pad to nearest 16
				if (align < 16) {
					dataWriter.Write(new byte[align]);
					totalDataSize += align;
				}

				didxWriter.Write(desc.id);
				didxWriter.Write(totalDataSize);
				didxWriter.Write(desc.data.Length);

				dataWriter.Write(desc.data);

				totalDataSize += desc.data.Length;
			}
			sectionData[sectionData.FindIndex(x => x.Key == "DIDX")] = new KeyValuePair<string, byte[]>("DIDX", ((MemoryStream)didxWriter.BaseStream).ToArray());
			sectionData[sectionData.FindIndex(x => x.Key == "DATA")] = new KeyValuePair<string, byte[]>("DATA", ((MemoryStream)dataWriter.BaseStream).ToArray());

			// Write all data to file
			using var writer = new BinaryWriter(new FileStream(file, FileMode.Create));
			foreach (var pair in sectionData) {
				writer.Write(Encoding.UTF8.GetBytes(pair.Key));
				writer.Write(pair.Value.Length);
				writer.Write(pair.Value);
			}
		}

		public string GetFilePath() {
			return filePath;
		}

		public List<StreamDescription> GetWemFiles() {
			return streamDescriptions;
		}
	}
}