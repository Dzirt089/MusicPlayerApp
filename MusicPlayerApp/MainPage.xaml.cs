using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;


namespace MusicPlayerApp;

public partial class MainPage : ContentPage
{
	private readonly MediaElement media = new();
	private readonly Label nameLabel = new() { FontSize = 22, HorizontalOptions = LayoutOptions.Center };
	private readonly List<string> playlist = new();
	private int idx = -1;

	public MainPage()
	{
		BuildUI();
#if ANDROID
		LockScreenManager.Init(this);
#endif
	}

	void BuildUI()
	{
		var stack = new VerticalStackLayout { Padding = 30, Spacing = 20 };
		stack.Add(nameLabel);
		stack.Add(media);

		var grid = new Grid
		{
			ColumnDefinitions =
			{
				new ColumnDefinition(GridLength.Star),
				new ColumnDefinition(GridLength.Star),
				new ColumnDefinition(GridLength.Star),
				new ColumnDefinition(GridLength.Star),
				new ColumnDefinition(GridLength.Star)
			}
		};

		var btns = new[]
		{
			("Выбрать", (Action)(async () => await Pick())),
			("▶/❚❚", () => PlayPause()),
			("↺ -15", () => media.SeekTo(media.Position - TimeSpan.FromSeconds(15))),
			("↻ +30", () => media.SeekTo(media.Position + TimeSpan.FromSeconds(30))),
			("Next", Next)
		};

		for (int i = 0; i < btns.Length; i++)
		{
			var b = new Button { Text = btns[i].Item1, FontSize = 16 };
			int index = i; // ← КЛЮЧЕВАЯ СТРОКА!
			b.Clicked += (_, __) => btns[index].Item2();
			grid.Add(b);
			Grid.SetColumn(b, i);
		}

		stack.Add(grid);
		Content = new ScrollView { Content = stack };
		media.MediaEnded += (_, __) => Next();
	}

	async Task Pick()
	{
		var custom = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
		{
			{ DevicePlatform.Android, new[] { "audio/*" } },
			{ DevicePlatform.iOS, new[] { "public.audio" } },
			{ DevicePlatform.WinUI, new[] { ".mp3", ".wav", ".m4a" } }
		});

		var files = await FilePicker.Default.PickMultipleAsync(new PickOptions
		{
			PickerTitle = "Выберите аудио",
			FileTypes = custom
		});

		if (files?.Any() == true)
		{
			playlist.Clear();
			playlist.AddRange(files.Select(f => f.FullPath));
			idx = 0;
			Load();
		}
	}

	void Load()
	{
		var path = playlist[idx];
		var name = Path.GetFileName(path);
		nameLabel.Text = name;
		media.MetadataTitle = name;
		media.MetadataArtist = "";
		media.Source = MediaSource.FromFile(path);
		media.Play();
#if ANDROID
		LockScreenManager.Update(title: name);
#endif
	}

	public void PlayPause()
	{
		if (media.CurrentState == MediaElementState.Playing)
			media.Pause();
		else
			media.Play();
	}

	public void Rewind15() => media.SeekTo(media.Position - TimeSpan.FromSeconds(15));
	public void Forward30() => media.SeekTo(media.Position + TimeSpan.FromSeconds(30));

	void Next()
	{
		if (++idx < playlist.Count) Load();
	}

}