using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VRemoteClient.Models.CustomLayouts;
using VRemoteClient.Models.Enums;

namespace VRemoteClient.Utils
{
    public static class FileUtils
    {
        private static string DefaultFilter =
                "Text files (*.txt)|*.txt|" +
                "Word documents (*.doc;*.docx)|*.doc;*.docx|" +
                "Excel files (*.xls;*.xlsx)|*.xls;*.xlsx|" +
                "PDF files (*.pdf)|*.pdf|" +
                "Images (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|" +
                "Executable files (*.exe)|*.exe|" +
                "ZIP archives (*.zip)|*.zip|" +
                "All files (*.*)|*.*";

        public static Icon GetIconByFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return null;

            string tempFilePath = Path.Combine(Path.GetTempPath(), fileName);
            if (!File.Exists(tempFilePath))
                File.WriteAllBytes(tempFilePath, new byte[0]);

            FileInfo info = new FileInfo(tempFilePath);
            return  Icon.ExtractAssociatedIcon(tempFilePath);
        }
        public static string? OpenFileDialogAndSaveFile(string fileName, string filter = null, int defaultFilterIndex = 1)
        {
            using (var dialog = new SaveFileDialog())
            {
                // Set default file name
                if (!string.IsNullOrWhiteSpace(fileName))
                    dialog.FileName = fileName;

                dialog.Filter = string.IsNullOrWhiteSpace(filter) ? DefaultFilter : filter;
                dialog.FilterIndex = defaultFilterIndex;

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
