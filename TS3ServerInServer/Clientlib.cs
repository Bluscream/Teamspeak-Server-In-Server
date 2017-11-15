using System;
using System.Collections.Generic;
using System.IO;
using TS3Client;
using TS3Client.Commands;
using TS3Client.Full;
using TS3Client.Messages;
using System.Linq;
using System.Threading;
using System.Data.SQLite;
using System.Data;
using ClientUidT = System.String;
using ClientDbIdT = System.UInt64;
using ClientIdT = System.UInt16;
using ChannelIdT = System.UInt64;
using ServerGroupIdT = System.UInt64;
using ChannelGroupIdT = System.UInt64;
using System.Text.RegularExpressions;

namespace TS3ServerInServer {
	public enum FriendStatus {
		Friend = 0,
		Blocked = 1,
		Neutral = 2,
		Unknown = 3,
		Malformed = 4
	}
	static class Clientlib {

		public static string RandomString(int length) {
			const string chars = "abcdefghiklmnopqrstuvwxyz0123456789";
			return new string(Enumerable.Repeat(chars, length)
			  .Select(s => s[new Random().Next(s.Length)]).ToArray());
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

		public static ChannelCreated ChannelCreate(Ts3FullClient cli, string name, string password, int neededTP, string phoname) {
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
	}
}
