using System;
using System.Text;

namespace DidiIndexingBot
{
	public struct MessageEntry
	{
		public long id { get; }
		public DateTime date { get; }
		public string from_name { get; }
		public long from_id { get; }
		public long reply_to_message_id { get; }
		public string forwarded_from { get; }
		public string text { get; }

		public MessageEntry(long id, DateTime date, string from_name, long from_id, long reply_to_message_id, string forwarded_from, string text)
		{
			this.id = id;
			this.date = date;
			this.from_name = from_name;
			this.from_id = from_id;
			this.reply_to_message_id = reply_to_message_id;
			this.forwarded_from = forwarded_from;
			this.text = text;
		}

		public string ToSearchListString()
		{
			return $"<b>{EscapeHTMLTag(from_name)}({date:d}): </b>{EscapeHTMLTag(text)}";
		}

		public string ToArchiveString()
		{
			StringBuilder sb = new();

			long groupURL = Math.Abs(long.Parse(Program.secret["groupId"])) - 1000000000000;

			// message info
			sb.AppendLine("<code>=== Message Archive ===</code>");

			sb.AppendLine();
			sb.AppendLine(EscapeHTMLTag(text));
			sb.AppendLine();

			// info
			sb.AppendLine("<code>=== Message Info ===</code>");

			// Sender info
			sb.AppendLine($"<code>USR:</code> {EscapeHTMLTag(from_name)}");

			// time
			sb.AppendLine($"<code>D/T:</code> {date:s}");

			// uid
			sb.AppendLine($"<code>UID:</code> {from_id}"); // avoid mentioning user
			//sb.AppendLine($"<code>UID:</code> <a href=\"tg://user?id={from_id}\">{from_id}</a> ");

			// message id
			sb.AppendLine($"<code>MID:</code> <a href=\"https://t.me/c/{groupURL}/{id}\">{id}</a>");

			// Reply info (if any)
			if (reply_to_message_id != 0)
				sb.AppendLine($"<code>REP:</code> <a href=\"https://t.me/c/{groupURL}/{reply_to_message_id}\">{reply_to_message_id}</a>");

			// Forward info (if any)
			if (!string.IsNullOrEmpty(forwarded_from))
				sb.AppendLine($"<code>FWD:</code> {EscapeHTMLTag(forwarded_from)}");

			return sb.ToString();
		}

		public string ToWhoSaidString()
		{
			return EscapeHTMLTag(text);
		}

		public string ToWhoSaidExplanationString()
		{
			long groupURL = Math.Abs(long.Parse(Program.secret["groupId"])) - 1000000000000;
			return $"This was said by {EscapeHTMLTag(from_name)} at {date:s}.\n" +
				   $"<code>MID:</code> <a href=\"https://t.me/c/{groupURL}/{id}\">{id}</a>";
		}

		private static string EscapeHTMLTag(string input)
		{
			return input.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
		}
	}
}