using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Serialization;


namespace Protocol
{

    /// <summary>
    /// Request object used to communicate from ther plugin to the service
    /// </summary>
    [Serializable]
    public class Request
    {
        /// <summary>
        /// The requested task
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "This is to be a flat object with no function as its a protocol object")]
        public RequestTask RequestTask;
        /// <summary>
        /// The message body
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "This is to be a flat object with no function as its a protocol object")]
        public string MessageBody;
        public Request() {}
    }

    /// <summary>
    /// Response object used to communicate response information from the serivce to the plugin
    /// </summary>
    [Serializable]
    public class Response
    {
        /// <summary>
        /// The response code
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "This is to be a flat object with no function as its a protocol object")]
        public ResponseCode ResponseCode;
        /// <summary>
        /// The message body
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "This is to be a flat object with no function as its a protocol object")]
        public string MessageBody;
        public Response() { }
    }

    public enum ResponseCode
    {
        /// <summary>
        /// The response code used to indicate that function was a void function, that has no response information
        /// </summary>
        None,
        /// <summary>
        /// The response code used to indicate that their was a error while executing the Tech service code(admin functions)
        /// </summary>
        Error,
    }

    /// <summary>
    /// This enum edientifies the task asked to be carried out by the service
    /// </summary>
    public enum RequestTask
    {
        Task1,
		Task2,
        StopNamedPipeServer
    }

    /// <summary>
    /// Helper utilities
    /// </summary>
    public static class ServiceBrokerProtocolHelper
    {
        /// <summary>
        /// Serializes the specified protocol object.
        /// </summary>
        /// <typeparam name="T">Object type you'd like to serialize</typeparam>
        /// <param name="protocolObject">The protocol object.</param>
        /// <returns>xml string representing the object</returns>
        public static string Serialize<T>(T protocolObject)
        {
            var xmlSerializer = new XmlSerializer(typeof (T));
            using (var sw = new StringWriter(CultureInfo.CurrentCulture))
            {
                xmlSerializer.Serialize(sw, protocolObject);
                return sw.ToString();
            }
        }

        /// <summary>
        /// Deserializes the specified raw request.
        /// </summary>
        /// <typeparam name="T">Object type you'd like to deserialize</typeparam>
        /// <param name="rawRequest">The raw request.</param>
        /// <returns>the object, deserialized</returns>
        public static T Deserialize<T>(string rawRequest)
        {
            var xmlSerializer = new XmlSerializer(typeof (T));
            using (var textReader = new XmlTextReader(new StringReader(rawRequest)))
            {
                return (T) xmlSerializer.Deserialize(textReader);
            }
        }

        public static Response SendRequest(Request request, Stream pipeClient, Action<string,string> loggingFunction) 
        {
            loggingFunction(MethodBase.GetCurrentMethod().Name,"Sending request to service...");
            var streamString = new StreamString(pipeClient);
            
            loggingFunction(MethodBase.GetCurrentMethod().Name,"Serializing request...");
            var requestXml = ServiceBrokerProtocolHelper.Serialize(request);

            loggingFunction(MethodBase.GetCurrentMethod().Name,"Transmitting request...");

            // Send request
            streamString.WriteString(requestXml);
            loggingFunction(MethodBase.GetCurrentMethod().Name,"Request sent to service.");

            loggingFunction(MethodBase.GetCurrentMethod().Name,"Receiving response from service...");
            
            //Recieve the response
            var response =  ServiceBrokerProtocolHelper.Deserialize<Response>(streamString.ReadString());
            loggingFunction(MethodBase.GetCurrentMethod().Name,string.Format(CultureInfo.CurrentCulture,"Response received from service: Code={0}, MessageBody={1}", response.ResponseCode, response.MessageBody));
            return response;
        }
    }
}
