using System;
using System.Collections.Generic;
using System.Windows;

namespace KarateTournamentApp.Services.Navigation
{
    public class WpfWindowService : IWindowService
    {
        private readonly Dictionary<Type, Func<FrameworkElement>> _viewFactories = new();

        public void Register<TViewModel, TView>() where TView : FrameworkElement, new()
        {
            _viewFactories[typeof(TViewModel)] = () => new TView();
        }

        public IWindowHandle Show(object viewModel, WindowOptions? options = null)
        {
            var window = CreateWindow(viewModel, options);
            window.Show();
            return new WpfWindowHandle(window);
        }

        public bool? ShowDialog(object viewModel, WindowOptions? options = null)
        {
            var window = CreateWindow(viewModel, options);
            return window.ShowDialog();
        }

        private Window CreateWindow(object viewModel, WindowOptions? options)
        {
            options ??= new WindowOptions();

            if (!_viewFactories.TryGetValue(viewModel.GetType(), out var factory))
            {
                throw new InvalidOperationException($"No view registered for {viewModel.GetType().Name}");
            }

            var content = factory();
            content.DataContext = viewModel;

            return new Window
            {
                Content = content,
                Title = options.Title ?? string.Empty,
                Width = options.Width ?? double.NaN,
                Height = options.Height ?? double.NaN,
                WindowState = options.SizeState == WindowSizeState.Maximized ? WindowState.Maximized : WindowState.Normal,
                WindowStyle = options.Borderless ? WindowStyle.None : WindowStyle.SingleBorderWindow,
                ResizeMode = options.Resizable ? ResizeMode.CanResize : ResizeMode.NoResize,
                Topmost = options.Topmost,
                WindowStartupLocation = options.StartupPosition switch
                {
                    WindowStartupPosition.CenterScreen => WindowStartupLocation.CenterScreen,
                    WindowStartupPosition.CenterOwner => WindowStartupLocation.CenterOwner,
                    _ => WindowStartupLocation.Manual
                }
            };
        }
    }

    internal class WpfWindowHandle : IWindowHandle
    {
        private readonly Window _window;

        public WpfWindowHandle(Window window)
        {
            _window = window;
            _window.Closed += (s, e) => Closed?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler? Closed;

        public void Close() => _window.Close();
    }
}
