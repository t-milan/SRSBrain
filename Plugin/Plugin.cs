using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using EsapiEssentials.Plugin;
using Plugin;
using Plugin.Models;
using System.Numerics;
using System.Windows.Media.Imaging;
//using MathNet.Numerics.LinearAlgebra; 

// TODO: Replace the following version attributes by creating AssemblyInfo.cs. You can do this in the properties of the Visual Studio project.
[assembly: AssemblyVersion("1.0.2.2")]
[assembly: AssemblyFileVersion("1.0.0.1")]
[assembly: AssemblyInformationalVersion("1.0")]

// TODO: Uncomment the following line if the script requires write access.
[assembly: ESAPIScript(IsWriteable = true)]

namespace VMS.TPS
{
    public class Script : ScriptBaseWithWindow
  {

    //[MethodImpl(MethodImplOptions.NoInlining)]

        // do I need an empty public Script() here?
        public Script()
        {

        }

        public override void Execute(PluginScriptContext context, Window window)
        {
            //context.Patient.BeginModifications();
            //ONLY EXECUTABLE???
            //Patient patient = app.OpenPatientById("ZZZ_CUBE_TM");
            //Course course = patient.Courses.FirstOrDefault(c => c.Id == "MultiMetTest");
            //ExternalPlanSetup planSetup = course.ExternalPlanSetups.FirstOrDefault(ps => ps.Id == "Plan1");

            TargetListViewModel targetListVM = new TargetListViewModel(new ContextModel(context));
            TargetListControl targetListControl = new TargetListControl(targetListVM);

            window.Width = 550;
            window.Height = 435;
            window.Title = "SRS Brain Treatment Geometry";
            string iconPath = "pack://application:,,,/" + Assembly.GetExecutingAssembly().GetName().Name + ";component/Resources/Head.ico";
            Uri iconUri = new Uri(iconPath, UriKind.Absolute);
            window.Icon = BitmapFrame.Create(iconUri);
            //"450" d: DesignWidth = "800" >
             window.Content = targetListControl;
        }
    }
}
