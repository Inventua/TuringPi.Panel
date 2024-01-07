using System.Device.Gpio;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TuringPi.Panel.Services;

namespace TuringPi.Panel;

[ApiController]
public class WebApiController : ControllerBase
{
  [HttpGet]
  [Route("/settings")]
  public IActionResult Settings(IOptions<AppSettings> settings)
  {
    AppSettings? result = settings.Value.SafeCopy();
    return new JsonResult(result);
  }

  [HttpGet]
  [Route("/turing")]
  public async Task<IActionResult> Turing(TuringController turingController, IOptions<AppSettings> settings)
  {
    TuringInformation result = new()
    {
      BMCVersion = await turingController.Version(),
      NetworkAddress = (await turingController.QueryNetworkAddress()).ToString(),
      MACAddress = await turingController.QueryMac()
    };
    return new JsonResult(result);
  }

  private class TuringInformation
  {
    public string? BMCVersion { get; set; }
    public string? NetworkAddress { get; set; }
    public string? MACAddress { get; set; }
  }


  [HttpGet]
  [Route("/shell")]
  public IActionResult Shell(UI.Shell shell)
  {
    return new JsonResult(shell);
  }


  [HttpGet]
  [Route("/nodes")]
  public async Task<IActionResult> Nodes(TuringController turingController, DeviceController deviceController, IOptions<AppSettings> settings)
  {
    List<NodeInformation> result = new();

    foreach (var item in settings.Value.Nodes.Select((node, index) => new { index = index + 1, node = node }))
    {
      if (String.IsNullOrEmpty(item.node.Name) || String.IsNullOrEmpty(item.node.HostName))
      {
        result.Add(new());
      }
      else
      {
        NodeInformation info = new()
        {
          Name = item.node.Name,
          HostName = item.node.HostName,
          IsConfigured = true,
          IsNodePowered = await turingController.QueryNodePower(item.index)
        };

        if (info.IsNodePowered)
        {
          info.CPUTemperature = await deviceController.QueryCPUTemperature(item.node);
          info.NetworkAddress = (await deviceController.QueryNetworkAddress(item.node)).ToString();
        }

        result.Add(info);
      }
    }

    return new JsonResult(result);
  }

  private class NodeInformation
  {
    public string? Name { get; set; }
    public string? HostName { get; set; }

    public Boolean IsConfigured { get; set; } = false;
    public Boolean IsNodePowered { get; set; } = false;

    public decimal? CPUTemperature { get; set; }
    public string? NetworkAddress { get; set; }
  }
}
