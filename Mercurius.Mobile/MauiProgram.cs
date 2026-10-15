using CommunityToolkit.Maui;
using Mercurius.Mobile.Configuration;
using Mercurius.Mobile.Data;
using Mercurius.Mobile.Services;
using Microsoft.Extensions.Logging;
using ZXing.Net.Maui.Controls;

namespace Mercurius.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.UseBarcodeReader() // ZXing camera scanning — see ScanBarcodePopup
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
		builder.Services.AddSingleton<DeviceSettingsService>();
		builder.Services.AddSingleton<HardwareScannerService>();
		// Transient, not singleton: IHttpClientFactory builds a fresh handler pipeline per client
		// and throws ("'InnerHandler' property must be null") if one DelegatingHandler instance is
		// shared — which a singleton is, as soon as two clients (below) both use it.
		builder.Services.AddTransient<AuthHeaderHandler>();
		builder.Services.AddTransient<DeviceInfoHeaderHandler>();

		// Client for login/logout. Bearer token attached too (harmless when there isn't one yet),
		// so the logout audit call is authenticated.
		builder.Services.AddHttpClient<AuthApiService>(client =>
		{
			client.BaseAddress = new Uri(ApiConfig.BaseUrl);
		}).AddHttpMessageHandler<DeviceInfoHeaderHandler>()
		  .AddHttpMessageHandler<AuthHeaderHandler>();

		// Authenticated client for everything else — AuthHeaderHandler attaches the bearer token.
		builder.Services.AddHttpClient("MercuriusApi", client =>
		{
			client.BaseAddress = new Uri(ApiConfig.BaseUrl);
		}).AddHttpMessageHandler<DeviceInfoHeaderHandler>()
		  .AddHttpMessageHandler<AuthHeaderHandler>();

		builder.Services.AddSingleton<SyncApiService>();
		builder.Services.AddSingleton<SyncService>();
		builder.Services.AddSingleton<SyncCoordinator>();

		// ============================================
		// PAGES
		// ============================================
		builder.Services.AddTransient<LoginPage>();
		builder.Services.AddTransient<AppShell>();
		builder.Services.AddTransient<MainPage>();
		builder.Services.AddTransient<ProductsPage>();
		builder.Services.AddTransient<SalesPage>();
		builder.Services.AddTransient<ReceiptsPage>();
		builder.Services.AddTransient<SettingsPage>();

		return builder.Build();
	}
}
