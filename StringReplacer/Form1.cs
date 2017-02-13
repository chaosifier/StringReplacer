using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace StringReplacer
{
    public partial class Form1 : Form
    {
        BackgroundWorker bworker;
        String _path = "";
        Boolean recursive = false;
        Boolean matchCase = false;
        Boolean includeFileNames = false;
        String findString = "";
        String replaceString = "";
        CommonOpenFileDialog cofd = new CommonOpenFileDialog();
        Boolean _isFolder = false;
        static List<String> mediaExtensions = new List<string>() { ".PNG", ".JPG", ".JPEG", ".BMP", ".GIF", ".WAV", ".MID", ".MIDI", ".WMA", ".MP3", ".OGG", ".RMA", ".AVI", ".MP4", ".DIVX", ".WMV", ".WOFF2", ".WOFF", ".EOT", ".SVG", ".TTF", ".OTF", ".EXE" };
        List<String> findStringFormats = new List<string>();
        List<String> replaceStringFormats = new List<string>();
        decimal totalJobSize = 0;
        decimal completedJobSize = 0;
        bool cancelled = false;
        string logFilePath = "";

        public Form1()
        {
            InitializeComponent();
            labelPath.Text = "";
        }

        private void buttonStart_Click(object sender, EventArgs e)
        {
            WindowsPrincipal myPrincipal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            if (myPrincipal.IsInRole(WindowsBuiltInRole.Administrator) == false)
            {
                MessageBox.Show("You need to run the application using the \"run as administrator\" option", "Admin Priveleges Required", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            if (_path == "")
            {
                MessageBox.Show("Please choose a file or _path first!", "No path provided", MessageBoxButtons.OK, MessageBoxIcon.Information);
                buttonBrowse.Focus();
                return;
            }

            if (textBoxFind.Text == "")
            {
                MessageBox.Show("This field cannot be empty!", "What to find?", MessageBoxButtons.OK, MessageBoxIcon.Information);
                textBoxFind.Focus();
                return;
            }

            if (textBoxReplaceWith.Text == "")
            {
                MessageBox.Show("This field cannot be empty!", "Replace with what?", MessageBoxButtons.OK, MessageBoxIcon.Information);
                textBoxReplaceWith.Focus();
                return;
            }

            findString = textBoxFind.Text;
            replaceString = textBoxReplaceWith.Text;

            foreach (String ext in textBoxIgnoreExtensions.Text.Split(','))
            {
                mediaExtensions.Add(ext.Trim().ToUpperInvariant());
            }

            findStringFormats.Clear();
            replaceStringFormats.Clear();
            if (!matchCase)
            {
                // All upper
                findStringFormats.Add(findString.ToUpperInvariant());
                replaceStringFormats.Add(replaceString.ToUpperInvariant());

                // All lower
                findStringFormats.Add(findString.ToLowerInvariant());
                replaceStringFormats.Add(replaceString.ToLowerInvariant());

                // First upper
                findStringFormats.Add(findString.First().ToString().ToUpper() + String.Concat(findString.Skip(1)));
                replaceStringFormats.Add(replaceString.First().ToString().ToUpper() + String.Concat(replaceString.Skip(1)));
            }

            //if (_isFolder)
            //{
            //    totalJobSize = GetDirectorySize(_path);
            //}
            //else
            //{
            //    totalJobSize = GetFileSize(_path);
            //}

            cancelled = false;
            pictureBox1.Visible = true;
            buttonStart.Text = "Cancel Operation";
            buttonStart.Click -= buttonStart_Click;
            buttonStart.Click += ButtonStop_Click;

            logFilePath = Path.Combine(Directory.GetCurrentDirectory(), "log-" + DateTime.Now.ToString("yyyyMMddHHmmssffff") + ".txt");
            //textBoxLog.Clear();

            bworker = new BackgroundWorker();
            bworker.WorkerSupportsCancellation = true;
            bworker.WorkerReportsProgress = true;
            bworker.DoWork += backgroundWorker_DoWork;
            bworker.ProgressChanged += backgroundWorker_ProgressChanged;
            bworker.RunWorkerCompleted += backgroundWorker_RunWorkerCompleted;
            bworker.RunWorkerAsync();
        }

        private void backgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var mainBgWorker = (BackgroundWorker)sender;
            mainBgWorker.ReportProgress(0, new String[] { "Estimating work size.." });
            var size = _isFolder ? GetDirectorySize(_path) : GetFileSize(_path);

            mainBgWorker.ReportProgress(0, new String[] { "Total work size : " + size + " bytes.", size.ToString() });

            BackgroundWorker innerBgWorker = new BackgroundWorker();
            innerBgWorker.WorkerSupportsCancellation = true;

            innerBgWorker.DoWork += delegate { stringReplace(_path, _isFolder); };

            innerBgWorker.RunWorkerAsync();

            while (!((mainBgWorker.CancellationPending || !innerBgWorker.IsBusy)))
            {
                Thread.Sleep(100);
            }

            if (mainBgWorker.CancellationPending)
            {
                mainBgWorker.ReportProgress(0, new String[] { "Total work size : " + size, size.ToString(), "cancelled" });
                innerBgWorker.CancelAsync();
                e.Cancel = true;
            }
            return;
        }

        private void ButtonStop_Click(object sender, EventArgs e)
        {
            bworker.CancelAsync();
            buttonStart.Click -= ButtonStop_Click;
            Log("Cancelling operation...");
            buttonStart.Enabled = false;
        }

        private void backgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.ProgressPercentage != -1)
                progressBar1.Value = e.ProgressPercentage;
            if (e.UserState != null)
            {
                var userState = (String[])e.UserState;
                Log(userState[0]);

                if (1 < userState.Length && !String.IsNullOrEmpty(userState[1]))
                    totalJobSize = Convert.ToDecimal(userState[1]);

                cancelled = 2 < userState.Length && !String.IsNullOrEmpty(userState[2]) && userState[2] == "cancelled";
            }
        }

        private void backgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (cancelled)
                progressBar1.Value = 0;
            else
                progressBar1.Value = 100;

            pictureBox1.Visible = false;

            buttonStart.Enabled = true;
            buttonStart.Click += buttonStart_Click;
            buttonStart.Click -= ButtonStop_Click;
            buttonStart.Text = "Begin Replacing!";

            if (e.Cancelled)
            {
                cancelled = true;
                MessageBox.Show("The operation was cancelled.", "Failed", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
                MessageBox.Show("Operation completed successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

            labelPath.Text = "";
            progressBar1.Value = 0;
            completedJobSize = 0;
            totalJobSize = 0;

            dispose();
        }

        private decimal GetDirectorySize(string p)
        {
            string[] a = Directory.GetFiles(p, "*.*", SearchOption.AllDirectories);

            decimal b = 0;
            foreach (string name in a)
            {
                FileInfo info = new FileInfo(name);
                b += info.Length;
            }
            return b;
        }

        private long GetFileSize(string p)
        {
            FileInfo info = new FileInfo(p);
            return info.Length;
        }

        private void checkBoxRecursive_CheckedChanged(object sender, EventArgs e)
        {
            recursive = ((CheckBox)sender).Checked;
        }

        private void checkBoxIncludeFilename_CheckedChanged(object sender, EventArgs e)
        {
            includeFileNames = ((CheckBox)sender).Checked;
        }

        private void checkBoxMatchCase_CheckedChanged(object sender, EventArgs e)
        {
            matchCase = ((CheckBox)sender).Checked;
        }

        private void buttonBrowse_Click(object sender, EventArgs e)
        {
            if (comboBox1.SelectedIndex == 0)
            {
                if (openFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    _path = openFileDialog1.FileName;
                    labelPath.Text = _path;
                }
            }
            else
            {
                cofd.IsFolderPicker = true;
                if (cofd.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    _path = cofd.FileName;
                    labelPath.Text = _path;
                }
            }

        }

        private void Form1_Load(object sender, EventArgs e)
        {
            comboBox1.SelectedIndex = 0;
        }

        private void labelPath_TextChanged(object sender, EventArgs e)
        {
            if (_path != "")
            {
                _path = labelPath.Text;
                labelPath.Visible = true;
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox1.SelectedIndex == 0)
            {
                _isFolder = false;
                checkBoxRecursive.Enabled = false;
            }
            else
            {
                _isFolder = true;
                checkBoxRecursive.Enabled = true;
            }

            labelPath.Text = "";
        }

        // if true returned in recursion, return true recursively to exit the entire recursion
        public bool stringReplace(string path, Boolean isFolder)
        {
            try
            {
                if (isFolder)
                {
                    //ShellWindows _shellWindows = new ShellWindows();
                    //string processType;

                    //foreach (InternetExplorer ie in _shellWindows)
                    //{
                    //    processType = Path.GetFileNameWithoutExtension(ie.FullName).ToLower();
                    //    if (processType.Equals("explorer") && ie.LocationURL.Contains(path))
                    //        try
                    //        {
                    //            ie.Quit();
                    //        }
                    //        catch (Exception e)
                    //        {
                    //            MessageBox.Show("Failed to close open window. Please close any open windows inside this directory. \n Error message : " + e.Message);
                    //        }
                    //}

                    DirectoryInfo dinfo = new DirectoryInfo(path);

                    if (recursive)
                    {
                        foreach (var fi in dinfo.GetDirectories())
                        {
                            bool exitRecursion = stringReplace(fi.FullName, true);
                            if (exitRecursion) return true;
                        }
                    }

                    foreach (var di in dinfo.GetFiles())
                    {
                        bool exitRecursion = stringReplace(di.FullName, false);
                        if (exitRecursion) return true;
                    }
                }
                else
                {
                    // replace string in file
                    if (!IsMediaFile(path))
                    {
                        try
                        {
                            if (!matchCase)
                            {
                                int i = 0;
                                foreach (string str in findStringFormats)
                                {
                                    //bworker.ReportProgress(-1, new String[] { "Replacing " + str + " with " + replaceStringFormats[i] + " in " + path });
                                    string text = File.ReadAllText(path);
                                    text = text.Replace(str, replaceStringFormats[i]);
                                    File.WriteAllText(path, text);

                                    i++;
                                }
                            }
                            else
                            {
                                //bworker.ReportProgress(-1, new String[] { "Replacing " + findString + " with " + replaceString + " in " + path });
                                string text = File.ReadAllText(path);
                                text = text.Replace(findString, replaceString);
                                File.WriteAllText(path, text);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log(ex.Message);
                            if (cancelled) return true;
                            if (MessageBox.Show("Failed to replace string in file : " + path + "\n Make sure the file isn't being used anywhere else.\n Error message : " + ex.Message + "\n Press Yes to ignore the error and continue. Press No to abort the entire operation.", "An error occured.", MessageBoxButtons.YesNo) == DialogResult.No)
                            {
                                return true; ;
                            }
                        }
                        var currentFileSize = GetFileSize(path);
                        completedJobSize += currentFileSize;
                        int completedPercentage = Convert.ToInt32((completedJobSize / totalJobSize) * 100);
                        bworker.ReportProgress(completedPercentage);

                        Debug.WriteLine("Total : " + totalJobSize);
                        Debug.WriteLine("Current : " + currentFileSize);
                        Debug.WriteLine("Completed : " + completedJobSize);
                        Debug.WriteLine("Percent complete : " + completedPercentage);
                    }
                }

                // rename file or directory
                string fileOrDirName = Path.GetFileName(path);

                if (includeFileNames)
                {
                    try
                    {
                        if (fileOrDirName.ToLower().Contains(findString.ToLower()))
                        {
                            int j = 0;
                            if (!matchCase)
                            {
                                foreach (string str in findStringFormats)
                                {
                                    fileOrDirName = fileOrDirName.Replace(str, replaceStringFormats[j]);
                                    string finalPath = Path.Combine(Path.GetDirectoryName(path), fileOrDirName);

                                    if (isFolder)
                                    {
                                        bworker.ReportProgress(-1, new String[] { "Renaming folder " + path + " to " + finalPath });
                                        Directory.Move(path, Path.Combine(finalPath + "temp"));
                                        Directory.Move(finalPath + "temp", finalPath);
                                    }
                                    else
                                    {
                                        bworker.ReportProgress(-1, new String[] { "Renaming file " + path + " to " + finalPath });
                                        File.Move(path, finalPath + "temp");
                                        File.Move(finalPath + "temp", finalPath);
                                    }
                                    j++;
                                    path = finalPath;
                                }
                            }
                        }
                        else
                        {
                            fileOrDirName = fileOrDirName.Replace(findString, replaceString);
                            string finalPath = Path.Combine(Path.GetDirectoryName(path), fileOrDirName);

                            if (isFolder)
                            {
                                //bworker.ReportProgress(-1, new String[] { "Renaming folder " + path + " to " + finalPath });
                                Directory.Move(path, Path.Combine(finalPath + "temp"));
                                Directory.Move(finalPath + "temp", finalPath);
                            }
                            else
                            {
                                //bworker.ReportProgress(-1, new String[] { "Renaming file " + path + " to " + finalPath });
                                File.Move(path, Path.Combine(finalPath + "temp"));
                                File.Move(finalPath + "temp", finalPath);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log(ex.Message);
                        if (cancelled) return true;
                        if (MessageBox.Show("Failed to rename " + (isFolder ? "folder. " : "file.\n") + "Error message : " + ex.Message + "\n Press Yes to ignore the error and continue. Press No to abort the entire operation.", "An error occured.", MessageBoxButtons.YesNo) == DialogResult.No)
                            return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Log(ex.Message);
                if (cancelled)
                    return true;
                else
                    return false;
            }
        }

        static bool IsMediaFile(string path)
        {
            return -1 != mediaExtensions.IndexOf(Path.GetExtension(path).ToUpperInvariant());
        }

        public void Log(string msg)
        {
            File.AppendAllText(logFilePath, msg + "\n");
            //textBoxLog.Text += msg + "\r\n";
            //textBoxLog.AppendText(msg);
        }

        public void dispose()
        {
            bworker.Dispose();
        }
    }
}
