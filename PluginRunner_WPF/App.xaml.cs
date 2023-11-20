using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using EsapiEssentials;
using EsapiEssentials.PluginRunner;
using VMS.TPS;
using VMS.TPS.Common.Model.API;
[assembly: ESAPIScript(IsWriteable = true)]

namespace PluginRunner_WPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            // Note: EsapiEssentials and EsapiEssentials.PluginRunner must be referenced,
            // as well as the project that contains the Script class
            ScriptRunner.Run(new Script());
        }

        // Fix UnauthorizedScriptingAPIAccessException
        public void DoNothing(PlanSetup plan) { }
    }
}
