using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ParseLog
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string[] lines;

            using (var reader = new StreamReader(@"C:\Users\Trevor\Documents\HypeKT.txt"))
            {
                string fileText = reader.ReadToEnd();
                lines = fileText.Split("\n");
                reader.Close();
            }

            Regex rx = new Regex(@"hype-train", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Regex extract = new Regex(@"\{.*\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            string last = "";

            using(var writer = new StreamWriter("out.txt"))
            {
                foreach (var line in lines)
                {
                    if (rx.IsMatch(line))
                    {
                        var match = extract.Match(line);

                        if (last.CompareTo(match.Value) != 0)
                        {
                            //writer.WriteLine(match.Value.Replace("\\\"", "\""));
                            JObject? obj = JsonConvert.DeserializeObject<JObject>(match.Value);
                            JToken? messageText = obj?.SelectToken("data.message");
                            
                            if (messageText != null)
                            {
                                string? msgText = messageText.ToObject<string>();
                                if (msgText != null)
                                {
                                    JObject? messageObj = JsonConvert.DeserializeObject<JObject>(msgText);
                                    obj["data"]["message"] = messageObj;
                                }
                            }
                            
                            if (obj != null) {
                                writer.WriteLine(obj.ToString(Formatting.Indented));
                            }
                        }

                        last = match.Value;
                    }
                }
            }
        }
    }
}