using DidiIndexingBot.RecordImporter.Models;
using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace DidiIndexingBot.RecordImporter
{
	public static class Importer
	{
		public static void Import(string databaseFilePath, string jsonRecordPath )
		{
			RootObject ro = JsonSerializer.Deserialize<RootObject>(File.ReadAllText(jsonRecordPath));

			using SqliteConnection connection = new($"Data Source={databaseFilePath}");

			connection.Open();

			using SqliteTransaction transaction = connection.BeginTransaction();

			// command 

			SqliteCommand insertMessageCommand = connection.CreateCommand();
			insertMessageCommand.CommandText =
			@"
				INSERT INTO messages (id, date, from_name, from_id, text, reply_to_message_id, forwarded_from)
				VALUES ($id, $date, $from_name, $from_id, $text, $reply_to_message_id, $forwarded_from);
			";

			// parameters

			SqliteParameter parameter_id = insertMessageCommand.CreateParameter();
			parameter_id.ParameterName = "$id";
			insertMessageCommand.Parameters.Add(parameter_id);

			SqliteParameter parameter_date = insertMessageCommand.CreateParameter();
			parameter_date.ParameterName = "$date";
			insertMessageCommand.Parameters.Add(parameter_date);

			SqliteParameter parameter_from_name = insertMessageCommand.CreateParameter();
			parameter_from_name.ParameterName = "$from_name";
			insertMessageCommand.Parameters.Add(parameter_from_name);

			SqliteParameter parameter_from_id = insertMessageCommand.CreateParameter();
			parameter_from_id.ParameterName = "$from_id";
			insertMessageCommand.Parameters.Add(parameter_from_id);

			SqliteParameter parameter_text = insertMessageCommand.CreateParameter();
			parameter_text.ParameterName = "$text";
			insertMessageCommand.Parameters.Add(parameter_text);

			SqliteParameter parameter_reply_to_message_id = insertMessageCommand.CreateParameter();
			parameter_reply_to_message_id.ParameterName = "$reply_to_message_id";
			insertMessageCommand.Parameters.Add(parameter_reply_to_message_id);

			SqliteParameter parameter_forwarded_from = insertMessageCommand.CreateParameter();
			parameter_forwarded_from.ParameterName = "$forwarded_from";
			insertMessageCommand.Parameters.Add(parameter_forwarded_from);

			// fill in commands

			int counter = 0;
			Console.WriteLine("Processing...");
			foreach (Message msg in ro.messages)
			{
				if (msg.type == "message")
				{
					if (msg.id % 1000 == 0)
					{
						Console.Write($"\r{msg.id} - {counter} ");
					}

					parameter_id.Value = msg.id.ToString();
					parameter_date.Value = msg.date.ToString("s");
					parameter_from_name.Value = msg.from ?? "[deleted account]";
					parameter_from_id.Value = msg.from_id.Replace("user", "").Replace("channel", "");
					parameter_text.Value = GetMsgText(msg);
					parameter_reply_to_message_id.Value = msg.reply_to_message_id.ToString();
					parameter_forwarded_from.Value = msg.forwarded_from ?? string.Empty;
					try
					{
						insertMessageCommand.ExecuteNonQuery();
					}
					catch (Exception)
					{
						Console.Error.WriteLine($"ERROR on message {msg.id}");
						throw;
					}

					counter++;
				}
			}

			// fts5 search

			Console.Write("Creating fts5 table...");

			SqliteCommand fts5InsertAllCommand = connection.CreateCommand();
			fts5InsertAllCommand.CommandText =
			@"
				INSERT INTO search SELECT id, text FROM messages;
			";

			fts5InsertAllCommand.ExecuteNonQuery();

			Console.WriteLine("Done");

			transaction.Commit();
			connection.Close();
			Console.WriteLine($"\nTotal entries: {counter}");
		}

		private static string GetMsgText(Message msg)
		{
			string text = string.Empty;

			if (msg.text.ToString() == string.Empty)
			{
				if (msg.sticker_emoji != null)
				{
					text = $"[Sticker {msg.sticker_emoji}]";
				}
				else if (msg.media_type != null)
				{
					text = $"[Media {msg.media_type}]";
				}
				else if (msg.photo != null)
				{
					text = $"[Photo]";
				}
				else if (msg.file != null)
				{
					text = $"[File]";
				}
				else if (msg.game_title != null)
				{
					text = $"[Game {msg.game_title}]";
				}
				else if (msg.poll != null)
				{
					StringBuilder sb = new();
					sb.AppendLine($"[Poll {msg.poll.question}]");
					sb.AppendLine($"[Total Voters {msg.poll.total_voters}]");
					foreach (Answer answer in msg.poll.answers)
					{
						sb.AppendLine($"[Option {answer.text} - {answer.voters} votes]");
					}

					text = sb.ToString();
				}
				else if (msg.location_information != null)
				{
					text = $"[Location longitude: {msg.location_information.longitude} latitude: {msg.location_information.latitude}]";
				}
				else
				{
					text = "[Unknown Message Type]";
				}
			}
			else
			{
				text = CompoundText.Parse(msg.text.ToString());
			}

			return text;
		}
	}
}
