using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Dynamic;
using System.Net;
using System.Net.Sockets;
using System.Xml;
using System.Xml.Linq;
using System.Device.Location;
using System.Threading;
using System.Diagnostics;

namespace restaurant_locator
{
    class Program
    {
        static Hashtable zipHash = new Hashtable();
        static String APIKey = "AIzaSyAAQbevauqrUWCObSUKx9W7GIAMAc6U2mA"; //av445
        static string data = null;
        static Socket handler;
        static Double searchRadius = new Double();
        static GeoCoordinate src = null;
        static int queriesFired = 1;
        static int matchCount = 0;
        static Object senderLock = new Object();
        static Object counterLock = new Object();
        static Object queriesLock = new Object();
        static String TAMUAPIKey = "d66857a8877d48da8ace33d3f3c4d21c";
        static TimeSpan startTime = (DateTime.UtcNow - new DateTime(1970, 1, 1));
        static Double startSeconds = startTime.TotalMilliseconds;
        static TimeSpan currTime = new TimeSpan();
        static Double currSeconds = new Double();

        static void restaurantSearch(string address)
        {
            Thread tamu = null;
            Thread t = null;
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            //var requestURI = String.Format("https://maps.googleapis.com/maps/api/geocode/xml?address={0}&key={1}", Uri.EscapeDataString(address), APIKey);
            var requestURI = String.Format("https://maps.googleapis.com/maps/api/geocode/xml?address={0}", Uri.EscapeDataString(address));

            var request = WebRequest.Create(requestURI);
            var response = request.GetResponse();
            XmlDocument xmlResult = new XmlDocument();
            xmlResult.Load(response.GetResponseStream());

            String status = xmlResult.SelectSingleNode("/GeocodeResponse/status").InnerText;
            //Check status message
            if (!status.Equals("OK"))
            {
                //An error occured or there were no matches
                //Console.WriteLine("Query wasn't successful. Status code - " + status);
                sendToClient("Query wasn't successful. Status code - " + status + '\n');
                return;
            }

            int i = 1;
            String zipCode = null;

            while (i > 0)
            {
                //Picking up the postal code
                String type = xmlResult.SelectSingleNode("/GeocodeResponse/result/address_component[" + i + "]/type").InnerText;
                if (type.Equals("postal_code"))
                {
                    zipCode = xmlResult.SelectSingleNode("/GeocodeResponse/result/address_component[" + i + "]/long_name").InnerText;
                    break;
                }
                i = i + 1;
            }

            queriesFired = 1;
            matchCount = 0;
            i = 0;

            if (!(zipHash.ContainsKey(zipCode)))
            {
                //If there are no restaurants in the zipcode
                //Console.WriteLine("No matches!");
                sendToClient("No matches!\n");
                return;
            }
            else
            {
                String latitude = xmlResult.SelectSingleNode("GeocodeResponse/result/geometry/location/lat").InnerText;
                String longitude = xmlResult.SelectSingleNode("GeocodeResponse/result/geometry/location/lng").InnerText;

                Double lat = Double.Parse(latitude);
                Double lng = Double.Parse(longitude);

                src = new GeoCoordinate(lat, lng);
                ArrayList matchList = (ArrayList)zipHash[zipCode];

                foreach (String[] restaurantAddress in matchList)
                {
                    //Iterating over each restaurant in the matching zipcode
                    if (queriesFired == 5)
                    {
                        //Logic to ensure that you don't cross the query rate for the Google GeoCoding API
                        //5 queries per second
                        //t = new Thread(() => getTamuLatLong(restaurantAddress));
                        //t.Start();
                        getTamuLatLong(restaurantAddress);
                        currTime = (DateTime.UtcNow - new DateTime(1970, 1, 1));
                        currSeconds = currTime.TotalMilliseconds;

                        int interval = (int)(currSeconds - startSeconds);

                        if (interval < 1000)
                        {
                            //System.Threading.Thread.Sleep(1000 - interval);
                            continue;
                        }

                        startTime = (DateTime.UtcNow - new DateTime(1970, 1, 1));
                        startSeconds = startTime.TotalMilliseconds;
                        queriesFired = 0;
                        continue;
                    }

                    String dstAddress = restaurantAddress[9];
                    getGoogleLatLong(dstAddress);
                    queriesFired++;
                }
                //Console.WriteLine("Number of matches = " + matchCount);
                stopWatch.Stop();
            }
            sendToClient("Time elapsed = " + stopWatch.ElapsedMilliseconds.ToString() + "milliseconds \n");
            sendToClient("Number of matches = " + matchCount + '\n');
        }

        static void getGoogleLatLong(String address)
        {
            Console.WriteLine("Google");
            Double[] latlng = new Double[2];
            
            var requestURI = String.Format("https://maps.googleapis.com/maps/api/geocode/xml?address={0}&key={1}", Uri.EscapeDataString(address), APIKey);
            //var requestURI = String.Format("https://maps.googleapis.com/maps/api/geocode/xml?address={0}", Uri.EscapeDataString(address));
            var request = WebRequest.Create(requestURI);
            WebResponse response = null;

            try
            {
                response = request.GetResponse();
            }
            catch (WebException e)
            {
                return;
            }

            XmlDocument xmlResult = new XmlDocument();
            xmlResult.Load(response.GetResponseStream());

            String status = xmlResult.SelectSingleNode("/GeocodeResponse/status").InnerText;
            //Check status message
            if (!status.Equals("OK"))
            {
                //An error occured or there were no matches
                return;
            }

            String latitude = xmlResult.SelectSingleNode("GeocodeResponse/result/geometry/location/lat").InnerText;
            String longitude = xmlResult.SelectSingleNode("GeocodeResponse/result/geometry/location/lng").InnerText;

            latlng[0] = Double.Parse(latitude);
            latlng[1] = Double.Parse(longitude);
            
            GeoCoordinate dst = new GeoCoordinate(latlng[0], latlng[1]);

            //Calculate distance between the input location and the restaurant and convert to miles
            Double distance = src.GetDistanceTo(dst);
            distance = distance / 1609.344;

            if (distance <= searchRadius)
            {
                //Console.WriteLine(dstAddress + " | " + distance);
                lock (senderLock)
                {
                    sendToClient(address + " | " + distance + '\n');
                }
                lock (counterLock)
                {
                    matchCount++;
                }
            }
            return;
        }

        static void getTamuLatLong(String[] address)
        {
            Console.WriteLine("TAMU");
            Double[] latlng = new Double[2];

            var requestURI = String.Format("http://geoservices.tamu.edu/Services/Geocode/WebService/GeocoderWebServiceHttpNonParsed_V04_01.aspx?apikey={0}&version=4.01&allowties=false&format=xml&streetAddress={1}&city={2}&state={3}&zip={4}", TAMUAPIKey, Uri.EscapeDataString(address[4]), address[5], address[6], address[7]);
            
            var request = WebRequest.Create(requestURI);
            WebResponse response = null;

            try
            {
                response = request.GetResponse();
            }
            catch (WebException e)
            {
                return;
            }

            XmlDocument xmlResult = new XmlDocument();
            xmlResult.Load(response.GetResponseStream());

            String latitude = xmlResult.SelectSingleNode("WebServiceGeocodeResult/OutputGeocodes/OutputGeocode/Latitude").InnerText;
            String longitude = xmlResult.SelectSingleNode("WebServiceGeocodeResult/OutputGeocodes/OutputGeocode/Longitude").InnerText;

            latlng[0] = Double.Parse(latitude);
            latlng[1] = Double.Parse(longitude);

            GeoCoordinate dst = new GeoCoordinate(latlng[0], latlng[1]);

            //Calculate distance between the input location and the restaurant and convert to miles
            Double distance = src.GetDistanceTo(dst);
            distance = distance / 1609.344;

            if (distance <= searchRadius)
            {
                //Console.WriteLine(dstAddress + " | " + distance);
                lock (senderLock)
                {
                    sendToClient(address[9] + " | " + distance + '\n');
                }
                lock (counterLock)
                {
                    matchCount++;
                }
            }

            return;
        }

        static void sendToClient(String message)
        {
            try
            {
                byte[] messageToSend = Encoding.ASCII.GetBytes(message);
                handler.Send(messageToSend);
            } catch(Exception e) {
                Console.WriteLine("FAILED");
                return;
            }
            //Console.WriteLine(message);
        }

        //https://msdn.microsoft.com/en-us/library/6y0e13d3(v=vs.110).aspx
        public static void startListening()
        {
            byte[] bytes = new Byte[1024];
            IPHostEntry ipHostInfo = Dns.Resolve(Dns.GetHostName());
            IPAddress ipAddress = ipHostInfo.AddressList[0];
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 11000);

            //Create a TCP/IP Socket
            Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            //Bind socket and start listening
            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(10);

                while (true)
                {
                    Console.WriteLine("Waiting for a connection...");
                    // Program is suspended while waiting for an incoming connection.
                    //Socket handler = listener.Accept();
                    handler = listener.Accept();
                    data = null;

                    String searchAddress = null;
                    String stringRadius = null;

                    //Get street address from client
                    /*byte[] msg = Encoding.ASCII.GetBytes("Enter the address\n");
                    handler.Send(msg);*/
                    sendToClient("Enter the addess\n");
                    while (true)
                    {
                        bytes = new byte[1024];
                        int bytesRec = handler.Receive(bytes);
                        searchAddress += Encoding.ASCII.GetString(bytes, 0, bytesRec);
                        if (searchAddress.IndexOf("\n") > -1)
                        {
                            break;
                        }
                    }
                    Console.WriteLine(searchAddress);

                    //Get search radius from client
                    /*msg = Encoding.ASCII.GetBytes("Enter the search radius (miles)\n");
                    handler.Send(msg);*/
                    sendToClient("Enter the search radius\n");
                    while (true)
                    {
                        bytes = new byte[1024];
                        int bytesRec = handler.Receive(bytes);
                        stringRadius += Encoding.ASCII.GetString(bytes, 0, bytesRec);
                        if (stringRadius.IndexOf("\n") > -1)
                        {
                            break;
                        }
                    }
                    searchRadius = Double.Parse(stringRadius);
                    Console.WriteLine(searchRadius);

                    //msg = Encoding.ASCII.GetBytes(data);
                    //handler.Send(msg);

                    restaurantSearch(searchAddress);
                    lock (senderLock)
                    {
                        sendToClient("Done Searching\n");
                    }
                    Thread.Sleep(100);
                    handler.Shutdown(SocketShutdown.Both);
                    handler.Close();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        static void Main(string[] args)
        {
            /*
             * Download the csv file and store in a hashtable of arraylists
             * Delete the csv file
             * Keys to the hashtable are the zipcodes of the restaurants
             * */
            String[] lines;
            String[][] splitLines;

            WebClient webClient = new WebClient();

            //Downloading the csv file
            //webClient.DownloadFile("http://www.cs.cornell.edu/Courses/CS5412/2015sp/_cuonly/restaurants_all.csv", @"Z:\restaurants_all.csv");
            //lines = System.IO.File.ReadAllLines(@"Z:\restaurants_all.csv");
            //System.IO.File.Delete(@"Z:\restaurants_all.csv");

            //Using a subset of the csv file
            lines = System.IO.File.ReadAllLines(@"Z:\restaurants_all.csv");

            //Split the addresses
            splitLines = new String[lines.Length][];
            for (int i = 0; i < lines.Length; i++)
            {
                splitLines[i] = lines[i].Split(',');
                //Create a 9th element which is the concatenated address
                splitLines[i][9] = splitLines[i][3] + " " + splitLines[i][4] + " " + splitLines[i][5] + " " + splitLines[i][6] + " " + splitLines[i][7];
            }

            //Create a hashtable of arraylists
            foreach (String[] address in splitLines)
            {
                if (zipHash.ContainsKey(address[7]))
                {
                    //If the key already exists, append the address to the arraylist
                    ArrayList tmp = (ArrayList)zipHash[address[7]];
                    tmp.Add(address);
                }
                else
                {
                    //If the key doesn't exist, create it and append the address
                    ArrayList tmp = new ArrayList();
                    tmp.Add(address);
                    zipHash[address[7]] = tmp;
                }
            }

            /*
             * User enters street address and search radius
             * Return a list of restaurants that fall within the search radius
             * */
            /*String searchAddress;
            Console.Write("Enter your address - ");
            searchAddress = Console.ReadLine();
            Double searchRadius = new Double();
            Console.Write("Enter your search radius - ");
            searchRadius = Double.Parse(Console.ReadLine());
            restaurantSearch(searchAddress, searchRadius);
            Console.WriteLine("Done");*/
            startListening();
        }
    }
}
