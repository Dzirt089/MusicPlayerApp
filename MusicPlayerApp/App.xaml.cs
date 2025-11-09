namespace MusicPlayerApp
{
	public partial class App : Application
	{
		public App()
		{
			InitializeComponent();
			MainPage = new MainPage();
		}

		protected override void OnSleep()
		{
			base.OnSleep();
			// Принудительно сохраняем при переходе в фон
			MainPage.Instance?.SavePosition();
		}

		protected override void OnResume()
		{
			base.OnResume();
			// Можно добавить восстановление при возвращении
		}

		protected override Window CreateWindow(IActivationState? activationState)
		{
			return new Window(new AppShell());
		}

		private readonly MainPage MainPage;
	}
}