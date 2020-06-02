using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MailKit.Net.Smtp;
using MimeKit;
using CI = System.Globalization.CultureInfo;

namespace ResultsMailer
{
	class Program
	{
		static void Main(string[] args)
		{
			string command = args[0];
			string csvFileName = args[1];
			string templateFileName = args[2];
			string settingsFileName = args[3];
			string logFileName = args[4];

			// Load and parse CSV file
			Console.Write("Loading CSV...");
			string[] csvLines = File.ReadAllLines(csvFileName, Encoding.UTF8);
			string[][] csvCells = new string[csvLines.Length][];
			for(int i = 0; i < csvLines.Length; ++i)
			{
				string line = csvLines[i];
				var cells = new List<string>();
				bool inString = false;
				var cell = new StringBuilder();
				for(int j = 0; j < line.Length; ++j)
				{
					char c = line[j];
					switch(c)
					{
						case ',':
							if(inString)
							{
								cell.Append(c);
							}
							else
							{
								cells.Add(cell.ToString());
								cell.Clear();
							}
							break;
						case '"':
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
							break;
						default:
							cell.Append(c);
							break;
					}
				}
				if(cell.Length != 0) cells.Add(cell.ToString());
				csvCells[i] = cells.ToArray();
			}
			Console.WriteLine(" " + csvCells.Length.ToString(CI.InvariantCulture) + " lines loaded.");

			// Load and instantiate template
			Console.Write("Preparing messages...");
			string template = File.ReadAllText(templateFileName, Encoding.UTF8);
			int lineEnd = template.IndexOf('\n');
			string subject = template[..lineEnd].Trim();
			template = template[(lineEnd + 1)..];
			lineEnd = template.IndexOf('\n');
			int toIndex = int.Parse(template[..lineEnd].Trim(), CI.InvariantCulture) - 1;
			template = template[(lineEnd + 1)..];
			string[] messages = new string[csvCells.Length];
			string[] tos = new string[csvCells.Length];
			string[] logs = new string[csvCells.Length];
			for(int i = 0; i < csvCells.Length; ++i)
			{
				string[] cell = csvCells[i];
				tos[i] = cell[toIndex];
				string message = template;
				for(int j = cell.Length - 1; j >= 0; --j) // we go backwards so that we replace e.g. %10 with column 10, not column 1
				{
					message = message.Replace("%" + (j + 1).ToString(CI.InvariantCulture), cell[j]);
				}
				if(message.Contains('%'))
				{
					Console.WriteLine();
					Console.WriteLine("Error: Missing column or \"%\" symbol in CSV file line " + (i + 1).ToString(CI.InvariantCulture) + ".");
					return;
				}
				messages[i] = message;
			}
			Console.WriteLine(" " + messages.Length.ToString(CI.InvariantCulture) + " messages prepared.");

			// Load settings
			Console.Write("Loading server configuration...");
			string[] settingsLines = File.ReadAllLines(settingsFileName, Encoding.UTF8);
			string senderName = settingsLines[0];
			string senderEmail = settingsLines[1];
			string server = settingsLines[2];
			int port = int.Parse(settingsLines[3]);
			string user = settingsLines[4];
			string password = settingsLines[5];
			Console.WriteLine(" done.");

			// Create and send emails
			Console.WriteLine("Sending:");
			using(var messageLogWriter = new StreamWriter(File.OpenWrite(logFileName)) { AutoFlush = true })
			using(var client = new SmtpClient())
			{
				client.Connect(server, port);
				client.Authenticate(user, password);
				for(int i = 0; i < messages.Length; ++i)
				{
					string message = messages[i];
					string to = tos[i];
					Console.WriteLine("* " + to);

					// Create email
					var mail = new MimeMessage();
					mail.From.Add(new MailboxAddress(senderName, senderEmail));
					mail.To.Add(new MailboxAddress((string)null, to));
					mail.Subject = subject;
					mail.Body = new TextPart() { Text = message };

					// Send email
					if(command == "send") client.Send(mail);
					messageLogWriter.WriteLine(to + ": " + message.Replace("\r", "").Replace("\n", "/"));
				}
				client.Disconnect(true);
			}
			Console.WriteLine("Done.");
		}
	}
}
