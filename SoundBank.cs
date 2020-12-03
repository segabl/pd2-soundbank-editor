using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace PD2SoundBankEditor {
	public class WemFile {
		public uint id;
		public uint dataOffset;
		public uint dataLength;
		public byte[] data;
		public byte[] unknownData;

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

		private List<WemFile> wemFiles;

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
			Trace.WriteLine($"List {listMagicNum} {listChunkLen}");
			wemFiles = new List<WemFile>();
			for (var i = 0; i < listChunkLen; i += 12) {
				var wem = new WemFile {
					id = reader.ReadUInt32(),
					dataOffset = reader.ReadUInt32(),
					dataLength = reader.ReadUInt32()
				};
				wemFiles.Add(wem);
			}

			// Stream data
			dataMagicNum = reader.ReadUInt32();
			dataChunkLen = reader.ReadUInt32();
			Trace.WriteLine($"Data {dataMagicNum} {dataChunkLen}");
			for (var i = 0; i < wemFiles.Count; i++) {
				var wem = wemFiles[i];
				Trace.WriteLine($"Reading data for {wem.id}...");
				wem.data = reader.ReadBytes((int)wem.dataLength);
				var nextOffset = i < wemFiles.Count - 1 ? wemFiles[i + 1].dataOffset : dataChunkLen;
				var unknownDataAmount = nextOffset - wem.dataOffset - wem.dataLength;
				wem.unknownData = reader.ReadBytes((int)unknownDataAmount);
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
			writer.Write(wemFiles.Count * 12); // listChunkLen
			var totalDataSize = 0;
			foreach (var wem in wemFiles) {
				writer.Write(wem.id);
				writer.Write(totalDataSize); // wem.dataOffset
				writer.Write(wem.data.Length); // wem.dataLength
				totalDataSize += wem.data.Length + wem.unknownData.Length;
			}

			writer.Write(dataMagicNum);
			writer.Write(totalDataSize); // dataChunkLen
			foreach (var wem in wemFiles) {
				writer.Write(wem.data);
				writer.Write(wem.unknownData);
			}

			writer.Write(unknownData);
		}

		public string GetFilePath() {
			return filePath;
		}

		public List<WemFile> GetWemFiles() {
			return wemFiles;
		}
	}
}