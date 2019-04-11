﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using EnvDTE;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TemplateWizard;
using NuGet.VisualStudio;

namespace ServiceStackVS.NuGetInstallerWizard
{
    public static class Utils
    {
        public static IEnumerable<XElement> ElementsNoNamespace(this XContainer container, string localName)
        {
            return container.Elements().Where(e => e.Name.LocalName == localName);
        }

        public static IEnumerable<XElement> ElementsNoNamespace(this IEnumerable<XContainer> source, string localName)
        {
            return source.Elements().Where(e => e.Name.LocalName == localName);
        }
    }

    public class NuGetPackageInstallerWizard : NuGetPackageInstallerMultiProjectWizard { }

    public class NuGetPackageInstallerMultiProjectWizard : IWizard
    {
        IEnumerable<string> _packages;

        public void RunStarted(object automationObject, Dictionary<string, string> replacementsDictionary, WizardRunKind runKind, object[] customParams)
        {
            if (customParams.Length > 0)
            {
                var vstemplate = XDocument.Load((string)customParams[0]);
                _packages = vstemplate.Root
                    .ElementsNoNamespace("WizardData")
                    .ElementsNoNamespace("packages")
                    .ElementsNoNamespace("package")
                    .Select(e => e.Attribute("id").Value)
                    .ToList();
            }
        }

        public void ProjectFinishedGenerating(Project project)
        {
            var componentModel = (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));
            var _installer = componentModel.GetService<IVsPackageInstaller2>();

            foreach (var package in _packages)
            {
                try
                {
                    _installer.InstallLatestPackage(null, project, package, false, false);
                }
                catch {}
            }
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
}


//using System;
//using System.Collections.Generic;
//using System.ComponentModel.Composition;
//using System.ComponentModel.Composition.Hosting;
//using System.IO;
//using System.Linq;
//using System.Xml.Linq;
//using EnvDTE;
//using Microsoft.VisualStudio.ComponentModelHost;
//using Microsoft.VisualStudio.Shell;
//using Microsoft.VisualStudio.Shell.Interop;
//using Microsoft.VisualStudio.TemplateWizard;
//using NuGet;
//using NuGet.VisualStudio;
//using ServiceStackVS.Common;
//using ServiceStack;
//using IServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

//namespace ServiceStackVS.NuGetInstallerWizard
//{
//    public class NuGetPackageInstallerWizard : IWizard
//    {
//        [Import]
//        internal IVsPackageInstaller Installer { get; set; }
//        [Import]
//        internal IVsPackageInstallerServices PackageServices { get; set; }

//        private const string NugetV2Url = "https://packages.nuget.org/api/v2";

//        private IPackageRepository _nuGetPackageRepository;
//        private IPackageRepository NuGetPackageRepository
//        {
//            get
//            {
//                return _nuGetPackageRepository ??
//                       (_nuGetPackageRepository =
//                           PackageRepositoryFactory.Default.CreateRepository(NugetV2Url));
//            }
//        }

//        private IPackageRepository _cachedRepository;
//        private IPackageRepository CachedRepository
//        {
//            get
//            {
//                string userAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
//                string cachePath = Path.Combine(userAppData, "NuGet\\Cache");
//                return _cachedRepository ??
//                       (_cachedRepository = PackageRepositoryFactory.Default.CreateRepository(cachePath));
//            }
//        }

//        private IVsStatusbar _bar;
//        private IVsStatusbar StatusBar
//        {
//            get { return _bar ?? (_bar = Package.GetGlobalService(typeof(SVsStatusbar)) as IVsStatusbar); }
//        }

//        private const string ServiceStackVsOutputWindowPane = "5e5ab647-6a69-44a8-a2db-6a324b7b7e6d";
//        private OutputWindowWriter _serviceStackOutputWindowWriter;
//        private OutputWindowWriter OutputWindowWriter
//        {
//            get
//            {
//                return _serviceStackOutputWindowWriter ?? 
//                    (_serviceStackOutputWindowWriter = new OutputWindowWriter(ServiceStackVsOutputWindowPane, "ServiceStackVS"));
//            }
//        }

//        private NuGetWizardDataPackage _rootPackage;

//        private List<NuGetWizardDataPackage> _packagesToLoad = new List<NuGetWizardDataPackage>();

//        public void RunStarted(object automationObject, Dictionary<string, string> replacementsDictionary, WizardRunKind runKind, object[] customParams)
//        {
//            if (runKind != WizardRunKind.AsNewProject) return;

//            using (var serviceProvider = new ServiceProvider((IServiceProvider) automationObject))
//            {
//                var componentModel = (IComponentModel) serviceProvider.GetService(typeof (SComponentModel));
//                using (var container = new CompositionContainer(componentModel.DefaultExportProvider))
//                {
//                    container.ComposeParts(this);
//                }
//            }

//            string wizardData = replacementsDictionary["$wizarddata$"];
//            XElement element = XElement.Parse("<WizardData>" + wizardData + "</WizardData>");
//            _packagesToLoad = element.ExtractNuGetPackages();
//            _rootPackage = NuGetPackageInstallerMultiProjectWizard.RootNuGetPackage;

//            if (!UseParentProjectRootPackage(_rootPackage) && element.HasRootPackage())
//            {
//                _rootPackage = element.GetRootPackage();
//                _rootPackage.Version = NuGetPackageRepository.GetLatestVersionOfPackage(_rootPackage.Id);
//            }
//        }

//        private static bool UseParentProjectRootPackage(NuGetWizardDataPackage rootPackage)
//        {
//            return rootPackage != null &&
//                   !(string.IsNullOrEmpty(rootPackage.Version) || rootPackage.Version.EqualsIgnoreCase("latest"));
//        }

//        public void ProjectFinishedGenerating(Project project)
//        {
//            OutputWindowWriter.WriteLine("--- Installing latest ServiceStack NuGet dependencies for '" + project.Name + "' ---");
//            foreach (var packageFromWizard in _packagesToLoad)
//            {
//                try
//                {
//                    AddNuGetDependencyIfMissing(project, packageFromWizard.Id, packageFromWizard.Version);
//                }
//                catch (Exception e)
//                {
//                    OutputWindowWriter.WriteLine("--- Failed to install ServiceStack NuGet dependencies ---");
//                    OutputWindowWriter.WriteLine(e.Message);
//                    OutputWindowWriter.Show();
//                    throw;
//                }
//            }
//            OutputWindowWriter.WriteLine("--- Finished installing latest ServiceStack NuGet dependencies for '" + project.Name + "' ---");
//        }

//        private void UpdateStatusMessage(string message)
//        {
//            int frozen = 1;
//            int retries = 0;
//            while (frozen != 0 && retries < 10)
//            {
//                StatusBar.IsFrozen(out frozen);
//                if (frozen == 0)
//                {
//                    StatusBar.SetText(message);
//                }
//                System.Threading.Thread.Sleep(10);
//                retries++;
//            }
//        }

//        public void ProjectItemFinishedGenerating(ProjectItem projectItem)
//        {

//        }

//        public bool ShouldAddProjectItem(string filePath)
//        {
//            return true;
//        }

//        public void BeforeOpeningFile(ProjectItem projectItem)
//        {

//        }

//        public void RunFinished()
//        {

//        }

//        private void AddNuGetDependencyIfMissing(Project project, string packageId, string version = null)
//        {
//            if (_rootPackage != null && (version == null || version.ToLower() == "latest"))
//            {
//                version = _rootPackage.Version;
//            }

//            if (TryInstallPackageFromCache(project, packageId, version)) return;

//            var installedPackages = PackageServices.GetInstalledPackages(project);
//            version = string.IsNullOrEmpty(version) || version == "latest" ? null : version; //if empty or latest, set to null
//            //Check if existing nuget reference exists
//            if (installedPackages.FirstOrDefault(x => x.Id == packageId) == null)
//            {
//                UpdateStatusMessage("Installing " + packageId + " from NuGet...");
//                Installer.InstallPackage("https://www.nuget.org/api/v2/",
//                         project,
//                         packageId,
//                         version, //Null is latest version of packageId
//                         ignoreDependencies: false);
//            }
//        }

//        private bool TryInstallPackageFromCache(Project project, string packageId, string version)
//        {
//            var envVersion = Environment.GetEnvironmentVariable("SERVICESTACK_PACKAGE_VERSION");
//            if (!string.IsNullOrEmpty(envVersion))
//            {
//                version = envVersion;
//            }

//            string userAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
//            string cachePath = Path.Combine(userAppData, "NuGet\\Cache");
//            bool cacheExists = Directory.Exists(cachePath);
//            bool useLatest = string.IsNullOrEmpty(version) || version == "latest";
//            var latestCachePackage = CachedRepository.FindPackagesById(packageId)
//                    .OrderByDescending(x => x.Version.ToString())
//                    .FirstOrDefault(x => useLatest && x.IsLatestVersion || x.Version.ToString() == version);
//            if (!useLatest && latestCachePackage != null)
//            {
//                InstallPackageFromLocalCache(project, packageId, cachePath, version);
//                return true;
//            }

//            List<IPackage> latestNugetPackages;

//            try
//            {
//                latestNugetPackages = NuGetPackageRepository.FindPackagesById(packageId).ToList();
//            }
//            catch (Exception)
//            {
//                //Nuget down or no connection
//                //Try and revert to latest cached packages
//                OutputWindowWriter.WriteLine("--- WARNING: Unable to contact NuGet servers. Attempting to use local cache ---");
//                if (latestCachePackage == null)
//                {
//                    OutputWindowWriter.WriteLine("--- ERROR: Unable to install ServiceStack from NuGet or local cache ---");
//                    throw new WizardBackoutException("Failed to installed package from cache:" + packageId);
//                }
//                OutputWindowWriter.WriteLine("--- Installing " + packageId + " - " + latestCachePackage.Version + " from local cache ---");
//                InstallPackageFromLocalCache(project, packageId, cachePath, latestCachePackage.Version.ToString());
//                return true;
//            }

//            if (cacheExists)
//            {
//                var latestNugetPackage =
//                    latestNugetPackages.FirstOrDefault(x => useLatest && x.IsLatestVersion || x.Version.ToString() == version);
//                bool useCache = latestCachePackage != null &&
//                                latestNugetPackage != null &&
//                                latestNugetPackage.Version == latestCachePackage.Version;
//                if (useCache)
//                {
//                    OutputWindowWriter.WriteLine("--- Installing " + packageId + " - " + latestNugetPackage.Version + " ---");
//                    InstallPackageFromLocalCache(project, packageId, cachePath, latestNugetPackage.Version.ToString());
//                    return true;
//                }

//                if (latestNugetPackage == null)
//                {
//                    throw new ArgumentException("Invalid or unavailable version provided");
//                }

//                OutputWindowWriter.WriteLine("--- Installing " + packageId + " - " + latestNugetPackage.Version + " ---");
//            }
//            return false;
//        }

//        private void InstallPackageFromLocalCache(Project project, string packageId, string cachePath, string version)
//        {
//            try
//            {
//                Installer.InstallPackage(
//                cachePath,
//                project,
//                packageId,
//                version,
//                ignoreDependencies: false);
//            }
//            catch (Exception)
//            {
//                //Fall back to NuGet, local cache might not have required dependency
//                Installer.InstallPackage(
//                NugetV2Url,
//                project,
//                packageId,
//                version,
//                ignoreDependencies: false);
//            }

//        }
//    }  
//}
