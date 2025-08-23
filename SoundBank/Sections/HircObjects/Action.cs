using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PD2SoundBankEditor {
	public class Action : HircObject {
		public byte ActionScope;
		public byte ActionType;
        public string ActionScopeString
        {
            get => ActionScope switch
            {
                0x01 => "Object: Switch or Trigger",
                0x02 => "Global",
                0x03 => "Object",
                0x04 => "Object: State",
                0x05 => "All",
                0x09 => "All Except Referenced",
                _ => "Unknown"
            };
        }
        public string ActionTypeString
        {
            get => ActionType switch
            {
                0x01 => "Stop",
                0x02 => "Pause",
                0x03 => "Resume",
                0x04 => "Play",
                0x05 => "Trigger",
                0x06 => "Mute",
                0x07 => "Unmute",
                0x08 => "Set Voice Pitch",
                0x09 => "Reset Voice Pitch",
                0x0A => "Set Voice Volume",
                0x0B => "Reset Voice Volume",
                0x0C => "Set Bus Volume",
                0x0D => "Reset Bus Volume",
                0x0E => "Set Voice Low-pass Filter",
                0x0F => "Reset Voice Low-pass Filter",
                0x10 => "Enable State",
                0x11 => "Disable State",
                0x12 => "Set State",
                0x13 => "Set Game Parameter",
                0x14 => "Reset Game Parameter",
                0x19 => "Set Switch",
                0x1A => "Enable/Disable Bypass",
                0x1B => "Reset Bypass Effect",
                0x1C => "Break",
                0x1E => "Seek",
                _ => "Unknown"
            };
        }

        public uint ObjectId;
		public byte ParameterNumber;
		public Dictionary<byte, byte[]> Parameters = new();

        public uint SwitchGroupId;
        public uint SwitchId;
		
		public byte[] Unhandled;

		public Action(HircSection section, byte type, BinaryReader reader) : base(section, type, reader) { }

		public override void Read(BinaryReader reader, int amount) {
			var dataOffset = (int)reader.BaseStream.Position;

			ActionScope = reader.ReadByte();

			ActionType = reader.ReadByte();
			ObjectId = reader.ReadUInt32();
			reader.ReadByte(); // Always 00

			ParameterNumber = reader.ReadByte();
			for (byte i = 0; i < ParameterNumber; i++)
			{
				Parameters[reader.ReadByte()] = reader.ReadBytes(4);
			}
            reader.ReadByte(); // Always 00

            if (ActionType == 0x12 || ActionType == 0x19)
            {
                SwitchGroupId = reader.ReadUInt32();
                SwitchId = reader.ReadUInt32();
            }

            Unhandled = reader.ReadBytes(amount + dataOffset - (int)reader.BaseStream.Position); // Leftover data
		}

		public override void Write(BinaryWriter writer) {
			using var dataWriter = new BinaryWriter(new MemoryStream());

			dataWriter.Write(ActionScope);
			dataWriter.Write(ActionType);
			dataWriter.Write(ObjectId);
			dataWriter.Write(0x00);
            dataWriter.Write(ParameterNumber);
            for (byte i = 0; i < ParameterNumber; i++)
            {
                dataWriter.Write(Parameters.FirstOrDefault(x => x.Value == Parameters[i]).Key);
                dataWriter.Write(Parameters[i]);
            }
            writer.Write(0x00);
            if (ActionType == 0x12 || ActionType == 0x19)
            {
                dataWriter.Write(SwitchGroupId);
                dataWriter.Write(SwitchId);
            }

			dataWriter.Write(Unhandled);
			Data = (dataWriter.BaseStream as MemoryStream).ToArray();

			base.Write(writer);
		}
	}
}