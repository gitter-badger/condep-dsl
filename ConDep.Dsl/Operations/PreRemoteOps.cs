﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ConDep.Dsl.Config;
using ConDep.Dsl.Logging;
using ConDep.Dsl.PSScripts;
using ConDep.Dsl.Remote;
using ConDep.Dsl.Resources;
using ConDep.Dsl.SemanticModel;

namespace ConDep.Dsl.Operations
{
    internal class PreRemoteOps : IOperateRemote
    {
        const string TMP_FOLDER = @"{0}\temp\ConDep\{1}";

        private void CopyResourceFiles(Assembly assembly, IEnumerable<string> resources, ServerConfig server)
        {
            if (resources == null || assembly == null) return;
            
            foreach (var path in resources.Select(resource => ExtractPowerShellFileFromResource(assembly, resource)).Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                CopyFile(path, server);
            }
            var src = Path.Combine(Path.GetDirectoryName(GetType().Assembly.Location), "ConDep.Remote.dll");
            var dst = string.Format(@"{0}\{1}", server.TempFolderDos, "ConDep.Remote.dll");
            CopyFile(src, dst, server);
        }

        private string ExtractPowerShellFileFromResource(Assembly assembly, string resource)
        {
            var regex = new Regex(@".+\.(.+\.(ps1|psm1))");
            var match = regex.Match(resource);
            if (match.Success)
            {
                var resourceName = match.Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(resourceName))
                {
                    var resourceNamespace = resource.Replace("." + resourceName, "");
                    return ConDepResourceFiles.GetFilePath(assembly, resourceNamespace, resourceName, true);
                }
            }
            return null;
        }

        private void CopyFile(string srcPath, ServerConfig server)
        {
            var dstPath = string.Format(@"{0}\PSScripts\ConDep\{1}", server.TempFolderDos, Path.GetFileName(srcPath));
            CopyFile(srcPath, dstPath, server);
        }

        private void CopyFile(string srcPath, string dstPath, ServerConfig server)
        {
            var filePublisher = new FilePublisher();
            filePublisher.PublishFile(srcPath, dstPath, server);
        }

        public void Execute(ServerConfig server, IReportStatus status, ConDepSettings settings)
        {
            server.TempFolderDos = string.Format(TMP_FOLDER, "%windir%", ConDepGlobals.ExecId);
            server.TempFolderPowerShell = string.Format(TMP_FOLDER, "$env:windir", ConDepGlobals.ExecId);
            TempInstallConDepNode(status, server);

            CopyResourceFiles(Assembly.GetExecutingAssembly(), PowerShellResources.PowerShellScriptResources, server);

            if (settings.Options.Assembly != null)
            {
                var assemblyResources = settings.Options.Assembly.GetManifestResourceNames();
                CopyResourceFiles(settings.Options.Assembly, assemblyResources, server);
            }

        }

        public bool IsValid(Notification notification)
        {
            return true;
        }

        private void TempInstallConDepNode(IReportStatus status, ServerConfig server)
        {
            Logger.LogSectionStart("Deploying ConDep Node");
            try
            {
                var listenUrl = "http://{0}:80/ConDepNode/";
                var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "ConDepNode.exe");
                var byteArray = File.ReadAllBytes(path);
                var psCopyFileOp = new ConDepNodePublisher(byteArray, Path.Combine(server.TempFolderPowerShell, Path.GetFileName(path)), string.Format(listenUrl, "localhost"));
                psCopyFileOp.Execute(server);
                Logger.Info(string.Format("ConDep Node successfully deployed to {0}", server.Name));
                Logger.Info(string.Format("Node listening on {0}", string.Format(listenUrl, server.Name)));
            }
            finally
            {
                Logger.LogSectionEnd("Deploying ConDep Node");
            }
        }
    }
}