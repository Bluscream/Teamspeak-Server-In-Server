using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TS3ServerInServer {
	class RandomNick {
		//private static string[] prefix;
		private static string[] names;
		private static string[] names1;
		private static string[] names2;
		private static string[] suffix;
		public static void Main() {
			//prefix = File.ReadAllLines("names_prefix.txt");
			names = File.ReadAllLines("names.txt");
			names1 = File.ReadAllLines("names1.txt");
			names2 = File.ReadAllLines("names2.txt");
			suffix = File.ReadAllLines("names_suffix.txt");
		}
		public static string GetRandomNick() {
			var rand = new Random();
			var strBuilder = new StringBuilder();
			var r = rand.Next(0, 1);
			switch (r) {
				case 0:
					strBuilder.Append(names[rand.Next(names.Length)]);
					break;
				case 1:
					strBuilder.Append(names1[rand.Next(names1.Length)]);
					strBuilder.Append(names2[rand.Next(names2.Length)]);
					break;
				default:
					break;
			}
			if (rand.NextDouble() > 0.5)
				strBuilder.Append(suffix[rand.Next(suffix.Length)]);
			return strBuilder.ToString();
		}
	}
}
