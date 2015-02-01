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

namespace restaurant_locator
{
    class Program
    {
        static void googleGeoCode(string address)
        {
            String APIKey = "AIzaSyAAQbevauqrUWCObSUKx9W7GIAMAc6U2mA"; //av445
            var requestURI = String.Format("https://maps.googleapis.com/maps/api/geocode/xml?address={0}&key={1}", Uri.EscapeDataString(address), APIKey);
            Console.WriteLine(requestURI);

            var request = WebRequest.Create(requestURI);
            var response = request.GetResponse();
            XmlDocument xmlResult = new XmlDocument();
            xmlResult.Load(response.GetResponseStream());

            String status = xmlResult.SelectSingleNode("/GeocodeResponse/status").InnerText;
            String zipCode = xmlResult.SelectSingleNode("/GeocodeResponse/result/address_component[7]/long_name").InnerText;
            Console.WriteLine("status = " + status);
            Console.WriteLine("zipCode = " + zipCode);
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
            Hashtable zipHash = new Hashtable();
            List<String[]> zipList = new List<String[]>();

            WebClient webClient = new WebClient();

            //Downloading the csv file
            //webClient.DownloadFile("http://www.cs.cornell.edu/Courses/CS5412/2015sp/_cuonly/restaurants_all.csv", @"Z:\restaurants_all.csv");
            //lines = System.IO.File.ReadAllLines(@"Z:\restaurants_all.csv");
            //System.IO.File.Delete(@"Z:\restaurants_all.csv");

            //Using a subset of the csv file
            lines = System.IO.File.ReadAllLines(@"Z:\restaurants_some.csv");


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
            googleGeoCode("306 Stewart Ave, Ithaca, NY 14850");
        }
    }
}
