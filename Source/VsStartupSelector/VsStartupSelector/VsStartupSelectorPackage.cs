using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using EnvDTE;
using EnvDTE80;
using Microsoft.Win32;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;

namespace iSp.VsStartupSelector
{
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(GuidList.guidVsStartupSelectorPkgString)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionOpening_string)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string)]
    public sealed class VsStartupSelectorPackage : Package
    {
        private DTE2 dte;

        private SolutionEvents solutionEvents;

        private MenuCommand chooseAppMenuItem;

        private SelectionEvents selectionEvents;

        private bool isSolutionOpened;

        private Project selectedProject;


        public VsStartupSelectorPackage()
        {
        }


        protected override void Initialize()
        {
            base.Initialize();

            dte = (DTE2)GetService(typeof(DTE));

            solutionEvents = dte.Events.SolutionEvents;
            solutionEvents.Opened += SolutionEvents_Opened;
            solutionEvents.AfterClosing += SolutionEvents_AfterClosing;

            selectionEvents = dte.Events.SelectionEvents;
            selectionEvents.OnChange += SelectionEvents_OnChange;

            var menuService = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (menuService != null)
            {
                var chooseAppMenuCommandID = new CommandID(GuidList.guidVsStartupSelectorCmdSet, (int)PkgCmdIDList.cmdidChooseApp);
                chooseAppMenuItem = new MenuCommand(ChooseAppMenuItemCallback, chooseAppMenuCommandID);
                menuService.AddCommand(chooseAppMenuItem);
            }
        }

        private void SelectionEvents_OnChange()
        {
            var activeSolutionProjects = dte.ActiveSolutionProjects as Array;
            if (activeSolutionProjects != null && activeSolutionProjects.Length > 0)
                selectedProject = activeSolutionProjects.GetValue(0) as Project;

            UpdateUI();
        }

        private void SolutionEvents_Opened()
        {
            isSolutionOpened = true;
            UpdateUI();
        }

        private void SolutionEvents_AfterClosing()
        {
            isSolutionOpened = false;
            UpdateUI();
        }

        private void UpdateUI()
        {
            chooseAppMenuItem.Visible = isSolutionOpened;
            chooseAppMenuItem.Enabled = selectedProject != null;
        }

        private void ChooseAppMenuItemCallback(object sender, EventArgs e)
        {
            if (selectedProject == null)
                return;

            var dialog = new OpenFileDialog();
            dialog.CheckFileExists = true;
            dialog.CheckPathExists = true;
            dialog.DefaultExt = ".exe";
            dialog.Filter = "Executables (*.exe)|*.exe";
            dialog.FilterIndex = 0;
            dialog.Multiselect = false;

            if (dialog.ShowDialog() != true)
                return;

            var fileName = dialog.FileName;

            var regex = new Regex(@"\\\\(?<host>.*?)\\(?<drive>.)\$\\(?<path>.*)");
            var match = regex.Match(fileName);
            if (match.Success)
            {
                var host = match.Groups["host"].Value;
                var drive = match.Groups["drive"].Value;
                var path = match.Groups["path"].Value;

                var config = selectedProject.ConfigurationManager.ActiveConfiguration;
                config.Properties.Item("StartAction").Value = 1;
                config.Properties.Item("StartProgram").Value = drive + @":\" + path;
                config.Properties.Item("RemoteDebugMachine").Value = host;
                config.Properties.Item("RemoteDebugEnabled").Value = true;
                selectedProject.Save();
                return;
            }

            if (Path.IsPathRooted(fileName))
            {
                var config = selectedProject.ConfigurationManager.ActiveConfiguration;
                config.Properties.Item("StartAction").Value = 1;
                config.Properties.Item("StartProgram").Value = fileName;
                config.Properties.Item("RemoteDebugMachine").Value = String.Empty;
                config.Properties.Item("RemoteDebugEnabled").Value = false;
                selectedProject.Save();
                return;
            }

            MessageBox.Show(
                "Unsupported path format. Select the path with one of formats:\n" +
                "  \\\\hostname\\driveletter$\\path\\app.exe\n" +
                "  driverletter:\\path\\app.exe",
                "Startup Selector",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
