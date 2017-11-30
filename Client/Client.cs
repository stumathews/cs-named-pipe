using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Permissions;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Microsoft.PowerShell;
using Microsoft.Win32;

namespace TheNameSpace
{
    public class Client
    {
        /// <summary>
        /// The log message function the customer provides us and that we'll use
        /// </summary>
        private readonly Action<string,string> _logMessageFunction;
        private static readonly object ThreadLock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="TechService"/> class.
        /// </summary>
        /// <param name="logFunction">The custom log function to use for printing messages.</param>
        public TechService(Action<string, string> logFunction)
        {
            _logMessageFunction = logFunction;
        }

        /// <summary>
        /// Sends a synchonous/blocking message to the Tech service.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <remarks>This function blocks until it can connect to the Tech service, ie it waits until the server can make the connection.</remarks>
        /// <returns>A response from the service</returns>
        public Response SendMessage(Request request)
        {
            // This lock ensures that no other client thread can connect until we're done this function(which includes this function getting a valid response first)
            lock (ThreadLock)
            {
                LogMessage(MethodBase.GetCurrentMethod().Name,"Sending message to service...");
                var pipeClient = new NamedPipeClientStream(".", StringConstants.TechServicePipeName, PipeDirection.InOut, PipeOptions.WriteThrough, TokenImpersonationLevel.Impersonation);
                
                LogMessage(MethodBase.GetCurrentMethod().Name,"Waiting to connect to service...");
                pipeClient.Connect();
                
                LogMessage(MethodBase.GetCurrentMethod().Name,"Connected to service.");
                return ServiceBrokerProtocolHelper.SendRequest(request, pipeClient, LogMessage);
            }
        }
        
        private void LogMessage(string method, string message)
        {
            if (_logMessageFunction != null)
            {
                _logMessageFunction(method, message);
            }
        }
        
    }
}
