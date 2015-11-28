﻿using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Xml.XPath;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestWindow.Extensibility;
using EnvDTE;
using GoogleTestAdapter;
using GoogleTestAdapter.Helpers;

namespace GoogleTestAdapterVSIX
{

    [Export(typeof(IRunSettingsService))]
    [SettingsName(GoogleTestConstants.SettingsName)]
    public class RunSettingsService : IRunSettingsService
    {
        public string Name { get { return GoogleTestConstants.SettingsName; } }

        public string SolutionSettingsFile_ForTesting { get; set; } = null;

        private IGlobalRunSettings globalRunSettings;

        [ImportingConstructor]
        public RunSettingsService([Import(typeof(IGlobalRunSettings))] IGlobalRunSettings globalRunSettings)
        {
            this.globalRunSettings = globalRunSettings;
        }

        public IXPathNavigable AddRunSettings(IXPathNavigable userRunSettingDocument, IRunSettingsConfigurationInfo configurationInfo, ILogger logger)
        {
            XPathNavigator userRunSettingsNavigator = userRunSettingDocument.CreateNavigator();
            if (!userRunSettingsNavigator.MoveToChild("RunSettings", ""))
            {
                logger.Log(MessageLevel.Warning, "RunSettingsDocument does not contain a RunSettings node! Canceling settings merging...");
                return userRunSettingsNavigator;
            }

            var finalRunSettings = new RunSettings();

            if (CopyToUnsetValues(userRunSettingsNavigator, finalRunSettings))
            {
                userRunSettingsNavigator.DeleteSelf(); // this node is to be replaced by the final run settings
            }

            // FIXME test code
            string solutionRunSettingsFile = SolutionSettingsFile_ForTesting ?? GetSolutionSettingsXmlFile();
            try
            {
                if (File.Exists(solutionRunSettingsFile))
                {
                    var solutionRunSettingsDocument = new XPathDocument(solutionRunSettingsFile);
                    XPathNavigator solutionRunSettingsNavigator = solutionRunSettingsDocument.CreateNavigator();
                    if (solutionRunSettingsNavigator.MoveToChild("RunSettings", ""))
                    {
                        CopyToUnsetValues(solutionRunSettingsNavigator, finalRunSettings);
                    }
                }
            }
            catch (Exception e)
            {
                logger.Log(MessageLevel.Warning,
                    $"Solution test settings file could not be parsed, check file: {solutionRunSettingsFile}");
                logger.LogException(e);
            }

            finalRunSettings.GetUnsetValuesFrom(globalRunSettings.RunSettings);

            userRunSettingsNavigator.AppendChild(finalRunSettings.ToXml().CreateNavigator());
            userRunSettingsNavigator.MoveToRoot();

            return userRunSettingsNavigator;
        }

        private bool CopyToUnsetValues(XPathNavigator sourceNavigator, RunSettings targetRunSettings)
        {
            if (sourceNavigator.MoveToChild(GoogleTestConstants.SettingsName, ""))
            {
                RunSettings sourceRunSettings = RunSettings.LoadFromXml(sourceNavigator.ReadSubtree());
                targetRunSettings.GetUnsetValuesFrom(sourceRunSettings);

                return true;
            }

            return false;
        }

        private string GetSolutionSettingsXmlFile()
        {
            DTE dte = Package.GetGlobalService(typeof(DTE)) as DTE;
            return Path.ChangeExtension(dte.Solution.FullName, GoogleTestConstants.SettingsExtension);
        }

    }

}