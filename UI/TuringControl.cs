using System.Reflection.Metadata.Ecma335;
using System.Timers;
using Microsoft.Extensions.Options;
using TuringPi.Panel.Services;

namespace TuringPi.Panel.UI;

public class TuringControl : Menu
{
  private TuringController TuringController { get; set; }
  private DeviceController DeviceController { get; set; }

  public TuringControl(Shell shell, TuringController turingController, DeviceController deviceController, IOptions<AppSettings> settings) : base(shell, settings, false)
  {
    this.TuringController = turingController;
    this.DeviceController = deviceController;

    base.PageOpened += Page_Opened;
  }


  private Task Page_Opened(object? sender, EventArgs e)
  {
    this.ClearMenuItems();

    this.AddMenuItem("Reboot Turing", async () =>
    {
      this.GetDisplay()?.SetCursorPosition(1, 0);
      this.GetDisplay()?.Write(PadDisplayValue("Rebooting ..."));

      ////// safely shutdown all nodes
      ////foreach (AppSettings.Node node in this.AppSettings.Value.Nodes)
      ////{
      ////  await this.DeviceController.Shutdown(node);
      ////}
      ////// give the nodes time to shut down
      ////await Task.Delay(TimeSpan.FromSeconds(15));

      // reboot the turing
      await this.TuringController.Reboot();
    });

    this.AddBackMenuItem();

    this.ShowMenu();

    return Task.CompletedTask;
  }

}