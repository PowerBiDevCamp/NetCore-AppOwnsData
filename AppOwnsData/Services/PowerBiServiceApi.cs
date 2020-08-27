using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Web;
using Microsoft.PowerBI.Api;
using Microsoft.PowerBI.Api.Models;
using Microsoft.Rest;
using Newtonsoft.Json;

namespace AppOwnsData.Services
{

    public class WorkspaceViewModel
    {
        public Group Workspace;
        public IList<Dashboard> Dashboards;
        public IList<Report> Reports;
        public IList<Dataset> Datasets;
        public IList<Dataflow> Dataflows;
    }

    public class PowerBiServiceApi
    {

        private ITokenAcquisition tokenAcquisition { get; }
        private string urlPowerBiServiceApiRoot { get; }

        public PowerBiServiceApi(IConfiguration configuration, ITokenAcquisition tokenAcquisition)
        {
            this.urlPowerBiServiceApiRoot = configuration["PowerBi:ServiceRootUrl"];
            this.tokenAcquisition = tokenAcquisition;
        }

        public static readonly string[] RequiredScopes = new string[] {
      "https://analysis.windows.net/powerbi/api/.default"
    };

        public string GetAccessToken()
        {
            return this.tokenAcquisition.GetAccessTokenForAppAsync(RequiredScopes[0]).Result;
        }

        public PowerBIClient GetPowerBiClient()
        {
            var tokenCredentials = new TokenCredentials(GetAccessToken(), "Bearer");
            return new PowerBIClient(new Uri(urlPowerBiServiceApiRoot), tokenCredentials);
        }

        public async Task<string> GetEmbeddedViewModel(string appWorkspaceId = "")
        {

            var accessToken = this.tokenAcquisition.GetAccessTokenForAppAsync(RequiredScopes[0]).Result;
            var tokenCredentials = new TokenCredentials(accessToken, "Bearer");
            PowerBIClient pbiClient = new PowerBIClient(new Uri(urlPowerBiServiceApiRoot), tokenCredentials);

            Object viewModel;

            Guid workspaceId = new Guid(appWorkspaceId);
            var workspaces = (await pbiClient.Groups.GetGroupsAsync()).Value;
            var currentWorkspace = workspaces.First((workspace) => workspace.Id == workspaceId);
            var datasets = (await pbiClient.Datasets.GetDatasetsInGroupAsync(workspaceId)).Value;
            var reports = (await pbiClient.Reports.GetReportsInGroupAsync(workspaceId)).Value;

            IList<GenerateTokenRequestV2Dataset> datasetRequests = new List<GenerateTokenRequestV2Dataset>();
            foreach (var dataset in datasets)
            {
                datasetRequests.Add(new GenerateTokenRequestV2Dataset(dataset.Id));
            };

            IList<GenerateTokenRequestV2Report> reportRequests = new List<GenerateTokenRequestV2Report>();
            foreach (var report in reports)
            {
                reportRequests.Add(new GenerateTokenRequestV2Report(report.Id, allowEdit: true));
            };


            IList<GenerateTokenRequestV2TargetWorkspace> workspaceRequests =
              new GenerateTokenRequestV2TargetWorkspace[] {
          new GenerateTokenRequestV2TargetWorkspace(workspaceId)
            };


            GenerateTokenRequestV2 tokenRequest =
              new GenerateTokenRequestV2(datasets: datasetRequests,
                                          reports: reportRequests,
                                          targetWorkspaces: workspaceRequests);


            // call to Power BI Service API and pass GenerateTokenRequest object to generate embed token
            string embedToken = pbiClient.EmbedToken.GenerateToken(tokenRequest).Token;


            viewModel = new
            {
                workspaces = workspaces,
                currentWorkspace = currentWorkspace.Name,
                datasets = datasets,
                reports = reports,
                token = embedToken
            };

            return JsonConvert.SerializeObject(viewModel);
        }

        public async Task<Group> GetFirstWorkspace()
        {
            PowerBIClient pbiClient = this.GetPowerBiClient();
            var workspaces = (await pbiClient.Groups.GetGroupsAsync()).Value;
            if (workspaces.Count > 0)
            {
                return workspaces.First();
            }
            else
            {
                return null;
            }
        }

        public async Task<IList<Group>> GetWorkspaces()
        {
            PowerBIClient pbiClient = this.GetPowerBiClient();
            var workspaces = (await pbiClient.Groups.GetGroupsAsync()).Value;
            return workspaces;
        }

        public async Task<WorkspaceViewModel> GetWorkspaceDetails(string workspaceId)
        {

            PowerBIClient pbiClient = this.GetPowerBiClient();

            string filter = $"id eq '{workspaceId}'";

            var workspaceIdGuid = new Guid(workspaceId);

            return new WorkspaceViewModel
            {
                Workspace = (await pbiClient.Groups.GetGroupsAsync(filter)).Value.First(),
                Dashboards = (await pbiClient.Dashboards.GetDashboardsInGroupAsync(workspaceIdGuid)).Value,
                Reports = (await pbiClient.Reports.GetReportsInGroupAsync(workspaceIdGuid)).Value,
                Datasets = (await pbiClient.Datasets.GetDatasetsInGroupAsync(workspaceIdGuid)).Value,
                Dataflows = (await pbiClient.Dataflows.GetDataflowsAsync(workspaceIdGuid)).Value
            };

        }

        public string CreateAppWorkspace(string Name)
        {
            PowerBIClient pbiClient = this.GetPowerBiClient();
            // create new app workspace
            GroupCreationRequest request = new GroupCreationRequest(Name);
            Group aws = pbiClient.Groups.CreateGroup(request);

            pbiClient.Groups.AddGroupUser(aws.Id, new GroupUser
            {
                EmailAddress = "tedp@powerbidevcamp.net",
                GroupUserAccessRight = "Admin"
            });

            // return app workspace ID
            return aws.Id.ToString();
        }

        public void DeleteAppWorkspace(string WorkspaceId)
        {
            PowerBIClient pbiClient = this.GetPowerBiClient();
            Guid workspaceIdGuid = new Guid(WorkspaceId);
            pbiClient.Groups.DeleteGroup(workspaceIdGuid);
        }

        public void PublishPBIX(string appWorkspaceId, string PbixFilePath, string ImportName)
        {
            PowerBIClient pbiClient = this.GetPowerBiClient();
            FileStream stream = new FileStream(PbixFilePath, FileMode.Open, FileAccess.Read);
            var import = pbiClient.Imports.PostImportWithFileInGroup(new Guid(appWorkspaceId), stream, ImportName);
            Console.WriteLine("Publishing process completed");
        }
    }
}
