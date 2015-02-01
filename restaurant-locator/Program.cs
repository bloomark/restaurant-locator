using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Dynamic;
using System.Net;
using System.Xml;
using System.Xml.Linq;
using System.Device.Location;
using System.Threading;

namespace restaurant_locator
{
    class Program
    {
        static Hashtable zipHash = new Hashtable();
        static String APIKey = "AIzaSyAAQbevauqrUWCObSUKx9W7GIAMAc6U2mA"; //av445

        static void googleGeoCode(string address, Double searchRadius)
        {
            var requestURI = String.Format("https://maps.googleapis.com/maps/api/geocode/xml?address={0}&key={1}", Uri.EscapeDataString(address), APIKey);
            
            var request = WebRequest.Create(requestURI);
            var response = request.GetResponse();
            XmlDocument xmlResult = new XmlDocument();
            xmlResult.Load(response.GetResponseStream());

            String status = xmlResult.SelectSingleNode("/GeocodeResponse/status").InnerText;
            //Check status message
            if (!status.Equals("OK"))
            {
                //An error occured or there were no matches
                Console.WriteLine("Query wasn't successful. Status code - " + status);
                return;
            }

            int i = 1;
            String zipCode = null;

            while(i > 0)
            {
                String type = xmlResult.SelectSingleNode("/GeocodeResponse/result/address_component[" + i + "]/type").InnerText;
                if(type.Equals("postal_code"))
                {
                    zipCode = xmlResult.SelectSingleNode("/GeocodeResponse/result/address_component[" + i + "]/long_name").InnerText;
                    break;
                }
                i = i+1;
            }

            TimeSpan startTime = (DateTime.UtcNow - new DateTime(1970, 1, 1));
            Double startSeconds = startTime.TotalMilliseconds;

            int queriesFired = 0;
            int matchCount = 0;
            
            if (!(zipHash.ContainsKey(zipCode)))
            {
                Console.WriteLine("No matches!");
                return;
            }
            else
            {
                String latitude = xmlResult.SelectSingleNode("GeocodeResponse/result/geometry/location/lat").InnerText;
                String longitude = xmlResult.SelectSingleNode("GeocodeResponse/result/geometry/location/lng").InnerText;

                Double lat = Double.Parse(latitude);
                Double lng = Double.Parse(longitude);

                GeoCoordinate src = new GeoCoordinate(lat, lng);

                ArrayList matchList = (ArrayList)zipHash[zipCode];
                foreach (String[] restaurantAddress in matchList)
                {
                    if (queriesFired%5 == 0)
                    {
                        TimeSpan currTime = (DateTime.UtcNow - new DateTime(1970, 1, 1));
                        Double currSeconds = currTime.TotalMilliseconds;

                        int interval = (int)(currSeconds - startSeconds);

                        if (interval < 1000)
                        {
                            System.Threading.Thread.Sleep(1000 - interval);
                        }
                        
                        startTime = (DateTime.UtcNow - new DateTime(1970, 1, 1));
                        startSeconds = startTime.TotalMilliseconds;
                        queriesFired = 0;
                    }

                    String dstAddress = restaurantAddress[3] + " " + restaurantAddress[4] + " " + restaurantAddress[5] + " " + restaurantAddress[6] + " " + restaurantAddress[7];
                    Double[] latlng = getLatLong(dstAddress);
                    queriesFired++;
                    if (latlng[0].Equals(91.0) || latlng[1].Equals(181.0) || latlng[0].Equals(null) || latlng[1].Equals(null))
                    {
                        continue;
                    }
                    GeoCoordinate dst = new GeoCoordinate(latlng[0], latlng[1]);
                    
                    Double distance = src.GetDistanceTo(dst);
                    distance = distance / 1609.344;

                    if (distance <= searchRadius)
                    {
                        Console.WriteLine(dstAddress + " | " + distance);
                        matchCount++;
                    }
                    System.Threading.Thread.Sleep(100);
                }
                Console.WriteLine("Number of matches = " + matchCount);
            }
        }

        static Double[] getLatLong(String address)
        {
            Double[] latlng = new Double[2];
            
            var requestURI = String.Format("https://maps.googleapis.com/maps/api/geocode/xml?address={0}&key={1}", Uri.EscapeDataString(address), APIKey);
            
            var request = WebRequest.Create(requestURI);
            WebResponse response = null;

            try
            {
                response = request.GetResponse();
            }
            catch(WebException e)
            {
                latlng[0] = 91.0;
                latlng[1] = 181.0;
                return latlng;
            }

            XmlDocument xmlResult = new XmlDocument();
            xmlResult.Load(response.GetResponseStream());

            String status = xmlResult.SelectSingleNode("/GeocodeResponse/status").InnerText;
            //Check status message
            if (!status.Equals("OK"))
            {
                //An error occured or there were no matches
                latlng[0] = 91.0;
                latlng[1] = 181.0;
            }

            String latitude = xmlResult.SelectSingleNode("GeocodeResponse/result/geometry/location/lat").InnerText;
            String longitude = xmlResult.SelectSingleNode("GeocodeResponse/result/geometry/location/lng").InnerText;

            latlng[0] = Double.Parse(latitude);
            latlng[1] = Double.Parse(longitude);

            return latlng;
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
            }

            //Create a hashtable of arraylists
            foreach (String[] address in splitLines)
            {
                if (zipHash.ContainsKey(address[7]))
                {
                    ArrayList tmp = (ArrayList)zipHash[address[7]];
                    tmp.Add(address);
                }
                else
                {
                    ArrayList tmp = new ArrayList();
                    tmp.Add(address);
                    zipHash[address[7]] = tmp;
                }
            }

            /*
             * User enters street address and search radius
             * Return a list of restaurants that fall within the search radius
             * */
            String searchAddress;
            Console.Write("Enter your address - ");
            searchAddress = Console.ReadLine();
            Double searchRadius = new Double();
            Console.Write("Enter your search radius - ");
            searchRadius = Double.Parse(Console.ReadLine());
            googleGeoCode(searchAddress, searchRadius);
        }
    }
}
