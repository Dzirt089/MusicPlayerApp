namespace MusicPlayerApp;

public partial class PlaylistPage : ContentPage
{
	public class PlaylistItem
	{
		public string Name { get; set; } = string.Empty;
		public string Path { get; set; } = string.Empty;
		public int Index { get; set; }
	}

	private readonly List<PlaylistItem> _playlistItems = new();
	private readonly MainPage _mainPage;

	public PlaylistPage(MainPage mainPage)
	{
		InitializeComponent();
		_mainPage = mainPage;
		LoadPlaylist();
	}

	private void LoadPlaylist()
	{
		_playlistItems.Clear();

		if (_mainPage.Playlist != null)
		{
			for (int i = 0; i < _mainPage.Playlist.Count; i++)
			{
				_playlistItems.Add(new PlaylistItem
				{
					Name = System.IO.Path.GetFileName(_mainPage.Playlist[i]),
					Path = _mainPage.Playlist[i],
					Index = i
				});
			}
		}

		playlistCollection.ItemsSource = _playlistItems;
	}

	private void OnTrackSelected(object sender, SelectionChangedEventArgs e)
	{
		if (e.CurrentSelection.FirstOrDefault() is PlaylistItem selectedItem)
		{
			_mainPage.PlayTrack(selectedItem.Index);
			Navigation.PopAsync();
		}
	}

	private void OnBackClicked(object sender, EventArgs e)
	{
		Navigation.PopAsync();
	}
}