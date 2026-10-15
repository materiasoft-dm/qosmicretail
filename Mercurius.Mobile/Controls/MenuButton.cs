namespace Mercurius.Mobile.Controls;

// The "☰" that opens the sidebar, placed in each page's own header. Needed because Shell's own
// navigation bar (which normally carries this button) is hidden — it duplicated every page's
// custom header ("Sell" shown twice) — and because on Android the left-edge swipe that would
// otherwise open the drawer is taken by the system Back gesture. Only visible when the sidebar
// is actually collapsible (FlyoutBehavior.Flyout, i.e. Android phones/tablets); where it's locked
// open (Windows) there's nothing to open, so it hides itself.
public class MenuButton : ContentView
{
    public MenuButton()
    {
        var button = new Button
        {
            Text = "☰",
            FontSize = 22,
            TextColor = Colors.White,
            BackgroundColor = Colors.Transparent,
            Padding = new Thickness(0),
            WidthRequest = 44,
            HeightRequest = 44,
            MinimumWidthRequest = 0,
            MinimumHeightRequest = 0,
            VerticalOptions = LayoutOptions.Center,
            AutomationId = "OpenMenuButton"
        };
        SemanticProperties.SetDescription(button, "Open menu");
        button.Clicked += (_, _) =>
        {
            if (Shell.Current != null) Shell.Current.FlyoutIsPresented = true;
        };

        Content = button;
        VerticalOptions = LayoutOptions.Center;
        IsVisible = false;
        Loaded += (_, _) => IsVisible = Shell.Current?.FlyoutBehavior == FlyoutBehavior.Flyout;
    }
}
