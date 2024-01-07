using System.Timers;
using Microsoft.Extensions.Options;
using TuringPi.Panel.Services;

namespace TuringPi.Panel.UI;

/// <summary>
/// Page to display information for each node.  
/// </summary>
/// <remarks>
/// The node name and IP address are displayed.  The user can use the left and right buttons to cycle through the nodes.  The Action button closes the page.
/// </remarks>
public class NodeInformation : PageBase
{
  private DeviceController DeviceController { get; }
  private int NodeIndex { get; set; } = 0;

  private ILogger<NodeInformation> Logger { get; }

  public NodeInformation(Shell shell, DeviceController deviceController, IOptions<AppSettings> settings, ILogger<NodeInformation> logger) : base(shell, settings, false)
  {
    this.DeviceController = deviceController;
    this.Logger = logger;

    base.PageOpened += this.Page_Opened;
    base.PageClosed += this.Page_Closed;
    base.Refresh += this.Page_Refresh;
    base.ButtonPressed += this.Page_ButtonPressed;
  }

  private Task Page_Opened(object? sender, EventArgs e)
  {
    this.Logger?.LogDebug("Node Page Opened");

    this.NodeIndex = -1;

    this.GetDisplay()?.Clear();
    this.GetDisplay()?.Write("Node Info ...");

    return Task.CompletedTask;
  }

  private async Task Page_Refresh(object? sender, ElapsedEventArgs e)
  {
    NavigateRight();
    this.StopProgress();
    await DisplayNodeInformation();
    this.ScheduleRefresh(TimeSpan.FromSeconds(20), false);
  }

  /// <summary>
  /// Query the node and display information on-screen.
  /// </summary>
  private Task DisplayNodeInformation()
  {
    AppSettings.Node? node = this.AppSettings.Value.Nodes.Skip(this.NodeIndex).FirstOrDefault();

    if (!String.IsNullOrEmpty(node?.Name) && !String.IsNullOrEmpty(node?.HostName))
    {
      this.GetDisplay()?.SetCursorPosition(0, 0);
      this.GetDisplay()?.Write(PadDisplayValue(node.Name));

      this.GetDisplay()?.SetCursorPosition(0, 1);
      this.GetDisplay()?.Write(PadDisplayValue("..."));

      _ = Task.Run(async () =>
      {
        this.ShowProgress(TimeSpan.FromSeconds(4));
        System.Net.IPAddress address = await this.DeviceController.QueryNetworkAddress(node);
        this.StopProgress();
        this.GetDisplay()?.SetCursorPosition(0, 1);

        if (address == System.Net.IPAddress.Any)
        {
          this.GetDisplay()?.Write(PadDisplayValue("Offline"));
        }
        else
        {
          this.GetDisplay()?.Write(PadDisplayValue(address.ToString()));
        }
      });
    }
    else
    {
      this.Logger?.LogInformation("N{NodeIndex} not setup.", this.NodeIndex);
    }

    return Task.CompletedTask;
  }

  private Task Page_Closed(object? sender, EventArgs e)
  {
    this.StopProgress();
    return Task.CompletedTask;
  }

  private void NavigateLeft()
  {
    if (this.NodeIndex > 0)
    {
      this.NodeIndex--;
    }
    else
    {
      this.NodeIndex = this.AppSettings.Value.Nodes.Count() - 1;
    }
  }

  private void NavigateRight()
  {
    if (this.NodeIndex < this.AppSettings.Value.Nodes.Count() - 1)
    {
      this.NodeIndex++;
    }
    else
    {
      this.NodeIndex = 0;
    }
  }

  private async Task Page_ButtonPressed(object? sender, ButtonPressedEventArgs e)
  {
    switch (e.Button)
    {
      case ButtonPressedEventArgs.Buttons.LeftButton:
        // stay at the selected node for 2 minutes after a button press
        this.ScheduleRefresh(TimeSpan.FromMinutes(2), true);
        NavigateLeft();
        await DisplayNodeInformation();
        break;

      case ButtonPressedEventArgs.Buttons.RightButton:
        // stay at the selected node for 2 minutes after a button press
        this.ScheduleRefresh(TimeSpan.FromMinutes(2), true);
        NavigateRight();
        await DisplayNodeInformation();
        break;

      case ButtonPressedEventArgs.Buttons.ActionButton:
        this.Shell?.ClosePage();
        break;
    }
  }
}
