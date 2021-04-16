using System;
using System.Collections.Generic;
using System.Text;

namespace Utilities
{
    public class FunctionDetails
    {
        private string functionAppKey;

        private string functionAppName;

        private string functionAppURI;

        public string FunctionAppKey { get => functionAppKey; set => functionAppKey = value; }
        public string FunctionAppName { get => functionAppName; set => functionAppName = value; }
        public string FunctionAppURI { get => functionAppURI; set => functionAppURI = value; }
    }
}
