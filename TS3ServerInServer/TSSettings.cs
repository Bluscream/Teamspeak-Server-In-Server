using System;
using System.IO;
using TS3Client;
using System.Data.SQLite;
using System.Data;
using System.Text.RegularExpressions;
using ClientUidT = System.String;
using ClientDbIdT = System.UInt64;
using ClientIdT = System.UInt16;
using ChannelIdT = System.UInt64;
using ServerGroupIdT = System.UInt64;
using ChannelGroupIdT = System.UInt64;

namespace TS3ServerInServer
{
    class TSSettings {
		private static string dbfile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TS3Client", "settings.db");
		private static SQLiteConnection tssettingsdb;
		public static void OpenSettingsDB() {
			tssettingsdb = new SQLiteConnection(String.Format("Data Source={0};Version=3;", dbfile));
			tssettingsdb.Open();
		}
		public static void CloseSettingsDB() {
			tssettingsdb.Close();
			tssettingsdb.Dispose();
		}
		public static FriendStatus isFriend(ClientUidT uid) {
			if (!Regex.IsMatch(uid, "^[\\w\\d\\+/]+=$"))
				return FriendStatus.Malformed;
			var sql = $"SELECT value FROM contacts WHERE value LIKE '%IDS={uid}%'";
			SQLiteCommand cmd = new SQLiteCommand(sql, tssettingsdb);
			cmd.CommandType = CommandType.Text;
			//Console.WriteLine($"{cmd.CommandText}");
			var reader = cmd.ExecuteReader(); //.ExecuteQuery();
			while (reader.Read()) {
				string itemStrg = reader["value"].ToString();
				//Console.WriteLine($"contact: {itemStrg.Replace("\n", ", ")}");
				string[] _tmp = itemStrg.Split('\n');
				foreach (string item in _tmp) {
					//Console.WriteLine($"item: {item}");
					if (item.StartsWith("Friend=")) {
						return (FriendStatus)Enum.Parse(typeof(FriendStatus), item[item.Length - 1].ToString());
					}
				}
				//Console.Write("\r\n");
			}
			return FriendStatus.Unknown;
		}
	}
}
