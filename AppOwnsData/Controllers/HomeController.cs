using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using AppOwnsData.Models;
using AppOwnsData.Services;
using Microsoft.AspNetCore.Hosting;

namespace AppOwnsData.Controllers {
  public class HomeController : Controller {

    private PowerBiServiceApi powerBiServiceApi;
    private readonly IWebHostEnvironment Env;


    public HomeController(PowerBiServiceApi powerBiServiceApi, IWebHostEnvironment env) {
      this.powerBiServiceApi = powerBiServiceApi;
      this.Env = env;
    }

    public IActionResult Index() {
      return View();
    }

    public async Task<IActionResult> Embed(string workspaceId) {

      try {
        Guid guidTest = new Guid(workspaceId);
        var viewModel = await this.powerBiServiceApi.GetEmbeddedViewModel(workspaceId);
        return View(viewModel as object);
      }
      catch {
        var firstWorkspace = await this.powerBiServiceApi.GetFirstWorkspace();
        if (firstWorkspace == null) {
          return RedirectToPage("/Workspaces");
        }
        else {
          return RedirectToPage("/Embed", null, new { workspaceId = firstWorkspace.Id });
        }


      }
    }

    public async Task<IActionResult> Workspaces() {
      var viewModel = await this.powerBiServiceApi.GetWorkspaces();
      return View(viewModel);
    }

    public IActionResult Workspace(string workspaceId) {
      var viewModel = powerBiServiceApi.GetWorkspaceDetails(workspaceId).Result;
      return View(viewModel);
    }

    public IActionResult CreateWorkspace() {
      return View();
    }

    [HttpPost]
    public IActionResult CreateWorkspace(string WorkspaceName, string AddContent) {
      try {
        string appWorkspaceId = this.powerBiServiceApi.CreateAppWorkspace(WorkspaceName);
        if (AddContent.Equals("on")) {
          // upload sample PBIX file #1
          string pbixPath = this.Env.WebRootPath + @"/PBIX/COVID-19 US.pbix";
          string importName = "COVID-19 US";
          this.powerBiServiceApi.PublishPBIX(appWorkspaceId, pbixPath, importName);
        }
        return RedirectToAction("Workspaces");
      }
      catch (Exception ex) {
        string errorMessage = "Error trying to create new app workspace - " + ex.Message;
        return RedirectToAction("Error", new { ErrorMessage = errorMessage });
      }
    }

    public IActionResult DeleteWorkspace(string workspaceId) {
      this.powerBiServiceApi.DeleteAppWorkspace(workspaceId);
      // return to workspaces page
      return RedirectToAction("Workspaces");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() {
      return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
  }
}
