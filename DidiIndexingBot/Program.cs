using DidiIndexingBot.RecordImporter;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace DidiIndexingBot
{

	/*
		Commands Cheat Sheet

		/search {term}				- Search message that contains {term}
		/viewarchive {message id}	- View archive for message with that id
		/debug@Didi_Indexing_Bot	- Show debug message

		Bot Father format
		
		search - /search {term}: Search message that contains {term}
		viewarchive - /viewarchive {message id}: View archive for message with that id
		debug - Show debug message
		
	 */
	public static class Program
	{
		private static readonly string BotName = "@Didi_Indexing_Bot";
		private static readonly string buildID = "Build 2022-01-01-01";
		
		// Secret configuration
		private static readonly IConfigurationRoot configuration = 
			new ConfigurationBuilder()
				.AddUserSecrets(typeof(Program).Assembly)
				.Build();
		private static readonly TelegramBotClient Bot = new(configuration["botAPIKey"]);

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

			Bot.StartReceiving(HandleUpdateAsync, HandleErrorAsync, cancellationToken: cts.Token);
            Console.Write(".");

            Console.Write("Booted up.\n");
            Console.WriteLine("=================================");
            Console.ResetColor();

            //Wait to stop
            Console.ReadLine();
            cts.Cancel();
        }

		/// <summary>
		/// Handle telegram update event
		/// </summary>
		/// <param name="botClient"></param>
		/// <param name="update"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
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
			if (message.Chat.Id != long.Parse(configuration["groupId"]))
				return;

			try
			{
				switch (message.Type)
				{
					case MessageType.Text:
						// commands
						if (message.Text.StartsWith("/search "))
						{
							await HandleSearchCommand(message, cancellationToken);
							return;
						}
						if (message.Text.StartsWith("/viewarchive "))
						{
							await HandleViewArchiveCommand(message, cancellationToken);
							return;
						}
						if (message.Text.Contains(BotName) && message.Text.StartsWith("/debug"))
						{
							await HandleDebugCommand(message, cancellationToken);
							return;
						}

						//recording


						break;
					case MessageType.Photo:
						break;
					case MessageType.Audio:
						break;
					case MessageType.Video:
						break;
					case MessageType.Voice:
						break;
					case MessageType.Document:
						break;
					case MessageType.Sticker:
						break;
					case MessageType.Location:
						break;
					case MessageType.Game:
						break;
					case MessageType.Poll:
						break;
					case MessageType.Dice:
						break;
					default:
						break;
				}
			}
			catch (Exception e)
			{
				LogWithTime(e.Message);
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
			if (callbackQuery.Message.Chat.Id != long.Parse(configuration["groupId"]))
				return;

			// use callbackQuery.Data to find message
			await Bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, $"You clicked button with data {callbackQuery.Data}", replyToMessageId: callbackQuery.Message.ReplyToMessage.MessageId, cancellationToken: cancellationToken);

			// delete old message
			await Bot.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);
		}			

		/// <summary>
		/// Handle Search Command
		/// </summary>
		/// <param name="message">message that contains the command</param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		private static async Task HandleSearchCommand(Message message, CancellationToken cancellationToken)
		{
			string msg = message.Text.Replace("/search", string.Empty).Replace(BotName, string.Empty).Trim();

			LogWithTime($"Searching for {msg}...");


			// build keyboard

			// option list
			List<List<InlineKeyboardButton>> keyboard = new();
			for (int i = 0; i < 2; i++)
			{
				List<InlineKeyboardButton> row = new();
				keyboard.Add(row);
				for (int j = 0; j < 5; j++)
				{
					int buttonId = i * 5 + j;

					// use callbackData to send chat id
					string callbackData = (buttonId + 1).ToString();


					row.Add(InlineKeyboardButton.WithCallbackData($"{buttonId + 1}", callbackData));
				}
			}

			// page option
			List<InlineKeyboardButton> option = new();
			option.Add(InlineKeyboardButton.WithCallbackData($"<-", "Back"));
			option.Add(InlineKeyboardButton.WithCallbackData($"->", "Next"));
			keyboard.Add(option);


			InlineKeyboardMarkup inlineKeyboard = new(keyboard);
			await Bot.SendTextMessageAsync(message.Chat.Id, $"This is a test message", replyToMessageId: message.MessageId, replyMarkup: inlineKeyboard, cancellationToken: cancellationToken);
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
			
			LogWithTime($"View archive for {msg}...");
		}

		/// <summary>
		/// Handle debug command
		/// </summary>
		/// <param name="message">message that contains the command<</param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		private static async Task HandleDebugCommand(Message message, CancellationToken cancellationToken)
		{
			LogWithTime($"sending debug command");

			StringBuilder sb = new();
			sb.AppendLine("> Debug Message <");
			sb.AppendLine("This Bot is not working yet");
			// add more debug message here

			await Bot.SendTextMessageAsync(message.Chat.Id, sb.ToString(), replyToMessageId: message.MessageId, cancellationToken: cancellationToken);
		}

		private static void LogWithTime(string message)
		{
			Console.WriteLine($"[{DateTime.Now}] {message}");
		}
	}
}