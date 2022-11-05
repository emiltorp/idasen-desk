using Hardcodet.Wpf.TaskbarNotification;
using Idasen.BluetoothLE.Core;
using System;
using System.Drawing;

namespace Idasen.SystemTray
{
    public class DynamicIconCreator : IDynamicIconCreator
    {
        public void Update(TaskbarIcon taskbarIcon, int height)
        {
            Guard.ArgumentNotNull(taskbarIcon,
                                    nameof(taskbarIcon));

            PushIcons(taskbarIcon, CreateIcon(height), height);
        }

        private Icon CreateIcon(int height)
        {
            int width = height >= 100 ? 24 : 16;

            using Pen pen = new(_brushDarkBlue);
            using SolidBrush brush = new(_brushLightBlue);
            using Font font = new("Consolas", 8);

            using Bitmap bitmap = new(width, 16);

            using Graphics graph = Graphics.FromImage(bitmap);

            //draw two horizontal lines
            graph.DrawLine(pen, 0, 15, width, 15);
            graph.DrawLine(pen, 0, 0, width, 0);

            //draw the string including the value at origin
            graph.DrawString($"{height}", font, brush, new PointF(-1, 1));

            IntPtr icon = bitmap.GetHicon();

            //create a new icon from the handle
            return Icon.FromHandle(icon);
        }

        private void PushIcons(TaskbarIcon taskbarIcon, Icon icon, int value)
        {
            if (!taskbarIcon.Dispatcher.CheckAccess())
            {
                _ = taskbarIcon.Dispatcher
                           .BeginInvoke(new Action(() => PushIcons(taskbarIcon,
                                                                          icon,
                                                                          value)));

                return;
            }

            //push the icons to the system tray
            taskbarIcon.Icon = icon;
            taskbarIcon.ToolTipText = $"Desk Height: {value}cm";
        }

        private readonly Color _brushDarkBlue = ColorTranslator.FromHtml("#FF0048A3");
        private readonly Color _brushLightBlue = ColorTranslator.FromHtml("#FF0098F3");
    }
}