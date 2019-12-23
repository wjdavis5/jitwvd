using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.Management.Compute.Fluent;
using Microsoft.Azure.Management.Compute.Fluent.Models;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Extensions.Configuration;

namespace StartVm
{
    public static class Function1
    {
        [FunctionName("GetVms")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req,
            ILogger log)
        {
            
            //var credentials = SdkContext.AzureCredentialsFactory
            //    .FromFile(Environment.GetEnvironmentVariable("AZURE_AUTH_LOCATION"));
            try
            {
                var azure = SetupAazure();
                var listOfRelevantVms = new List<ScgVm>();
                var vms = await azure.VirtualMachines.ListAsync();
                foreach (var vm in vms)
                {
                    var scgVm = new ScgVm(vm.Id,vm.ComputerName,vm.PowerState.Value,vm.ResourceGroupName);
                    listOfRelevantVms.Add(scgVm);
                }
                var serialized = JsonConvert.SerializeObject(listOfRelevantVms, Formatting.Indented, new JsonSerializerSettings()
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });

                return new OkObjectResult($"{serialized}");
            }
            catch (Exception ex)
            {
                return new ExceptionResult(ex,true);
            }
        }

        [FunctionName("PowerOffVm")]
        public static async Task<IActionResult> PowerOff(
            [HttpTrigger(AuthorizationLevel.Function, "post")]HttpRequest req,ILogger log)
        {
            var azure = SetupAazure();
            using var streamReader = new StreamReader(req.Body);
            var rawReq = await streamReader.ReadToEndAsync();
            var vm = JsonConvert.DeserializeObject<ScgVm>(rawReq);
            await azure.VirtualMachines.PowerOffAsync(vm.ResourceGroup, vm.ComputerName);
            return new OkResult();
        }

        [FunctionName("PowerOnVm")]
        public static async Task<IActionResult> PowerOn(
            [HttpTrigger(AuthorizationLevel.Function, "post")]HttpRequest req, ILogger log)
        {
            var azure = SetupAazure();
            using var streamReader = new StreamReader(req.Body);
            var rawReq = await streamReader.ReadToEndAsync();
            var vm = JsonConvert.DeserializeObject<ScgVm>(rawReq);
            await azure.VirtualMachines.StartAsync(vm.ResourceGroup, vm.ComputerName);
            return new OkResult();
        }

        public static IAzure SetupAazure()
        {
            var credentials = SdkContext.AzureCredentialsFactory.FromFile("./azureauth.properties");

            var azure = Azure
                .Configure()
                .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
                .Authenticate(credentials)
                .WithDefaultSubscription();
            return azure;
        }
    }

    public class ScgVm
    {
        public string ResourceGroup { get; set; }
        public string Id { get; set; }
        public string ComputerName { get; set; }
        public string CurrentState { get; set; }

        public ScgVm(string id, string computerName, string currentState, string resourceGroup)
        {
            Id = id;
            ComputerName = computerName;
            CurrentState = currentState;
            ResourceGroup = resourceGroup;
        }
    }
}
