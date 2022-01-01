namespace DidiIndexingBot.RecordImporter.Models
{
	public class RootObject
	{
		public string name { get; set; }
		public string type { get; set; }
		public int id { get; set; }
		public Message[] messages { get; set; }
	}
}