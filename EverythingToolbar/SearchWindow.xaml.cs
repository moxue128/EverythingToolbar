﻿using EverythingToolbar.Helpers;
using EverythingToolbar.Properties;
using NHotkey;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace EverythingToolbar
{
    public partial class SearchWindow : Window
    {
        public static double taskbarHeight = 0;
        public static double taskbarWidth = 0;

        public new double Height
        {
            get
            {
                return (double)GetValue(HeightProperty);
            }
            set
            {
                double screenHeight = SystemParameters.PrimaryScreenHeight;
                double newHeight = Math.Max(Math.Min(screenHeight - taskbarHeight, value), 300);
                SetValue(HeightProperty, newHeight);
            }
        }
        
        public new double Width
        {
            get
            {
                return (double)GetValue(WidthProperty);
            }
            set
            {
                double screenWidth = SystemParameters.PrimaryScreenWidth;
                double newWidth = Math.Max(Math.Min(screenWidth - taskbarWidth, value), 300);
                SetValue(WidthProperty, newWidth);
            }
        }

        public static readonly SearchWindow Instance = new SearchWindow();
        public event EventHandler<EventArgs> Hiding;
        public event EventHandler<EventArgs> Showing;

        private SearchWindow()
        {
            InitializeComponent();

            Loaded += (s, _) =>
            {
                ResourceManager.Instance.ResourceChanged += (sender, e) => { Resources = e.NewResource; };
                ResourceManager.Instance.AutoApplyTheme();
            };

            DataContext = EverythingSearch.Instance;

            if (Settings.Default.isUpgradeRequired)
            {
                Settings.Default.Upgrade();
                Settings.Default.isUpgradeRequired = false;
                Settings.Default.Save();
            }
            Settings.Default.PropertyChanged += (s, e) => Settings.Default.Save();
        }

        private void OnActivated(object sender, EventArgs e)
        {
            if (TaskbarStateManager.Instance.IsIcon)
            {
                NativeMethods.SetForegroundWindow(((HwndSource)PresentationSource.FromVisual(this)).Handle);
                SearchBox.Focus();
            }

            EventDispatcher.Instance.InvokeFocusRequested(sender, e);
        }

        private void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (e.NewFocus == null)  // New focus outside application
            {
                Hide();
            }
        }

        private void OpenSearchInEverything(object sender, RoutedEventArgs e)
        {
            EverythingSearch.Instance.OpenLastSearchInEverything();
        }

        public new void Hide()
        {
            if (Visibility != Visibility.Visible)
                return;

            HistoryManager.Instance.AddToHistory(EverythingSearch.Instance.SearchTerm);
            Hiding?.Invoke(this, new EventArgs());
        }

        public new void Show()
        {
            if (Visibility == Visibility.Visible)
                return;

            ShowActivated = TaskbarStateManager.Instance.IsIcon;
            base.Show();

            // Bring to top and immediately behind taskbar
            Topmost = true;
            Topmost = false;

            Showing?.Invoke(this, new EventArgs());
        }

        public void Show(object sender, HotkeyEventArgs e)
        {
            Show();
        }

        public void Toggle()
        {
            if (Visibility == Visibility.Visible)
                Hide();
            else
                Show();
        }

        public void AnimateShow(double left, double top, double width, double height, Edge taskbarEdge)
        {
            Width = width;
            Height = height;
            if (taskbarEdge == Edge.Right || taskbarEdge == Edge.Left)
                Top = top;
            else
                Left = left;

            if (Environment.OSVersion.Version >= Utils.WindowsVersion.Windows11)
                AnimateShowWin11(left, top, width, height, taskbarEdge);
            else
                AnimateShowWin10(left, top, width, height, taskbarEdge);
        }

        private void AnimateShowWin10(double left, double top, double width, double height, Edge taskbarEdge)
        {
            Height = Settings.Default.popupSize.Height;
            Width = Settings.Default.popupSize.Width;

            int modifier = taskbarEdge == Edge.Right || taskbarEdge == Edge.Bottom ? 1 : -1;
            Duration duration = TimeSpan.FromSeconds(Settings.Default.isAnimationsDisabled ? 0 : 0.4);
            DoubleAnimation outer = new DoubleAnimation(modifier * 150, 0, duration)
            {
                EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
            };
            DependencyProperty outerProp = taskbarEdge == Edge.Bottom || taskbarEdge == Edge.Top ? TranslateTransform.YProperty : TranslateTransform.XProperty;
            translateTransform?.BeginAnimation(outerProp, outer);

            DoubleAnimation opacity = new DoubleAnimation(0, 1, duration)
            {
                EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
            };
            PopupMarginBorder?.BeginAnimation(OpacityProperty, opacity);

            duration = Settings.Default.isAnimationsDisabled ? TimeSpan.Zero : TimeSpan.FromSeconds(0.8);
            ThicknessAnimation inner = new ThicknessAnimation(new Thickness(0), duration)
            {
                EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
            };
            if (taskbarEdge == Edge.Top)
                inner.From = new Thickness(0, -50, 0, 50);
            else if (taskbarEdge == Edge.Right)
                inner.From = new Thickness(50, 0, -50, 0);
            else if (taskbarEdge == Edge.Bottom)
                inner.From = new Thickness(0, 50, 0, -50);
            else if (taskbarEdge == Edge.Left)
                inner.From = new Thickness(-50, 0, 50, 0);
            ContentGrid.BeginAnimation(MarginProperty, inner);
        }

        private void AnimateShowWin11(double left, double top, double width, double height, Edge taskbarEdge)
        {
            int sign = taskbarEdge == Edge.Right || taskbarEdge == Edge.Bottom ? 1 : -1;
            double offset = taskbarEdge == Edge.Right || taskbarEdge == Edge.Left ? width : height;
            double target = taskbarEdge == Edge.Right || taskbarEdge == Edge.Left ? left : top;
            Duration duration = Settings.Default.isAnimationsDisabled ? TimeSpan.Zero : TimeSpan.FromSeconds(0.2);
            DoubleAnimation positionAnimation = new DoubleAnimation(target + offset * sign, target, duration)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            DependencyProperty positionProperty = taskbarEdge == Edge.Right || taskbarEdge == Edge.Left ? LeftProperty : TopProperty;
            BeginAnimation(positionProperty, positionAnimation);

            duration = Settings.Default.isAnimationsDisabled ? TimeSpan.Zero : TimeSpan.FromSeconds(0.4);
            ThicknessAnimation contentAnimation = new ThicknessAnimation(new Thickness(0), duration)
            {
                EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
            };
            contentAnimation.Completed += (s, e) =>
            {
                // Force the window to stay on top until the hide animation moves it out of view
                Topmost = true;
            };
            if (TaskbarStateManager.Instance.TaskbarEdge == Edge.Top)
                contentAnimation.From = new Thickness(0, -50, 0, 50);
            else if (TaskbarStateManager.Instance.TaskbarEdge == Edge.Right)
                contentAnimation.From = new Thickness(50, 0, -50, 0);
            else if (TaskbarStateManager.Instance.TaskbarEdge == Edge.Bottom)
                contentAnimation.From = new Thickness(0, 50, 0, -50);
            else if (TaskbarStateManager.Instance.TaskbarEdge == Edge.Left)
                contentAnimation.From = new Thickness(-50, 0, 50, 0);
            ContentGrid.BeginAnimation(MarginProperty, contentAnimation);
        }

        private void AnimateHideWin10()
        {
            // Move the window back so the next opening animation will not jump
            DoubleAnimation animation = new DoubleAnimation(Top + Height, TimeSpan.Zero);
            BeginAnimation(TopProperty, animation);
            base.Hide();
        }

        private void AnimateHideWin11(double left, double top, double width, double height, Edge taskbarEdge)
        {
            // Animation should take place behind taskbar
            Topmost = false;

            int sign = taskbarEdge == Edge.Right || taskbarEdge == Edge.Bottom ? 1 : -1;
            double offset = taskbarEdge == Edge.Right || taskbarEdge == Edge.Left ? width : height;
            double target = taskbarEdge == Edge.Right || taskbarEdge == Edge.Left ? left : top;
            Duration duration = Settings.Default.isAnimationsDisabled ? TimeSpan.Zero : TimeSpan.FromSeconds(0.2);
            DoubleAnimation positionAnimation = new DoubleAnimation(target, target + offset * sign, duration)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            DependencyProperty positionProperty = taskbarEdge == Edge.Right || taskbarEdge == Edge.Left ? LeftProperty : TopProperty;
            positionAnimation.Completed += (s, e) => { base.Hide(); };
            BeginAnimation(positionProperty, positionAnimation);

            duration = Settings.Default.isAnimationsDisabled ? TimeSpan.Zero : TimeSpan.FromSeconds(0.5);
            ThicknessAnimation contentAnimation = new ThicknessAnimation(new Thickness(0), duration)
            {
                EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseIn }
            };
            if (TaskbarStateManager.Instance.TaskbarEdge == Edge.Top)
                contentAnimation.To = new Thickness(0, -50, 0, 50);
            else if (TaskbarStateManager.Instance.TaskbarEdge == Edge.Right)
                contentAnimation.To = new Thickness(50, 0, -50, 0);
            else if (TaskbarStateManager.Instance.TaskbarEdge == Edge.Bottom)
                contentAnimation.To = new Thickness(0, 50, 0, -50);
            else if (TaskbarStateManager.Instance.TaskbarEdge == Edge.Left)
                contentAnimation.To = new Thickness(-50, 0, 50, 0);
            ContentGrid.BeginAnimation(MarginProperty, contentAnimation);
        }

        public void AnimateHide(double left, double top, double width, double height, Edge taskbarEdge)
        {
            if (Environment.OSVersion.Version >= Utils.WindowsVersion.Windows11)
                AnimateHideWin11(left, top, width, height, taskbarEdge);
            else
                AnimateHideWin10();

        }
    }
}
