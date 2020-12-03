using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

// see https://bobdoleowndu.github.io/mgsv/documentation/soundswapping.html

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

		private string headerString;
		private uint headerChunkLen;
		private byte[] headerData;

		private string didxString;
		private uint didxChunkLen;

		private List<StreamDescription> streamDescriptions;

		private string dataString;
		private uint dataChunkLen;

		private List<KeyValuePair<string, byte[]>> otherData = new List<KeyValuePair<string, byte[]>>();

		public SoundBank(string file) {
			filePath = file;

			using var reader = new BinaryReader(new FileStream(file, FileMode.Open));

			// BKHD header
			headerString = Encoding.UTF8.GetString(reader.ReadBytes(4));
			headerChunkLen = reader.ReadUInt32();
			Trace.WriteLine($"{headerString} {headerChunkLen}");
			// BKHD data
			headerData = reader.ReadBytes((int)headerChunkLen);

			// DIDX header
			didxString = Encoding.UTF8.GetString(reader.ReadBytes(4));
			if (didxString != "DIDX") {
				throw new FileFormatException($"Unsupported soundbank type with header \"{didxString}\".");
			}
			didxChunkLen = reader.ReadUInt32();
			Trace.WriteLine($"{didxString} {didxChunkLen}");
			// DIDX data
			streamDescriptions = new List<StreamDescription>();
			for (var i = 0; i < didxChunkLen; i += 12) {
				var desc = new StreamDescription {
					id = reader.ReadUInt32(),
					dataOffset = reader.ReadUInt32(),
					dataLength = reader.ReadUInt32()
				};
				streamDescriptions.Add(desc);
			}

			// DATA header
			dataString = Encoding.UTF8.GetString(reader.ReadBytes(4));
			dataChunkLen = reader.ReadUInt32();
			Trace.WriteLine($"{dataString} {dataChunkLen}");
			// DATA data
			for (var i = 0; i < streamDescriptions.Count; i++) {
				var desc = streamDescriptions[i];
				Trace.WriteLine($"Reading data for {desc.id}...");
				desc.data = reader.ReadBytes((int)desc.dataLength);
				var nextOffset = i < streamDescriptions.Count - 1 ? streamDescriptions[i + 1].dataOffset : dataChunkLen;
				var paddingAmount = nextOffset - desc.dataOffset - desc.dataLength;
				reader.ReadBytes((int)paddingAmount);
			}

			// All other remaining data
			while (true) {
				var otherString = Encoding.UTF8.GetString(reader.ReadBytes(4));
				if (otherString == "") {
					return;
				}
				var otherChunkLen = reader.ReadUInt32();
				Trace.WriteLine($"{otherString} {otherChunkLen}");
				otherData.Add(new KeyValuePair<string, byte[]>(otherString, reader.ReadBytes((int)otherChunkLen)));
			}
		}

		public void Save(string file) {

			using var writer = new BinaryWriter(new FileStream(file, FileMode.Create));

			// BKHD data
			writer.Write(Encoding.UTF8.GetBytes(headerString));
			writer.Write(headerChunkLen);
			writer.Write(headerData);

			// DIDX data
			writer.Write(Encoding.UTF8.GetBytes(didxString));
			writer.Write(streamDescriptions.Count * 12); // listChunkLen
			var totalDataSize = 0;
			foreach (var desc in streamDescriptions) {
				var align = 16 - (totalDataSize % 16); // pad to nearest 16
				if (align < 16) {
					totalDataSize += align;
				}
				writer.Write(desc.id);
				writer.Write(totalDataSize); // wem.dataOffset
				writer.Write(desc.data.Length); // wem.dataLength
				totalDataSize += desc.data.Length;
			}

			// DATA data
			writer.Write(Encoding.UTF8.GetBytes(dataString));
			writer.Write(totalDataSize); // dataChunkLen
			var bytesWritten = 0;
			foreach (var desc in streamDescriptions) {
				var align = 16 - (bytesWritten % 16);  // pad to nearest 16
				if (align < 16) {
					writer.Write(new byte[align]);
					bytesWritten += align;
				}
				writer.Write(desc.data);
				bytesWritten += desc.data.Length;
			}

			// All other remaining data
			foreach (var pair in otherData) {
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