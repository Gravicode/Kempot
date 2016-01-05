//Written by Mif Masterz @ Gravicode
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.NetduinoPlus;
using System.Text;
using System.Collections;

namespace Kempot
{

    public class Program
    {
        public static void Main()
        {
            //wait for connection
            if (!Microsoft.SPOT.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()[0].IsDhcpEnabled)
            {
                // using static IP
                while (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable()) ; // wait for network connectivity
            }
            else
            {
                // using DHCP
                while (IPAddress.GetDefaultLocalAddress() == IPAddress.Any) ; // wait for DHCP-allocated IP address
            }

            //allow the use of DHCP for the Network interface
            //Microsoft.SPOT.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()[0].EnableDhcp();
            RemoteDevice myDevice = new RemoteDevice();

            //waiting forever
            Thread.Sleep(Timeout.Infinite);
        }

    }

    public class RemoteDevice
    {
        public WebServer webServer;
        ArrayList DevicePorts;
        public RemoteDevice()
        {
            DevicePorts = new ArrayList();
            DevicePorts.Add(new OutputPort(Pins.GPIO_PIN_D0, false));
            DevicePorts.Add(new OutputPort(Pins.GPIO_PIN_D1, false));
            DevicePorts.Add(new OutputPort(Pins.GPIO_PIN_D2, false));
            DevicePorts.Add(new OutputPort(Pins.GPIO_PIN_D3, false));
            DevicePorts.Add(new OutputPort(Pins.GPIO_PIN_D4, false));
            DevicePorts.Add(new OutputPort(Pins.GPIO_PIN_D5, false));
            DevicePorts.Add(new OutputPort(Pins.GPIO_PIN_D6, false));
            DevicePorts.Add(new OutputPort(Pins.GPIO_PIN_D7, false));
            DevicePorts.Add(new OutputPort(Pins.GPIO_PIN_D8, false));
            DevicePorts.Add(new OutputPort(Pins.GPIO_PIN_D9, false));
            DevicePorts.Add(new OutputPort(Pins.GPIO_PIN_D10, false));
            DevicePorts.Add(new OutputPort(Pins.GPIO_PIN_D11, false));
            DevicePorts.Add(new OutputPort(Pins.GPIO_PIN_D12, false));
            DevicePorts.Add(new OutputPort(Pins.GPIO_PIN_D13, false));
            DevicePorts.Add(new OutputPort(Pins.ONBOARD_LED, false));

            webServer = new WebServer();
            webServer.ReceivedRequestEvent += webServer_ReceivedRequestEvent;
            webServer.ListenRequests();
        }

        void webServer_ReceivedRequestEvent(string request)
        {

            if (
                                (request.IndexOf("GET /index.htm") == 0) || //index page requested?
                                (request.IndexOf("GET / HTTP") == 0) //root requested?
               )
            {
                String response = GetIndexPage();
                webServer.Serve(response, WebServer.MimeType.html);
            }

            else

                if (request.IndexOf("GET /STATUS") >= 0)
                {
                    String response = getDeviceState();
                    webServer.Serve(response, WebServer.MimeType.xml);
                }
                else
                {
                    bool isSwitched = false;
                    for (int i = 0; i <= 14; i++)
                    {
                        if (request.IndexOf("GET /PIN" + i + "_ON") >= 0)
                        {
                            String response = BuildXMLMessage(SwitchDevice(i, true));
                            webServer.Serve(response, WebServer.MimeType.xml);
                            isSwitched = true;
                            break;
                        }
                        else if (request.IndexOf("GET /PIN" + i + "_OFF") >= 0)
                        {
                            String response = BuildXMLMessage(SwitchDevice(i, false));
                            webServer.Serve(response, WebServer.MimeType.xml);
                            isSwitched = true;
                            break;
                        }
                        /*
                        else if (request.IndexOf("GET /PIN" + i + "_STATUS") >= 0)
                        {
                            String response = BuildXMLMessage(((OutputPort)DevicePorts[i]).Read().ToString());
                            webServer.Serve(response, WebServer.MimeType.xml);
                            isSwitched = true;
                            break;
                        }*/
                    }
                    if (!isSwitched)
                    {
                        webServer.ServeWith404(String.Empty);
                    }
                }
        }

        private string BuildXMLMessage(string Message)
        {
            string BodyStr = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<message><voice>" + Message + "</voice></message>";
            return BodyStr;
        }

        /// <summary>
        /// Gets the index page from the resources.
        /// </summary>
        /// <returns></returns>

        private string GetIndexPage()
        {
            string IP = Microsoft.SPOT.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()[0].IPAddress;
            var content = @"<html><head></head><body><h1>Kempot Mini Web Server is Up</h1>
                <ul><li>Cek PIN Status : <a href='http://" + IP+ "/STATUS'>Check!</a></li>";
            for (int i = 0; i < 15; i++)
            {
                content += "<li>Change PIN " + i + " : <a href='http://" + IP + "/PIN" + i + "_ON'>ON</a> / <a href='http://" + IP + "/PIN" + i + "_OFF'>OFF</a></li>";            
            }
            content += "</ul><br/><p>PIN 14 is on board LED..</p></body></html>";
            return content;
        }
        /// <summary>
        /// Switch device based on params
        /// </summary>
        private string SwitchDevice(int DeviceID, bool State)
        {
            OutputPort selectedPort = (OutputPort)DevicePorts[DeviceID];

            if (selectedPort != null)
            {
                selectedPort.Write(State);
                return "[DEVICE] is " + (State ? "ON" : "OFF");
            }
            return "There is no device using that port";
        }

        private string getDeviceState()
        {
            string Relays = string.Empty;
            for (int i = 0; i <= 14; i++)
            {
                bool state = ((OutputPort)DevicePorts[i]).Read();
                Relays += "<pin id=\"" + i + "\">" + state + "</pin>";
            }
            string DeviceXML = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<devices>" + Relays + "</devices>";
            return DeviceXML;
        }
    }

    public class WebServer : IDisposable
    {
        /// <summary>
        /// Socket to listen to web requests.
        /// </summary>
        public enum MimeType { html, xml, json };
        private Socket _socket = null;
        const int WebServerPort = 80;
        Socket connectionSocket;
        //private OutputPort _led = new OutputPort(Pins.ONBOARD_LED, false);
        /// <summary>
        /// An open connection to onbaord led so we can blink it with every request
        /// </summary>

        public delegate void IncomingRequestEventHandler(string request);
        public event IncomingRequestEventHandler ReceivedRequestEvent;
        private void CallRequestEvent(string request)
        {
            // Event will be null if there are no subscribers
            if (ReceivedRequestEvent != null)
            {
                ReceivedRequestEvent(request);
            }
        }

        /// <summary>
        /// Creates a webserver, listening on port 80.
        /// </summary>
        /// <remarks>. It expects to get it's IP address through
        /// DHCP, where it acts as client.
        /// The data served comes from the provided collector.</remarks>
        public WebServer()
        {
            //wait till netduino get network address
            //Thread.Sleep(5000);
            //Initialize Socket class
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //Request and bind to an IP from DHCP server
            _socket.Bind(new IPEndPoint(IPAddress.Any, WebServerPort));
            //Debug print our IP address
            Debug.Print("Device IP: " + Microsoft.SPOT.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()[0].IPAddress);
            //Start listen for web requests, with short queue, to keep the load low on the netduino
            _socket.Listen(3);

        }

        /// <summary>
        /// Continuously accepts requests and serves them.
        /// </summary>
        public void ListenRequests()
        {
            while (true)
            {
                using (connectionSocket = _socket.Accept()) //serve a new connection request
                {
                    //Get clients IP
                    IPEndPoint clientIP = connectionSocket.RemoteEndPoint as IPEndPoint;
                    EndPoint clientEndPoint = connectionSocket.RemoteEndPoint;
                    int bytesReceived = connectionSocket.Available;
                    if (bytesReceived > 0)
                    {
                        try
                        {
                            //Get request
                            byte[] buffer = new byte[bytesReceived];
                            int byteCount = connectionSocket.Receive(buffer, bytesReceived, SocketFlags.None);
                            string request = new string(Encoding.UTF8.GetChars(buffer));
                            Debug.Print("request accept:" + request);
                            CallRequestEvent(request);
                        }
                        catch (Exception ex)
                        {
                            Debug.Print("request error: " + ex.Message);
                        }

                    }
                }
            }
        }

        /// <summary>
        /// Serves the response to the client with a status code of 200 OK.
        /// </summary>
        /// <param name="response">The response to send.</param>
        /// <param name="socket">The socket to send the response with.</param>
        public void Serve(string response, MimeType tipe)
        {
            string mimestr = string.Empty;
            switch (tipe)
            {
                case MimeType.html:
                    mimestr = "text/html";
                    break;
                case MimeType.json:
                    mimestr = "application/json";
                    break;
                case MimeType.xml:
                    mimestr = "text/xml";
                    break;
            }
            string content = "HTTP/1.1 200 OK\r\n" +
"Content-Type: " + mimestr + "; charset=utf-8\r\n\r\n" + response;
            SendResponse(content);
        }
        /// <summary>
        /// Sends the header and response to the client using the given socket. Blinks the LED afterwards.
        /// </summary>
        /// <param name="response"></param>
        /// <param name="socket"></param>
        /// <param name="header"></param>
        private void SendResponse(string response)
        {
            //socket.Send(Encoding.UTF8.GetBytes(header), header.Length, SocketFlags.None);
            connectionSocket.Send(Encoding.UTF8.GetBytes(response), response.Length, SocketFlags.None);
            var sendStream = new NetworkStream(connectionSocket, false);
            Blink();
        }

        /// <summary>
        /// Serves the response to the client with a status code of 404 Not Found.
        /// </summary>
        /// <param name="response">The response to send.</param>
        /// <param name="socket">The socket to send the response with.</param>
        public void ServeWith404(string response)
        {
            string header = "HTTP/1.0 404 Not Found\r\nContent-Type: text; charset=utf-8\r\nContent-Length: " + response.Length.ToString() + "\r\nConnection: close\r\n\r\n";
            SendResponse(header);
        }

        /// <summary>
        /// Blinks the onboard LED
        /// </summary>
        private void Blink()
        {
            Debug.Print("Response sent");
            //_led.Write(true);
            //Thread.Sleep(100);
            //_led.Write(false);
        }

        #region IDisposable Members
        ~WebServer()
        {
            Dispose();
        }
        public void Dispose()
        {
            if (_socket != null)
                _socket.Close();
        }
        #endregion
    }
}
