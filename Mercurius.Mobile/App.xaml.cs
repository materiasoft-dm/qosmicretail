using Mercurius.Mobile.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Mercurius.Mobile;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// Show the login page first, then quietly swap to the Shell if a valid session already
		// exists — a SecureStorage read is fast enough that this doesn't need its own splash page.
		var loginPage = IPlatformApplication.Current!.Services.GetRequiredService<LoginPage>();
		var window = new Window(loginPage);

		// This app is tablet/laptop-first (a POS/inventory tool used at a counter or back office,
		// not on the go) — on Windows/Mac Catalyst give it a proper desktop-sized window instead of
		// the tiny default, and don't let it be resized down to something the layouts can't use.
		window.Width = 1440;
		window.Height = 900;
		window.MinimumWidth = 1100;
		window.MinimumHeight = 700;

		window.Created += async (_, _) =>
		{
			var sessionService = IPlatformApplication.Current!.Services.GetRequiredService<SessionService>();
			if (await sessionService.IsLoggedInAsync())
			{
				var shell = IPlatformApplication.Current!.Services.GetRequiredService<AppShell>();
				window.Page = shell;
			}
		};

		return window;
	}
}
