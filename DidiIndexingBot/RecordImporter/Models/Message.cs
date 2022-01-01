using System;

namespace DidiIndexingBot.RecordImporter.Models
{
	public class Message
	{
		public int id { get; set; }
		public string type { get; set; }
		public DateTime date { get; set; }
		public string actor { get; set; }
		public string actor_id { get; set; }
		public string action { get; set; }
		public string title { get; set; }
		public string[] members { get; set; }
		public object text { get; set; }
		public DateTime edited { get; set; }
		public string from { get; set; }
		public string from_id { get; set; }
		public string file { get; set; }
		public string thumbnail { get; set; }
		public string media_type { get; set; }
		public string sticker_emoji { get; set; }
		public int width { get; set; }
		public int height { get; set; }
		public int reply_to_message_id { get; set; }
		public string forwarded_from { get; set; }
		public string photo { get; set; }
		public string mime_type { get; set; }
		public int duration_seconds { get; set; }
		public string via_bot { get; set; }
		public int message_id { get; set; }
		public string inviter { get; set; }
		public string game_title { get; set; }
		public string game_description { get; set; }
		public string game_link { get; set; }
		public int game_message_id { get; set; }
		public int score { get; set; }
		public string performer { get; set; }
		public Poll poll { get; set; }
		public Location_Information location_information { get; set; }
		public string place_name { get; set; }
		public string address { get; set; }
		public int duration { get; set; }
		public int schedule_date { get; set; }
		public string author { get; set; }
	}
}