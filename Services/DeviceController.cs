using System.Net;

namespace TuringPi.Panel.Services;

public class DeviceController : SshDeviceController
{
  // list of IPV4 prefixes which are not "real" NIC addresses
  static readonly string[] LOCAL_IP_PREFIX = ["127", "169"];

  private ILogger<DeviceController> Logger { get; }

  public DeviceController(ILogger<DeviceController> logger) : base(logger)
  {
    Logger = logger;
  }

  // This can only work if we alter the settings in /etc/sudoers to allow shutdown without a password entry, which would be
  // an undesirable setting.
  //public async Task<bool> Shutdown(AppSettings.Node node)
  //{
  //  try
  //  {
  //    string result = await QueryDevice(node, $"shutdown now");
  //    return true;
  //  }
  //  catch (Exception ex)
  //  {
  //    Logger?.LogError(ex, "Shutdown {node}", node.Name);
  //    return false;
  //  }
  //}

  public async Task<decimal> QueryCPUTemperature(AppSettings.Node node)
  {
    try
    {
      string result = await QueryDevice(node, "cat /sys/class/thermal/thermal_zone0/temp");
      if (decimal.TryParse(result, out decimal temperature))
      {
        return temperature / 1000;
      }
      return decimal.MinValue;
    }
    catch (Exception ex)
    {
      Logger?.LogError(ex, "QueryCPUTemperature {node}", node.Name);
      return decimal.MinValue;
    }
  }

  public async Task <IPAddress> QueryNetworkAddress(AppSettings.Node node)
  {
    try
    {
      string result = await QueryDevice(node, "ifconfig | grep -w inet | awk '{print $2}'");

      foreach (string resultLine in result.Split(Environment.NewLine))
      {
        if (!LOCAL_IP_PREFIX.Contains(resultLine.Split('.').FirstOrDefault()))
        {
          if (IPAddress.TryParse(resultLine, out IPAddress? address))
          {
            return address;
          }
          else
          {
            Logger?.LogWarning("QueryNetworkAddress {node}, returned value '{resultLine}' is not an IP address.", node.Name, resultLine);
            return IPAddress.Any;
          }
        }
      }
    }
    catch (Exception ex)
    {
      Logger?.LogError(ex, "QueryNetworkAddress {node}", node.Name);
    }

    return IPAddress.Any;
  }
}
