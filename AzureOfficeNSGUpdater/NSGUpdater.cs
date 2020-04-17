using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.Network.Fluent.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AzureOfficeNSGUpdater
{
    public static class NSGUpdater
    {
        [FunctionName("NSGUpdater")]
        public static async void Run([TimerTrigger("0 */5 * * * *")]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            HttpClient client = new HttpClient();

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
                            Name = endpoint.serviceAreaDisplayName,
                            IpRange = ip,
                            PortRange = ports,
                            IsTCP = tcp,
                            Required = endpoint.required

                        };

                        rules.Add(rs);
                        log.LogInformation($"Adding RuleSet for IP Range: {rs.IpRange} - {rs.Name}");
                    }
                }
            }
            #endregion

            //Now Azure
            //The Function need to have a system assigned managed identity.
            //Follow the first step here to create one: https://www.azurecorner.com/using-managed-service-identity-in-azure-functions-to-access-azure-sql-database/
            //Next you need to give a Role Assignment on the target Resources/Resourcegroup to the Function App (I chose "Creator", but other more specialized role might fit better)

            //Getting the credentials for the Managed Service Identity
            var creds = SdkContext.AzureCredentialsFactory.FromSystemAssignedManagedServiceIdentity(Microsoft.Azure.Management.ResourceManager.Fluent.Authentication.MSIResourceType.AppService, AzureEnvironment.AzureGlobalCloud);

            //"Connecting" to Azure.
            var azure = Azure
                    .Configure()
                    .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
                    .Authenticate(creds)
                    .WithDefaultSubscription();

            //Getting the existing NSG. If there is only one NSG per Resource Group you could probably also use GetBy..Group..
            var nsg = await azure.NetworkSecurityGroups.GetByIdAsync("/subscriptions/5becef9c-f620-40ef-9b8b-bff338e19893/resourceGroups/berndfunction/providers/Microsoft.Network/networkSecurityGroups/myNSG");

            var update = nsg.Update();
            int prio = 200;
            Random r = new Random();
            foreach(var rule in rules)
            {
                try
                {
                    var ruleName = rule.Name;
                    ruleName += r.Next();

                    update.DefineRule(ruleName)
                        .AllowInbound()
                        .FromAddress(rule.IpRange)
                        .FromAnyPort()
                        .ToAnyAddress()
                        .ToAnyPort()
                        .WithProtocol(rule.IsTCP ? SecurityRuleProtocol.Tcp : SecurityRuleProtocol.Udp)
                        .WithPriority(prio)
                        .WithDescription($"{rule.Name} is Required: {rule.Required}")
                        .Attach()
                    .DefineRule(ruleName)
                        .AllowOutbound()
                        .FromAnyAddress()
                        .FromAnyPort()
                        .ToAddress(rule.IpRange)
                        .ToAnyPort()
                        .WithProtocol(rule.IsTCP ? SecurityRuleProtocol.Tcp : SecurityRuleProtocol.Udp)
                        .WithPriority(prio)
                        .WithDescription($"{rule.Name} is Required: {rule.Required}")
                        .Attach();

                    log.LogInformation($"NSG Rule defined for: {rule.Name}");
                    prio += 5;
                }
                catch (Exception ex)
                {
                    log.LogInformation($"Exception for: {ex.Message}");
                    throw;
                }
            }

            log.LogInformation($"Applying NSG Rules");
            try
            {
                var result = await update.ApplyAsync();
            }
            catch(Exception ex)
            {
                log.LogInformation($"Exception for: {ex.Message}");
                throw;
            }

            //var publicIPAddress = await azure.PublicIPAddresses.Define("myPublicIP").WithRegion(Region.EuropeWest).WithExistingResourceGroup("berndfunction").WithDynamicIP().CreateAsync();
            //var azure = Azure.Authenticate();
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
    

