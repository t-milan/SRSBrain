using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;
using VMS.TPS.Common.Model.API;
using Plugin;
using Plugin.Models;

// TODO: Replace the following version attributes by creating AssemblyInfo.cs. You can do this in the properties of the Visual Studio project.
[assembly: AssemblyVersion("1.0.4.3")]
[assembly: AssemblyFileVersion("1.0.0.1")]
[assembly: AssemblyInformationalVersion("1.0")]

// The script requires write access (it adds/removes beams and modifies the plan).
[assembly: ESAPIScript(IsWriteable = true)]

namespace VMS.TPS
{
    public class Script
    {
        // IMPORTANT: keep the loadable surface of this class free of dependency
        // types (no external base class, no fields of external types). The CLR
        // runs this static constructor before any member that touches a
        // dependency is JIT-compiled, which is the only way to register the
        // resolver below before the dependencies it loads are needed. All
        // dependency usage (MvvmLight, Accord, miniball, ...) lives inside
        // Execute's body, which is JIT-compiled only after this ctor has run.
        static Script()
        {
            // Dependencies are deployed in a Lib\SRSBrain subfolder next to this
            // plugin DLL (see the MoveDependenciesToLib build target). Eclipse only
            // probes the script folder itself, so resolve those assemblies here.
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                string dir = Path.GetDirectoryName(GetPluginLocation());
                string dll = Path.Combine(dir, @"Lib\SRSBrain", new AssemblyName(args.Name).Name + ".dll");
                return File.Exists(dll) ? Assembly.LoadFrom(dll) : null;
            };
        }

        public Script()
        {
        }

        public void Execute(ScriptContext context, Window window)
        {
            SRSViewModel srsViewModel = new SRSViewModel(new ContextModel(context));
            SRSView srsView = new SRSView(srsViewModel);

            window.Width = 560;
            window.Height = 467;
            window.Title = "SRS Brain Treatment Geometry";
            string iconPath = "pack://application:,,,/" + Assembly.GetExecutingAssembly().GetName().Name + ";component/Resources/Head.ico";
            Uri iconUri = new Uri(iconPath, UriKind.Absolute);
            window.Icon = BitmapFrame.Create(iconUri);
            window.Content = srsView;
        }

        // Resolve the folder this plugin DLL lives in. Eclipse sometimes loads
        // the plugin from raw bytes, leaving Assembly.Location empty; fall back to
        // the CodeBase URI in that case.
        private static string GetPluginLocation()
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            if (!string.IsNullOrEmpty(asm.Location))
                return asm.Location;
            return new Uri(asm.CodeBase).LocalPath;
        }
    }
}
