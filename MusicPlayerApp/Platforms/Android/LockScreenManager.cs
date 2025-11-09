#if ANDROID
using Android.App;
using Android.Content;
using Android.Media;
using Android.Media.Session;
using Android.OS;

namespace MusicPlayerApp;


public static class LockScreenManager
{
	private static MediaSession? _session;
	private static MainPage? _page;

	public static void Init(MainPage page)
	{
		_page = page;

		var activity = Platform.CurrentActivity ??
				  throw new InvalidOperationException("CurrentActivity is null. Make sure to call Init after app is fully initialized.");


		_session = new MediaSession(activity, "MusicPlayer");

		_session.SetCallback(new Callback());
		_session.SetFlags(MediaSession.FlagHandlesMediaButtons | MediaSession.FlagHandlesTransportControls);
		_session.Active = true;

		// Показываем уведомление (обязательно для локскрина)
		ShowNotification();
	}

	public static void Update(string title)
	{
		if (_session == null) return; // ← ЗАЩИТА ОТ КРАША!

		var metadata = new MediaMetadata.Builder()
			.PutString(MediaMetadata.MetadataKeyTitle, title)
			.PutString(MediaMetadata.MetadataKeyArtist, "")
			.Build();

		var state = new PlaybackState.Builder()
			.SetActions(
				PlaybackState.ActionPlayPause |
				PlaybackState.ActionSkipToPrevious |
				PlaybackState.ActionSkipToNext)
			.AddCustomAction("REWIND_15", "Rewind 15s", 0)
			.AddCustomAction("FORWARD_30", "Forward 30s", 0)
			.SetState(PlaybackStateCode.Playing, 0, 1f) // 3 = Playing
			.Build();

		_session.SetMetadata(metadata);
		_session.SetPlaybackState(state);
	}

	private static void ShowNotification()
	{
		var channel = new NotificationChannel("music", "Music", NotificationImportance.Low);
		var manager = Platform.AppContext.GetSystemService(Context.NotificationService) as NotificationManager;
		manager?.CreateNotificationChannel(channel);

		var builder = new Notification.Builder(Platform.AppContext, "music")
			.SetSmallIcon(Resource.Drawable.abc_btn_check_material)
			.SetContentTitle("Music Player")
			.SetContentText("Ready")
			.SetOngoing(true);

		manager?.Notify(1, builder.Build());
	}

	private class Callback : MediaSession.Callback
	{
		public override void OnPlay() => _page?.PlayPause();
		public override void OnPause() => _page?.PlayPause();
		public override void OnSkipToPrevious() => _page?.Rewind15();
		public override void OnSkipToNext() => _page?.Forward30();
		public override void OnCustomAction(string action, Bundle? extras)
		{
			if (action == "REWIND_15") _page?.Rewind15();
			if (action == "FORWARD_30") _page?.Forward30();
		}
	}

	public static void UpdatePosition(TimeSpan position)
	{
		if (_session == null) return;

		var state = new PlaybackState.Builder()
			.SetActions(
				PlaybackState.ActionPlayPause |
				PlaybackState.ActionSkipToPrevious |
				PlaybackState.ActionSkipToNext)
			.AddCustomAction("REWIND_15", "Rewind 15s", 0)
			.AddCustomAction("FORWARD_30", "Forward 30s", 0)
			.SetState(PlaybackStateCode.Playing, (long)position.TotalMilliseconds, 1f)
			.Build();

		_session.SetPlaybackState(state);
	}
}
#endif