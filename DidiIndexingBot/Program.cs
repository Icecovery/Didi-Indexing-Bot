using DidiIndexingBot.RecordImporter;
using System;
using System.IO;

namespace DidiIndexingBot
{
	public static class Program
	{
		public static void Main(string[] args)
		{
			if (args.Length == 2)
			{
				Console.WriteLine($"databaseFilePath = {args[0]}\njsonRecordPath = {args[1]}");

				Importer.Import(databaseFilePath: Path.Combine(Directory.GetCurrentDirectory(), args[0]),
								jsonRecordPath: Path.Combine(Directory.GetCurrentDirectory(), args[1]));
				return;
			}
		}
	}
}