using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;

using System.Diagnostics;

namespace MusicPlayerApp;

public partial class MainPage : ContentPage
{
	private readonly MediaElement _media;
	private readonly List<string> _playlist = new();
	private int _currentIndex = -1;
	private bool _isUserSeeking = false;

	public List<string> Playlist => _playlist;
	public int CurrentIndex => _currentIndex;

	public MainPage()
	{
		InitializeComponent();

		// Создаем MediaElement программно
		_media = new MediaElement
		{
			BackgroundColor = Colors.Transparent,
			HorizontalOptions = LayoutOptions.Fill,
			VerticalOptions = LayoutOptions.Fill
		};

		// Добавляем MediaElement в контейнер
		mediaContainer.Content = _media;

		InitializeEventHandlers();

		// ВОССТАНАВЛИВАЕМ ПОСЛЕДНИЙ ПЛЕЙЛИСТ
		if (Preferences.ContainsKey("LastPlaylist"))
		{
			var json = Preferences.Get("LastPlaylist", "");
			_playlist = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
			_currentIndex = Preferences.Get("LastIndex", 0);
			if (_currentIndex < _playlist.Count)
				LoadTrack(_currentIndex);
		}

		Instance = this;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();

#if ANDROID
        try
        {
            LockScreenManager.Init(this);
        }
        catch (InvalidOperationException ex)
        {
            Debug.WriteLine($"LockScreenManager init failed: {ex.Message}");
        }
#endif
	}

	private void InitializeEventHandlers()
	{
		// Обработчики кнопок
		selectMusicBtn.Clicked += async (s, e) => await PickMusic();
		playlistBtn.Clicked += async (s, e) => await ShowPlaylist();
		playPauseBtn.Clicked += (s, e) => PlayPause();
		previousBtn.Clicked += (s, e) => PreviousTrack();
		nextBtn.Clicked += (s, e) => NextTrack();
		rewind15Btn.Clicked += (s, e) => Rewind15();
		forward30Btn.Clicked += (s, e) => Forward30();

		// Обработчики медиа
		_media.MediaOpened += OnMediaOpened;
		_media.MediaEnded += OnMediaEnded;
		_media.PositionChanged += OnPositionChanged;
		_media.MediaFailed += OnMediaFailed;

		// Обработчики слайдера
		progressSlider.DragStarted += (s, e) => _isUserSeeking = true;
		progressSlider.DragCompleted += (s, e) =>
		{
			_isUserSeeking = false;
			var newPosition = TimeSpan.FromSeconds(progressSlider.Value);
			_media.SeekTo(newPosition);
			SavePosition();
		};
	}

	private void OnMediaFailed(object sender, MediaFailedEventArgs e)
	{
		Debug.WriteLine($"❌ Ошибка медиа: {e.ErrorMessage}");
		MainThread.BeginInvokeOnMainThread(async () =>
		{
			await DisplayAlert("Ошибка воспроизведения", $"Не удалось воспроизвести файл: {e.ErrorMessage}", "OK");
		});
	}

	private async Task PickMusic()
	{
		try
		{
			var files = await FilePicker.Default.PickMultipleAsync(new PickOptions
			{
				PickerTitle = "Выберите аудиофайлы"
			});

			if (files?.Any() == true)
			{
				_playlist.Clear();
				_playlist.AddRange(files.Select(f => f.FullPath));
				_currentIndex = 0;

				// СОХРАНЯЕМ ПЛЕЙЛИСТ
				var json = System.Text.Json.JsonSerializer.Serialize(_playlist);
				Preferences.Set("LastPlaylist", json);

				LoadTrack(_currentIndex);
			}
		}
		catch (Exception ex)
		{
			await DisplayAlert("Ошибка", $"Не удалось выбрать файлы: {ex.Message}", "OK");
		}
	}

	public static MainPage Instance { get; private set; }

	private void LoadTrack(int index)
	{
		try
		{
			if (index < 0 || index >= _playlist.Count) return;

			var path = _playlist[index];
			var name = Path.GetFileName(path);

			titleLabel.Text = name;
			_media.Source = MediaSource.FromFile(path);

			// ВОССТАНАВЛИВАЕМ ПОЗИЦИЮ
			if (Preferences.ContainsKey("LastPosition") && Preferences.Get("LastPath", "") == path)
			{
				var savedMs = Preferences.Get("LastPosition", 0L);
				var pos = TimeSpan.FromMilliseconds(savedMs);

				void MediaOpenedHandler(object s, EventArgs e)
				{
					_media.MediaOpened -= MediaOpenedHandler;
					_media.SeekTo(pos);
					UpdateTimeLabels();
				}

				_media.MediaOpened += MediaOpenedHandler;
			}
			else
			{
				_media.MediaOpened += (s, e) => UpdateTimeLabels();
			}

			_media.Play();

#if ANDROID
            LockScreenManager.Update(title: name);
#endif

			// СОХРАНЯЕМ ПОСЛЕДНИЙ ТРЕК
			Preferences.Set("LastPath", path);
			Preferences.Set("LastIndex", index);

			UpdatePlayPauseButton();
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Ошибка загрузки трека: {ex}");
			DisplayAlert("Ошибка", $"Не удалось загрузить трек: {ex.Message}", "OK");
		}
	}

	private void OnMediaOpened(object sender, EventArgs e)
	{
		UpdateTimeLabels();
	}

	private void OnPositionChanged(object sender, MediaPositionChangedEventArgs e)
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			if (!_isUserSeeking)
			{
				progressSlider.Value = e.Position.TotalSeconds;
				currentTimeLabel.Text = FormatTime(e.Position);
			}

			if (_media.Duration != TimeSpan.Zero)
			{
				progressSlider.Maximum = _media.Duration.TotalSeconds;
				totalTimeLabel.Text = FormatTime(_media.Duration);
			}

#if ANDROID
            if (_media.CurrentState == MediaElementState.Playing)
                LockScreenManager.UpdatePosition(e.Position);
#endif

			// Авто-сохранение каждые 5 секунд
			if (_media.CurrentState == MediaElementState.Playing &&
				(long)e.Position.TotalMilliseconds % 5000 < 100)
			{
				SavePosition();
			}
		});
	}

	private void OnMediaEnded(object sender, EventArgs e)
	{
		NextTrack();
	}

	private void UpdateTimeLabels()
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			if (_media.Duration != TimeSpan.Zero)
			{
				totalTimeLabel.Text = FormatTime(_media.Duration);
				progressSlider.Maximum = _media.Duration.TotalSeconds;
			}
			currentTimeLabel.Text = FormatTime(_media.Position);
		});
	}

	private static string FormatTime(TimeSpan time)
	{
		return $"{(int)time.TotalMinutes}:{time.Seconds:00}";
	}

	private void UpdatePlayPauseButton()
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			playPauseBtn.Text = _media.CurrentState == MediaElementState.Playing ? "❚❚" : "▶";
		});
	}

	// Основные методы управления
	public void PlayPause()
	{
		if (_media.CurrentState == MediaElementState.Playing)
		{
			_media.Pause();
			SavePosition();
		}
		else
		{
			_media.Play();
		}
		UpdatePlayPauseButton();
	}

	public async void Rewind15()
	{
		var newPos = _media.Position - TimeSpan.FromSeconds(15);
		if (newPos < TimeSpan.Zero) newPos = TimeSpan.Zero;

		_media.SeekTo(newPos);
		await Task.Delay(100);
		SavePosition();
	}

	public async void Forward30()
	{
		var newPos = _media.Position + TimeSpan.FromSeconds(30);
		if (_media.Duration != TimeSpan.Zero && newPos > _media.Duration)
			newPos = _media.Duration - TimeSpan.FromSeconds(1);

		_media.SeekTo(newPos);
		await Task.Delay(100);
		SavePosition();
	}

	private void PreviousTrack()
	{
		SavePosition();
		if (_playlist.Count == 0) return;

		_currentIndex = (_currentIndex - 1 + _playlist.Count) % _playlist.Count;
		LoadTrack(_currentIndex);
	}

	private void NextTrack()
	{
		SavePosition();
		if (_playlist.Count == 0) return;

		_currentIndex = (_currentIndex + 1) % _playlist.Count;
		LoadTrack(_currentIndex);
	}

	public void PlayTrack(int index)
	{
		if (index >= 0 && index < _playlist.Count)
		{
			_currentIndex = index;
			LoadTrack(index);
		}
	}

	private async Task ShowPlaylist()
	{
		if (_playlist.Count == 0)
		{
			await DisplayAlert("Плейлист", "Плейлист пуст", "OK");
			return;
		}
		await Navigation.PushAsync(new PlaylistPage(this));
	}

	public void SavePosition()
	{
		if (_media.Source != null && _currentIndex >= 0 && _currentIndex < _playlist.Count)
		{
			Preferences.Set("LastPosition", (long)_media.Position.TotalMilliseconds);
		}
	}

	public void ClearPlaylistData()
	{
		_playlist.Clear();
		_currentIndex = -1;

		//Очистка настроек
		Preferences.Remove("LastPlaylist");
		Preferences.Remove("LastIndex");
		Preferences.Remove("LastPath");
		Preferences.Remove("LastPosition");

		// Сбрасываем интерфейс
		titleLabel.Text = "Выберите музыку";
		currentTimeLabel.Text = "0:00";
		totalTimeLabel.Text = "0:00";
		progressSlider.Value = 0;

		// Останавливаем воспроизведение
		_media.Stop();
		UpdatePlayPauseButton();
	}
}