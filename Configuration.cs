using System;
using System.Text.Json.Serialization;

namespace ResultsMailer
{
	sealed class Configuration
	{
		public sealed class DataConfiguration
		{
			[JsonPropertyName("file")]
			public string File { get; set; }

			[JsonPropertyName("columns")]
			public int Columns { get; set; } = -1;

			[JsonPropertyName("separator")]
			public char Separator { get; set; } = ';';

			[JsonPropertyName("header-rows")]
			public int HeaderRows { get; set; } = 0;

			[JsonPropertyName("to-columns")]
			public int[] ToColumns { get; set; }

			[JsonPropertyName("exclude-if-empty-column")]
			public int ExcludeIfEmptyColumn { get; set; }

			[JsonPropertyName("exclude-if-non-empty-column")]
			public int ExcludeIfNonEmptyColumn { get; set; }

			public bool Check(Action<string> reportError)
			{
				if(string.IsNullOrWhiteSpace(File))
				{
					reportError("Missing or empty data file name.");
					return false;
				}
				if(Columns < 0)
				{
					reportError("Invalid number of columns or number of columns not specified.");
					return false;
				}
				return true;
			}
		}

		public sealed class TemplateConfiguration
		{
			[JsonPropertyName("file")]
			public string File { get; set; }

			[JsonPropertyName("subject")]
			public string Subject { get; set; }

			public bool Check(Action<string> reportError)
			{
				if(string.IsNullOrWhiteSpace(File))
				{
					reportError("Missing or empty template file name.");
					return false;
				}
				if(string.IsNullOrWhiteSpace(Subject))
				{
					reportError("Missing or empty email subject.");
					return false;
				}
				return true;
			}
		}

		public sealed class EmailAddress
		{
			[JsonPropertyName("name")]
			public string Name { get; set; }

			[JsonPropertyName("email")]
			public string EMail { get; set; }

			public bool Check(Action<string> reportError)
			{
				if(string.IsNullOrWhiteSpace(EMail))
				{
					reportError("Missing or invalid email address.");
					return false;
				}
				return true;
			}
		}

		public sealed class ServerConfiguration
		{
			[JsonPropertyName("host")]
			public string Host { get; set; }

			[JsonPropertyName("port")]
			public int Port { get; set; }

			[JsonPropertyName("login")]
			public string Login { get; set; }

			[JsonPropertyName("password")]
			public string Password { get; set; }

			public bool Check(Action<string> reportError)
			{
				if(string.IsNullOrWhiteSpace(Host))
				{
					reportError("Missing or invalid server hostname.");
					return false;
				}
				if(string.IsNullOrWhiteSpace(Login))
				{
					reportError("Missing or invalid server login name.");
					return false;
				}
				if(string.IsNullOrWhiteSpace(Password))
				{
					reportError("Missing or invalid server password.");
					return false;
				}
				return true;
			}
		}

		[JsonPropertyName("log")]
		public string Log { get; set; }

		[JsonPropertyName("data")]
		public DataConfiguration Data { get; set; }

		[JsonPropertyName("template")]
		public TemplateConfiguration Template { get; set; }

		[JsonPropertyName("sender")]
		public EmailAddress Sender { get; set; }

		[JsonPropertyName("reply-to")]
		public EmailAddress ReplyTo { get; set; }

		[JsonPropertyName("server")]
		public ServerConfiguration Server { get; set; }

		public bool Check(Action<string> reportError)
		{
			if(string.IsNullOrWhiteSpace(Log))
			{
				reportError("Missing or empty log file name.");
				return false;
			}
			if(Data == null)
			{
				reportError("Missing data configuration.");
				return false;
			}
			if(Template == null)
			{
				reportError("Missing template configuration.");
				return false;
			}
			if(Sender == null)
			{
				reportError("Missing sender configuration.");
				return false;
			}
			if(Server == null)
			{
				reportError("Missing server configuration.");
				return false;
			}
			return Data.Check(reportError)
				&& Template.Check(reportError)
				&& Sender.Check(reportError)
				&& (ReplyTo?.Check(reportError) ?? true)
				&& Server.Check(reportError);
		}
	}
}
