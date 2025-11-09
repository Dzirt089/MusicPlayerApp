using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;


namespace MusicPlayerApp;

public partial class MainPage : ContentPage
{
	private readonly MediaElement media = new();
	private readonly Label _nameLabel = new() { FontSize = 22, HorizontalOptions = LayoutOptions.Center };
	private readonly List<string> playlist = new();
	private int idx = -1;
	// В класс добавьте поля
	private readonly Slider _positionSlider = new();
	private readonly Label _timeLabel = new() { FontSize = 12, HorizontalOptions = LayoutOptions.Center };

	public MainPage()
	{
		var stack = new VerticalStackLayout { Padding = 30, Spacing = 20 };
		stack.Add(_nameLabel);
		stack.Add(media);

		// Добавляем прогресс-бар
		var progressGrid = new Grid();
		progressGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
		progressGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

		_positionSlider.ValueChanged += OnPositionSliderValueChanged;
		progressGrid.Add(_positionSlider);
		progressGrid.Add(_timeLabel, 0, 1);

		stack.Add(progressGrid);

		BuildUI();

		// ВОССТАНАВЛИВАЕМ ПОСЛЕДНИЙ ПЛЕЙЛИСТ
		if (Preferences.ContainsKey("LastPlaylist"))
		{
			var json = Preferences.Get("LastPlaylist", "");
			playlist = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json)!;
			idx = Preferences.Get("LastIndex", 0);
			if (idx < playlist.Count)
				Load();
		}

		// СОХРАНЯЕМ ПРИ ЗАКРЫТИИ
		this.Unloaded += (s, e) => SavePosition();
		Instance = this; // ← ЗДЕСЬ!
	}
	protected override void OnAppearing()
	{
		base.OnAppearing();

#if ANDROID
		// Инициализируем только если еще не инициализировано
		try
		{
			LockScreenManager.Init(this);
		}
		catch (InvalidOperationException ex)
		{
			// Логируем ошибку, но не крашим приложение
			System.Diagnostics.Debug.WriteLine($"LockScreenManager init failed: {ex.Message}");
		}
#endif
	}
	// Обработчик изменения слайдера
	private void OnPositionSliderValueChanged(object sender, ValueChangedEventArgs e)
	{
		if (media.Duration != TimeSpan.Zero)
		{
			var newPosition = TimeSpan.FromSeconds(e.NewValue * media.Duration.TotalSeconds);
			media.SeekTo(newPosition);
			SavePosition();
		}
	}

	// Обновление слайдера в реальном времени
	void SetupMediaHandlers()
	{
		media.PositionChanged += (s, e) =>
		{
			if (media.Duration != TimeSpan.Zero && media.Duration.TotalSeconds > 0)
			{
				_positionSlider.Maximum = media.Duration.TotalSeconds;
				_positionSlider.Value = media.Position.TotalSeconds;

				_timeLabel.Text = $"{media.Position:mm\\:ss} / {media.Duration:mm\\:ss}";
			}

#if ANDROID
			if (media.CurrentState == MediaElementState.Playing)
				LockScreenManager.UpdatePosition(media.Position);
#endif
		};

		media.MediaOpened += (s, e) =>
		{
			_positionSlider.Maximum = media.Duration.TotalSeconds;
			_timeLabel.Text = $"00:00 / {media.Duration:mm\\:ss}";
		};
	}

	void BuildUI()
	{
		var stack = new VerticalStackLayout { Padding = 30, Spacing = 20 };
		stack.Add(_nameLabel);
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
			("↺ -15", () => Rewind15()),
			("↻ +30", () => Forward30()),
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

		//var clearBtn = new Button { Text = "Очистить", BackgroundColor = Colors.Red };
		//clearBtn.Clicked += (_, __) =>
		//{
		//	playlist.Clear();
		//	idx = -1;
		//	nameLabel.Text = "Плейлист пуст";
		//	media.Stop();
		//	Preferences.Clear();
		//};
		//grid.Add(clearBtn);
		//Grid.SetColumn(clearBtn, 5);

		media.PositionChanged += (s, e) =>
		{
#if ANDROID
			if (media.CurrentState == MediaElementState.Playing)
				LockScreenManager.UpdatePosition(media.Position);
#endif

			// Авто-сохранение каждые 5 секунд во время воспроизведения
			if (media.CurrentState == MediaElementState.Playing &&
				(long)media.Position.TotalMilliseconds % 5000 < 100)
			{
				SavePosition();
			}
		};

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

			// СОХРАНЯЕМ ПЛЕЙЛИСТ
			var json = System.Text.Json.JsonSerializer.Serialize(playlist);
			Preferences.Set("LastPlaylist", json);

			Load();
		}
	}

	public static MainPage Instance { get; private set; }


	void Load()
	{
		var path = playlist[idx];
		var name = Path.GetFileName(path);
		_nameLabel.Text = $"{idx + 1}/{playlist.Count} • {name}";

		media.MetadataTitle = name;
		media.Source = MediaSource.FromFile(path);

		// ВОССТАНАВЛИВАЕМ ПОЗИЦИЮ - исправленная версия
		if (Preferences.ContainsKey("LastPosition") &&
			Preferences.Get("LastPath", "") == path)
		{
			var savedMs = Preferences.Get("LastPosition", 0L);
			var pos = TimeSpan.FromMilliseconds(savedMs);

			// Используем локальную функцию вместо отдельного метода
			void MediaOpenedHandler(object s, EventArgs e)
			{
				media.MediaOpened -= MediaOpenedHandler;
				media.SeekTo(pos);
			}

			media.MediaOpened += MediaOpenedHandler;
		}

		media.Play();

#if ANDROID
		LockScreenManager.Update(title: name);
#endif

		// СОХРАНЯЕМ ПОСЛЕДНИЙ ТРЕК
		Preferences.Set("LastPath", path);
		Preferences.Set("LastIndex", idx);
	}

	public void PlayPause()
	{
		if (media.CurrentState == MediaElementState.Playing)
		{
			media.Pause();
			SavePosition(); // ← СОХРАНЯЕМ
		}
		else
		{
			media.Play();
		}
	}

	public async void Rewind15()
	{
		var newPos = media.Position - TimeSpan.FromSeconds(15);
		if (newPos < TimeSpan.Zero) newPos = TimeSpan.Zero;

		bool wasPlaying = media.CurrentState == MediaElementState.Playing;
		if (wasPlaying) media.Pause();

		media.SeekTo(newPos);

		// Даем время для применения seek
		await Task.Delay(100);

		if (wasPlaying)
		{
			media.Play();
#if ANDROID
			LockScreenManager.UpdatePosition(newPos);
#endif
		}

		SavePosition();
	}

	public async void Forward30()
	{
		var newPos = media.Position + TimeSpan.FromSeconds(30);
		if (media.Duration != TimeSpan.Zero && newPos > media.Duration)
			newPos = media.Duration - TimeSpan.FromSeconds(1); // Небольшой отступ от конца

		bool wasPlaying = media.CurrentState == MediaElementState.Playing;
		if (wasPlaying) media.Pause();

		media.SeekTo(newPos);

		// Даем время для применения seek
		await Task.Delay(100);

		if (wasPlaying)
		{
			media.Play();
#if ANDROID
			LockScreenManager.UpdatePosition(newPos);
#endif
		}

		SavePosition();
	}

	void Next()
	{
		SavePosition(); // ← СОХРАНЯЕМ ПЕРЕД СМЕНЫ
		if (++idx < playlist.Count) Load();
	}
	public void SavePosition()
	{
		if (media.Source != null)
		{
			Preferences.Set("LastPosition", (long)media.Position.TotalMilliseconds);
		}
	}
}