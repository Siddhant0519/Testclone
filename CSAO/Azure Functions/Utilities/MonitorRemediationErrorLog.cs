using System;
using System.Collections.Generic;
using System.Text;

namespace Utilities
{
    public class MonitorRemediationErrorLog
    {
        public string id { get; set; }
        public string ServiceType { get ; set ; }
        public string ResourceURI { get; set ; }
        public string OperationName { get ; set ; }
        public string ExceptionDetails { get; set ; }
        public string FunctionAppName { get ; set ; }
        public string SubFunctionName { get ; set ; }
        public string ErrorType { get ; set ; }
        public string ErrorCode { get ; set ; }
        public DateTime LogTimeStamp { get ; set ; }
    }
}
