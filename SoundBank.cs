using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

// http://wiki.xentax.com/index.php/Wwise_SoundBank_(*.bnk)

namespace PD2SoundBankEditor {
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
		private List<KeyValuePair<string, byte[]>> sectionData = new List<KeyValuePair<string, byte[]>>();

		public bool IsDirty { get => StreamInfos.Any(info => info.IsDirty); }
		public string FilePath { get; private set; }
		public List<StreamInfo> StreamInfos { get; private set; } = new List<StreamInfo>();

		public SoundBank(string file) {
			FilePath = file;

			// Read all headers and their data
			using var reader = new BinaryReader(new FileStream(file, FileMode.Open));
			while (true) {
				var sectionString = Encoding.UTF8.GetString(reader.ReadBytes(4));
				if (sectionString == "") {
					break;
				}
				var sectionLength = reader.ReadUInt32();
				Trace.WriteLine($"{sectionString}: {sectionLength} bytes");
				sectionData.Add(new KeyValuePair<string, byte[]>(sectionString, reader.ReadBytes((int)sectionLength)));
			}
		}

		public void ProcessStreamData(object sender = null) {
			var didxIndex = sectionData.FindIndex(x => x.Key == "DIDX");
			var dataIndex = sectionData.FindIndex(x => x.Key == "DATA");
			if (didxIndex < 0 || dataIndex < 0) {
				return;
			}

			// Get the embedded stream data
			var didxData = sectionData[didxIndex].Value;
			var dataData = sectionData[dataIndex].Value;
			using var didxReader = new BinaryReader(new MemoryStream(didxData));
			for (var i = 0; i < didxData.Length; i += 12) {
				StreamInfos.Add(new StreamInfo(didxReader.ReadUInt32(), dataData.Skip((int)didxReader.ReadUInt32()).Take((int)didxReader.ReadUInt32()).ToArray()));
				if (sender != null) {
					(sender as BackgroundWorker).ReportProgress((int)((float)i / didxData.Length * 100));
				}
			}
		}

		public void Save(string file) {

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

				totalDataSize += info.Data.Length;
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

			FilePath = file;
		}
	}
}