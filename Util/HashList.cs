using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Windows;

namespace PD2SoundBankEditor
{
    public static class HashList
    {
        static private Dictionary<uint, string> MatchTable = new Dictionary<uint, string>();
        static private string HashlistFile = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "hashlist");

        static HashList()
        {
            if (!File.Exists(HashlistFile))
            {
                var result = MessageBox.Show("Soundname hashlist for event, switch and parameter names not found. Download?", "Information", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (result == MessageBoxResult.Yes)
                {
                    DownloadHashlist();
                }
                return;
            }
            ProcessHashlist();
        }

        static private void ProcessHashlist()
        {
            List<string> strings = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(HashlistFile));
            foreach (string s in strings)
            {
                MatchTable[FNVHash(s)] = s;
            }
        }

        static private uint FNVHash(string name)
        {
            var namebytes = Encoding.UTF8.GetBytes(name.ToLower());
            var hash = 2166136261; // FNV initial offset

            foreach (byte namebyte in namebytes)
            {
                hash = hash * 16777619; // FNV prime
                hash = hash ^ namebyte;
                hash = hash & 0xFFFFFFFF;
            }

            return hash;
        }

        static public string DehashId(uint id)
        {
            string stringId;
            MatchTable.TryGetValue(id, out stringId);

            if (stringId != null)
            {
                return stringId;
            } else
            {
                return "(?) " + id.ToString();
            }
        }

        static private void DownloadHashlist()
        {
            try
            {
                var client = new WebClient();
                client.Headers.Add("User-Agent:PD2SoundbankEditor");
                client.DownloadFile(new Uri("https://raw.githubusercontent.com/Javgarag/pd2-mods/refs/heads/main/resources/wwise-ids/wwise-string-list.json"), HashlistFile);
                ProcessHashlist();
            } catch (Exception ex) {
                MessageBox.Show("Couldn't download hashlist: " + ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
