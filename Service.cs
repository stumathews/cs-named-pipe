/****************************** Module Header ******************************\
* Module Name:  TheService.cs
* Project:      TheService
\***************************************************************************/

#region Using directives

using System;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Pipes;
using System.Management.Automation;
using System.Management.Automation.Remoting;
using System.Management.Automation.Runspaces;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading;

#endregion


namespace Service
{    
    public partial class TheService : ServiceBase
    {        
        
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Need to know if an error occured so the service doesn stop on unhandled exception")]
        public TheService()
        {

            try
            {
                InitializeComponent();
                LogMessage("TheService() constructor called.", EventLogEntryType.Information);
            }
            catch (Exception exception)
            {
                LogUnhandledException(exception);
            }
        }

        private void LogUnhandledException(Exception exception)
        {
            var fmt = string.Format(CultureInfo.CurrentCulture, "An unhandled exception occured while initializing the Service. Message = {0} StackStace={1}", exception.Message, exception.StackTrace);
            LogMessage(fmt, EventLogEntryType.Error);
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Need to know if an error occured so the service doesn stop on unhandled exception")]
        protected override void OnStart(string[] args)
        {

            try
            {
                LogMessage("TheService: OnStart() called.", EventLogEntryType.Information);
                StartNamedPipeServer();
            }
            catch (Exception exception)
            {
                LogUnhandledException(exception);
            }

        }

        /// <summary>
        /// Logs the message to the event viewer
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="type"></param>
        private void LogMessage(string message, EventLogEntryType type )
        {
            var fmt = string.Format(CultureInfo.CurrentCulture, "{0}: {1}", ServiceName, message);
            switch (type)
            {
                    case EventLogEntryType.Error:
                       
                        break;
                    case EventLogEntryType.Information:
                        
                        break;
                case EventLogEntryType.Warning:
                   
                    break;
            }
        }

        /// <summary>
        /// When implemented in a derived class, executes when a Stop command is sent to the service by the Service Control Manager (SCM). Specifies actions to take when a service stops running.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Need to know if an error occured so the service doesn stop on unhandled exception")]
        protected override void OnStop()
        {
            try
            {
                LogMessage("TheService: Stop() called.", EventLogEntryType.Information);
                
                ShutdownPipeServer();
            }
            catch (Exception exception)
            {
                LogUnhandledException(exception);
            }
        }

        private void ShutdownPipeServer()
        {
            // Ask the named pipe server to stop itself gracefully. This is required so that a listening thread isnt waiting for connections which will prevent shutdown of this service
            // Basically this will be the last connection it will accept. Start the service to being again or call StartNamePipeServer()
            var pipeClient = new NamedPipeClientStream(".", StringConstants.TechServicePipeName, PipeDirection.InOut, PipeOptions.WriteThrough, TokenImpersonationLevel.Impersonation);
            pipeClient.Connect();
            ServiceBrokerProtocolHelper.SendRequest(new Request { RequestTask = RequestTask.StopNamedPipeServer }, pipeClient, (function, message) => LogMessage(message, EventLogEntryType.Information));
        }

        /// <summary>
        /// Starts the named pipe server, waits for messages
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Need to know if an error occured so the service doesn stop on unhandled exception")]
        private void StartNamedPipeServer()
        {
            new Thread(() =>
            {
                var done = false;
                do
                {
                    NamedPipeServerStream pipeServer = null;
                    try
                    {

                        var security = new PipeSecurity();
                        security.AddAccessRule(new PipeAccessRule(@"NT Service\AccountName", PipeAccessRights.FullControl, AccessControlType.Allow));
                        security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User, PipeAccessRights.FullControl, AccessControlType.Allow));
                        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null), PipeAccessRights.ReadWrite, AccessControlType.Allow));

                        pipeServer = new NamedPipeServerStream(StringConstants.TechServicePipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Message, PipeOptions.WriteThrough, 1024, 1024, security);
                        var threadId = Thread.CurrentThread.ManagedThreadId;

                        LogMessage("Waiting for connection on thread..." + threadId, EventLogEntryType.Information);
                        pipeServer.WaitForConnection();

                        var ss = new StreamString(pipeServer);
                        var rawProtocolRequest = ss.ReadString();

                        var request = ServiceBrokerProtocolHelper.Deserialize<Request>(rawProtocolRequest);
                        var noResultResponse = new Response {MessageBody = String.Empty, ResponseCode = ResponseCode.None};
                        var response = noResultResponse;

                        LogMessage(string.Format(CultureInfo.CurrentCulture, "Received new message from plugin. Type={0} MessageBody={1}", request.RequestTask, request.MessageBody), EventLogEntryType.Information);

                        try
                        {
                            switch (request.RequestTask)
                            {
                                case RequestTask.Task1:
                                    Task1(request.MessageBody);
                                    response = noResultResponse;
                                    break;
                                case RequestTask.Task2:
                                    Task2(request.MessageBody);
                                    response = noResultResponse;
                                    break;
                                
                                case RequestTask.StopNamedPipeServer:
                                    done = true;
                                    LogMessage("Shutting down named pipe server thread on request.", EventLogEntryType.Information);
                                    response = noResultResponse;
                                    break;
                                default:
                                    LogMessage("Unknown message received from broker plugin:" + rawProtocolRequest, EventLogEntryType.Error);
                                    response = noResultResponse;
                                    break;
                            }
                        }
                        catch (Exception e)
                        {
                            // Problem while running any of the admin functions? Send back an error response
                            var logMessage = "Error occured while running protocol request:" + e.Message;
                            LogMessage(logMessage, EventLogEntryType.Error);
                            response = new Response {ResponseCode = ResponseCode.Error, MessageBody = logMessage};
                        }
                        finally
                        {
                            // We will *always* send the response of some sort. 
                            // This serves in the very least as an acknowledgement of pipe function being finished either successfully or unsuccessfully depending on the response code
                            LogMessage(string.Format(CultureInfo.CurrentCulture, "Sending response of '{0}' type='{1}'", response.MessageBody, response.ResponseCode), EventLogEntryType.Information);
                            ss.WriteString(ServiceBrokerProtocolHelper.Serialize(response));
                        }
                    }
                    catch (Exception e)
                    {
                        // Problem with the named pipe functionality? Send back an error response
                        var message = string.Format(CultureInfo.CurrentCulture, "Unexpected exception occured setting up named pipe. Message = '{0}', StackTrace = {1}", e.Message, e.StackTrace);
                        LogMessage(message, EventLogEntryType.Error);
                    }
                    finally
                    {
                        LogMessage("Finished with connection...", EventLogEntryType.Information);
                        // Note if this thread is aborted such as when the service restarts, this is guarenteed still to run, freeing up resources in this thread...
                        if (pipeServer != null)
                        {
                            LogMessage("Closing server pipe.", EventLogEntryType.Information);
                            pipeServer.Close();
                        }
                    }
                } while (!done);
            }).Start();
        }
    }
}
