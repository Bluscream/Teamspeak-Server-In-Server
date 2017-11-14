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

// ReSharper disable All
namespace TS3ServerInServer {
	static class Program {
		static List<Ts3FullClient> clients;
		static int cnt = -1;
		static string[] channels;
		static string[] ids;
		static ClientUidT ownerUID;
		private static ClientDbIdT ownerDBID;
		private static ChannelGroupIdT adminCGID = 0;
		private static ChannelGroupIdT modCGID = 0;
		private static ChannelGroupIdT banCGID = 0;
		private static List<ChannelIdT> cids = new List<ChannelIdT>();
		static ConnectionDataFull con = new ConnectionDataFull();
		private static Random random = new Random();
		static private Timer AntiAFK { get; set; }
		private static string idfile = "ids.txt";
		private static string chanfile = "chans.txt";
		private static string dbfile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TS3Client", "settings.db");
		private static SQLiteConnection tssettingsdb;
		private static List<ClientUidT> done = new List<ClientUidT>();
		public static string RandomString(int length) {
			const string chars = "abcdefghiklmnopqrstuvwxyz0123456789+/=";
			return new string(Enumerable.Repeat(chars, length)
			  .Select(s => s[random.Next(s.Length)]).ToArray());
		}
		public static FriendStatus isFriend(ClientUidT uid) {
			if (!Regex.IsMatch(uid, "^[\\w\\d+/=]+$"))
				return FriendStatus.Malformed;
			var sql = $"SELECT value FROM contacts WHERE value LIKE '%IDS={uid}%'";
			SQLiteCommand cmd = new SQLiteCommand(sql, tssettingsdb);
			cmd.CommandType = CommandType.Text;
			Console.WriteLine($"{cmd.CommandText}");
			var reader = cmd.ExecuteReader(); //.ExecuteQuery();
			while (reader.Read()) {
				string itemStrg = reader["value"].ToString();
				Console.WriteLine($"contact: {itemStrg.Replace("\n", ", ")}");
				string[] _tmp = itemStrg.Split('\n');
				foreach (string item in _tmp) {
					Console.WriteLine($"item: {item}");
					if (item.StartsWith("Friend=")) {
						return (FriendStatus) Enum.Parse(typeof(FriendStatus), item[item.Length - 1].ToString());
					}
				}
				//Console.Write("\r\n");
			}
			return FriendStatus.Unknown;
		}
		public static IEnumerable<ResponseDictionary> SetClientChannelGroup(Ts3FullClient cli, ChannelGroupIdT cgid, ChannelIdT cid, ClientDbIdT cldbid) {
			Console.WriteLine($"Trying to set channelgroup {cgid} for client {cldbid} in channel {cid}");
			return cli.Send("setclientchannelgroup",
				new CommandParameter("cgid", cgid),
				new CommandParameter("cid", cid),
				new CommandParameter("cldbid", cldbid)
			);
		}
		public static void SetAllChannelGroup(Ts3FullClient cli, ChannelGroupIdT cgid, ClientDbIdT cldbid) {
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
		static void Main() {
			clients = new List<Ts3FullClient>();
			channels = File.ReadAllLines(chanfile);
			var _con = channels[0].Split(',');
			Console.WriteLine(channels[0]);
			channels = channels.Skip(1).ToArray();
			Console.CancelKeyPress += (s, e) => {
				if (e.SpecialKey == ConsoleSpecialKey.ControlC) {
					e.Cancel = true;
					for (int i = 0; i < clients.Count; i++) {
						clients[i]?.Disconnect();
						Thread.Sleep(Convert.ToInt32(_con[6]));
					}
					tssettingsdb.Close();
					tssettingsdb.Dispose();
					Environment.Exit(0);
				}
			};
			con.Address = _con[0];
			con.Username = _con[1];
			con.Password = _con[2];
			ownerUID = _con[4];
			adminCGID = uint.Parse(_con[7]); //Convert.ToUInt64(_con[7]);
			modCGID = Convert.ToUInt64(_con[8]);
			banCGID = Convert.ToUInt64(_con[9]);
			if (!File.Exists(idfile)) {
				using (File.Create(idfile)) { }
			}
			ids = File.ReadAllLines(idfile);
			tssettingsdb = new SQLiteConnection(String.Format("Data Source={0};Version=3;", dbfile));
			tssettingsdb.Open();
			for (int i = 0; i < channels.Length; i++) {
				var client = new Ts3FullClient(EventDispatchType.DoubleThread);
				client.OnConnected += OnConnected;
				client.OnDisconnected += OnDisconnected;
				client.OnErrorEvent += OnErrorEvent;
				client.OnTextMessageReceived += OnTextMessageReceived;
				client.OnClientMoved += OnClientMoved;
				var _identity = ids.Select(x => x.Split(',')).ToList();
				IdentityData ID;
				try {
					ID = Ts3Crypt.LoadIdentity(_identity[i][0], ulong.Parse(_identity[i][1]));
				} catch (Exception) {
					ID = Ts3Crypt.GenerateNewIdentity(Convert.ToInt32(_con[3]));
					File.AppendAllText(idfile, ID.PrivateKeyString + "," + ID.ValidKeyOffset + "\r\n");
				}
				Console.WriteLine("#" + i + " UID: " + ID.ClientUid);
				con.Identity = ID;
				//Array values = Enum.GetValues(typeof(VersionSign));
				//Random random = new Random();
				//con.VersionSign = (VersionSign)values.GetValue(random.Next(values.Length));
				//var t = typeof(VersionSign).GetFields();
				con.VersionSign = VersionSign.VER_WIN_3_UNKNOWN;
				//con.VersionSign = new VersionSign("YaTQA-3.9pre [Build: 32503680000]", "ServerQuery", String.Empty);
				con.HWID = RandomString(32) + "," + RandomString(32);
				Console.WriteLine("#" + i + " HWID: " + con.HWID);
				client.Connect(con);
				clients.Add(client);
				Thread.Sleep(Convert.ToInt32(_con[5]));
			}
			AntiAFK = new Timer(OnTick, "on", 5000, 5000);
			Console.WriteLine("End");
			Console.ReadLine();
		}

		private static void OnTextMessageReceived(object sender, IEnumerable<TextMessage> msgs) {
			foreach (var msg in msgs) {
				if (msg.InvokerUid != ownerUID) { continue; }
				var _msg = msg.Message.ToLower();
				try {
					if (_msg.StartsWith("!scg ")) {
						var client = (Ts3FullClient)sender;
						var param = _msg.Replace("!scg ", "");
						var _params = param.Split(' ');
						client.Send("setclientchannelgroup",
							new CommandParameter("cgid", _params[0]),
							new CommandParameter("cid", _params[1]),
							new CommandParameter("cldbid", _params[2])
						);
					}
					if (_msg.StartsWith("!badges ")) {
						var client = (Ts3FullClient)sender;
						var param = _msg.Replace("!badges ", "");
						client.Send("clientupdate",
							new CommandParameter("client_badges", param)
						);
					}
				} catch (Exception e) {
					Console.WriteLine("Catched exception: " + e.Message);
					continue;
				}
			}
		}

		private static void CheckClient(object sender, ClientIdT clid) {
			var cl = (Ts3FullClient)sender;
			ClientUidT cluid = ClientGetUidFromClid(cl, clid);
			Console.WriteLine($"clid={clid} cluid={cluid}");
			if (done.Contains(cluid)) return;
			var dbid = Convert.ToUInt64(cl.Send("clientgetdbidfromuid", new CommandParameter("cluid", cluid)).FirstOrDefault()["cldbid"]);
			var friend = isFriend(cluid);
			Console.WriteLine($"#{clid} dbid={dbid} cluid={cluid} friend={friend}");
			switch (friend) {
				case FriendStatus.Blocked:
					SetAllChannelGroup(cl, banCGID, dbid);
					break;
				case FriendStatus.Friend:
					SetAllChannelGroup(cl, modCGID, dbid);
					break;
				default:
					break;
			}
			done.Add(cluid);
		}

		private static void OnClientMoved(object sender, IEnumerable<ClientMoved> e) {
			foreach (var client in e) {
				if (client.InvokerUid == ownerUID)
					Console.WriteLine(ownerUID + "joined some channel.");
				if (client.Reason == MoveReason.UserAction) {
					//Console.WriteLine($"#{client.ClientId} {String.Join(",", cids)}.Contains({client.TargetChannelId}) = {cids.Contains(client.TargetChannelId)}");
					if (cids.Contains(client.TargetChannelId)) {
						CheckClient((Ts3FullClient)sender, client.ClientId);
					}
				}
				//Console.WriteLine($"ClientId={client.ClientId} InvokerId={client.InvokerId}  InvokerName={client.InvokerName}  InvokerUid={client.InvokerUid}  NotifyType={client.NotifyType}  Reason={client.Reason} TargetChannelId={client.TargetChannelId}");
			}
		}

		private static void OnDisconnected(object sender, DisconnectEventArgs e) {
			int myId = Interlocked.Increment(ref cnt);
			var client = (Ts3FullClient)sender;
			Console.WriteLine("Disconnected id={0} clid={1}", myId, client.ClientId);
			clients.Remove(client);
		}

		private static void OnConnected(object sender, EventArgs e) {
			int myId = Interlocked.Increment(ref cnt);
			var client = (Ts3FullClient)sender;
			Console.WriteLine("Connected id={0} clid={1}", myId, client.ClientId);
			var channel = channels[myId].Split(',');
			/*var response = client.Send("channellist");
			var channel_name_in_use = true;
			foreach (var chan in response) {
				if (chan["channel_name"] == channel[0])
					channel_name_in_use = true; break;
			}*/
			ulong cid = 0;
			/*if (channel_name_in_use) {
				ret = client.ChannelCreate(channel[0] + "_", namePhonetic: channel[3], password: channel[1], neededTP: Convert.ToInt32(channel[2]));
			} else {*/
			//ret = client.ChannelCreate(channel[0], namePhonetic: channel[3], password: channel[1], neededTP: Convert.ToInt32(channel[2]));
			try {
				cid = ChannelCreate(client, channel[0], channel[1], Convert.ToInt32(channel[2]), channel[3]).ChannelId;
			} catch (Ts3CommandException err) {
				if (err.ErrorStatus.Id == Ts3ErrorCode.channel_name_inuse)
					cid = ChannelCreate(client, channel[0] + "_", channel[1], Convert.ToInt32(channel[2]), channel[3]).ChannelId;
				Console.WriteLine("Error while creating channel " + channel[0] + " " + err.ErrorStatus + "\n" + err.Message);
			}
			cids.Add(cid);
			ownerDBID = Convert.ToUInt64(client.Send("clientgetdbidfromuid", new CommandParameter("cluid", ownerUID)).FirstOrDefault()["cldbid"]);
			try {
				SetClientChannelGroup(client, modCGID, cid, ownerDBID);
			} catch (Ts3CommandException err) {
				Console.WriteLine("Error while setting channelgroup " + channel[0] + " " + err.ErrorStatus + "\n" + err.Message);
				return;
			}
		}

		private static void OnTick(object state) {
			foreach (var client in clients) {
				client.Send("clientupdate", new CommandParameter("client_input_muted", 0));
			}
		}

		private static void OnErrorEvent(object sender, CommandError e) {
			var client = (Ts3FullClient)sender;
			Console.WriteLine(e.ErrorFormat());
			if (!client.Connected) {
				Console.WriteLine("Could not connect: " + e.Message + " (" + e.ExtraMessage);
			}
		}
	}
}
