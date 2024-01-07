using Microsoft.Extensions.Options;

namespace TuringPi.Panel.UI;

/// <summary>
/// Base class for menu pages.  This class provides menu content and scroll up/down behaviour.
/// </summary>
public abstract class Menu : PageBase
{
  protected int ScrollPosition { get; set; } = 0;

  private readonly List<MenuItem> MenuItems = [];

  protected class MenuItem
  {
    public string Caption { get; set; }
    public PageBase? Page { get; set; }
    public Action? Action { get; set; }

    public MenuItem(string caption)
    {
      this.Caption = caption;    
    }

    public MenuItem(string caption, PageBase page)
    {
      this.Caption = caption;
      this.Page = page;
    }

    public MenuItem(string caption, Action action)
    {
      this.Caption = caption;
      this.Action = action;
    }
  }

  public Menu(Shell shell, IOptions<AppSettings> settings) : this(shell, settings, false) { }

  public Menu(Shell shell, IOptions<AppSettings> settings, Boolean autoRefresh) : base(shell, settings, autoRefresh)
  {
    base.PageOpened += this.Page_Opened;
    base.ButtonPressed += this.Page_ButtonPressed;
  }

  private Task Page_Opened(object? sender, EventArgs e)
  {    
    // return to the top of the menu
    this.ScrollPosition = 0;

    // display the menu
    ShowMenu();

    return Task.CompletedTask;
  }

  /// <summary>
  /// Remove all entries from the menu.  
  /// </summary>
  /// <remarks>
  /// This function does not update the screen.  Call ShowMenu to update the screen.
  /// </remarks>
  protected void ClearMenuItems()
  {
    this.MenuItems.Clear();
  }

  /// <summary>
  /// Add an "information only" menu item with no page or action linked to it.
  /// </summary>
  /// <param name="caption"></param>
  /// <remarks>
  /// This function does not update the on-screen display of the menu.  Call ShowMenu to update the screen.
  /// </remarks>
  protected void AddMenuItem(string caption)
  {
    this.MenuItems.Add(new(caption));
  }

  /// <summary>
  /// Add a menu item with an action linked to it.  When the user clicks the Action button the action is executed.
  /// </summary>
  /// <param name="caption"></param>
  /// <param name="action"></param>
  /// <remarks>
  /// This function does not update the on-screen display of the menu.  Call ShowMenu to update the screen.
  /// </remarks>

  protected void AddMenuItem(string caption, Action action)
  {
    this.MenuItems.Add(new(caption, action));
  }

  /// <summary>
  /// Add a menu item with a page linked to it.  When the user clicks the Action button the page is displayed.
  /// </summary>
  /// <param name="caption"></param>
  /// <param name="page"></param>
  /// <remarks>
  /// This function does not update the on-screen display of the menu.  Call ShowMenu to update the screen.
  /// </remarks>

  protected void AddMenuItem(string caption, UI.PageBase page)
  {
    this.MenuItems.Add(new(caption, page));
  }

  /// <summary>
  /// Add a "Back" menu item, with an action which closes the current page.
  /// </summary>
  protected void AddBackMenuItem()
  {
    this.MenuItems.Add(new("Back", () => this.Close()));
  }

  /// <summary>
  /// Re-draw the menu.
  /// </summary>
  protected void ShowMenu()
  {
    Boolean showIndicator = this.MenuItems.Where(item => item.Action != null || item.Page != null).Any();
    string currentItemIndicator = "";

    this.GetDisplay()?.Clear();
    this.StopProgress();

    foreach (var item in this.MenuItems.Skip(this.ScrollPosition).Take(this.PanelHeight).Select((menuItem, index) => new { menuItem, index })) 
    {
      this.GetDisplay()?.SetCursorPosition(0, item.index);

      if (showIndicator)
      {
        if (item.index == 0)
        {
          // current selection
          if (item.menuItem.Action != null || item.menuItem.Page != null)
          {
            // item has action
            currentItemIndicator = GetSpecialCharacter(SpecialCharacters.CHAR_SELECTED_PAGE);
          }
          else
          {
            // item has no action
            currentItemIndicator = GetSpecialCharacter(SpecialCharacters.CHAR_SELECTED_ITEM);
          }
        }
        else
        {
          // not current selection
          currentItemIndicator = " ";
        }
      }

      this.GetDisplay()?.Write(PadDisplayValue($"{currentItemIndicator}{item.menuItem.Caption}"));
    }

    // show "up" scroll indicator if we are not at the top item
    if (this.ScrollPosition > 0)
    {
      this.GetDisplay()?.SetCursorPosition(this.PanelWidth - 1, 0);
      this.GetDisplay()?.Write(GetSpecialCharacter(SpecialCharacters.CHAR_UP_ARROW));
    }

    // show "down" scroll indicator if we are not at the buttom item
    if (this.ScrollPosition < this.MenuItems.Count - 1)
    {
      this.GetDisplay()?.SetCursorPosition(this.PanelWidth - 1, this.PanelHeight - 1);
      this.GetDisplay()?.Write(GetSpecialCharacter(SpecialCharacters.CHAR_DOWN_ARROW));
    }
  }

  /// <summary>
  /// Handle button presses.  The left button scrolls up, the right button scrolls down and the action button executes the 
  /// Action or opens the Page which is associated with currently-selected menu item.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  /// <returns></returns>
  private async Task Page_ButtonPressed(object? sender, ButtonPressedEventArgs e)
  {
    switch (e.Button)
    {
      case ButtonPressedEventArgs.Buttons.LeftButton:
        if (this.ScrollPosition > 0)
        {
          this.ScrollPosition--;
          ShowMenu();
        }        
        break;

      case ButtonPressedEventArgs.Buttons.RightButton:
        if (this.ScrollPosition < this.MenuItems.Count - 1)
        {
          this.ScrollPosition++;
          ShowMenu();
        }        
        break;

      case ButtonPressedEventArgs.Buttons.ActionButton:
        MenuItem? item = this.MenuItems.Skip(this.ScrollPosition).FirstOrDefault();
        if (item != null)
        {
          if (item.Page != null)
          {
            await this.Shell.OpenPage(item.Page);
          }
          else
          {
            item.Action?.Invoke();
          }
        }
        break;
    }
  }    
}
