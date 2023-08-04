using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace PD2SoundBankEditor {
	public class SoundBank {
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
					var offset = reader.ReadUInt32();
					var length = reader.ReadUInt32();
					SoundBank.StreamInfos.Add(new StreamInfo(SoundBank, id, (int)offset, (int)length));
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
				public uint ObjectId { get; protected set; }
				public ushort PluginType { get; protected set; }
				public ushort PluginCompany { get; protected set; }
				public uint StreamType { get; protected set; }
				public uint SourceId { get; protected set; }
				public uint FileId { get; protected set; }
				public uint FileOffset { get; protected set; }
				public uint FileSize { get; protected set; }
				public byte SourceBits { get; protected set; }
				public uint UnknownSize { get; protected set; }
				public byte OverrideParentEffects {  get; protected set; }
				public byte EffectBitMask { get; protected set; }
				public List<Tuple<byte, uint>> Effects { get; protected set; } = new();
				public uint OutputBus { get; protected set; }
				public uint ParentObject { get; protected set; }
				public byte OverrideParentPriority { get; protected set; }
				public byte PriorityDistanceFactorEnabled { get; protected set; }
				public List<Tuple<byte, float>> Parameters { get; protected set; } = new();
				public byte[] Unhandled { get; protected set; }

				public ObjectSound(SectionHIRC section, byte type, BinaryReader reader) : base(section, type, reader) { }

				protected override void Read(BinaryReader reader, int amount) {
					var dataOffset = (int)reader.BaseStream.Position;

					ObjectId = reader.ReadUInt32();
					PluginType = reader.ReadUInt16();
					PluginCompany = reader.ReadUInt16();
					StreamType = reader.ReadUInt32(); // 0 = embedded, 1 = streamed, 2 = prefetch
					SourceId = reader.ReadUInt32();
					FileId = reader.ReadUInt32();

					if (StreamType == 0) {
						FileOffset = reader.ReadUInt32();
						FileSize = reader.ReadUInt32();
						StreamInfo = Section.SoundBank.StreamInfos.Find(x => x.Id == SourceId);
						if (StreamInfo != null) {
							StreamInfo.HasReferences = true;
						}
					}
					SourceBits = reader.ReadByte(); // 0 = sfx, 1 = voice

					Trace.WriteLine($"================ {SourceId} ================");
					var audioType = StreamType switch { 0 => "embedded", 1 => "streamed", 2 => "prefetched", _ => "unknown" };
					var soundType = SourceBits switch { 0 => "sfx", 1 => "voice", _ => "unknown" };
					Trace.WriteLine($"plugin type: {PluginType}, audio type: {audioType}, sound type: {soundType}");

					if (PluginType != 1) {
						UnknownSize = reader.ReadUInt32(); // Unknown size field
					}

					OverrideParentEffects = reader.ReadByte();
					var numEffects = reader.ReadByte();
					if (numEffects > 0) {
						Trace.WriteLine($"effects:");

						EffectBitMask = reader.ReadByte();
						for (var i = 0; i < numEffects; i++) {
							var effectIndex = reader.ReadByte();
							var effectId = reader.ReadUInt32();

							Trace.WriteLine($"\tindex: {effectIndex}, object: {effectId}");

							Effects.Add(new Tuple<byte, uint>(effectIndex, effectId));
							reader.ReadBytes(2); // 2 zero bytes
						}
					}
					OutputBus = reader.ReadUInt32();
					ParentObject = reader.ReadUInt32();

					//Trace.WriteLine($"bus: {OutputBus}, parent: {ParentObject}");

					OverrideParentPriority = reader.ReadByte();
					PriorityDistanceFactorEnabled = reader.ReadByte();

					//Trace.WriteLine($"override parent priority: {OverrideParentPriority}, priority offset enabled: {PriorityOffsetEnabled}");

					var numParams = reader.ReadByte();
					var bytesLeft = amount + dataOffset - (int)reader.BaseStream.Position;

					if (bytesLeft < numParams * 5) {
						throw new FileFormatException($"Soundbank data is malformed (expected {bytesLeft} bytes left, attempted to read at least {numParams * 5})");
					}

					if (numParams > 0) {
						Trace.WriteLine($"params:");

						for (var i = 0; i < numParams; i++) {
							var paramType = reader.ReadByte();
							var paramValue = reader.ReadSingle();

							Trace.WriteLine($"\ttype: {paramType}, value: {paramValue}");

							Parameters.Add(new Tuple<byte, float>(paramType, paramValue));
						}
					}
					
					Unhandled = reader.ReadBytes(amount + dataOffset - (int)reader.BaseStream.Position); // Leftover data
				}
				
				public override void Write(BinaryWriter writer) {
					if (StreamInfo != null) {
						FileOffset = (uint)(Section.SoundBank.Sections.Find(x => x.Name == "DATA").DataOffset + StreamInfo.Offset);
						FileSize = (uint)StreamInfo.Data.Length;
					}

					/* TEST */
					//Parameters.Add(new Tuple<byte, float>(2, 100));

					using var dataWriter = new BinaryWriter(new MemoryStream());
					dataWriter.Write(ObjectId);
					dataWriter.Write(PluginType);
					dataWriter.Write(PluginCompany);
					dataWriter.Write(StreamType);
					dataWriter.Write(SourceId);
					dataWriter.Write(FileId);
					if (StreamType == 0) {
						dataWriter.Write(FileOffset);
						dataWriter.Write(FileSize);
					}
					dataWriter.Write(SourceBits);
					if (PluginType != 1) {
						dataWriter.Write(UnknownSize);
					}
					dataWriter.Write(OverrideParentEffects);
					dataWriter.Write((byte)Effects.Count);
					if (Effects.Count > 0) {
						dataWriter.Write(EffectBitMask);
						foreach (var effect in Effects) {
							dataWriter.Write(effect.Item1);
							dataWriter.Write(effect.Item2);
							dataWriter.Write((ushort)0);
						}
					}
					dataWriter.Write(OutputBus);
					dataWriter.Write(ParentObject);
					dataWriter.Write(OverrideParentPriority);
					dataWriter.Write(PriorityDistanceFactorEnabled);
					dataWriter.Write((byte)Parameters.Count);
					foreach (var param in Parameters) {
						dataWriter.Write(param.Item1);
						dataWriter.Write(param.Item2);
					}
					dataWriter.Write(Unhandled);
					Data = (dataWriter.BaseStream as MemoryStream).ToArray();

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