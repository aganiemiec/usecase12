#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2023 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using ShareX.HelpersLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ShareX
{
    /// <summary>
    /// Manages the list of Watch Folders. Provides methods:
    /// <list type="bullet">
    /// <item><description>to update the list,</description></item>
    /// <item><description>to add new Watch Folder,</description></item>
    /// <item><description>to change the Watch Folder state,</description></item>
    /// <item><description>to remove Watch Folder from the list,</description></item>
    /// <item><description>to dispose the Watch Folders,</description></item>
    /// </list>
    /// </summary>
    public class WatchFolderManager : IDisposable
    {
        /// <value>
        /// Represents a list of Watch Folders.
        /// </value>
        public List<WatchFolder> WatchFolders { get; private set; }

        /// <summary>
        /// Updates the Watch Folder list with the lists of Watch Folders from the Program's Default Task Settings and Task Settings of Hotkeys.
        /// If the WatchFolder list is not null, then first the Watch Folders are unregistered.
        /// Afterwards, the WatchFolders property is updated with new folders.
        /// </summary>
        public void UpdateWatchFolders()
        {
            if (WatchFolders != null)
            {
                UnregisterAllWatchFolders();
            }

            WatchFolders = new List<WatchFolder>();

            foreach (WatchFolderSettings defaultWatchFolderSetting in Program.DefaultTaskSettings.WatchFolderList)
            {
                AddWatchFolder(defaultWatchFolderSetting, Program.DefaultTaskSettings);
            }

            foreach (HotkeySettings hotkeySetting in Program.HotkeysConfig.Hotkeys)
            {
                foreach (WatchFolderSettings watchFolderSetting in hotkeySetting.TaskSettings.WatchFolderList)
                {
                    AddWatchFolder(watchFolderSetting, hotkeySetting.TaskSettings);
                }
            }
        }

        private WatchFolder FindWatchFolder(WatchFolderSettings watchFolderSetting)
        {
            return WatchFolders.FirstOrDefault(watchFolder => watchFolder.Settings == watchFolderSetting);
        }

        private bool IsExist(WatchFolderSettings watchFolderSetting)
        {
            return FindWatchFolder(watchFolderSetting) != null;
        }

        /// <summary>
        /// Adds new Watch Folder to the list based on the provided Watch Folder Settings and Task Settings,
        /// and enables the Watch Folder based on the provided Task Settings.
        /// If the Watch Folder with such Watch Folder Settings already exists in the list, then it's not added twice.
        /// The new Watch Folder instance is created with the Settings from <paramref name="watchFolderSetting"/>
        /// and Task Settings from <paramref name="taskSettings"/>.
        /// The File Watcher Trigger of new Watch Folder instance is created to perform uploading file to destination path.
        /// If the Watch Folder Settings has <c>MoveFilesToScreenshotsFolder</c> property set to true, 
        /// then the destination path is taken from the <paramref name="taskSettings"/>.
        /// If <c>MoveFilesToScreenshotsFolder</c> is set to false, then the destination path is the origin path.
        /// </summary>
        /// <param name="watchFolderSetting">Settings for new Watch Folder.</param>
        /// <param name="taskSettings">Task Settings for new Watch Folder.</param>
        public void AddWatchFolder(WatchFolderSettings watchFolderSetting, TaskSettings taskSettings)
        {
            if (!IsExist(watchFolderSetting))
            {
                if (!taskSettings.WatchFolderList.Contains(watchFolderSetting))
                {
                    taskSettings.WatchFolderList.Add(watchFolderSetting);
                }

                WatchFolder watchFolder = new WatchFolder();
                watchFolder.Settings = watchFolderSetting;
                watchFolder.TaskSettings = taskSettings;

                watchFolder.FileWatcherTrigger += origPath =>
                {
                    TaskSettings taskSettingsCopy = TaskSettings.GetSafeTaskSettings(taskSettings);
                    string destPath = origPath;

                    if (watchFolderSetting.MoveFilesToScreenshotsFolder)
                    {
                        string screenshotsFolder = TaskHelpers.GetScreenshotsFolder(taskSettingsCopy);
                        string fileName = Path.GetFileName(origPath);
                        destPath = TaskHelpers.HandleExistsFile(screenshotsFolder, fileName, taskSettingsCopy);
                        FileHelpers.CreateDirectoryFromFilePath(destPath);
                        File.Move(origPath, destPath);
                    }

                    UploadManager.UploadFile(destPath, taskSettingsCopy);
                };

                WatchFolders.Add(watchFolder);

                if (taskSettings.WatchFolderEnabled)
                {
                    watchFolder.Enable();
                }
            }
        }

        /// <summary>
        /// Removes the Watch Folder from the Watch Folder list based on the <paramref name="watchFolderSetting"/>.
        /// </summary>
        /// <param name="watchFolderSetting">Watch Folder Setting to find a Watch Folder to be removed from the list.</param>
        public void RemoveWatchFolder(WatchFolderSettings watchFolderSetting)
        {
            using (WatchFolder watchFolder = FindWatchFolder(watchFolderSetting))
            {
                if (watchFolder != null)
                {
                    watchFolder.TaskSettings.WatchFolderList.Remove(watchFolderSetting);
                    WatchFolders.Remove(watchFolder);
                }
            }
        }

        /// <summary>
        /// Enables or disposes the Watch Folder from the Watch Folder list.
        /// The Watch Folder is found based on the <paramref name="watchFolderSetting"/>.
        /// If the <c>WatchFolderEnabled</c> property of the Watch Folder's Task Settings is set to true,
        /// then the Watch Folder is being enabled.
        /// If not, then it's being disposed.
        /// </summary>
        /// <param name="watchFolderSetting">Watch Folder Setting to find a Watch Folder to be updated.</param>
        public void UpdateWatchFolderState(WatchFolderSettings watchFolderSetting)
        {
            WatchFolder watchFolder = FindWatchFolder(watchFolderSetting);
            if (watchFolder != null)
            {
                if (watchFolder.TaskSettings.WatchFolderEnabled)
                {
                    watchFolder.Enable();
                }
                else
                {
                    watchFolder.Dispose();
                }
            }
        }

        /// <summary>
        /// Disposes all of the Watch Folders from the WatchFolders list property.
        /// </summary>
        public void UnregisterAllWatchFolders()
        {
            if (WatchFolders != null)
            {
                foreach (WatchFolder watchFolder in WatchFolders)
                {
                    if (watchFolder != null)
                    {
                        watchFolder.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// Disposes all of the Watch Folders from the WatchFolders list property.
        /// </summary>
        public void Dispose()
        {
            UnregisterAllWatchFolders();
        }
    }
}