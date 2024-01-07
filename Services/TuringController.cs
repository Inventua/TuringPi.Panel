using System.Net;
using Microsoft.Extensions.Options;

namespace TuringPi.Panel.Services;

public class TuringController : SshDeviceController
{
  private IOptions<AppSettings> Settings { get; }
  private ILogger<TuringController> Logger { get; }

  public TuringController(IOptions<AppSettings> settings, ILogger<TuringController> logger) : base(logger)
  {
    Settings = settings;
    Logger = logger;
  }

  public async Task<string> Version()
  {
    try
    {
      string result = await QueryDevice(Settings.Value.TuringPi, "tpi info | grep -w version | awk '{print $3}'");
      return result.Trim();
    }
    catch (Exception ex)
    {
      Logger?.LogError(ex, "Version");
      return "";
    }
  }

  public async Task<bool> Reboot()
  {
    try
    {
      string result = await QueryDevice(Settings.Value.TuringPi, "tpi reboot");
      return true;
    }
    catch (Exception ex)
    {
      Logger?.LogError(ex, "Reboot");
      return false;
    }
  }

  public async Task<IPAddress> QueryNetworkAddress()
  {
    try
    {
      string result = await QueryDevice(Settings.Value.TuringPi, "tpi info | grep -w ip | awk '{print $3}'");

      if (IPAddress.TryParse(result.Trim(), out IPAddress? address))
      {
        return address;
      }
      else
      {
        Logger?.LogWarning("QueryNetworkAddress {node}, returned value {resultLine} is not an IP address.", Settings.Value.TuringPi.Name, result);
        return IPAddress.Any;
      }
    }
    catch (Exception ex)
    {
      Logger?.LogError(ex, "QueryNetworkAddress {node}", Settings.Value.TuringPi.Name);
    }

    return IPAddress.Any;
  }

  public async Task<string> QueryMac()
  {
    try
    {
      string result = await QueryDevice(Settings.Value.TuringPi, "tpi info | grep -w mac | awk '{print $3}'");
      return result.Trim();
    }
    catch (Exception ex)
    {
      Logger?.LogError(ex, "QueryNetworkAddress {node}", Settings.Value.TuringPi.Name);
    }

    return "";
  }

  public async Task <bool> QueryNodePower(int index)
  {
    try
    {
      string result = await QueryDevice(Settings.Value.TuringPi, $"tpi power status | grep -w node{index}: | awk '{{print $2}}'");
      return result.Trim().Equals("on", StringComparison.OrdinalIgnoreCase);
    }
    catch (Exception ex)
    {
      Logger?.LogError(ex, "Power status {node}", index);
      return false;
    }
  }

  public async Task<Boolean[]> QueryNodePower()
  {
    try
    {
      string result = await QueryDevice(Settings.Value.TuringPi, $"tpi power status");

      Boolean[] powerStatus = new Boolean[result.Split("\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length];

      foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(result, "node(?<index>[0-9]{1}):[\\s]*(?<status>.*)"))
      {
        if (match.Success)
        {
          if (int.TryParse(match.Groups["index"].Value, out int index) && powerStatus.Length > index - 1)
          {
            powerStatus[index - 1] = match.Groups["status"].Value.Equals("On", StringComparison.OrdinalIgnoreCase);
          }
        }
      }

      return powerStatus;
    }
    catch (Exception ex)
    {
      Logger?.LogError(ex, "Power status (all)");
      return new Boolean[0];
    }
    
  }

  public async Task <bool> PowerOnNode(int index)
  {
    try
    {
      string result = await QueryDevice(Settings.Value.TuringPi, $"tpi power --node {index} on");
      return true;
    }
    catch (Exception ex)
    {
      Logger?.LogError(ex, "Power On {node}", index);
      return false;
    }
  }

  public async Task<bool> PowerOnAllNodes()
  {
    try
    {
      string result = await QueryDevice(Settings.Value.TuringPi, $"tpi power on");
      return true;
    }
    catch (Exception ex)
    {
      Logger?.LogError(ex, "Power On All");
      return false;
    }
  }

  public async Task<bool> PowerOffNode(int index)
  {
    try
    {
      string result = await QueryDevice(Settings.Value.TuringPi, $"tpi power --node {index} off");
      return true;
    }
    catch (Exception ex)
    {
      Logger?.LogError(ex, "Power On {node}", index);
      return false;
    }
  }

  public async Task<bool> PowerOffAllNodes()
  {
    try
    {
      string result = await QueryDevice(Settings.Value.TuringPi, $"tpi power off");
      return true;
    }
    catch (Exception ex)
    {
      Logger?.LogError(ex, "Power Off All");
      return false;
    }
  }
}
