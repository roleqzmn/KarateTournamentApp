using System;
using System.Windows;

namespace KarateTournamentApp.Services.Navigation
{
    public enum WindowSizeState { Normal, Maximized }
    public enum WindowStartupPosition { Manual, CenterScreen, CenterOwner }

    public class WindowOptions
    {
        public string? Title { get; set; }
        public double? Width { get; set; }
        public double? Height { get; set; }
        public WindowSizeState SizeState { get; set; } = WindowSizeState.Normal;
        public WindowStartupPosition StartupPosition { get; set; } = WindowStartupPosition.Manual;
        public bool Borderless { get; set; }
        public bool Resizable { get; set; } = true;
        public bool Topmost { get; set; }
    }

    public interface IWindowHandle
    {
        event EventHandler? Closed;
        void Close();
    }

    public interface IWindowService
    {
        // Composition-root only: maps a ViewModel type to the View used to display it.
        void Register<TViewModel, TView>() where TView : FrameworkElement, new();

        IWindowHandle Show(object viewModel, WindowOptions? options = null);
        bool? ShowDialog(object viewModel, WindowOptions? options = null);
    }
}
