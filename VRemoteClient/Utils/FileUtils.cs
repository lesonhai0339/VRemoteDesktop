using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VRemoteClient.Models.CustomLayouts;
using VRemoteClient.Models.Enums;

namespace VRemoteClient.Utils
{
    public static class FileUtils
    {
        public static string? OpenFileDialogAndSaveFile()
        {
            using (var dialog = new SaveFileDialog())
            {
                return dialog.ShowDialog() == DialogResult.OK
                       && !string.IsNullOrWhiteSpace(dialog.FileName)
                    ? dialog.FileName
                    : null;
            }
        }

        public static string OpenFileDialogAndGetFilePath()
        {
            using (var dialog = new OpenFileDialog())
            {
                DialogResult result = dialog.ShowDialog();
                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.FileName))
                {
                    return dialog.FileName;
                }
                else
                {
                    return null;
                }
            }
        }
        public static string OpenDirectoryDialogAndGetDirectoryPath()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                DialogResult result = dialog.ShowDialog();
                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    return dialog.SelectedPath;
                }
                else
                {
                    return null;
                }
            }
        }
    }
}
