using System;
using System.IO;
using System.Text;

namespace Client;

/// <summary>
/// Reads and writes strings to an IOStream using a custom protocol to serialize the packets.
/// <remarks>Both the server and client shoud use this class to receive and send respectively to manage internal custom protocol</remarks>
/// </summary>
public class StreamString
{
    private readonly Stream _ioStream;
    private readonly UnicodeEncoding _streamEncoding;

    public StreamString(Stream ioStream)
    {
        _ioStream = ioStream;
        _streamEncoding = new UnicodeEncoding();
    }

    /// <summary>
    /// Reads the string.
    /// </summary>
    /// <returns></returns>
    public string ReadString()
    {
        // How many bytes should I (the server) expect for the message length?
        var lenBytes = _ioStream.ReadByte();
        if (lenBytes != 4)
        {
            throw new InvalidOperationException("The message length must be represented in the next 4 bytes");
        }

        var messageLength = new byte[lenBytes];
        _ioStream.Read(messageLength, 0, lenBytes);

        // Interpret the expected bytes as the length of the incomming message
        var actualLength = BitConverter.ToInt32(messageLength, 0);

        // read the rest of actually string message as determined by the actualLength above
        var inBuffer = new byte[actualLength];
        _ioStream.Read(inBuffer, 0, actualLength);

        return _streamEncoding.GetString(inBuffer);
    }

    /// <summary>
    /// Writes the string.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns></returns>
    public int WriteString(string value)
    {
        var outBuffer = _streamEncoding.GetBytes(value);

        // Determine how many bytes it will take to store a number as large as outBuffer.Length
        // It will always be 4!
        var lengthBytes = BitConverter.GetBytes(outBuffer.Length);

        // Tell the server to expect lengthBytes that it should use to interpret as the length of bytes it must use to interpret the size of the message size
        _ioStream.WriteByte((byte)lengthBytes.Length);

        // Now send the actual bytes
        _ioStream.Write(lengthBytes, 0, lengthBytes.Length);

        // Now send the actual message which is of a length that can be stored in lengthBytes of bytes.
        _ioStream.Write(outBuffer, 0, outBuffer.Length);
        _ioStream.Flush();

        return outBuffer.Length + 2;
    }
}
