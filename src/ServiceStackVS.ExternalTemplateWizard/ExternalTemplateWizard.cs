﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using EnvDTE;
using Microsoft.VisualStudio.TemplateWizard;
using ServiceStack;
using ServiceStackVS.Common;
using NuGet.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using NuGet.Protocol.Core.Types;
using static NuGet.Protocol.Core.Types.Repository;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceStackVS.ExternalTemplateWizard
{
    public class ExternalTemplateWizard : IWizard
    {
        private string templatesRootDir;
        private string externalTemplateDir;
        private string solutionDir;
        private string externalTemplateName;
        private string externalProjectPath;
        private string externalSolutionPath;
        private string projectName;
        private string safeProjectNameReplace;
        private string targetFrameworkMoniker;

        private string slnOutputName;
        private string projOutputName;
        private List<TemplatedFile> allTemplatedFiles = new List<TemplatedFile>();
        private Dictionary<string, string> localReplacementsDictionary;

        private const string ServiceStackVsOutputWindowPane = "5e5ab647-6a69-44a8-a2db-6a324b7b7e6d";
        private OutputWindowWriter _serviceStackOutputWindowWriter;
        private OutputWindowWriter OutputWindowWriter
        {
            get
            {
                return _serviceStackOutputWindowWriter ??
                    (_serviceStackOutputWindowWriter = new OutputWindowWriter(ServiceStackVsOutputWindowPane, "ServiceStackVS"));
            }
        }

        public async void RunStarted(object automationObject, Dictionary<string, string> replacementsDictionary, WizardRunKind runKind, object[] customParams)
        {
            try
            {
                projectName = replacementsDictionary["$safeprojectname$"];

                replacementsDictionary.TryGetValue("$targetframeworkversion$", out targetFrameworkMoniker);
                targetFrameworkMoniker = targetFrameworkMoniker == null
                    ? "net45"
                    : "net" + targetFrameworkMoniker.Replace(".", "");

                localReplacementsDictionary = new Dictionary<string, string>(replacementsDictionary)
            {
                { "$targetframeworkmoniker$", targetFrameworkMoniker },
            };

                string latestVersion = await GetLatestVersionOfPackageAsync("ServiceStack.Interfaces").ConfigureAwait(false);
                localReplacementsDictionary.Add("$currentServiceStackVersion$", latestVersion);

                templatesRootDir = Path.GetDirectoryName(customParams[0] as string);
                localReplacementsDictionary.Add("$saferootprojectname$", projectName);
                if (templatesRootDir == null)
                {
                    throw new WizardBackoutException("Failed to create project, 'customParams' does not contain extension template path.");
                }
                solutionDir = Path.GetDirectoryName(localReplacementsDictionary["$destinationdirectory$"]);
                string wizardData = localReplacementsDictionary["$wizarddata$"];
                XElement element = XElement.Parse("<WizardData>" + wizardData + "</WizardData>");
                XElement externalTemplateRoot = null;
                if (element.Descendants().FirstOrDefault(x => x.Name.LocalName == "ExternalTemplate") != null)
                {
                    externalTemplateRoot =
                        element.Descendants().First(x => x.Name.LocalName == "ExternalTemplate");
                }
                if (externalTemplateRoot == null)
                {
                    return;
                }
                safeProjectNameReplace = externalTemplateRoot.GetAttributeValue("safeProjectNameReplace");
                externalTemplateName = externalTemplateRoot.GetExternalTemplateName();
                externalTemplateDir = Path.Combine(templatesRootDir, externalTemplateName);
                externalSolutionPath = Path.Combine(externalTemplateDir,
                    externalTemplateRoot.GetExternalTemplateSolutionFileName());
                externalProjectPath = Path.Combine(externalTemplateDir,
                    externalTemplateRoot.GetExternalTemplateProjectFileName());
                allTemplatedFiles = externalTemplateRoot.GetListOfTemplatedFiles();

                slnOutputName = externalTemplateRoot.GetAttributeValue("outputSolutionName");
                projOutputName = externalTemplateRoot.GetAttributeValue("outputProjectName");
            }
            catch (Exception ex)
            {
                OutputWindowWriter.WriteLine($"ExternalTemplateWizard Failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public void ProjectFinishedGenerating(Project project)
        {
            if (externalSolutionPath == null)
            {
                return;
            }
            //Compute value for external template and replace 'safeprojectname' leaving 'saferootprojectname' as original value.
            if (safeProjectNameReplace != null)
            {
                localReplacementsDictionary["$safeprojectname$"] =
                   safeProjectNameReplace.ReplaceAllTokens(localReplacementsDictionary);
            }
            
            //Create solution
            string solutionContents = File.ReadAllText(externalSolutionPath);
            File.WriteAllText(Path.Combine(solutionDir, slnOutputName.ReplaceAllTokens(localReplacementsDictionary)), solutionContents.ReplaceAllTokens(localReplacementsDictionary));

            //Create proj
            string projectContents = File.ReadAllText(externalProjectPath);
            string projectContainerPath = Path.Combine(solutionDir, projectName);
            var projContainerInfo = Directory.CreateDirectory(projectContainerPath);
            string projOutputNameResult = projOutputName.ReplaceAllTokens(localReplacementsDictionary);
            string projectContentsPath = Path.Combine(projContainerInfo.FullName,
                projOutputNameResult.Substring(0, projOutputNameResult.Length - 7));
            var projPath = Directory.CreateDirectory(projectContentsPath);
            File.WriteAllText(Path.Combine(projPath.FullName, projOutputNameResult), projectContents.ReplaceAllTokens(localReplacementsDictionary));

            //Create files

            foreach (var templatedFile in allTemplatedFiles)
            {
                if (templatedFile.IsTemplatableFile())
                {
                    string templateContents = File.ReadAllText(Path.Combine(externalTemplateDir, templatedFile.Name));
                    string resultContents = templateContents.ReplaceAllTokens(localReplacementsDictionary);
                    string fileOutputPath = projPath.FullName;
                    if (templatedFile.Dest != null)
                    {
                        fileOutputPath =
                            Directory.CreateDirectory(Path.Combine(projPath.FullName, templatedFile.Dest)).FullName;
                    }
                    File.WriteAllText(Path.Combine(fileOutputPath, templatedFile.Name), resultContents);
                }
                else
                {
                    string fileOutputPath = projPath.FullName;
                    if (templatedFile.Dest != null)
                    {
                        fileOutputPath = Directory.CreateDirectory(Path.Combine(projPath.FullName, templatedFile.Dest)).FullName;
                    }
                    File.Copy(Path.Combine(externalTemplateDir, templatedFile.Name), Path.Combine(fileOutputPath, templatedFile.Name));
                }
                
            }
        }

        private async Task<string> GetLatestVersionOfPackageAsync(string packageId)
        {
            var componentModel = (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));
            var _installer = componentModel.GetService<IVsPackageInstallerServices>();
            var providerFactory = new ProviderFactory();
            var sourceRepository = Repository.CreateSource(providerFactory.GetCoreV3(), "https://api.nuget.org/v3/index.json");
            var packageMetadataResource = await sourceRepository.GetResourceAsync<FindPackageByIdResource>();
            var sourceCache = new SourceCacheContext();

            var versions = await packageMetadataResource.GetAllVersionsAsync(packageId, sourceCache, new NuGet.Common.NullLogger(), CancellationToken.None);

            return versions.First().OriginalVersion;
        }

        public void ProjectItemFinishedGenerating(ProjectItem projectItem)
        {
            
        }

        public bool ShouldAddProjectItem(string filePath)
        {
            return true;
        }

        public void BeforeOpeningFile(ProjectItem projectItem)
        {
            
        }

        public void RunFinished()
        {
            
        }
    }

    public class TemplatedFile
    {
        public string Name { get; set; }
        public string Dest { get; set; }
    }

    public static class ExternalTemplateUtils
    {

        public static List<string> ExlcudedFileExtensions = new List<string>
        {
            "dll",
            "icns",
            "png",
            "jpg",
            "ico"
        };

        public static string GetExternalTemplateName(this XElement element)
        {
            return GetAttributeValue(element,"name");
        }

        public static bool IsTemplatableFile(this TemplatedFile templatedFile)
        {
            return !ExlcudedFileExtensions.Any(x => templatedFile.Name.EndsWith(x));
        }

        public static string GetExternalTemplateProjectFileName(this XElement element)
        {
            return GetAttributeValue(element, "projFile");
        }

        public static string GetExternalTemplateSolutionFileName(this XElement element)
        {
            return GetAttributeValue(element, "solutionFile");
        }

        public static List<TemplatedFile> GetListOfTemplatedFiles(this XElement element)
        {
            List<TemplatedFile> result = new List<TemplatedFile>();

            if (element.Descendants().Any(x => x.Name.LocalName == "Files") == false)
            {
                return result;
            }

            List<XElement> fileElements = element.Descendants().Where(x => x.Name.LocalName == "File").ToList();
            result = fileElements.Select(x => new TemplatedFile { Name = x.GetAttributeValue("name"), Dest = x.GetAttributeValue("dest") }).ToList();
            return result;
        }

        public static string GetAttributeValue(this XElement element, string attributeName)
        {
            string result = null;
            if (element.HasAttributes && element.Attributes().Any(x => x.Name.LocalName == attributeName))
            {
                result = element.Attributes().First(x => x.Name.LocalName == attributeName).Value;
            }
            return result;
        }

        public static string ReplaceAllTokens(this string contents, Dictionary<string, string> replacementsDictionary)
        {
            string result = contents;
            foreach (var pair in replacementsDictionary)
            {
                result = result.Replace(pair.Key, pair.Value);
            }
            return result;
        }
    }
}
