using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

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

		private uint headerMagicNum;
		private uint headerChunkLen;
		private byte[] headerData;

		private uint listMagicNum;
		private uint listChunkLen;

		private List<StreamDescription> streamDescriptions;

		private uint dataMagicNum;
		private uint dataChunkLen;

		private byte[] unknownData;

		public SoundBank(string file) {
			filePath = file;

			using var reader = new BinaryReader(new FileStream(file, FileMode.Open));

			// Main header data
			headerMagicNum = reader.ReadUInt32();
			headerChunkLen = reader.ReadUInt32();
			Trace.WriteLine($"Header {headerMagicNum} {headerChunkLen}");
			headerData = reader.ReadBytes((int)headerChunkLen);

			// Stream list
			listMagicNum = reader.ReadUInt32();
			listChunkLen = reader.ReadUInt32();
			if (listChunkLen % 12 != 0) {
				throw new FileFormatException("Stream list chunk length is not a multiple of 12.");
			}
			Trace.WriteLine($"List {listMagicNum} {listChunkLen}");
			streamDescriptions = new List<StreamDescription>();
			for (var i = 0; i < listChunkLen; i += 12) {
				var desc = new StreamDescription {
					id = reader.ReadUInt32(),
					dataOffset = reader.ReadUInt32(),
					dataLength = reader.ReadUInt32()
				};
				streamDescriptions.Add(desc);
			}

			// Stream data
			dataMagicNum = reader.ReadUInt32();
			dataChunkLen = reader.ReadUInt32();
			Trace.WriteLine($"Data {dataMagicNum} {dataChunkLen}");
			for (var i = 0; i < streamDescriptions.Count; i++) {
				var desc = streamDescriptions[i];
				Trace.WriteLine($"Reading data for {desc.id}...");
				desc.data = reader.ReadBytes((int)desc.dataLength);
				var nextOffset = i < streamDescriptions.Count - 1 ? streamDescriptions[i + 1].dataOffset : dataChunkLen;
				var paddingAmount = nextOffset - desc.dataOffset - desc.dataLength;
				reader.ReadBytes((int)paddingAmount);
			}

			// Unknown
			var num = 0;
			var buffer = new byte[128];
			var data = new List<byte>();
			while ((num = reader.Read(buffer, 0, buffer.Length)) > 0) {
				if (num == buffer.Length) {
					data.AddRange(buffer);
				} else {
					for (var i = 0; i < num; i++) {
						data.Add(buffer[i]);
					}
				}
			}
			unknownData = data.ToArray();
			Trace.WriteLine($"{unknownData.Length} bytes of unknown data left");
		}

		public void Save(string file) {

			using var writer = new BinaryWriter(new FileStream(file, FileMode.Create));

			writer.Write(headerMagicNum);
			writer.Write(headerChunkLen);
			writer.Write(headerData);

			writer.Write(listMagicNum);
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

			writer.Write(dataMagicNum);
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

			writer.Write(unknownData);
		}

		public string GetFilePath() {
			return filePath;
		}

		public List<StreamDescription> GetWemFiles() {
			return streamDescriptions;
		}
	}
}