using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TuringPi.Panel.UI;

public class Shell : IHostedService
{
  private IServiceProvider Services { get; }
  public Drivers.Panel Panel { get; }
  private ILogger<Shell> Logger { get; }

  public Stack<PageBase> Pages { get; } = new();

  public Shell(IServiceProvider services, Drivers.Panel panel, ILogger<Shell> logger)
  {
    this.Services = services;
    this.Panel = panel;
    this.Logger = logger;

    this.Panel.ButtonPressed += async (sender, e) => { await OnButtonPressed(e); };
    this.Panel.ButtonDoublePressed += async (sender, e) => { await OnButtonDoublePressed(e); };
    this.Panel.ButtonHold += async (sender, e) => { await OnButtonHold(e); };    
  }

  /// <summary>
  /// IHostedService start implementation.  Opens the splash page.
  /// </summary>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  public async Task StartAsync(CancellationToken cancellationToken)
  {
    this.Logger.LogDebug("Shell Started.");

    await this.OpenPage<Splash>();
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    return Task.CompletedTask;
  }

  /// <summary>
  /// Get the currently open page.
  /// </summary>
  public PageBase? CurrentPage
  {
    get
    {
      return Pages.Peek();
    }
  }

  /// <summary>
  /// Open the page specified by <paramref name="page"/>.
  /// </summary>
  /// <param name="page"></param>
  /// <returns></returns>
  public async Task OpenPage(PageBase page)
  {
    try
    {
      Pages.Push(page);
      await page.OnPageOpened(new());
    }
    catch (Exception ex)
    {
      Logger?.LogError(ex, "Open Page {type}", page.GetType().Name);
    }
  }

  /// <summary>
  /// Open the page specifed by <typeparamref name="TPage"/>.
  /// </summary>
  /// <typeparam name="TPage"></typeparam>
  /// <returns></returns>
  public async Task OpenPage<TPage>() where TPage : PageBase
  {
    try
    {
      PageBase page = this.Services.GetRequiredService<TPage>();
      this.Pages.Push(page);
      await page.OnPageOpened(new());
      this.Logger?.LogDebug("Opened page '{type}'.  Pages in stack: {count}.", typeof(TPage).Name, this.Pages.Count);
    }
    catch (Exception ex)
    {
      this.Logger?.LogError(ex, "Error opening page '{type}'", typeof(TPage));
    }
  }

  /// <summary>
  /// Clear the page stack.
  /// </summary>
  /// <returns></returns>
  public async Task ClearPageStack()
  {
    // we must invoke a PageClosed event for the current (top) page
    PageBase page = this.Pages.Pop();
    await page.OnPageClosed(new());

    this.Pages.Clear();
  }

  /// <summary>
  /// Return to the home page by rewinding the stack to the first page.
  /// </summary>
  /// <returns></returns>
  public async Task ReturnHome()
  {
    // only return home if there is more than 1 page in the stack
    if (this.Pages.Count > 1)
    {
      // we must invoke a PageClosed event for the current (top) page
      PageBase page = this.Pages.Pop();
      await page.OnPageClosed(new());

      // rewind the remaining pages in the stack
      while (Pages.Count > 1)
      {
        this.Pages.Pop();
      }

      this.CurrentPage?.OnPageOpened(new());

      this.Logger?.LogDebug("Navigated to home page '{type}'.  Pages in stack: {count}.", CurrentPage?.GetType().Name, this.Pages.Count);
    }
  }

  /// <summary>
  /// Close the current page.
  /// </summary>
  /// <returns></returns>
  public async Task ClosePage()
  {
    PageBase page = this.Pages.Pop();
    await page.OnPageClosed(new());

    if (this.CurrentPage != null)
    {
      await this.CurrentPage.OnPageOpened(new());
    }
  }

  /// <summary>
  /// Invoke a ButtonPressed event on the current page.
  /// </summary>
  /// <param name="button"></param>
  /// <returns></returns>
  private async Task OnButtonPressed(ButtonPressedEventArgs e)
  {
    if (this.CurrentPage != null)
    {
      await this.CurrentPage.OnButtonPressed(e);
    }
  }

  /// <summary>
  /// Invoke a ButtonDoublePressed event on the current page.
  /// </summary>
  /// <param name="button"></param>
  /// <returns></returns>
  private async Task OnButtonDoublePressed(ButtonDoublePressedEventArgs e)
  {
    if (this.CurrentPage != null)
    {
      await this.CurrentPage.OnButtonDoublePressed(e);
    }
  }

  /// <summary>
  /// Invoke a ButtonHold event on the current page.
  /// </summary>
  /// <param name="button"></param>
  /// <returns></returns>
  private async Task OnButtonHold(ButtonHoldEventArgs e)
  {
    if (e.Button == ButtonPressedEventArgs.Buttons.LeftButton)
    {
      // hold of the left button returns home
      await this.ReturnHome();
    }
    else
    {
      if (this.CurrentPage != null)
      {
        await this.CurrentPage.OnButtonHold(e);
      }
    }
  }
}
