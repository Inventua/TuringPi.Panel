namespace TuringPi.Panel;

public delegate Task PageOpenedEventHandler<EventArgs>(object? sender, EventArgs e);
public delegate Task PageClosedEventHandler<EventArgs>(object? sender, EventArgs e);
public delegate Task RefreshEventHandler<ElapsedEventArgs>(object? sender, ElapsedEventArgs e);
public delegate Task ButtonPressedEventHandler<ButtonPressedEventArgs>(object? sender, ButtonPressedEventArgs e);
public delegate Task ButtonDoublePressedEventHandler<ButtonDoublePressedEventArgs>(object? sender, ButtonDoublePressedEventArgs e);
public delegate Task ButtonHoldEventHandler<ButtonHoldEventArgs>(object? sender, ButtonHoldEventArgs e);

public static class Events
{
  public static async Task InvokeAsync<TEventArgs>(this MulticastDelegate handler, object sender, TEventArgs args)
  {
    Delegate[] delegates = handler.GetInvocationList();
    
    if (delegates.Any() == true)
    {
      IEnumerable<Task?> tasks = delegates.Select(e => e.DynamicInvoke(sender, args) as Task);
      await Task.WhenAll((IEnumerable<Task>)tasks.Where(task => task != null));
    }
  }
}