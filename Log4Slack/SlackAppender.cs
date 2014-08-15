using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net.Appender;

namespace Log4Slack {
    public class SlackAppender : AppenderSkeleton {
        protected override void Append(log4net.Core.LoggingEvent loggingEvent) {
            throw new NotImplementedException();
        }
    }
}
