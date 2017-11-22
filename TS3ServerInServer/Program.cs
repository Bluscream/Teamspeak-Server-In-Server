using System;
using System.Collections.Generic;
using System.IO;
using TS3Client;
using TS3Client.Commands;
using TS3Client.Full;
using TS3Client.Messages;
using System.Linq;
using System.Threading;
using System.Data;
using IniParser;
using IniParser.Model;
using ClientUidT = System.String;
using ClientDbIdT = System.UInt64;
using ClientIdT = System.UInt16;
using ChannelIdT = System.UInt64;
using ServerGroupIdT = System.UInt64;
using ChannelGroupIdT = System.UInt64;

// ReSharper disable All
namespace TS3ServerInServer {
	static class Program {
		private static ChannelGroupIdT adminCGID = 0; // TODO: Dynamic
		private static ChannelGroupIdT banCGID = 0; // TODO: Dynamic
		private static ChannelGroupIdT modCGID = 0; // TODO: Dynamic
		private static string[] banned_names = { "BAN", "NOT WELCOME" };
		private static string[] mod_names = { "MOD", "OPERATOR" };
		private static string[] admin_names = { "ADMIN" };
		private static ClientDbIdT ownerDBID;
		private static ClientUidT ownerUID;
		private static ConnectionDataFull con = new ConnectionDataFull();
		private static IniData cfg;
		private static List<ChannelIdT> cids = new List<ChannelIdT>();
		private static List<ClientUidT> done = new List<ClientUidT>();
		private static List<Ts3FullClient> clients;
		private static Timer AntiAFK { get; set; }
		private static bool isExit = false;
		private static bool locked = false;
		private static int cnt = -1;
		private static string cfgfile = "config.cfg";
		private static string chanfile = "chans.csv";
		private static string idfile = "ids.csv";
		private static string[] channels;
		private static string[] ids;
		static void Dispose() {
			if (isExit) return;
			isExit = true;
			lock (clients) {
				var _clients = clients.ToArray();
				foreach (var client in _clients) {
					client.OnConnected -= OnConnected;
					client.OnDisconnected -= OnDisconnected;
					client.OnErrorEvent -= OnErrorEvent;
					client.OnTextMessageReceived -= OnTextMessageReceived;
					client.OnClientMoved -= OnClientMoved;
					client.OnClientEnterView -= OnClientEnterView;
					client?.Disconnect();
					Thread.Sleep(int.Parse(cfg["general"]["DisconnectSleepMS"]));
				}
			}
			TSSettings.CloseDB();
		}
		static void Main() {
			AppDomain.CurrentDomain.ProcessExit += new EventHandler(CurrentDomain_ProcessExit);
			clients = new List<Ts3FullClient>();
			channels = File.ReadAllLines(chanfile);
			var parser = new FileIniDataParser();
			cfg = parser.ReadFile(cfgfile);
			RandomNick rndnick = new RandomNick();
			rndnick.Init();
			TSSettings.OpenDB();
			Console.CancelKeyPress += (s, e) => {
				if (e.SpecialKey == ConsoleSpecialKey.ControlC) {
					e.Cancel = true;
					Dispose();
					Environment.Exit(0);
				}
			};
			con.Address = cfg["general"]["Address"];
			con.Password = cfg["general"]["ServerPassword"];
			ownerUID = cfg["general"]["OwnerUID"];
			/*
			adminCGID = uint.Parse(cfg["general"]["adminCGID"]);
			modCGID = uint.Parse(cfg["general"]["modCGID"]);
			banCGID = uint.Parse(cfg["general"]["banCGID"]);
			*/
			if (!File.Exists(idfile)) {
				using (File.Create(idfile)) { }
			}
			ids = File.ReadAllLines(idfile);
			for (int i = 0; i < channels.Length; i++) {
				if (isExit) return;
				con.Username = rndnick.GetRandomNick(); //cfg["general"]["Nickname"];
				var client = new Ts3FullClient(EventDispatchType.DoubleThread);
				client.OnConnected += OnConnected;
				client.OnDisconnected += OnDisconnected;
				client.OnErrorEvent += OnErrorEvent;
				client.OnTextMessageReceived += OnTextMessageReceived;
				client.OnClientMoved += OnClientMoved;
				client.OnClientEnterView += OnClientEnterView;
				var _identity = ids.Select(x => x.Split(',')).ToList();
				IdentityData ID;
				try {
					ID = Ts3Crypt.LoadIdentity(_identity[i][0], ulong.Parse(_identity[i][1]));
					if (i > 0) Thread.Sleep(int.Parse(cfg["general"]["ConnectSleepMS"]));
				} catch (Exception) {
					ID = Ts3Crypt.GenerateNewIdentity(int.Parse(cfg["general"]["MinLVL"]));
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
				con.HWID = $"{Clientlib.RandomString(32)},{Clientlib.RandomString(32)}";
				Console.WriteLine("#" + i + " HWID: " + con.HWID);
				client.Connect(con);
				clients.Add(client);
			}
			AntiAFK = new Timer(OnTick, "on", 114*10000, 114*10000);
			Console.WriteLine("End");
			Console.ReadLine();
			Dispose();
		}

#region events

		static void CurrentDomain_ProcessExit(object sender, EventArgs e) {
			Dispose();
		}

		private static void OnClientEnterView(object sender, IEnumerable<ClientEnterView> e) {

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
			var ownClient = (Ts3FullClient)sender;
			foreach (var client in e) {
				locked = true;
				if (client.InvokerUid == ownerUID)
					continue;
				if (client.Reason == MoveReason.UserAction && Clientlib.GetMyChannelID(ownClient) == client.TargetChannelId) {
					//Console.WriteLine($"#{client.ClientId} {String.Join(",", cids)}.Contains({client.TargetChannelId}) = {cids.Contains(client.TargetChannelId)}");
					if (cids.Contains(client.TargetChannelId)) {
						CheckClient((Ts3FullClient)sender, client.ClientId);
					}
				}
				//Console.WriteLine($"ClientId={client.ClientId} InvokerId={client.InvokerId}  InvokerName={client.InvokerName}  InvokerUid={client.InvokerUid}  NotifyType={client.NotifyType}  Reason={client.Reason} TargetChannelId={client.TargetChannelId}");
			}
			if (locked) locked = false;
		}

		private static void OnDisconnected(object sender, DisconnectEventArgs e) {
			var client = (Ts3FullClient)sender;
			Console.WriteLine("Disconnected id={0} clid={1}", cnt, client.ClientId);
			clients.Remove(client);
		}

		private static void OnConnected(object sender, EventArgs e) {
			try {
				int myId = Interlocked.Increment(ref cnt);
				var client = (Ts3FullClient)sender;
				if (myId == 0) CheckGroups(client);
				Console.WriteLine("Connected id={0} clid={1}", myId, client.ClientId);
				var channel = channels[myId].Split(',');
				/*var response = client.Send("channellist");
				var channel_name_in_use = false;
				foreach (var chan in response) {
					if (chan["channel_name"] == channel[0])
						channel_name_in_use = true; break;
				}*/
				ulong cid = 0;
				/*if (channel_name_in_use) {
				} else {*/
				try {
					cid = Clientlib.ChannelCreate(client, channel[0], channel[1], channel[2], channel[3]).ChannelId;
				} catch (Ts3CommandException err) {
					if (err.ErrorStatus.Id == Ts3ErrorCode.channel_name_inuse)
						cid = Clientlib.ChannelCreate(client, channel[0] + "_", channel[1], channel[2], channel[3]).ChannelId;
					Console.WriteLine("Error while creating channel " + channel[0] + " " + err.ErrorStatus + "\n" + err.Message);
				}
				cids.Add(cid);
				ownerDBID = uint.Parse(client.Send("clientgetdbidfromuid", new CommandParameter("cluid", ownerUID)).FirstOrDefault()["cldbid"]);
				try {
					Clientlib.SetClientChannelGroup(client, modCGID, cid, ownerDBID);
				} catch (Ts3CommandException err) {
					Console.WriteLine("Error while setting channelgroup " + channel[0] + " " + err.ErrorStatus + "\n" + err.Message);
					return;
				}
				client.Send("clientupdate",
					new CommandParameter("client_badges", "badges=c9e97536-5a2d-4c8e-a135-af404587a472")
				);
			} catch (Exception ex) {
				Console.WriteLine($"Catched exception in OnConnected:\n{ex}");
			}
		}

		private static void OnTick(object state) {
			try {
				lock (clients) {
					var bots = clients.ToArray();
					foreach (var client in clients) {
						try {
							client.Send("clientupdate", new CommandParameter("client_input_muted", 0));
						} catch (Exception ex) { Console.WriteLine($"Catched Exception in OnTick's for loop: {ex.Message}\r\n{ex.StackTrace}"); }
					}
				}
			} catch (Exception ex) { Console.WriteLine($"Catched Exception in OnTick: {ex.Message}\r\n{ex.StackTrace}"); }
		}

		private static void OnErrorEvent(object sender, CommandError e) {
			var client = (Ts3FullClient)sender;
			Console.WriteLine(e.ErrorFormat());
			if (!client.Connected) {
				Console.WriteLine("Could not connect: " + e.Message + " (" + e.ExtraMessage);
			}
		}

#endregion

#region functions

		private static void CheckGroups(Ts3FullClient client) {
			var response = Clientlib.GetAllChannelGroups(client);
			foreach (ChannelGroupListResponse gCLR in response) {
				if (gCLR.type == PermissionGroupDatabaseType.Regular) {
					foreach (string name in admin_names) {
						if (gCLR.cgName.ToString().ToLower().Contains(name.ToLower())) {
							adminCGID = gCLR.cgID; continue;
						}
					}
					foreach (string name in mod_names) {
						if (gCLR.cgName.ToString().ToLower().Contains(name.ToLower())) {
							modCGID = gCLR.cgID; continue;
						}
					}
					foreach (string name in banned_names) {
						if (gCLR.cgName.ToString().ToLower().Contains(name.ToLower())) {
							banCGID = gCLR.cgID; continue;
						}
					}
				}
			}
		}

		private static void CheckClient(object sender, ClientIdT clid) {
			var cl = (Ts3FullClient)sender;
			ClientUidT cluid = Clientlib.ClientGetUidFromClid(cl, clid);
			Console.WriteLine($"clid={clid} cluid={cluid}");
			if (done.Contains(cluid)) return;
			var dbid = uint.Parse(cl.Send("clientgetdbidfromuid", new CommandParameter("cluid", cluid)).FirstOrDefault()["cldbid"]);
			var friend = TSSettings.isFriend(cluid);
			Console.WriteLine($"#{clid} dbid={dbid} cluid={cluid} friend={friend}");
			switch (friend) {
				case FriendStatus.Blocked:
					Clientlib.SetAllChannelGroup(cl, banCGID, dbid, cids);
					break;
				case FriendStatus.Friend:
					Clientlib.SetAllChannelGroup(cl, modCGID, dbid, cids);
					break;
				default:
					break;
			}
			done.Add(cluid);
		}
#endregion
	}
}
