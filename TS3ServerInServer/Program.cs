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

// ReSharper disable All
namespace TS3ServerInServer {
	static class Program {
		static List<Ts3FullClient> clients;
		static int cnt = -1;
		static string[] channels;
		static string[] ids;
		static string ownerUID;
		private static int ownerDBID;
		private static int adminCGID = 0;
		private static int modCGID = 0;
		private static int banCGID = 0;
		private static List<ulong> cids;
		static ConnectionDataFull con = new ConnectionDataFull();
		private static Random random = new Random();
		static private Timer AntiAFK { get; set; }
		private static string idfile = "ids.txt";
		private static string chanfile = "chans.txt";
		private static string dbfile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TS3Client", "settings.db");
		private static SQLiteConnection tssettingsdb;
		private static List<string> done;
		public static string RandomString(int length) {
			const string chars = "abcdefghiklmnopqrstuvwxyz0123456789";
			return new string(Enumerable.Repeat(chars, length)
			  .Select(s => s[random.Next(s.Length)]).ToArray());
		}
		public static FriendStatus isFriend(string uid) {
			var sql = "SELECT * FROM contacts WHERE value LIKE '@param1'";
			SQLiteCommand cmd = new SQLiteCommand(sql, tssettingsdb);
			cmd.CommandType = CommandType.Text;
			cmd.Parameters.Add(new SQLiteParameter("@param1", uid));
			var reader = cmd.ExecuteReader(); //.ExecuteQuery();
			while (reader.Read()) {
				Console.WriteLine("new contact:" + reader["value"].ToString().Replace('\n', ', '));
				var _tmp = reader["value"].ToString().Split('\n');
				foreach (var item in _tmp) {
					if (item.StartsWith("Friend"))
						return (FriendStatus)Convert.ToInt32(item[-1]);
				}
			}
			return FriendStatus.Unknown;
		}
		public static IEnumerable<ResponseDictionary> SetClientChannelGroup(Ts3FullClient cli, int cgid, ulong cid, int cldbid) {
			return cli.Send("setclientchannelgroup",
				new CommandParameter("cgid", cgid),
				new CommandParameter("cid", cid),
				new CommandParameter("cldbid", ownerDBID)
			);
		}
		public static void SetAllChannelGroup(Ts3FullClient cli, int cgid, int cldbid) {
			foreach (var cid in cids) {
				SetClientChannelGroup(cli, cgid, cid, cldbid);
			}
		}
		static void Main() {
			clients = new List<Ts3FullClient>();
			Console.CancelKeyPress += (s, e) => {
				if (e.SpecialKey == ConsoleSpecialKey.ControlC) {
					e.Cancel = true;
					for (int i = 0; i < clients.Count; i++) {
						clients[i]?.Disconnect();
						Thread.Sleep(1000);
					}
					Environment.Exit(0);
				}
			};
			channels = File.ReadAllLines(chanfile);
			var _con = channels[0].Split(',');
			Console.WriteLine(channels[0]);
			channels = channels.Skip(1).ToArray();
			con.Address = _con[0];
			con.Username = _con[1];
			con.Password = _con[2];
			ownerUID = _con[4];
			adminCGID = Convert.ToInt32(_con[6]);
			modCGID = Convert.ToInt32(_con[7]);
			banCGID = Convert.ToInt32(_con[8]);
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

		private static void OnClientMoved(object sender, IEnumerable<ClientMoved> e) {
            foreach (var client in e) {
				if (client.InvokerUid == ownerUID)
					Console.WriteLine(ownerUID + "joined some channel.");
				if (client.Reason == MoveReason.UserOrChannelMoved && client.InvokerId == client.ClientId) {
					var cl = (Ts3FullClient)sender;
					var cluid = (cl.Send("clientgetuidfromclid", new CommandParameter("clid", client.ClientId)).FirstOrDefault()["cluid"]);
					if (done.Contains(cluid)) return;
					var dbid = Convert.ToInt32(cl.Send("clientgetdbidfromuid", new CommandParameter("cluid", cluid)).FirstOrDefault()["cldbid"]);
					switch (isFriend(cluid)) {
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
				var cmd = new Ts3Command("channelcreate", new List<ICommandPart>() {
					new CommandParameter("channel_name", channel[0]),
					new CommandParameter("channel_password", Ts3Crypt.HashPassword(channel[1])),
					new CommandParameter("channel_needed_talk_power", channel[2]),
					new CommandParameter("channel_name_phonetic", channel[3])
				});
				var createdchan = client.SendSpecialCommand(cmd, NotificationType.ChannelCreated).Notifications.Cast<ChannelCreated>();
				foreach (var chan in createdchan) {
					Console.WriteLine("#{0} CID: {1} CNAME: {2}", myId, chan.ChannelId, chan.Name);
					if (chan.Name == channel[0])
						cid = chan.ChannelId;
						cids.Add(cid);
				}
			} catch (Ts3CommandException err){
				Console.WriteLine("Error while creating channel " + channel[0] + " " + err.ErrorStatus + "\n" + err.Message);
				return;
			}
			ownerDBID = Convert.ToInt32(client.Send("clientgetdbidfromuid", new CommandParameter("cluid", ownerUID)).FirstOrDefault()["cldbid"]);
			try {
				client.Send("setclientchannelgroup",
					new CommandParameter("cgid", modCGID), //TODO Dynamic
					new CommandParameter("cid", cid),
					new CommandParameter("cldbid", ownerDBID)
				);
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
            if (!client.Connected)
            {
				Console.WriteLine("Could not connect: " + e.Message + " (" + e.ExtraMessage);
            }
        }
    }
}
