using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Newtonsoft.Json;

namespace Test
{
    class Program
    {
        private static readonly HttpClient client = new HttpClient();
        static async System.Threading.Tasks.Task Main(string[] args)
        {
            /*
             * The Documentation for the Office APIs specifies that each request requires an ID which is just a generated GUID.
             * https://docs.microsoft.com/en-us/Office365/Enterprise/office-365-ip-web-service#common-parameters
             */
            Guid requestId = Guid.NewGuid();

            // Building the URL
            var url = @"https://endpoints.office.com/endpoints/worldwide?clientrequestid=" + requestId;

            // Requesting the API
            var response = await client.GetAsync(url);

            var officeAPIsAsJson = await response.Content.ReadAsStringAsync();

            // Lazy People don't like Objects...
            dynamic officeEndpoints = JsonConvert.DeserializeObject(officeAPIsAsJson);

            #region PreppingStructure
            /// Everything in here is just to create a List of Objects that contain everything we need to make a rule. (IPs, Name, tcp or udp, required)
            var rules = new List<RuleSet>();

            foreach (dynamic endpoint in officeEndpoints)
            {
                string ports = "";
                bool tcp = true;
                if (endpoint.tcpPorts != null)
                {
                    ports = endpoint.tcpPorts;
                }
                else
                {
                    ports = endpoint.udpPorts;
                    tcp = false;
                }


                if (endpoint.ips != null)
                {
                    foreach (string ip in endpoint.ips)
                    {
                        RuleSet rs = new RuleSet
                        {
                            Name = endpoint.serviceAreaDiisplayName,
                            IpRange = ip,
                            PortRange = ports,
                            IsTCP = tcp,
                            Required = endpoint.required

                        };

                        rules.Add(rs);
                    }
                }
            }
            #endregion

            ////Now Azure
            //var azure = Azure.Configure().WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic);
            //var groupName = "sagetestgroup";
            //var resourceGroup = azure.ResourceGroups.Define(groupName); 


            Console.WriteLine("Hello World!");
        }
    }

    internal class RuleSet
    {
        public string Name { get; set; }
        public string IpRange { get; set; }
        public string PortRange { get; set; }
        public bool Required { get; set; }
        public bool IsTCP { get; set; }
    }
}
