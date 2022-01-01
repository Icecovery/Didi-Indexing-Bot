using System.Text;
using System.Text.Json;

namespace DidiIndexingBot.RecordImporter.Models
{
	public class CompoundText
	{
		public object[] elements { get; set; }

		public static string Parse(string input)
		{
			if (input.StartsWith("[") && input.EndsWith("]"))
			{
				try
				{
					StringBuilder sb = new StringBuilder();
					CompoundText text = JsonSerializer.Deserialize<CompoundText>($"{{\"elements\": {input}}}");
					foreach (object element in text.elements)
					{
						sb.Append(CompoundTextElement.Parse(element.ToString()));
					}
					return sb.ToString();
				}
				catch
				{
					return input; 
				}
			}
			return input;
		}
	}

	public class CompoundTextElement
	{
		public string type { get; set; }
		public string text { get; set; }

		public static string Parse(string input)
		{
			if (input.StartsWith("{") && input.EndsWith("}"))
			{
				try
				{
					CompoundTextElement element = JsonSerializer.Deserialize<CompoundTextElement>(input);
					return element.text;
				}
				catch
				{
					return input;
				}
			}
			return input;
		}
	}
}
