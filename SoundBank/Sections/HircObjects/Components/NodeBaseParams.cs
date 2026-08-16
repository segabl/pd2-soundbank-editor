using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;

namespace PD2SoundBankEditor {
    public class RangedProperty
    {
        float min;
        float max;
        public RangedProperty(BinaryReader reader)
        {
            min = reader.ReadSingle();
            max = reader.ReadSingle();
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(min);
            writer.Write(max);
        }
    }
    public class NodeBaseParams {
		public byte OverrideParentEffects;
		public byte EffectBitMask;
		public List<(byte, uint)> Effects = new();

		public byte OverrideParentMetadata;
		public byte MetadataBitMask;
		public List<(byte, uint)> MetadataParams = new();

		public byte OverrideAttachmentParams;
		public uint OutputBus;
		public uint ParentObject;
        public byte ParameterNodeByBitVector;

        public SortedDictionary<byte, float> Properties1 = new();
		public SortedDictionary<byte, RangedProperty> Properties2 = new();

		public byte BitsPositioning;
		public byte Bits3DPositioning;
		public byte PositioningPathMode;
		public uint PositioningTransitionTime;
		public List<AutomationPathVertex> AutomationPathVertices = new();
        public List<AutomationPlaylistItem> AutomationPlaylistItems = new();
        public List<AutomationParam3D> AutomationParams3D = new();

        public byte AuxParamsBitVector;
		public byte[] AuxIDs;
        public uint ReflectionsAuxBus;

        public byte AdvSettingsBitVector1;
		public byte VirtualQueueBehaviour;
		public ushort MaxNumInstance;
		public byte BelowThresholdBehavior;
        public byte AdvSettingsBitVector2;

        public List<StateProperty> StateProperties = new();
        public List<StateChunk> StateChunks = new();

        public List<RTPC> RTPCs = new();

		public NodeBaseParams(BinaryReader reader) {
			OverrideParentEffects = reader.ReadByte();
			var numEffects = reader.ReadByte();
			if (numEffects > 0) {
				EffectBitMask = reader.ReadByte();
				for (var i = 0; i < numEffects; i++) {
					var index = reader.ReadByte();
					var id = reader.ReadUInt32();
					Effects.Add((index, id));
					reader.ReadBytes(2); // is_share_set and is_rendered, in that order
				}
			}

            OverrideParentMetadata = reader.ReadByte();
            var numMetadata = reader.ReadByte();
            if (numMetadata > 0)
            {
                MetadataBitMask = reader.ReadByte();
                for (var i = 0; i < numEffects; i++)
                {
                    var index = reader.ReadByte();
                    var id = reader.ReadUInt32();
                    MetadataParams.Add((index, id));
                    reader.ReadBytes(1); // is_share_set
                }
            }

            OverrideAttachmentParams = reader.ReadByte();
            OutputBus = reader.ReadUInt32();
			ParentObject = reader.ReadUInt32();
            ParameterNodeByBitVector = reader.ReadByte();

			// ParameterNodeInitialParams
			// PropertyBundle
            var numProperties1 = reader.ReadByte();
			if (numProperties1 > 0) {
				var propertyTypes = new byte[numProperties1];
				for (var i = 0; i < numProperties1; i++) {
					propertyTypes[i] = reader.ReadByte();
				}

				for (var i = 0; i < numProperties1; i++) {
					var type = propertyTypes[i];
					var value = reader.ReadSingle();
					Properties1[type] = value;
				}
			}

			// RangedModifierPropertyBundle
			var numProperties2 = reader.ReadByte();
			if (numProperties2 > 0) {
				var propertyTypes = new byte[numProperties2];
				for (var i = 0; i < numProperties2; i++) {
					propertyTypes[i] = reader.ReadByte();
				}

				for (var i = 0; i < numProperties2; i++) {
					var type = propertyTypes[i];
					var value = new RangedProperty(reader);
					Properties2[type] = value;
				}
			}

            // PositioningParams
            BitsPositioning = reader.ReadByte();
			if ((BitsPositioning & 1) == 1) { // has listener-relative routing (3D)
				Bits3DPositioning = reader.ReadByte();
				if ((BitsPositioning & (1 << 6)) != 0) // position_3d_type not Emitter
				{
					PositioningPathMode = reader.ReadByte();
					PositioningTransitionTime = reader.ReadUInt32();

					// automation
					var num_vertices = reader.ReadUInt32();
					for (var i = 0; i < num_vertices; i++)
					{
						AutomationPathVertices.Add(new AutomationPathVertex(reader));
					}

					var num_playlist_items = reader.ReadUInt32();
                    for (var i = 0; i < num_playlist_items; i++)
                    {
                        AutomationPlaylistItems.Add(new AutomationPlaylistItem(reader));
                    }
                    for (var i = 0; i < num_playlist_items; i++)
                    {
                        AutomationParams3D.Add(new AutomationParam3D(reader));
                    }
                }
			}

			// AuxParams
			AuxParamsBitVector = reader.ReadByte();
            if ((AuxParamsBitVector & (1 << 3)) == 1) // has_aux
            {
				AuxIDs = reader.ReadBytes(4 * 4);
            }
			ReflectionsAuxBus = reader.ReadUInt32();

            // AdvSettingsParams
            AdvSettingsBitVector1 = reader.ReadByte();
			VirtualQueueBehaviour = reader.ReadByte();
			MaxNumInstance = reader.ReadUInt16();
			BelowThresholdBehavior = reader.ReadByte();
			AdvSettingsBitVector2 = reader.ReadByte();

			// ParameterNodeStateChunk
            var numStateProps = reader.ReadByte();
			for (var i = 0; i < numStateProps; i++) {
				StateProperties.Add(new StateProperty(reader));
			}
            var numStateChunks = reader.ReadByte();
            for (var i = 0; i < numStateChunks; i++)
            {
                StateChunks.Add(new StateChunk(reader));
            }

			// Initial RTPC
            var numRTPC = reader.ReadUInt16();
			for (var i = 0; i < numRTPC; i++) {
				RTPCs.Add(new RTPC(reader));
			}
		}

		public void Write(BinaryWriter writer) {
			writer.Write(OverrideParentEffects);
			writer.Write((byte)Effects.Count);
			if (Effects.Count > 0) {
				writer.Write(EffectBitMask);
				foreach (var (index, id) in Effects) {
					writer.Write(index);
					writer.Write(id);
					writer.Write((ushort)0);
				}
			}

            writer.Write(OverrideParentMetadata);
            writer.Write((byte)MetadataParams.Count);
            if (MetadataParams.Count > 0)
            {
                writer.Write(MetadataBitMask);
                foreach (var (index, id) in MetadataParams)
                {
                    writer.Write(index);
                    writer.Write(id);
                    writer.Write((byte)0);
                }
            }

			writer.Write(OverrideAttachmentParams);
            writer.Write(OutputBus);
			writer.Write(ParentObject);
			writer.Write(ParameterNodeByBitVector);

			// ParameterNodeInitialParams
			writer.Write((byte)Properties1.Count);
			foreach (var (type, _) in Properties1) {
				writer.Write(type);
			}
			foreach (var (_, value) in Properties1) {
				writer.Write(value);
			}

			writer.Write((byte)Properties2.Count);
			foreach (var (type, _) in Properties2) {
				writer.Write(type);
			}
			foreach (var (_, ranged_property) in Properties2) {
				ranged_property.Write(writer);
			}

			// PositioningParams
			writer.Write(BitsPositioning);
			if (BitsPositioning > 0)
			{
				writer.Write(Bits3DPositioning);
				if ((BitsPositioning & (1 << 6)) != 0)
				{
					writer.Write(PositioningPathMode);
					writer.Write(PositioningTransitionTime);
					writer.Write((uint)AutomationPathVertices.Count);
					foreach (var path_vertex in AutomationPathVertices)
					{
						path_vertex.Write(writer);
					}
                    writer.Write((uint)AutomationPlaylistItems.Count);
                    foreach (var playlist_item in AutomationPlaylistItems)
					{
						playlist_item.Write(writer);
					}
                    foreach (var automation_param in AutomationParams3D)
					{
						automation_param.Write(writer);
					}
				}
			}

			writer.Write(AuxParamsBitVector);
            if ((AuxParamsBitVector & (1 << 3)) == 1) // has_aux
            {
                writer.Write(AuxIDs);
            }
            writer.Write(ReflectionsAuxBus);

            writer.Write(AdvSettingsBitVector1);
			writer.Write(VirtualQueueBehaviour);
			writer.Write(MaxNumInstance);
			writer.Write(BelowThresholdBehavior);
            writer.Write(AdvSettingsBitVector2);

            writer.Write((byte)StateProperties.Count);
            foreach (var property in StateProperties)
            {
                property.Write(writer);
            }

            writer.Write((byte)StateChunks.Count);
			foreach (var chunk in StateChunks) {
				chunk.Write(writer);
			}

			writer.Write((ushort)RTPCs.Count);
			foreach (var rtpc in RTPCs) {
				rtpc.Write(writer);
			}
		}

		public Dictionary<string, string> DisplayProperties() {
			var properties = new Dictionary<string, string>() {
				{ "Max Instances", MaxNumInstance.ToString() }
			};

			var propList = new List<string>();
			foreach (var prop in Properties1) {
				var propName = prop.Key switch {
					0x00 => "Volume",
					0x01 => "LFE",
					0x02 => "Pitch",
					0x03 => "LPF",
					0x04 => "HPF",
					0x05 => "Bus Volume",
					0x06 => "Makeup Gain",
					0x07 => "Priority",
					_ => $"Unknown (0x{prop.Key:x2})"
				};
				propList.Add($"{propName}: {prop.Value}");
			}

			// add rangedproperties

			properties.Add("Properties", string.Join("\n", propList));

			return properties;
		}
	}
}