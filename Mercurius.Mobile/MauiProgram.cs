using CommunityToolkit.Maui;
using Mercurius.Mobile.Configuration;
using Mercurius.Mobile.Data;
using Mercurius.Mobile.Services;
using Microsoft.Extensions.Logging;

namespace Mercurius.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		// ============================================
		// LOCAL DATA + SYNC
		// ============================================
		builder.Services.AddSingleton<LocalDatabase>();
		builder.Services.AddSingleton<SessionService>();
		builder.Services.AddSingleton<AuthHeaderHandler>();

		// Unauthenticated client for login itself.
		builder.Services.AddHttpClient<AuthApiService>(client =>
		{
			client.BaseAddress = new Uri(ApiConfig.BaseUrl);
		});

		// Authenticated client for everything else — AuthHeaderHandler attaches the bearer token.
		builder.Services.AddHttpClient("MercuriusApi", client =>
		{
			client.BaseAddress = new Uri(ApiConfig.BaseUrl);
		}).AddHttpMessageHandler<AuthHeaderHandler>();

		builder.Services.AddSingleton<SyncApiService>();
		builder.Services.AddSingleton<SyncService>();

		// ============================================
		// PAGES
		// ============================================
		builder.Services.AddTransient<LoginPage>();
		builder.Services.AddTransient<AppShell>();
		builder.Services.AddTransient<MainPage>();
		builder.Services.AddTransient<ProductsPage>();
		builder.Services.AddTransient<SalesPage>();
		builder.Services.AddTransient<ReceiptsPage>();

		return builder.Build();
	}
}
