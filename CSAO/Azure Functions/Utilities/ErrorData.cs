using System;
using System.Collections.Generic;
using System.Text;

namespace Utilities
{
    public class ErrorData
    {
        private string ruleId;

        private string ruleName;

        private string ruleDisplayName;

        private string controlId;

        private string ruleDetailHtml;

        private string serviceType;

        private string resourceURI;

        private string operationName;

        private string exceptionDetails;

        private string functionAppName;

        private string subFunctionName;

        private string errorType;

        private string errorCode;

        private string message;

        private DateTime logTimeStamp;

        public string RuleId { get => ruleId; set => ruleId = value; }
        public string RuleName { get => ruleName; set => ruleName = value; }
        public string RuleDisplayName { get => ruleDisplayName; set => ruleDisplayName = value; }
        public string ControlId { get => controlId; set => controlId = value; }
        public string RuleDetailHtml { get => ruleDetailHtml; set => ruleDetailHtml = value; }
        public string ServiceType { get => serviceType; set => serviceType = value; }
        public string ResourceURI { get => resourceURI; set => resourceURI = value; }
        public string OperationName { get => operationName; set => operationName = value; }
        public string ExceptionDetails { get => exceptionDetails; set => exceptionDetails = value; }
        public string FunctionAppName { get => functionAppName; set => functionAppName = value; }
        public string SubFunctionName { get => subFunctionName; set => subFunctionName = value; }
        public string ErrorType { get => errorType; set => errorType = value; }
        public string ErrorCode { get => errorCode; set => errorCode = value; }
        public string Message { get => message; set => message = value; }
        public DateTime LogTimeStamp { get => logTimeStamp; set => logTimeStamp = value; }
    }
}
