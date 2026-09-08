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
