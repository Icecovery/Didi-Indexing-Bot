namespace DidiIndexingBot.RecordImporter.Models
{
	public class Poll
	{
		public string question { get; set; }
		public bool closed { get; set; }
		public int total_voters { get; set; }
		public Answer[] answers { get; set; }
	}
}