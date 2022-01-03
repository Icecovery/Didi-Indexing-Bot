using DidiIndexingBot.RecordImporter;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

/*  Commands Cheat Sheet

	/search {term}				- Search message that contains {term}
	/searchfts5 {term}			- Search message that contains {term}, force FTS5
	/searchlike {term}			- Search message that contains {term}, force LIKE
	/viewarchive {message id}	- View archive for message with that id
	/random@Didi_Indexing_Bot	- Get a random quote
	/whosaid@Didi_Indexing_Bot	- Who said this random quote?
	/debug@Didi_Indexing_Bot	- Show debug message

	Bot Father format
		
	search		- /search {term}: Search message that contains {term}
	searchfts5	- /searchfts5 {term}: Search message that contains {term}, force FTS5
	searchlike	- /searchlike {term}: Search message that contains {term}, force LIKE
	viewarchive	- /viewarchive {message id}: View archive for message with that id
	random		- /random: Get a random quote
	whosaid		- /whosaid: Who said this random quote? This command has a cooldown of 60s
	debug		- /debug: debug message
*/

namespace DidiIndexingBot
{
	public static class Program
	{
		private static readonly string BotName = "@didiIndexingBot";
		private static readonly string buildID = "Build 2022-01-02-01";
		private static SqliteConnection connection;
		public static readonly IConfigurationRoot secret = new ConfigurationBuilder().AddUserSecrets(typeof(Program).Assembly).Build();
		private static readonly TelegramBotClient Bot = new(secret["botAPIKey"]);
		private static readonly Regex isAlphanumeric = new("^[a-zA-Z0-9]*$");
		private static DateTime whoSaidLastCallTime = DateTime.MinValue;

		#region Constants
		private const int whoSaidQuestionSearchAttempt = 100;
		private const int whoSaidTextMinimumLength = 8;
		private const int whoSaidAnswerSearchAttempt = 20;
		private const int whoSaidWrongAnswerCount = 4;
		private const int whoSaidCoolDown = 60;
		private const int randomMessageSearchAttempt = 10;
		#endregion

		/// <summary>
		/// Main program entry point
		/// </summary>
		/// <param name="args">command line arguments</param>
		public static void Main(string[] args)
		{
			if (args.Length == 2)
			{
				Console.WriteLine($"databaseFilePath = {args[0]}\njsonRecordPath = {args[1]}");

				Importer.Import(databaseFilePath: Path.Combine(Directory.GetCurrentDirectory(), args[0]),
								jsonRecordPath: Path.Combine(Directory.GetCurrentDirectory(), args[1]));
				return;
			}
			else if (args.Length != 1)
			{
                Console.WriteLine($"Parameters: DidiIndexingBot databaseFilePath [jsonRecordPath]");
                return;
			}

			//Boot
			Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"==================================");
            Console.WriteLine($"||Indexing Bot V0.1 Telegram ed.||");
            Console.WriteLine($"||      {buildID}     ||");
            Console.WriteLine($"==================================");
			Console.Write($"[{DateTime.Now}] Booting.");

			using CancellationTokenSource cts = new();
			Console.Write(".");

			connection = new($"Data Source={args[0]}");
			Console.Write(".");

			Bot.StartReceiving(HandleUpdateAsync, HandleErrorAsync, cancellationToken: cts.Token);
            Console.Write(".");

            Console.Write("Booted up.\n");
            Console.WriteLine("=================================");
            Console.ResetColor();

			LogWithTime($"Using database {args[0]}");

			//Wait to stop
			Console.ReadLine();
            cts.Cancel();
        }

		#region Handle Telegram Events

		/// <summary>
		/// Handle telegram update event
		/// </summary>
		/// <param name="botClient"></param>
		/// <param name="update"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
		{
			try
			{
				if (update.CallbackQuery != null)
				{
					await HandleCallbackQuery(update.CallbackQuery, cancellationToken);
					return;
				}

				if (update.Message != null)
				{
					await HandleMessage(update.Message, cancellationToken);
					return;
				}
			}
			catch (Exception e)
			{
				LogWithTime($"Error when HandleUpdateAsync: {e.Message}");
				//throw;
			}
		}

		/// <summary>
		/// Handle error event
		/// </summary>
		/// <param name="botClient"></param>
		/// <param name="exception"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		private static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
		{
			string ErrorMessage = exception switch
			{
				ApiRequestException apiRequestException
					=> $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
				_ => exception.ToString()
			};

			Console.Error.WriteLine($"[{DateTime.Now}] {ErrorMessage}");

			return Task.CompletedTask;
		}

		/// <summary>
		/// Handle basic chat message
		/// </summary>
		/// <param name="message">message to handle</param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		private static async Task HandleMessage(Message message, CancellationToken cancellationToken)
		{
			if (message.Chat.Id != long.Parse(secret["groupId"]))
				return;

			try
			{
				string text = string.Empty;

				switch (message.Type)
				{
					case MessageType.Text:
						// commands
						if (message.Text.StartsWith("/search"))
						{
							await HandleSearchCommand(message, cancellationToken);
							return; // don't record search command query
						}
						if (message.Text.StartsWith("/viewarchive"))
						{
							await HandleViewArchiveCommand(message, cancellationToken);
						}
						if (message.Text.Contains(BotName) && message.Text.StartsWith("/debug"))
						{
							await HandleDebugCommand(message, cancellationToken);
						}
						if (message.Text.Contains(BotName) && message.Text.StartsWith("/random"))
						{
							await HandleRandomCommand(message, cancellationToken);
						}
						if (message.Text.Contains(BotName) && message.Text.StartsWith("/whosaid"))
						{
							await HandleWhoSaid(message, cancellationToken);
						}

						//recording

						text = message.Text;
						break;
					case MessageType.Photo:
						if (message.Animation != null)
							text = $"[Media animation]";
						else
							text = $"[Photo]";
						break;
					case MessageType.Audio:
						text = $"[Audio]";
						break;
					case MessageType.Video:
						text = $"[Media video_file]";
						break;
					case MessageType.Voice:
						text = $"[Media voice_message]";
						break;
					case MessageType.Document:
						text = $"[File]";
						break;
					case MessageType.Sticker:
						text = $"[Sticker {message.Sticker.Emoji}]";
						break;
					case MessageType.Location:
						text = $"[Location longitude: {message.Location.Longitude} latitude: {message.Location.Latitude}]";
						break;
					case MessageType.Game:
						text = $"[Game {message.Game.Title}]";
						break;
					case MessageType.Poll:
						StringBuilder sb = new();
						sb.AppendLine($"[Poll {message.Poll.Question}]");
						sb.AppendLine($"[Total Voters {message.Poll.TotalVoterCount}]");
						foreach (PollOption option in message.Poll.Options)
						{
							sb.AppendLine($"[Option {option.Text} - {option.VoterCount} votes]");
						}

						text = sb.ToString();
						break;
					case MessageType.Dice:
						text = $"[Dice {message.Dice.Emoji} value={message.Dice.Value}]";
						break;
				}			

				try
				{
					MessageEntry entry = new(id: message.MessageId,
											date: message.Date,
											from_name: $"{message.From.FirstName} {message.From.LastName}".Trim(),
											from_id: message.From.Id,
											reply_to_message_id: (message.ReplyToMessage == null) ? 0 : message.ReplyToMessage.MessageId,
											forwarded_from: (message.ForwardFromChat == null) ? string.Empty : $"{message.ForwardFromChat.FirstName} {message.ForwardFromChat.FirstName}",
											text: text);

					connection.Open();
					using SqliteTransaction transaction = connection.BeginTransaction();

					SqliteCommand insertMessageCommand = connection.CreateCommand();
					insertMessageCommand.CommandText =
					@"
						INSERT INTO messages (id, date, from_name, from_id, text, reply_to_message_id, forwarded_from)
						VALUES ($id, $date, $from_name, $from_id, $text, $reply_to_message_id, $forwarded_from);
					";

					insertMessageCommand.Parameters.AddWithValue("$id", entry.id);
					insertMessageCommand.Parameters.AddWithValue("$date", entry.date);
					insertMessageCommand.Parameters.AddWithValue("$from_name", entry.from_name);
					insertMessageCommand.Parameters.AddWithValue("$from_id", entry.from_id);
					insertMessageCommand.Parameters.AddWithValue("$text", entry.text);
					insertMessageCommand.Parameters.AddWithValue("$reply_to_message_id", entry.reply_to_message_id);
					insertMessageCommand.Parameters.AddWithValue("$forwarded_from", entry.forwarded_from);
					
					insertMessageCommand.ExecuteNonQuery();

					SqliteCommand fts5InsertCommand = connection.CreateCommand();
					fts5InsertCommand.CommandText =
					@"
						INSERT INTO search (id, text)
						VALUES ($id, $text);
					";
					fts5InsertCommand.Parameters.AddWithValue("$id", entry.id);
					fts5InsertCommand.Parameters.AddWithValue("$text", entry.text);

					fts5InsertCommand.ExecuteNonQuery();

					transaction.Commit();
					connection.Close();
				}
				catch (Exception e)
				{
					LogWithTime($"Error when adding archive: {e.Message} in ->\n{e.StackTrace}");
				}
				
			}
			catch (Exception e)
			{
				LogWithTime($"Error when Handle Message {e.Message}");
			}
		}

		/// <summary>
		/// Handle callback query for in-line keyboard markup
		/// </summary>
		/// <param name="callbackQuery">the query to handle</param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		private static async Task HandleCallbackQuery(CallbackQuery callbackQuery, CancellationToken cancellationToken)
		{
			if (callbackQuery.Message.Chat.Id != long.Parse(secret["groupId"]))
				return;

			if (callbackQuery.Message.ReplyToMessage == null)
			{
				// original message does not exists anymore
				await Bot.EditMessageTextAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, "Failed: Original query message does not exist anymore.", cancellationToken: cancellationToken);
				return;
			}

			if (callbackQuery.Data.StartsWith('p'))
			{
				// page command
				int targetPage = int.Parse(callbackQuery.Data[1..]);

				ParseSarchTerm(callbackQuery.Message.ReplyToMessage, out bool forceFTS5, out bool forceLIKE, out string msg);
				List<MessageEntry> results = await SearchDatabase(msg, targetPage, forceLIKE, forceFTS5);
				InlineKeyboardMarkup keyboard = MakeResultKeyboard(results, targetPage);

				await Bot.EditMessageTextAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, GenerateSearchResultText(results, targetPage), parseMode: ParseMode.Html, replyMarkup: keyboard, disableWebPagePreview: true, cancellationToken: cancellationToken);
			}
			else
			{
				long targetId = long.Parse(callbackQuery.Data);
				MessageEntry entry = (await SearchDatabase(targetId)).Value;

				//InlineKeyboardMarkup inlineKeyboard = MakeArchiveKeyboard(targetId, entry);

				await Bot.EditMessageTextAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, entry.ToArchiveString(), parseMode: ParseMode.Html, /*replyMarkup: inlineKeyboard,*/ cancellationToken: cancellationToken);
			}
		}

		#endregion

		#region Handle Commands

		/// <summary>
		/// Handle Search Command
		/// </summary>
		/// <param name="message">message that contains the command</param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		private static async Task HandleSearchCommand(Message message, CancellationToken cancellationToken)
		{
			ParseSarchTerm(message, out bool forceFTS5, out bool forceLIKE, out string msg);

			if (string.IsNullOrEmpty(msg))
			{
				await Bot.SendTextMessageAsync(message.Chat.Id,
											   "Usage:\n" +
											   "	/search {term}: Search message that contains {term}\n" +
											   "	/searchfts5 {term}: Search message that contains {term}, force FTS5\n" +
											   "	/searchlike {term}: Search message that contains {term}, force LIKE\n",
											   replyToMessageId: message.MessageId,
											   cancellationToken: cancellationToken);
				return;
			}

			LogWithTime($"Searching for {msg}...");

			List<MessageEntry> result = await SearchDatabase(msg, 0, forceLIKE, forceFTS5);
			if (result == null)
			{
				await Bot.SendTextMessageAsync(message.Chat.Id, $"Error encountered when searching, contact administrator to check log.", replyToMessageId: message.MessageId, cancellationToken: cancellationToken);
				return;
			}

			if (result.Count == 0)
			{
				await Bot.SendTextMessageAsync(message.Chat.Id, $"No result found, try another search term or force a search method.", replyToMessageId: message.MessageId, cancellationToken: cancellationToken);
				return;
			}

			InlineKeyboardMarkup inlineKeyboard = MakeResultKeyboard(result, 0);

			await Bot.SendTextMessageAsync(message.Chat.Id, GenerateSearchResultText(result, 0), parseMode: ParseMode.Html, replyToMessageId: message.MessageId, replyMarkup: inlineKeyboard, disableWebPagePreview: true, cancellationToken: cancellationToken);
		}

		/// <summary>
		/// Handle view archive command
		/// </summary>
		/// <param name="message">message that contains the command<</param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		private static async Task HandleViewArchiveCommand(Message message, CancellationToken cancellationToken)
		{
			string msg = message.Text.Replace("/viewarchive", string.Empty).Replace(BotName, string.Empty).Trim();

			if (string.IsNullOrEmpty(msg) || !long.TryParse(msg, out long targetId))
			{
				LogWithTime(msg);
				await Bot.SendTextMessageAsync(message.Chat.Id,
											   "Usage:\n" +
											   "	/viewarchive {message id}: View archive for message with that id\n",
											   replyToMessageId: message.MessageId,
											   cancellationToken: cancellationToken);
				return;
			}

			if (targetId > message.MessageId)
			{
				await Bot.SendTextMessageAsync(message.Chat.Id, "This is a future message, time travel support is not available at this moment.", replyToMessageId: message.MessageId, cancellationToken: cancellationToken);
				return;
			}

			if (targetId == message.MessageId)
			{
				await Bot.SendTextMessageAsync(message.Chat.Id, "You are sending this exact message.", replyToMessageId: message.MessageId, cancellationToken: cancellationToken);
				return;
			}

			LogWithTime($"View archive for {targetId}...");

			MessageEntry? entry = await SearchDatabase(targetId);
			if (entry == null)
			{
				await Bot.SendTextMessageAsync(message.Chat.Id, "Can't find a chat message with this id, it might be a system message, a deleted message, or a unarchived bot message.", replyToMessageId: message.MessageId, cancellationToken: cancellationToken);
				return;
			}

			//InlineKeyboardMarkup inlineKeyboard = MakeArchiveKeyboard(targetId, entry.Value);

			await Bot.SendTextMessageAsync(message.Chat.Id, entry.Value.ToArchiveString(), parseMode: ParseMode.Html, replyToMessageId: message.MessageId, /*replyMarkup: inlineKeyboard,*/ cancellationToken: cancellationToken);
		}

		/// <summary>
		/// Handle debug command
		/// </summary>
		/// <param name="message">message that contains the command<</param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		private static async Task HandleDebugCommand(Message message, CancellationToken cancellationToken)
		{
			LogWithTime($"Sending debug message");

			StringBuilder sb = new();
			sb.AppendLine($"<b>Debug Message</b>");
			sb.AppendLine($"If you can see this message, the bot can see you and has access to the Internet.");
			sb.AppendLine($"whoSaidQuestionSearchAttempt: {whoSaidQuestionSearchAttempt}");
			sb.AppendLine($"whoSaidAnswerSearchAttempt: {whoSaidAnswerSearchAttempt}");
			sb.AppendLine($"whoSaidCoolDown: {whoSaidCoolDown}");
			sb.AppendLine($"whoSaidTextMinimumLength: {whoSaidTextMinimumLength}");
			sb.AppendLine($"whoSaidWrongAnswerCount: {whoSaidWrongAnswerCount}");
			sb.AppendLine($"randomMessageSearchAttempt: {randomMessageSearchAttempt}");
			// add more debug message here

			await Bot.SendTextMessageAsync(message.Chat.Id, sb.ToString(), replyToMessageId: message.MessageId, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
		}

		/// <summary>
		/// Handle Random command
		/// </summary>
		/// <param name="message">message that contains the command<</param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		private static async Task HandleRandomCommand(Message message, CancellationToken cancellationToken)
		{
			LogWithTime($"Sending random message");

			Random r = new();
			MessageEntry? entry = null;

			for (int i = 0; i < randomMessageSearchAttempt; i++)
			{
				long targetId = r.NextInt64(0, message.MessageId);
				entry = await SearchDatabase(targetId);

				if (entry.HasValue)
					break;
			}
			if (!entry.HasValue)
			{
				await Bot.SendTextMessageAsync(message.Chat.Id, "Failed to fetch random message at this time, please try again.", replyToMessageId: message.MessageId, cancellationToken: cancellationToken);
				return;
			}

			//InlineKeyboardMarkup inlineKeyboard = MakeArchiveKeyboard(targetId, entry.Value);
			await Bot.SendTextMessageAsync(message.Chat.Id, entry.Value.ToArchiveString(), parseMode: ParseMode.Html, replyToMessageId: message.MessageId, /*replyMarkup: inlineKeyboard,*/ cancellationToken: cancellationToken);
		}

		/// <summary>
		/// Handle who said command
		/// </summary>
		/// <param name="message">message that contains the command<</param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		private static async Task HandleWhoSaid(Message message, CancellationToken cancellationToken)
		{
			if ((DateTime.Now - whoSaidLastCallTime).TotalSeconds < whoSaidCoolDown)
				return;

			whoSaidLastCallTime = DateTime.Now;

			LogWithTime($"Sending who said message");

			Random r = new(message.MessageId);
			MessageEntry? entry = null;

			for (int i = 0; i < whoSaidQuestionSearchAttempt; i++)
			{
				long targetId = r.NextInt64(0, message.MessageId);
				entry = await SearchDatabase(targetId);

				if (!entry.HasValue
					|| !string.IsNullOrEmpty(entry.Value.forwarded_from)
					|| entry.Value.text.Length < whoSaidTextMinimumLength
					|| entry.Value.text.StartsWith('[')
					|| entry.Value.text.StartsWith('/')
					|| entry.Value.text.StartsWith('@')
					|| entry.Value.from_name == "[deleted account]")
				{
					//LogWithTime($"Fail because {(entry != null ? entry.Value.text : $"No Value at {targetId}")}");
					entry = null;

					continue;
				}
				break;
			}
			if (!entry.HasValue)
			{
				await Bot.SendTextMessageAsync(message.Chat.Id, "Failed to fetch random message at this time, please try again.", replyToMessageId: message.MessageId, cancellationToken: cancellationToken);
				return;
			}


			List<string> options = new();

			int tries = 0;

			for (int i = 0; i < whoSaidWrongAnswerCount; i++)
			{
				tries++;
				if (tries >= whoSaidAnswerSearchAttempt)
				{
					await Bot.SendTextMessageAsync(message.Chat.Id, "Failed to generate enough answer at this time, please try again.", replyToMessageId: message.MessageId, cancellationToken: cancellationToken);
					return;
				}

				long targetId = r.NextInt64(0, message.MessageId);
				MessageEntry? optionEntry = await SearchDatabase(targetId);

				if (!optionEntry.HasValue || optionEntry.Value.from_id == entry.Value.from_id || options.Contains(optionEntry.Value.from_name) || optionEntry.Value.from_name == "[deleted account]")
				{
					i--;
					continue;
				}

				options.Add(optionEntry.Value.from_name);
			}

			int correct = r.Next(0, whoSaidWrongAnswerCount);
			options.Insert(correct, entry.Value.from_name);

			Message question = await Bot.SendTextMessageAsync(message.Chat.Id, 
				$"<code>Who said this?</code>\n" +
				$"\n" +
				$"{entry.Value.ToWhoSaidString()}\n" +
				$"\n" +
				$"<code>Answer in the quiz below. Cooldown: {whoSaidCoolDown}s\n" +
				$"If you find this quiz interesting, please 👍 it!</code>", 
				parseMode: ParseMode.Html, replyToMessageId: message.MessageId, cancellationToken: cancellationToken);

			await Bot.SendPollAsync(
				chatId: message.Chat.Id, 
				question: "Who said this?", 
				explanation: entry.Value.ToWhoSaidExplanationString(),
				explanationParseMode: ParseMode.Html,
				type: PollType.Quiz,
				options: options,
				correctOptionId: correct,
				openPeriod: whoSaidCoolDown,
				isAnonymous: false,
				replyToMessageId: question.MessageId,
				cancellationToken: cancellationToken);
		}
		#endregion

		#region Helper Methods

		private static async Task<List<MessageEntry>> SearchDatabase(string term, int pageNum, bool forceLike, bool forceFTS5)
		{
			try
			{
				connection.Open();

				SqliteCommand searchCommand = connection.CreateCommand();

				if (forceFTS5 || (!forceLike && isAlphanumeric.IsMatch(term)))
				{
					// fts5
					searchCommand.CommandText =
					@"
					SELECT *
					FROM messages AS msgs
					INNER JOIN 
					(
					    SELECT search.id 
					    FROM search
					    WHERE search MATCH $term
					    ORDER BY rank, id DESC
					    LIMIT 10 OFFSET $offset
					) AS result_ids
					ON msgs.id = result_ids.id;
					";
					searchCommand.Parameters.AddWithValue("$term", $"text: {term}");
				}
				else
				{
					// like
					searchCommand.CommandText =
					@"
						SELECT * 
						FROM messages 
						WHERE text LIKE $term 
						COLLATE NOCASE
						ORDER BY id DESC
						LIMIT 10 OFFSET $offset;
					";
					searchCommand.Parameters.AddWithValue("$term", $"%{term}%");
				}
				
				searchCommand.Parameters.AddWithValue("$offset", pageNum * 10);

				using SqliteDataReader reader = await searchCommand.ExecuteReaderAsync();

				List<MessageEntry> results = new();

				while (reader.Read())
				{
					//LogWithTime($"Find a entry id {reader.GetInt64(0)}");
					MessageEntry result = new(id: reader.GetInt64(0),
											  date: reader.GetDateTime(1),
											  from_name: reader.GetString(2),
											  from_id: reader.GetInt64(3),
											  text: reader.GetString(4),
											  reply_to_message_id: reader.GetInt64(5),
											  forwarded_from: reader.GetString(6));
					results.Add(result);
				}

				connection.Close();

				return results;
			}
			catch (Exception e)
			{
				LogWithTime($"Error when searching database {e.Message}");
				return null;
			}
		}
		
		private static async Task<MessageEntry?> SearchDatabase(long id)
		{
			try
			{
				connection.Open();

				SqliteCommand searchCommand = connection.CreateCommand();

				searchCommand.CommandText =
				@"
					SELECT *
					FROM messages
					WHERE id = $id;
				";

				searchCommand.Parameters.AddWithValue("$id", id);

				using SqliteDataReader reader = await searchCommand.ExecuteReaderAsync();

				MessageEntry? result = null;

				while (reader.Read())
				{
					result = new(id: reader.GetInt64(0),
								 date: reader.GetDateTime(1),
								 from_name: reader.GetString(2),
								 from_id: reader.GetInt64(3),
								 text: reader.GetString(4),
								 reply_to_message_id: reader.GetInt64(5),
								 forwarded_from: reader.GetString(6));
				}

				connection.Close();

				return result;
			}
			catch (Exception e)
			{
				LogWithTime($"Error when searching database {e.Message}");
				return null;
			}
		}

		private static InlineKeyboardMarkup MakeResultKeyboard(List<MessageEntry> entries, int pageNum)
		{
			// build keyboard

			// option list
			List<List<InlineKeyboardButton>> keyboard = new();
			for (int i = 0; i < 2; i++)
			{
				List<InlineKeyboardButton> row = new();
				
				for (int j = 0; j < 5; j++)
				{
					int buttonNum = i * 5 + j;

					if (buttonNum >= entries.Count)
						break;

					// use callbackData to send chat id
					string callbackData = entries[buttonNum].id.ToString();

					row.Add(InlineKeyboardButton.WithCallbackData($"{buttonNum + 1}", callbackData));
				}

				if (row.Count > 0)
					keyboard.Add(row);			
			}

			// page option
			List<InlineKeyboardButton> option = new();
			if (pageNum > 0)
				option.Add(InlineKeyboardButton.WithCallbackData($"⬅ Page {pageNum}", $"p{pageNum - 1}"));

			if (entries.Count >= 10)
				option.Add(InlineKeyboardButton.WithCallbackData($"Page {pageNum + 2} ➡", $"p{pageNum + 1}"));
			keyboard.Add(option);

			return new InlineKeyboardMarkup(keyboard);
		}

		private static InlineKeyboardMarkup MakeArchiveKeyboard(long targetId, MessageEntry entry)
		{
			long replyId = entry.reply_to_message_id;
			long groupURL = Math.Abs(long.Parse(secret["groupId"])) - 1000000000000;

			List<List<InlineKeyboardButton>> rows = new();

			List<InlineKeyboardButton> row1 = new();
			row1.Add(InlineKeyboardButton.WithUrl("View in telegram", $"https://t.me/c/{groupURL}/{targetId}"));
			rows.Add(row1);

			if (replyId != 0)
			{
				List<InlineKeyboardButton> row2 = new();
				row1.Add(InlineKeyboardButton.WithUrl("View reply in telegram", $"https://t.me/c/{groupURL}/{replyId}"));

				rows.Add(row2);
			}


			InlineKeyboardMarkup inlineKeyboard = new(rows);
			return inlineKeyboard;
		}

		private static string GenerateSearchResultText(List<MessageEntry> entries, int pageNum)
		{
			StringBuilder sb = new();

			sb.AppendLine($"<b>Results</b> <code>(Page {pageNum + 1})</code>");

			for (int i = 0; i < entries.Count; i++)
				sb.AppendLine($"<b>{i + 1}.</b> {entries[i].ToSearchListString()}");

			if (entries.Count == 0)
			{
				sb.AppendLine($"<code>No more results</code>");
			}

			return sb.ToString();
		}

		private static void ParseSarchTerm(Message message, out bool forceFTS5, out bool forceLIKE, out string msg)
		{
			forceFTS5 = message.Text.StartsWith("/searchfts5");
			forceLIKE = message.Text.StartsWith("/searchlike");
			msg = message.Text.Replace("/searchlike", string.Empty)
							  .Replace("/searchfts5", string.Empty)
							  .Replace("/search", string.Empty)
							  .Replace(BotName, string.Empty)
							  .Trim();
		}

		private static void LogWithTime(string message)
		{
			Console.WriteLine($"[{DateTime.Now}] {message}");
		}

		#endregion
	}
}