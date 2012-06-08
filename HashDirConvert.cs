using System;
using System.IO;

class Program
{
	static void Main(string[] args)
	{
		if (args.Length < 1) {
			Console.WriteLine("Specify HASHDIR");
			return;
		}
		var hashDir = Path.GetFullPath(args[0]);
		if (!hashDir.EndsWith("\\")) hashDir += "\\";
		for (var i = 0; i < (1 << 12); i++)
			Directory.CreateDirectory(hashDir + i.ToString("x3"));
		for (var l1 = 0; l1 < 256; l1++)
		{
			var d = String.Format("{0}{1:x2}\\", hashDir, l1);
			if (Directory.Exists(d))
			{
				for (var l2 = 0; l2 < 256; l2++)
				{
					var sd = String.Format("{0}{1:x2}", d, l2);
					if (Directory.Exists(sd))
					{
						foreach (var f in Directory.EnumerateFileSystemEntries(sd))
						{
							var n = Path.GetFileName(f);
							if (n.Length == 36)
							{
								var tf = String.Format("{0}{1:x2}{2:x1}\\{3:x1}{4}", hashDir, l1, l2 >> 4, l2 & 15, n);
								//Console.WriteLine("rename {0}=>{1}", f, tf);
								File.Move(f, tf);
							}
						}
						Console.WriteLine("delete {0}", sd);
						Directory.Delete(sd);
					}
				}
				Console.WriteLine("delete {0}", d);
				Directory.Delete(d);
			}
		}
	}
}
