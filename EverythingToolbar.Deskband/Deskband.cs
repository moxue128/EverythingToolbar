﻿using EverythingToolbar;
using EverythingToolbar.Helpers;
using EverythingToolbar.Properties;
using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace CSDeskBand
{
    [ComVisible(true)]
    [Guid("9d39b79c-e03c-4757-b1b6-ecce843748f3")]
    [CSDeskBandRegistration(Name = "EverythingToolbar")]
    public class Deskband : CSDeskBandWpf
    {
        private static ToolbarControl ToolbarControl;
        protected override UIElement UIElement => ToolbarControl;

        public Deskband()
        {
            try
            {
                ToolbarControl = new ToolbarControl();

                Options.MinHorizontalSize = new Size(18, 30);
                Options.MinVerticalSize = new Size(30, 40);

                EventDispatcher.Instance.FocusRequested += OnFocusRequested;
                EventDispatcher.Instance.UnfocusRequested += OnUnfocusRequested;
                TaskbarInfo.TaskbarEdgeChanged += OnTaskbarEdgeChanged;
                TaskbarInfo.TaskbarSizeChanged += OnTaskbarSizeChanged;

                //SearchResultsWindow.taskbarEdge = TaskbarInfo.Edge;
            }
            catch (Exception e)
            {
                ToolbarLogger.GetLogger("EverythingToolbar").Error(e, "Unhandled exception");
                if (MessageBox.Show(e.ToString() + "\n\n" + Resources.MessageBoxCopyException,
                    Resources.MessageBoxUnhandledExceptionTitle,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Error) == MessageBoxResult.Yes)
                {
                    Clipboard.SetText(e.ToString());
                }
            }
        }

        private void OnTaskbarSizeChanged(object sender, TaskbarSizeChangedEventArgs e)
        {
            if (TaskbarInfo.Edge == Edge.Left || TaskbarInfo.Edge == Edge.Right)
            {
                SearchResultsWindow.taskbarWidth = TaskbarInfo.Size.Width;
                SearchResultsWindow.taskbarHeight = 0;
            }

            if (TaskbarInfo.Edge == Edge.Top || TaskbarInfo.Edge == Edge.Bottom)
            {
                SearchResultsWindow.taskbarHeight = TaskbarInfo.Size.Height;
                SearchResultsWindow.taskbarWidth = 0;
            }
        }

        private void OnUnfocusRequested(object sender, EventArgs e)
        {
            UpdateFocus(false);
        }

        private void OnFocusRequested(object sender, EventArgs e)
		{
            UpdateFocus(true);
		}

		private void OnTaskbarEdgeChanged(object sender, TaskbarEdgeChangedEventArgs e)
        {
            //SearchResultsWindow.taskbarEdge = e.Edge;
            OnTaskbarSizeChanged(sender, null);
        }

        protected override void DeskbandOnClosed()
        {
            base.DeskbandOnClosed();
            ToolbarControl.Destroy();
            ToolbarControl = null;
        }
    }
}
