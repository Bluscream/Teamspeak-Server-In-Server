using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TS3ServerInServer {
	class RandomNick {
		//private static string[] prefix;
		private string[] names;
		private string[] names1;
		private string[] names2;
		private string[] suffix;

		public void Init() {
			//prefix = File.ReadAllLines("names_prefix.txt");
			names = File.ReadAllLines("names.txt");
			names1 = File.ReadAllLines("names1.txt");
			names2 = File.ReadAllLines("names2.txt");
			suffix = File.ReadAllLines("names_suffix.txt");
		}
		public string GetRandomNick() {
			var strBuilder = new StringBuilder();
			Random rand = new Random();
			switch (rand.Next(0,2)) {
				case 0:
					strBuilder.Append(names[rand.Next(names.Length)]);
					break;
				case 1:
					strBuilder.Append(names1[rand.Next(names1.Length)]);
					strBuilder.Append(names2[rand.Next(names2.Length)]);
					break;
				case 2:
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
