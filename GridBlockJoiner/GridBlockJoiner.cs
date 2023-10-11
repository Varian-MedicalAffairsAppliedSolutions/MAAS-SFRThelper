using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
//C:\Users\ekuusela\OneDrive - Varian Medical Systems\Documents\Eclipse Scripting API\Projects\GridBlockJoiner\GridBlockJoiner.cs
// TODO: Replace the following version attributes by creating AssemblyInfo.cs. You can do this in the properties of the Visual Studio project.
[assembly: AssemblyVersion("1.0.0.1")]
[assembly: AssemblyFileVersion("1.0.0.1")]
[assembly: AssemblyInformationalVersion("1.0")]

// TODO: Uncomment the following line if the script requires write access.

//for 15.x or later
[assembly: ESAPIScript(IsWriteable = true)]



//Luca's path for VMS.TPS.Common.Model: D:\AR\GridBlockCreator\Projects\
namespace VMS.TPS
{
  public class Script
  {
    public Script()
    {
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Execute(ScriptContext context /*, System.Windows.Window window, ScriptEnvironment environment*/)
    {
        if (context.Patient == null || context.StructureSet == null)
        {
            MessageBox.Show("No active Structure Set selected - exiting.");
            return;
        }

        
        {
            int targetCount = 0;
            foreach (var i in context.StructureSet.Structures)
            {
                if (i.DicomType == "PTV" && i.IsHighResolution == true) ++targetCount;
            }

            if (targetCount == 0)
            {
                MessageBox.Show("No hi-res PTV found - exiting.");
                return;
            }

        }

        GridBlockJoiner.TargetSelector mainWindow = new GridBlockJoiner.TargetSelector(context);
        mainWindow.ShowDialog();
    }
  }
}
