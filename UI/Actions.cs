using Microsoft.Extensions.Options;

namespace TuringPi.Panel.UI;

public class Actions : Menu
{
  public Actions(IServiceProvider services, Shell shell, IOptions<AppSettings> settings) : base(shell, settings)
  {
    this.AddMenuItem("Node Info", services.GetRequiredService<UI.NodeInformation>());
    this.AddMenuItem("Node Ctrl", services.GetRequiredService<UI.NodeControl>());

    this.AddMenuItem("Turing Info", services.GetRequiredService<UI.TuringInformation>());
    this.AddMenuItem("Turing Ctrl", services.GetRequiredService<UI.TuringControl>());
    
    this.AddBackMenuItem();    
  }
}
