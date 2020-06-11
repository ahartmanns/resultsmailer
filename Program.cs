using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using MailKit.Net.Smtp;
using MimeKit;
using CI = System.Globalization.CultureInfo;

namespace ResultsMailer
{
	class Program
	{
		static void Main(string[] args)
		{
			// Get and check arguments
			string command = args[0];
			if(command != "send" && command != "log")
			{
				Console.WriteLine("Error: Unsupported command.");
				return;
			}
			string configFile = args[1];
			if(!File.Exists(configFile))
			{
				Console.WriteLine("Error: Configuration file not found.");
				return;
			}
			string relativeDir = Path.GetDirectoryName(Path.GetFullPath(configFile));

			// Load configuration
			Configuration config;
			try
			{
				config = JsonSerializer.Deserialize<Configuration>(File.ReadAllText(configFile));
			}
			catch(JsonException je)
			{
				Console.WriteLine("Error reading configuration: " + je.Message);
				return;
			}
			if(!config.Check(error => Console.WriteLine("Error: " + error))) return;

			// Load and parse CSV file
			Console.Write("Loading CSV...");
			string csvFilePath = Path.IsPathFullyQualified(config.Data.File) ? config.Data.File : Path.Combine(relativeDir, config.Data.File);
			string[] csvAllLines = File.ReadAllLines(csvFilePath, Encoding.UTF8);
			var csvLines = new Span<string>(csvAllLines, config.Data.HeaderRows, csvAllLines.Length - config.Data.HeaderRows);
			var csvCells = new Span<string[]>(new string[csvLines.Length][]);
			int rowCount = 0;
			for(int i = 0; i < csvLines.Length; ++i)
			{
				string line = csvLines[i];
				var cells = new List<string>();
				bool inString = false;
				var cell = new StringBuilder();
				for(int j = 0; j < line.Length; ++j)
				{
					char c = line[j];
					if(c == config.Data.Separator)
					{
						if(inString)
						{
							cell.Append(c);
						}
						else
						{
							cells.Add(cell.ToString());
							cell.Clear();
						}
					}
					else if(c == '"')
					{
						if(inString)
						{
							if(j < line.Length - 1 && line[j + 1] == '"')
							{
								// Escaped quote
								cell.Append('"');
								++j;
							}
							else
							{
								// End of string
								inString = false;
							}
						}
						else
						{
							// Beginning of string
							inString = true;
						}
					}
					else
					{
						cell.Append(c);
					}
				}
				/*if(cell.Length != 0)*/ cells.Add(cell.ToString());
				if(cells.Count != config.Data.Columns)
				{
					Console.WriteLine();
					Console.WriteLine("Error: Expected " + config.Data.Columns.ToString(CI.InvariantCulture) + " columns in data file line " + (i + 1 + config.Data.HeaderRows).ToString(CI.InvariantCulture) + ", found " + cells.Count.ToString(CI.InvariantCulture) + ".");
					return;
				}
				if(config.Data.ExcludeIfEmptyColumn <= 0 || cells[config.Data.ExcludeIfEmptyColumn - 1].Length != 0)
				{
					csvCells[rowCount] = cells.ToArray();
					++rowCount;
				}
			}
			csvCells = csvCells.Slice(0, rowCount);
			Console.WriteLine(" " + csvCells.Length.ToString(CI.InvariantCulture) + " entries loaded.");

			// Load and instantiate template
			Console.Write("Preparing messages...");
			string templateFilePath = Path.IsPathFullyQualified(config.Template.File) ? config.Template.File : Path.Combine(relativeDir, config.Template.File);
			string template = File.ReadAllText(templateFilePath, Encoding.UTF8);
			int lineEnd = template.IndexOf('\n');
			string[] messages = new string[csvCells.Length];
			string[] tos = new string[csvCells.Length];
			string[] logs = new string[csvCells.Length];
			for(int i = 0; i < csvCells.Length; ++i)
			{
				string[] cell = csvCells[i];
				tos[i] = cell[config.Data.ToColumn - 1];
				string message = template;
				for(int j = cell.Length - 1; j >= 0; --j) // we go backwards so that we replace e.g. %10 with column 10, not with column 1 and leaving a 0
				{
					message = message.Replace("%" + (j + 1).ToString(CI.InvariantCulture), cell[j]);
				}
				if(message.Contains('%'))
				{
					Console.WriteLine();
					Console.WriteLine("Error: Missing column or \"%\" symbol in data file line " + (i + 1).ToString(CI.InvariantCulture) + ".");
					return;
				}
				messages[i] = message;
			}
			Console.WriteLine(" " + messages.Length.ToString(CI.InvariantCulture) + " messages prepared.");

			// Create and send emails
			if(command == "send")
			{
				Console.Write("Confirm by typing 'yes' to send " + messages.Length.ToString(CI.InvariantCulture) + " e-mails: ");
				string line = Console.ReadLine();
				if(line != "yes")
				{
					Console.WriteLine("Cancelled.");
					return;
				}
			}
			Console.WriteLine((command == "log" ? "Logging" : "Sending") + " " + messages.Length.ToString(CI.InvariantCulture) + " messages:");
			var senderAddress = new MailboxAddress(config.Sender.Name, config.Sender.EMail);
			var replyToAddress = config.ReplyTo == null ? null : new MailboxAddress(config.ReplyTo.Name, config.ReplyTo.EMail);
			string logFilePath = Path.IsPathFullyQualified(config.Log) ? config.Log : Path.Combine(relativeDir, config.Log);
			using(var messageLogWriter = new StreamWriter(File.Open(logFilePath, FileMode.Create, FileAccess.Write, FileShare.Read), Encoding.UTF8) { AutoFlush = true })
			using(var client = new SmtpClient())
			{
				client.Connect(config.Server.Host, config.Server.Port);
				client.Authenticate(config.Server.Login, config.Server.Password);
				for(int i = 0; i < messages.Length; ++i)
				{
					string message = messages[i];
					string to = tos[i];
					Console.WriteLine("* " + to);

					// Create email
					var mail = new MimeMessage();
					mail.From.Add(senderAddress);
					mail.To.Add(new MailboxAddress((string)null, to));
					if(replyToAddress != null) mail.ReplyTo.Add(replyToAddress);
					mail.Subject = config.Template.Subject;
					mail.Body = new TextPart() { Text = message };

					// Send email
					if(command == "send") client.Send(mail);
					messageLogWriter.WriteLine("To: " + mail.To.ToString());
					messageLogWriter.WriteLine("From: " + mail.From.ToString());
					if(replyToAddress != null) messageLogWriter.WriteLine("Reply-To: " + mail.ReplyTo.ToString());
					messageLogWriter.WriteLine("Subject: " + mail.Subject);
					messageLogWriter.WriteLine(">>>");
					messageLogWriter.WriteLine(message);
					messageLogWriter.WriteLine("<<<");
					messageLogWriter.WriteLine();
					messageLogWriter.WriteLine();
				}
				client.Disconnect(true);
			}
			Console.WriteLine("Done.");
		}
	}
}
