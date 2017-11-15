using System;
using System.Collections.Generic;
using TS3Client;
using TS3Client.Commands;
using TS3Client.Full;
using TS3Client.Messages;
using System.Linq;
using System.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TS3Client.Full;
using TS3Client.Messages;
using TS3Client.Commands;
using ClientUidT = System.String;
using ClientDbIdT = System.UInt64;
using ClientIdT = System.UInt16;
using ChannelIdT = System.UInt64;
using ServerGroupIdT = System.UInt64;
using ChannelGroupIdT = System.UInt64;

namespace TS3ServerInServer {
	public sealed class ChannelGroupListResponse : IResponse {
		public string ReturnCode { get; set; }
		public ChannelGroupIdT cgID { get; set; }
		public string cgName { get; set; }
		public PermissionGroupDatabaseType type { get; set; }
		public void SetField(string name, string value) {
			switch (name) {
				case "cgid": cgID = CommandDeserializer.DeserializeUInt64(value); break;
				case "name": cgName = CommandDeserializer.DeserializeString(value); break;
				case "type": type = CommandDeserializer.DeserializeEnum<PermissionGroupDatabaseType>(value); break;
			}
		}
	}
	class Clientlib {
		static Random random = new Random();
		public static string RandomString(int length) {
			const string chars = "abcdefghiklmnopqrstuvwxyz0123456789";
			return new string(Enumerable.Repeat(chars, length)
			  .Select(s => s[random.Next(s.Length)]).ToArray());
		}

		public static IEnumerable<ChannelGroupListResponse> GetAllChannelGroups(Ts3FullClient client) {
			 return client.Send<ChannelGroupListResponse>("channelgrouplist");
		}

		public static IEnumerable<ResponseDictionary> SetClientChannelGroup(Ts3FullClient cli, ChannelGroupIdT cgid, ChannelIdT cid, ClientDbIdT cldbid) {
			Console.WriteLine($"Trying to set channelgroup {cgid} for client {cldbid} in channel {cid}");
			try {
				return cli.Send("setclientchannelgroup",
					new CommandParameter("cgid", cgid),
					new CommandParameter("cid", cid),
					new CommandParameter("cldbid", cldbid)
				);
			} catch {
				return null;
			}
		}

		public static void SetAllChannelGroup(Ts3FullClient cli, ChannelGroupIdT cgid, ClientDbIdT cldbid, List<ChannelIdT> cids) {
			foreach (var cid in cids) {
				SetClientChannelGroup(cli, cgid, cid, cldbid);
			}
		}

		public static ChannelCreated ChannelCreate(Ts3FullClient cli, string name, string password, string neededTP, string phoname) {
			var cmd = new Ts3Command("channelcreate", new List<ICommandPart>() {
					new CommandParameter("channel_name", name),
					new CommandParameter("channel_password", Ts3Crypt.HashPassword(password)),
					new CommandParameter("channel_needed_talk_power", neededTP),
					new CommandParameter("channel_name_phonetic", phoname)
				});
			var createdchan = cli.SendSpecialCommand(cmd, NotificationType.ChannelCreated).Notifications.Cast<ChannelCreated>();
			foreach (var chan in createdchan) {
				Console.WriteLine("#{0} CID: {1} CNAME: {2}", cli.ClientId, chan.ChannelId, chan.Name);
				if (chan.Name == name)
					return chan;
			}
			return null;
		}

		public static ClientUidT ClientGetUidFromClid(Ts3FullClient cli, ClientIdT clid) {
			return cli.Send("clientgetuidfromclid", new CommandParameter("clid", clid)).FirstOrDefault()["cluid"].Replace("\\", "");
		}

		public static ChannelIdT GetMyChannelID(Ts3FullClient sender) {
			WhoAmI data = sender.WhoAmI();
			return data.ChannelId;
		}
	}
}
