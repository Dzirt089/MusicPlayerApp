using CommunityToolkit.Maui;

using MusicPlayerApp;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()  // Для toolkit
			.UseMauiCommunityToolkitMediaElement();  // Для MediaElement

		return builder.Build();
	}
}