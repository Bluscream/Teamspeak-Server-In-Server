using System;
using System.Collections.Generic;
using System.IO;
using TS3Client;
using TS3Client.Commands;
using TS3Client.Full;
using TS3Client.Messages;
using System.Linq;
using System.Threading;

// ReSharper disable All
namespace TS3ServerInServer {
	static class Program {
		static List<Ts3FullClient> clients;
		static int cnt = -1;
		static string[] channels;
		static string[] ids;
		static string owner;
		static ConnectionDataFull con = new ConnectionDataFull();
		private static Random random = new Random();
		private static string idfile = "ids.txt";
		private static string chanfile = "chans.txt";
		public static string RandomString(int length) {
			const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghiklmnopqrstuvwxyz0123456789";
			return new string(Enumerable.Repeat(chars, length)
			  .Select(s => s[random.Next(s.Length)]).ToArray());
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
			owner = _con[4];
			//con.VersionSign = VersionSign.VER_WIN_3_1_7_ALPHA;
			if (!File.Exists(idfile)) {
				using (File.Create(idfile)) { }
			}
			ids = File.ReadAllLines(idfile);
			for (int i = 0; i < channels.Length; i++) {
				var client = new Ts3FullClient(EventDispatchType.DoubleThread);
				client.OnConnected += OnConnected;
				client.OnDisconnected += OnDisconnected;
				client.OnErrorEvent += OnErrorEvent;
				client.OnTextMessageReceived += OnTextMessageReceived;
				//client.OnClientMoved += Client_OnClientMoved;
				var _identity = ids.Select(x => x.Split(',')).ToList();
				IdentityData ID;
				try {
					ID = Ts3Crypt.LoadIdentity(_identity[i][0], ulong.Parse(_identity[i][1]));
				} catch (Exception) {
					ID = Ts3Crypt.GenerateNewIdentity(Convert.ToInt32(_con[3]));
					File.AppendAllText(idfile, ID.PrivateKeyString + "," + ID.ValidKeyOffset + "\r\n");
				}
				Console.WriteLine("#" + i + " UID: " +ID.ClientUid);
				con.Identity = ID;
				//Array values = Enum.GetValues(typeof(VersionSign));
				//Random random = new Random();
				//con.VersionSign = (VersionSign)values.GetValue(random.Next(values.Length));
				//var t = typeof(VersionSign).GetFields();
				con.VersionSign = VersionSign.VER_WIN_3_UNKNOWN;
				con.HWID = RandomString(40);
				Console.WriteLine("#" + i + " HWID: " + con.HWID);
				client.Connect(con);
				clients.Add(client);;
				Thread.Sleep(2500);
			}
			Console.WriteLine("End");
			Console.ReadLine();
		}

		private static void OnTextMessageReceived(object sender, IEnumerable<TextMessage> msgs) {
			foreach (var msg in msgs) {
				if (msg.InvokerUid != owner) { continue; }
				var _msg = msg.Message.ToLower();
				try {
					if (_msg.StartsWith("!scg ")) {
						var client = (Ts3FullClient)sender;
						var param = _msg.Replace("!scg ", "");
						var _params = param.Split(',');
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
				} catch (Ts3CommandException e) {
					Console.WriteLine("Catched exception: " + e.Message);
					continue;
				}
			}
		}

		private static void OnClientMoved(object sender, IEnumerable<ClientMoved> e) {
            foreach (var client in e) {
				if (client.InvokerUid == owner)
					Console.WriteLine(owner + "joined some channel.");
            }
        }

		private static void OnDisconnected(object sender, DisconnectEventArgs e) {
			int myId = System.Threading.Interlocked.Increment(ref cnt);
			var client = (Ts3FullClient)sender;
			Console.WriteLine("Disconnected id={0} clid={1}", myId, client.ClientId);
		}

		private static void OnConnected(object sender, EventArgs e) {
			int myId = System.Threading.Interlocked.Increment(ref cnt);
			var client = (Ts3FullClient)sender;
			Console.WriteLine("Connected id={0} clid={1}", myId, client.ClientId);
			//var data = client.ClientInfo(client.ClientId);
			var channel = channels[myId].Split(',');
			/*var response = client.Send("channellist");
			var channel_name_in_use = true;
			foreach (var chan in response) {
				if (chan["channel_name"] == channel[0])
					channel_name_in_use = true; break;
			}*/
			IEnumerable<ResponseDictionary> ret;
			/*if (channel_name_in_use) {
				ret = client.ChannelCreate(channel[0] + "_", namePhonetic: channel[3], password: channel[1], neededTP: Convert.ToInt32(channel[2]));
			} else {*/
			//ret = client.ChannelCreate(channel[0], namePhonetic: channel[3], password: channel[1], neededTP: Convert.ToInt32(channel[2]));
			try {
				ret = client.Send("channelcreate",
					new CommandParameter("channel_name", channel[0]),
					new CommandParameter("channel_password", Ts3Crypt.HashPassword(channel[1])),
					new CommandParameter("channel_needed_talk_power", channel[2]),
					new CommandParameter("channel_name_phonetic", channel[3])
				);
			} catch (Ts3CommandException err){
				if (err.Message.StartsWith("channel_name_inuse")) {
					ret = client.Send("channelcreate",
					new CommandParameter("channel_name", channel[0] + "_"),
					new CommandParameter("channel_password", Ts3Crypt.HashPassword(channel[1])),
					new CommandParameter("channel_needed_talk_power", channel[2]),
					new CommandParameter("channel_name_phonetic", channel[3])
				);
				} else {
					Console.WriteLine("Error while creating channel " + channel[0] + " " + err.ErrorStatus + "\n" + err.Message);
					return;
				}
			}
			//}
			Thread.Sleep(250);
			string cid = "0";
			foreach (var resp in ret) {
				foreach (var kvp in resp) {
					Console.Write(kvp.Key + "=" + kvp.Value + " ");
					if (kvp.Key.Equals("cid"))
						cid = kvp.Value;
				}
				Console.Write("\r\n");
			}
			return;
			client.Send("setclientchannelgroup",
				new CommandParameter("cgid", 11),
				new CommandParameter("cid", cid),
				new CommandParameter("cldbid", 404954)
			);
			//client.ClientMove(response., Ts3Crypt.HashPassword(channel[1]));
		}
	
        private static void OnErrorEvent(object sender, CommandError e) {
            var client = (Ts3FullClient)sender;
            Console.WriteLine(e.ErrorFormat());
            if (!client.Connected)
            {
				Console.WriteLine("Could not connect: " + e.Message + " (" + e.ExtraMessage);
            	//client.Connect(con);
            }
        }
    }
}
