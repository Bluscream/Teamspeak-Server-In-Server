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
        static ConnectionDataFull con;
        static void Main() {
            clients = new List<Ts3FullClient>();
            Console.CancelKeyPress += (s, e) => {
                if (e.SpecialKey == ConsoleSpecialKey.ControlC) {
                    e.Cancel = true;
                    for (int i = 0; i < clients.Count; i++) {
                        clients[i]?.Disconnect();
                    }
                    Environment.Exit(0);
                }
            };
            channels = File.ReadAllLines("chans.txt");
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
                con = new ConnectionDataFull() { Address = "79.133.54.207:9987", Username = "ChannelWatcher", Identity = ID, Password = "" };
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
                var response = client.Send("channelcreate",
                    new CommandParameter("channel_name", channel[0]),
                    new CommandParameter("channel_password", Ts3Crypt.HashPassword(channel[1])),
                    new CommandParameter("channel_needed_talk_power", channel[2]),
                    new CommandParameter("channel_name_phonetic", channel[3])
                );
                Console.WriteLine(response.ToString());
                //var resp = response.ToString();
                return;
                Thread.Sleep(500);
                client.Send("setclientchannelgroup",
                    new CommandParameter("cgid", 11),
                    new CommandParameter("cid", 1),
                    new CommandParameter("cldbid", 404954)
                );
            } catch (Ts3CommandException) {
                Console.WriteLine("Error while creating channel " + channel[0]);
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
