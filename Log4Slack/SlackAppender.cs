using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using log4net.Appender;
using System.Linq;
using System.Text.RegularExpressions;
using System.Drawing;

namespace Log4Slack {

    public class Mapping
    {
        public string level { get; set; }
        public string foreColor { get; set; }
        public string backColor { get; set; }
    }

    public class SlackAppender : AppenderSkeleton {
        private readonly Process _currentProcess = Process.GetCurrentProcess();
        private List<Mapping> Mappings = new List<Mapping>();

        /// <summary>
        /// Slack token.
        /// https://api.slack.com/
        /// </summary>
        ////public string Token { get; set; }

        /// <summary>
        /// Slack webhook URL, with token.
        /// </summary>
        public string WebhookUrl { get; set; }

        /// <summary>
        /// Slack channel to send log events to.
        /// </summary>
        public string Channel { get; set; }

        /// <summary>
        /// Username to post to Slack as.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// The URL of the icon to use, if any.
        /// </summary>
        public string IconUrl { get; set; }

        /// <summary>
        /// The name of the Emoji icon to use, if any.
        /// </summary>
        public string IconEmoji { get; set; }

        /// <summary>
        /// Indicates whether or not to include additional details in message attachments.
        /// </summary>
        public bool AddAttachment { get; set; }

        /// <summary>
        /// Indicates whether or not to include the exception traces as fields on message attachments.
        /// Requires AddAttachment be true.
        /// </summary>
        public bool AddExceptionTraceField { get; set; }

        /// <summary>
        /// Indicates whether or not to append the logger name to the Stack username.
        /// </summary>
        public bool UsernameAppendLoggerName { get; set; }

        /// <summary>
        /// The optional proxy configuration for outgoing slack posts
        /// </summary>
        public string Proxy { get; set; }

        /// <summary>
        /// Whether to tell Slack API to automatically link @mentions
        /// </summary>
        public bool LinkNames { get; set; }

        public Mapping mapping { set { Mappings.Add(value); } }

        protected override void Append(log4net.Core.LoggingEvent loggingEvent) {
            // Initialze the Slack client
            var slackClient = new SlackClient(WebhookUrl.Expand());
            var attachments = new List<Attachment>();

            if (AddAttachment) {
                // Set fallback string
                var theAttachment = new Attachment(string.Format("[{0}] {1} in {2} on {3}", loggingEvent.Level.DisplayName, loggingEvent.LoggerName, _currentProcess.ProcessName, Environment.MachineName));

                // Determine attachment color
                switch (loggingEvent.Level.DisplayName.ToLowerInvariant()) {
                    case "warn":
                        theAttachment.Color = "warning";
                        break;
                    case "error":
                    case "fatal":
                        theAttachment.Color = "danger";
                        break;
                }

                //override colors from config if available
                var mapping = Mappings != null ? Mappings.FirstOrDefault(m => m.level.Equals(loggingEvent.Level.DisplayName,StringComparison.InvariantCultureIgnoreCase)) : null;
                if (mapping != null)
                {
                    var color = Color.FromName(mapping.backColor);
                    var hex = color.IsKnownColor ? String.Format("#{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B) : mapping.backColor;
                    theAttachment.Color = !string.IsNullOrEmpty(hex) ? hex : theAttachment.Color;
                }

                // Add attachment fields
                theAttachment.Fields = new List<Field> {
                    new Field("Process", Value: _currentProcess.ProcessName, Short: true),
                    new Field("Machine", Value: Environment.MachineName, Short: true)
                };
                if (!UsernameAppendLoggerName)
                    theAttachment.Fields.Insert(0, new Field("Logger", Value: loggingEvent.LoggerName, Short: true));

                // Add exception fields if exception occurred
                var exception = loggingEvent.ExceptionObject;
                if (exception != null) {
                    theAttachment.Fields.Insert(0, new Field("Exception Type", Value: exception.GetType().Name, Short: true));
                    if (AddExceptionTraceField && !string.IsNullOrWhiteSpace(exception.StackTrace)) {
                        var parts = exception.StackTrace.SplitOn(1990).ToArray(); // Split call stack into consecutive fields of ~2k characters
                        for (int idx = parts.Length - 1; idx >= 0; idx--) {
                            var name = "Exception Trace" + (idx > 0 ? string.Format(" {0}", idx + 1) : null);
                            theAttachment.Fields.Insert(0, new Field(name, Value: "```" + parts[idx].Replace("```", "'''") + "```"));
                        }
                    }

                    theAttachment.Fields.Insert(0, new Field("Exception Message", Value: exception.Message));
                }

                attachments.Add(theAttachment);
            }

            var formattedMessage = (Layout != null ? Layout.FormatString(loggingEvent) : loggingEvent.RenderedMessage);
            var username = Username.Expand() + (UsernameAppendLoggerName ? " - " + loggingEvent.LoggerName : null);
            slackClient.PostMessageAsync(formattedMessage, Proxy, username, Channel.Expand(), IconUrl.Expand(), IconEmoji.Expand(), attachments, LinkNames);
        }
    }

    internal static class Extensions {
        public static string Expand(this string text) {
            return text != null ? Environment.ExpandEnvironmentVariables(text) : null;
        }

        public static IEnumerable<string> SplitOn(this string text, int numChars) {
            var SplitOnPattern = new Regex(string.Format(@"(?<line>.{{1,{0}}})([\r\n]|$)", numChars), RegexOptions.Singleline | RegexOptions.IgnoreCase);
            return SplitOnPattern.Matches(text).OfType<Match>().Select(m => m.Groups["line"].Value);
        }

        public static string FormatString(this log4net.Layout.ILayout layout, log4net.Core.LoggingEvent loggingEvent) {
            using (var writer = new StringWriter()) {
                layout.Format(writer, loggingEvent);
                return writer.ToString();
            }
        }

    }
}
