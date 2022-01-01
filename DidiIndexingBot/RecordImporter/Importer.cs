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

			SqliteCommand command = connection.CreateCommand();
			command.CommandText =
			@"
				INSERT INTO messages (id, date, from_name, from_id, text, reply_to_message_id, forwarded_from)
				VALUES ($id, $date, $from_name, $from_id, $text, $reply_to_message_id, $forwarded_from);
			";

			// parameters

			SqliteParameter parameter_id = command.CreateParameter();
			parameter_id.ParameterName = "$id";
			command.Parameters.Add(parameter_id);

			SqliteParameter parameter_date = command.CreateParameter();
			parameter_date.ParameterName = "$date";
			command.Parameters.Add(parameter_date);

			SqliteParameter parameter_from_name = command.CreateParameter();
			parameter_from_name.ParameterName = "$from_name";
			command.Parameters.Add(parameter_from_name);

			SqliteParameter parameter_from_id = command.CreateParameter();
			parameter_from_id.ParameterName = "$from_id";
			command.Parameters.Add(parameter_from_id);

			SqliteParameter parameter_text = command.CreateParameter();
			parameter_text.ParameterName = "$text";
			command.Parameters.Add(parameter_text);

			SqliteParameter parameter_reply_to_message_id = command.CreateParameter();
			parameter_reply_to_message_id.ParameterName = "$reply_to_message_id";
			command.Parameters.Add(parameter_reply_to_message_id);

			SqliteParameter parameter_forwarded_from = command.CreateParameter();
			parameter_forwarded_from.ParameterName = "$forwarded_from";
			command.Parameters.Add(parameter_forwarded_from);

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
					parameter_date.Value = msg.date.ToString();
					parameter_from_name.Value = msg.from ?? "deleted account";
					parameter_from_id.Value = msg.from_id.Replace("user", "").Replace("channel", "");
					parameter_text.Value = GetMsgText(msg);
					parameter_reply_to_message_id.Value = msg.reply_to_message_id.ToString();
					parameter_forwarded_from.Value = msg.forwarded_from ?? string.Empty;
					try
					{
						command.ExecuteNonQuery();
					}
					catch (Exception)
					{
						Console.WriteLine("ERROR on message " + msg.id.ToString());
						throw;
					}

					counter++;
				}
			}

			transaction.Commit();
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
					StringBuilder sb = new StringBuilder();
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
