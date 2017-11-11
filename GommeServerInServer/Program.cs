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
        static ConnectionDataFull con = new ConnectionDataFull();
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
            channels = File.ReadAllLines("chans.txt");
			var _con = channels[0].Split(',');
			Console.WriteLine(channels[0]);
			channels = channels.Skip(1).ToArray();
			con.Address = _con[0];
			con.Username = _con[1];
			con.Password = _con[2];
			//con.VersionSign = VersionSign.VER_WIN_3_1_7_ALPHA;
			if (!File.Exists("ids.txt")) {
                using (File.Create("ids.txt")) { }
            }
            ids = File.ReadAllLines("ids.txt");
            for (int i = 0; i < channels.Length; i++) {
                var client = new Ts3FullClient(EventDispatchType.DoubleThread);
                client.OnConnected += Client_OnConnected;
                client.OnDisconnected += Client_OnDisconnected;
                client.OnErrorEvent += Client_OnErrorEvent;
                //client.OnClientMoved += Client_OnClientMoved;
                var _identity = ids.Select(x => x.Split(',')).ToList();
                IdentityData ID;
                try {
                    ID = Ts3Crypt.LoadIdentity(_identity[i][0], ulong.Parse(_identity[i][1]));
                } catch (Exception) {
                    ID = Ts3Crypt.GenerateNewIdentity(26);
                    File.AppendAllText("ids.txt", ID.PrivateKeyString + "," + ID.ValidKeyOffset + "\r\n");
                }
				con.Identity = ID;
				Array values = Enum.GetValues(typeof(VersionSign));
				Random random = new Random();
				con.VersionSign = (VersionSign)values.GetValue(random.Next(values.Length));
				client.Connect(con);
                clients.Add(client);
                Thread.Sleep(2500);
            }
            Console.WriteLine("End");
            Console.ReadLine();
        }

        /*private static void Client_OnClientMoved(object sender, IEnumerable<ClientMoved> e) {
            foreach (var client in e) {
                if (clie)
            }
        }*/

        private static void Client_OnDisconnected(object sender, DisconnectEventArgs e) {
            int myId = System.Threading.Interlocked.Increment(ref cnt);
            var client = (Ts3FullClient)sender;
            Console.WriteLine("Disconnected id={0} clid={1}", myId, client.ClientId);
        }

        private static void Client_OnConnected(object sender, EventArgs e) {
            int myId = System.Threading.Interlocked.Increment(ref cnt);
            var client = (Ts3FullClient)sender;
            Console.WriteLine("Connected id={0} clid={1}", myId, client.ClientId);
            //var data = client.ClientInfo(client.ClientId);
            var channel = channels[myId].Split(',');
            try {
				client.ChannelCreate(channel[0], namePhonetic: channel[3], password: channel[1], neededTP: Convert.ToInt32(channel[2]));
				/*var response = client.Send("channelcreate",
					new CommandParameter("channel_name", channel[0]),
					new CommandParameter("channel_password", Ts3Crypt.HashPassword(channel[1])),
					new CommandParameter("channel_needed_talk_power", channel[2]),
					new CommandParameter("channel_name_phonetic", channel[3])
				);
				Console.WriteLine(response.ToString());*/
				return;
                //var resp = response.ToString();
                Thread.Sleep(500);
                client.Send("setclientchannelgroup",
                    new CommandParameter("cgid", 11),
                    new CommandParameter("cid", 1),
                    new CommandParameter("cldbid", 404954)
                );
            } catch (Ts3CommandException err) {
				if (err.Message.StartsWith("channel_name_inuse")) {
					client.ChannelCreate(channel[0] + "_", namePhonetic: channel[3], password: channel[1], neededTP: Convert.ToInt32(channel[2]));
					/*var response = client.Send("channelcreate",
						new CommandParameter("channel_name", channel[0] + "_"),
						new CommandParameter("channel_password", Ts3Crypt.HashPassword(channel[1])),
						new CommandParameter("channel_needed_talk_power", channel[2]),
						new CommandParameter("channel_name_phonetic", channel[3])
					);
					Console.WriteLine(response.ToString());*/
				} else {
					Console.WriteLine("Error while creating channel " + channel[0] + " " + err.ErrorStatus + "\n" + err.Message);
				}
				//client.ClientMove(response., Ts3Crypt.HashPassword(channel[1]));
			}
        }

        private static void Client_OnErrorEvent(object sender, CommandError e) {
            var client = (Ts3FullClient)sender;
            Console.WriteLine(e.ErrorFormat());
            if (!client.Connected)
            {
            	client.Connect(con);
            }
        }
    }
}
